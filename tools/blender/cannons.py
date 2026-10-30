"""Dwie stylizowane armaty (bliźniacze działko i ciężka armata) w Blenderze, z teksturami i animacją wystrzału.

Uruchamianie (moduł bpy z PyPI albo Blender w trybie tła):
    python cannons.py --out art/cannons
    blender -b -P tools/blender/cannons.py -- --out art/cannons

Skrypt buduje modele z prymitywów i brył obrotowych, rozkłada UV (jedna mapa na armatę), a tekstury
w stylu ręcznie malowanym wypieka w Cycles z proceduralnych materiałów (rozjaśnione krawędzie, cień
w zagłębieniach, światło od góry, ziarno drewna). Na końcu zapisuje .blend, FBX i GLB dla silnika,
tekstury PNG oraz podglądy (zdjęcie i GIF z wystrzałem).
"""
import argparse
import math
import os
import random
import sys

import bpy  # noqa: I001 (bpy musi być przed bmesh)
import bmesh
import numpy as np
from mathutils import Euler, Matrix, Vector

R = math.radians
FPS = 30
FRAMES = 60
TEX = 1024
rng = random.Random(7)


# ---------------------------------------------------------------- kolory

def col(h, a=1.0):
    """Kolor sRGB w zapisie hex -> liniowe RGBA dla węzłów."""
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*lin, a)


PAL = {
    "iron": dict(dark="#232a36", mid="#394354", top="#5d6b82", cavity="#0f1218", edge="#a3b3c9",
                 rough=0.48, metal=0.45, bump=0.06),
    "brass": dict(dark="#6e4718", mid="#ad7a2c", top="#e3b452", cavity="#33200a", edge="#ffe7a3",
                  rough=0.34, metal=0.85, bump=0.03),
    "wood": dict(dark="#6f3818", mid="#ad622b", top="#df9d55", cavity="#341808", edge="#f3bf80",
                 light="#cf8642", rough=0.82, metal=0.0, bump=0.25),
}
MAT_INDEX = {"iron": 0, "brass": 1, "wood": 2}


# ---------------------------------------------------------------- materiały proceduralne (źródło wypieku)

class NodeKit:
    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nt.nodes.clear()

    def new(self, kind, **props):
        node = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    @staticmethod
    def sock(sockets, ident):
        return next(s for s in sockets if s.identifier == ident)

    def mix(self, fac, a, b):
        m = self.new("ShaderNodeMix", data_type="RGBA", clamp_factor=True)
        for ident, v in (("Factor_Float", fac), ("A_Color", a), ("B_Color", b)):
            s = self.sock(m.inputs, ident)
            if isinstance(v, (int, float, tuple)):
                s.default_value = v
            else:
                self.link(v, s)
        return self.sock(m.outputs, "Result_Color")

    def math(self, op, a, b=0.0, clamp=False):
        m = self.new("ShaderNodeMath", operation=op, use_clamp=clamp)
        for s, v in zip(m.inputs, (a, b)):
            if isinstance(v, (int, float)):
                s.default_value = v
            else:
                self.link(v, s)
        return m.outputs[0]

    def remap(self, v, a, b, c=0.0, d=1.0):
        m = self.new("ShaderNodeMapRange", clamp=True)
        self.link(v, m.inputs["Value"])
        m.inputs["From Min"].default_value = a
        m.inputs["From Max"].default_value = b
        m.inputs["To Min"].default_value = c
        m.inputs["To Max"].default_value = d
        return m.outputs["Result"]


MATINFO = {}


def painted_material(name, kind):
    """Materiał, który wygląda jak malowany ręcznie. Wyjścia do wypieku zapamiętane w MATINFO."""
    p = PAL[kind]
    mat = bpy.data.materials.new(name)
    k = NodeKit(mat)
    coord = k.new("ShaderNodeTexCoord").outputs["Object"]
    geo = k.new("ShaderNodeNewGeometry")

    # Krawędzie: różnica normalnej z węzła Bevel i normalnej siatki.
    bevel = k.new("ShaderNodeBevel", samples=12)
    bevel.inputs["Radius"].default_value = 0.022
    dot = k.new("ShaderNodeVectorMath", operation="DOT_PRODUCT")
    k.link(bevel.outputs["Normal"], dot.inputs[0])
    k.link(geo.outputs["Normal"], dot.inputs[1])
    edge_raw = k.remap(dot.outputs["Value"], 0.985, 0.90)

    # Pociągnięcia pędzla: rozciągnięty szum, który przerywa krawędzie i plami kolor.
    brush_map = k.new("ShaderNodeMapping")
    brush_map.inputs["Scale"].default_value = (5.0, 5.0, 14.0)
    k.link(coord, brush_map.inputs["Vector"])
    brush = k.new("ShaderNodeTexNoise", noise_dimensions="3D")
    brush.inputs["Scale"].default_value = 1.6
    brush.inputs["Detail"].default_value = 3.0
    brush.inputs["Roughness"].default_value = 0.55
    k.link(brush_map.outputs["Vector"], brush.inputs["Vector"])
    brush_f = brush.outputs["Fac"]
    edge = k.math("MULTIPLY", edge_raw, k.remap(brush_f, 0.38, 0.6, 0.45, 1.0), clamp=True)

    # Światło od góry i cień w zagłębieniach (tylko w obrębie obiektu, bo części się ruszają).
    sep = k.new("ShaderNodeSeparateXYZ")
    k.link(geo.outputs["Normal"], sep.inputs[0])
    top = k.remap(sep.outputs["Z"], -0.3, 1.0)
    top = k.math("POWER", top, 1.6)
    ao = k.new("ShaderNodeAmbientOcclusion", only_local=True, samples=16)
    ao.inputs["Distance"].default_value = 0.14
    ao_f = k.remap(ao.outputs["AO"], 0.15, 1.0, 0.0, 1.0)

    fine = k.new("ShaderNodeTexNoise")
    fine.inputs["Scale"].default_value = 38.0
    fine.inputs["Detail"].default_value = 2.0
    k.link(coord, fine.inputs["Vector"])

    if kind == "wood":
        grain_map = k.new("ShaderNodeMapping")
        grain_map.inputs["Scale"].default_value = (0.6, 1.0, 1.0)
        k.link(coord, grain_map.inputs["Vector"])
        wave = k.new("ShaderNodeTexWave", wave_type="BANDS", bands_direction="Y", wave_profile="SIN")
        wave.inputs["Scale"].default_value = 3.2
        wave.inputs["Distortion"].default_value = 7.0
        wave.inputs["Detail"].default_value = 2.5
        wave.inputs["Detail Scale"].default_value = 1.4
        k.link(grain_map.outputs["Vector"], wave.inputs["Vector"])
        grain = k.remap(wave.outputs["Fac"], 0.25, 0.85)
        c = k.mix(grain, col(p["dark"]), col(p["light"]))
        c = k.mix(k.remap(brush_f, 0.3, 0.7, 0.0, 0.5), c, col(p["mid"]))
        height = wave.outputs["Fac"]
    else:
        c = k.mix(k.remap(brush_f, 0.32, 0.68), col(p["dark"]), col(p["mid"]))
        height = fine.outputs["Fac"]
    c = k.mix(k.math("MULTIPLY", top, 0.75), c, col(p["top"]))
    c = k.mix(ao_f, col(p["cavity"]), c)
    c = k.mix(edge, c, col(p["edge"]))

    rough = k.math("ADD", p["rough"], k.math("MULTIPLY", k.math("SUBTRACT", brush_f, 0.5), 0.2))
    rough = k.math("SUBTRACT", rough, k.math("MULTIPLY", edge, 0.18), clamp=True)
    metal = k.new("ShaderNodeValue")
    metal.outputs[0].default_value = p["metal"]

    bump = k.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = p["bump"]
    bump.inputs["Distance"].default_value = 0.01
    k.link(height, bump.inputs["Height"])
    k.link(bevel.outputs["Normal"], bump.inputs["Normal"])

    bsdf = k.new("ShaderNodeBsdfPrincipled")
    k.link(c, bsdf.inputs["Base Color"])
    k.link(rough, bsdf.inputs["Roughness"])
    k.link(metal.outputs[0], bsdf.inputs["Metallic"])
    k.link(bump.outputs["Normal"], bsdf.inputs["Normal"])
    emit = k.new("ShaderNodeEmission")
    out = k.new("ShaderNodeOutputMaterial")
    k.link(bsdf.outputs[0], out.inputs["Surface"])
    tex = k.new("ShaderNodeTexImage", name="BAKE_TARGET")
    MATINFO[name] = dict(color=c, rough=rough, metal=metal.outputs[0], bsdf=bsdf, emit=emit, out=out, tex=tex)
    return mat


def fx_material(name, color, strength=0.0, rough=0.9):
    mat = bpy.data.materials.new(name)
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = col(color)
    bsdf.inputs["Roughness"].default_value = rough
    if strength:
        bsdf.inputs["Emission Color"].default_value = col(color)
        bsdf.inputs["Emission Strength"].default_value = strength
    mat.diffuse_color = col(color)
    return mat


# ---------------------------------------------------------------- budowa siatek

def done_layers(bm):
    return (bm.verts.layers.int.get("done") or bm.verts.layers.int.new("done"),
            bm.faces.layers.int.get("done") or bm.faces.layers.int.new("done"))


class Part:
    """Siatka składana z prymitywów; każdy dostaje własne fazowanie krawędzi i materiał."""

    def __init__(self):
        self.bm = bmesh.new()

    def add(self, build, kind="iron", bevel=0.012, seg=3, angle=32, mat_fn=None):
        # Nowe elementy rozpoznaje warstwa "done" (operatory bmesh czyszczą flagi tag).
        bm = self.bm
        done_v, done_f = done_layers(bm)
        build(bm)
        verts = [v for v in bm.verts if not v[done_v]]
        bmesh.ops.remove_doubles(bm, verts=verts, dist=1e-5)
        new = [f for f in bm.faces if not f[done_f]]
        bmesh.ops.recalc_face_normals(bm, faces=new)
        if bevel > 0:
            edges = list({e for f in new for e in f.edges
                          if len(e.link_faces) == 2 and e.calc_face_angle(0) > R(angle)})
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type="OFFSET", segments=seg,
                            profile=0.5, affect="EDGES", clamp_overlap=True, material=-1)
        for f in bm.faces:
            if not f[done_f]:
                f.material_index = MAT_INDEX[mat_fn(f.calc_center_median()) if mat_fn else kind]
                f[done_f] = 1
        for v in bm.verts:
            v[done_v] = 1
        return self

    def to_object(self, name, coll, mats, parent=None, loc=(0, 0, 0), rot=(0, 0, 0)):
        me = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        me.shade_smooth()
        me.set_sharp_from_angle(angle=R(38))
        for m in mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        ob.parent = parent
        ob.location = loc
        ob.rotation_euler = rot
        return ob


def box(size, loc, rot=(0, 0, 0)):
    return lambda bm: bmesh.ops.create_cube(
        bm, size=1.0, matrix=Matrix.LocRotScale(Vector(loc), Euler(rot), Vector(size)))


def cyl(r, depth, loc, rot=(0, 0, 0), seg=24, r2=None):
    return lambda bm: bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2,
        depth=depth, matrix=Matrix.LocRotScale(Vector(loc), Euler(rot), None))


def lathe(profile, loc=(0, 0, 0), rot=(0, 0, 0), seg=32):
    """Bryła obrotowa: profil (t, r) wzdłuż osi Z, potem obrót/przesunięcie macierzą."""
    def build(bm):
        vs = [bm.verts.new((r, 0.0, t)) for t, r in profile]
        es = [bm.edges.new((a, b)) for a, b in zip(vs, vs[1:])]
        bmesh.ops.spin(bm, geom=vs + es, cent=(0, 0, 0), axis=(0, 0, 1), dvec=(0, 0, 0),
                       angle=2 * math.pi, steps=seg, use_merge=True, use_duplicate=False)
        done_v, _ = done_layers(bm)
        bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if not v[done_v]], dist=1e-5)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=list({e for v in bm.verts if not v[done_v]
                                                                 for e in v.link_edges}))
        M = Matrix.LocRotScale(Vector(loc), Euler(rot), None)
        bmesh.ops.transform(bm, matrix=M, verts=[v for v in bm.verts if not v[done_v]])
    return build


def bolt(loc, rot=(0, 0, 0), r=0.022):
    return cyl(r, 0.022, loc, rot, seg=10)


# ---------------------------------------------------------------- części wspólne

def build_base(prefix, coll, mats, root):
    base = Part()
    for i, y in enumerate((-0.33, 0.0, 0.33)):
        h = 0.12 + rng.uniform(-0.008, 0.008)
        base.add(box((1.0 + rng.uniform(-0.02, 0.02), 0.32, h), (rng.uniform(-0.01, 0.01), y, h / 2),
                     (rng.uniform(-0.01, 0.01), rng.uniform(-0.012, 0.012), rng.uniform(-0.015, 0.015))),
                 "wood", bevel=0.018)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * 0.43, sy * 0.43
            base.add(box((0.2, 0.075, 0.022), (cx - sx * 0.06, cy, 0.135)), "iron", bevel=0.007, seg=2)
            base.add(box((0.075, 0.2, 0.022), (cx, cy - sy * 0.06, 0.135)), "iron", bevel=0.007, seg=2)
            for bx, by in ((cx, cy), (cx - sx * 0.12, cy), (cx, cy - sy * 0.12)):
                base.add(bolt((bx, by, 0.15)), "iron", bevel=0.008, seg=2)
    base.to_object(f"{prefix}_Base", coll, mats, root)

    tt = Part()
    tt.add(cyl(0.4, 0.1, (0, 0, 0.175), seg=40), "iron", bevel=0.015)
    for i in range(20):
        a = 2 * math.pi * i / 20
        tt.add(box((0.07, 0.06, 0.085), (math.cos(a) * 0.405, math.sin(a) * 0.405, 0.172), (0, 0, a)),
               "iron", bevel=0.01, seg=2)
    tt.add(cyl(0.34, 0.05, (0, 0, 0.245), seg=40), "iron", bevel=0.012)
    for i in range(8):
        a = 2 * math.pi * (i + 0.5) / 8
        tt.add(bolt((math.cos(a) * 0.37, math.sin(a) * 0.37, 0.228)), "brass", bevel=0.008, seg=2)
    tt.to_object(f"{prefix}_Turntable", coll, mats, root)
    return 0.27  # wysokość osi obrotu wieżyczki


def empty(name, coll, parent, loc=(0, 0, 0), rot=(0, 0, 0), size=0.15):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "PLAIN_AXES"
    ob.empty_display_size = size
    coll.objects.link(ob)
    ob.parent = parent
    ob.location = loc
    ob.rotation_euler = rot
    return ob


def muzzle_flash(name, coll, parent, loc, scale, mat_core, mat_outer):
    """Stylizowany błysk: gwiazda w płaszczyźnie wylotu, stożek ognia i jasny rdzeń."""
    p = Part()
    def star(bm):
        n = 8
        vs = []
        for i in range(2 * n):
            r = 0.17 if i % 2 == 0 else 0.07
            a = math.pi * i / n + rng.uniform(-0.08, 0.08)
            vs.append(bm.verts.new((math.cos(a) * r * rng.uniform(0.8, 1.15), 0.0, math.sin(a) * r)))
        c = bm.verts.new((0, -0.02, 0))
        for a, b in zip(vs, vs[1:] + vs[:1]):
            bm.faces.new((c, a, b))
    p.add(star, "iron", bevel=0)
    p.add(cyl(0.11, 0.38, (0, -0.19, 0), (R(90), 0, 0), seg=10, r2=0.0), "iron", bevel=0)
    p.add(lambda bm: bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.07,
                                                matrix=Matrix.Translation((0, -0.04, 0))), "brass", bevel=0)
    ob = p.to_object(name, coll, [mat_outer, mat_core], parent, loc)
    ob.data.shade_flat()
    ob.scale = (0, 0, 0)
    ob["base_scale"] = scale
    light = bpy.data.lights.new(name + "_Light", "POINT")
    light.color = (1.0, 0.62, 0.25)
    light.energy = 0
    light.shadow_soft_size = 0.2
    lo = bpy.data.objects.new(name + "_Light", light)
    coll.objects.link(lo)
    lo.parent = parent
    lo.location = Vector(loc) + Vector((0, -0.15, 0))
    return ob, lo


def smoke_puff(name, coll, parent, mat):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in bm.verts:
        v.co *= 1.0 + rng.uniform(-0.12, 0.12)
    p = Part()
    p.bm.free()
    p.bm = bm
    ob = p.to_object(name, coll, [mat], parent)
    ob.scale = (0, 0, 0)
    return ob


# ---------------------------------------------------------------- animacja

def key(ob, path, frame, value, interp="BEZIER"):
    bpy.context.preferences.edit.keyframe_new_interpolation_type = interp
    setattr(ob, path, value)
    ob.keyframe_insert(path, frame=frame)


def flash_keys(flash, light, f, energy=60.0):
    s = flash["base_scale"]
    key(flash, "scale", f - 1, (0, 0, 0), "CONSTANT")
    key(flash, "scale", f, (s, s, s), "LINEAR")
    key(flash, "scale", f + 1, (s * 1.25, s * 1.4, s * 1.25), "CONSTANT")
    key(flash, "scale", f + 2, (0, 0, 0), "CONSTANT")
    for fr, e in ((f - 1, 0), (f, energy), (f + 1, energy * 0.6), (f + 3, 0)):
        light.data.energy = e
        light.data.keyframe_insert("energy", frame=fr)


def smoke_keys(puffs, origin, direction, f, size, reach, life=32):
    up = Vector((0, 0, 1))
    side = direction.cross(up).normalized()
    for i, pf in enumerate(puffs):
        t0 = f + i
        s = size * rng.uniform(0.7, 1.15) * (1.0 - 0.08 * i)
        start = origin + direction * (0.04 + 0.06 * i)
        mid = origin + direction * (reach * (0.35 + 0.18 * i)) + side * rng.uniform(-0.06, 0.06)
        end = mid + direction * reach * 0.25 + up * rng.uniform(0.25, 0.4) + side * rng.uniform(-0.1, 0.1)
        pf.location = start
        key(pf, "scale", t0 - 1, (0, 0, 0), "CONSTANT")
        key(pf, "location", t0, start)
        key(pf, "scale", t0, (s * 0.3,) * 3)
        key(pf, "location", t0 + 5, mid)
        key(pf, "scale", t0 + 5, (s,) * 3)
        key(pf, "location", t0 + life, end)
        key(pf, "scale", t0 + life * 5 // 8, (s * 1.15,) * 3)
        key(pf, "scale", t0 + life, (0, 0, 0), "CONSTANT")


def muzzle_world(flash):
    bpy.context.view_layer.update()
    m = flash.matrix_world
    return m.translation.copy(), (m.to_3x3() @ Vector((0, -1, 0))).normalized()


# ---------------------------------------------------------------- armata ciężka

def build_heavy(coll, fx):
    mats = [painted_material(f"Heavy_{k}", k) for k in ("iron", "brass", "wood")]
    root = empty("Cannon_Heavy", coll, None, size=0.4)
    pivot_z = build_base("Heavy", coll, mats, root)

    tur = Part()
    tur.add(cyl(0.3, 0.06, (0, 0, 0.03), seg=36), "iron", bevel=0.014)
    tur.add(cyl(0.312, 0.024, (0, 0, 0.03), seg=36), "brass", bevel=0.006, seg=2)
    for sx in (-1, 1):
        tur.add(box((0.075, 0.38, 0.2), (sx * 0.2, 0.0, 0.18)), "iron", bevel=0.014)
        tur.add(cyl(0.17, 0.075, (sx * 0.2, 0.0, 0.3), (0, R(90), 0), seg=28), "iron", bevel=0.014)
        tur.add(cyl(0.065, 0.05, (sx * 0.26, 0.0, 0.32), (0, R(90), 0), seg=18), "brass", bevel=0.01)
        tur.add(cyl(0.03, 0.03, (sx * 0.29, 0.0, 0.32), (0, R(90), 0), seg=10), "iron", bevel=0.008, seg=2)
        for by, bz in ((-0.13, 0.12), (0.13, 0.12), (0.0, 0.42)):
            tur.add(bolt((sx * 0.24, by, bz), (0, R(90), 0), r=0.018), "iron", bevel=0.007, seg=2)
    turret = tur.to_object("Heavy_Turret", coll, mats, root, (0, 0, pivot_z))

    elev = empty("Heavy_Elevation", coll, turret, (0, 0, 0.32), (R(-30), 0, 0))

    prof = [(-0.42, 0.0), (-0.42, 0.038), (-0.40, 0.055), (-0.375, 0.04), (-0.355, 0.034),
            (-0.335, 0.095), (-0.31, 0.148), (-0.285, 0.158), (-0.235, 0.158), (-0.23, 0.142),
            (-0.05, 0.132), (-0.045, 0.146), (0.035, 0.146), (0.04, 0.128), (0.40, 0.104),
            (0.405, 0.12), (0.45, 0.12), (0.455, 0.104), (0.54, 0.098), (0.585, 0.128),
            (0.645, 0.136), (0.655, 0.12), (0.655, 0.074), (0.6, 0.068), (0.25, 0.068), (0.25, 0.0)]
    brass_t = ((-0.29, -0.228), (0.40, 0.456), (0.58, 0.66))
    def barrel_mat(c):
        t = -c.y
        return "brass" if any(a < t < b for a, b in brass_t) and math.hypot(c.x, c.z) > 0.09 else "iron"
    bar = Part()
    bar.add(lathe(prof, rot=(R(90), 0, 0), seg=36), "iron", bevel=0.008, seg=2, angle=40, mat_fn=barrel_mat)
    bar.add(cyl(0.055, 0.42, (0, 0, 0), (0, R(90), 0), seg=16), "iron", bevel=0.01)
    barrel = bar.to_object("Heavy_Barrel", coll, mats, elev)

    flash, light = muzzle_flash("Heavy_MuzzleFlash", coll, barrel, (0, -0.665, 0), 1.25, fx["core"], fx["flash"])
    puffs = [smoke_puff(f"Heavy_Smoke_{i + 1:02d}", coll, root, fx["smoke"]) for i in range(7)]

    # Wystrzał: przygarbienie, odrzut lufy, podskok wieżyczki, błysk i dym, powrót do pozycji.
    f = 12
    base_rot = elev.rotation_euler.x
    for fr, rx in ((1, base_rot), (8, base_rot - R(3)), (f, base_rot - R(3)), (f + 1, base_rot - R(10)),
                   (f + 7, base_rot + R(2)), (f + 16, base_rot - R(0.8)), (f + 26, base_rot), (FRAMES, base_rot)):
        key(elev, "rotation_euler", fr, (rx, 0, 0), "LINEAR" if fr == f else "BEZIER")
    for fr, y in ((1, 0.0), (f, 0.0), (f + 1, 0.17), (f + 3, 0.15), (f + 22, -0.01), (f + 28, 0.0), (FRAMES, 0.0)):
        key(barrel, "location", fr, (0, y, 0), "LINEAR" if fr == f else "BEZIER")
    for fr, s in ((1, (1, 1, 1)), (8, (1.03, 1.03, 0.94)), (f, (1.03, 1.03, 0.94)), (f + 1, (0.95, 0.95, 1.08)),
                  (f + 4, (1.05, 1.05, 0.93)), (f + 9, (0.99, 0.99, 1.015)), (f + 16, (1, 1, 1)), (FRAMES, (1, 1, 1))):
        key(turret, "scale", fr, s, "LINEAR" if fr == f else "BEZIER")
    for fr, (dy, rz) in ((1, (0, 0)), (f, (0, 0)), (f + 1, (0.035, R(2.5))), (f + 3, (0.02, R(-2))),
                         (f + 6, (0.006, R(1))), (f + 10, (0, R(-0.4))), (f + 16, (0, 0)), (FRAMES, (0, 0))):
        key(turret, "location", fr, (0, dy, pivot_z), "LINEAR" if fr == f else "BEZIER")
        key(turret, "rotation_euler", fr, (0, 0, rz), "LINEAR" if fr == f else "BEZIER")
    flash_keys(flash, light, f + 1, 120.0)
    origin, direction = muzzle_world(flash)
    smoke_keys(puffs, origin, direction, f + 1, 0.2, 0.9)
    return root, mats


# ---------------------------------------------------------------- bliźniacze działko

def build_twin(coll, fx):
    mats = [painted_material(f"Twin_{k}", k) for k in ("iron", "brass", "wood")]
    root = empty("Cannon_Twin", coll, None, size=0.4)
    pivot_z = build_base("Twin", coll, mats, root)

    tur = Part()
    tur.add(cyl(0.3, 0.06, (0, 0, 0.03), seg=36), "iron", bevel=0.014)
    tur.add(cyl(0.312, 0.024, (0, 0, 0.03), seg=36), "brass", bevel=0.006, seg=2)
    tur.add(cyl(0.15, 0.12, (0, 0, 0.13), seg=28, r2=0.12), "iron", bevel=0.014)
    tur.add(box((0.46, 0.24, 0.07), (0, 0, 0.215)), "iron", bevel=0.016)
    for sx in (-1, 1):
        tur.add(box((0.07, 0.22, 0.2), (sx * 0.2, 0, 0.35)), "iron", bevel=0.016)
        tur.add(cyl(0.11, 0.07, (sx * 0.2, 0, 0.44), (0, R(90), 0), seg=24), "iron", bevel=0.014)
        tur.add(cyl(0.055, 0.04, (sx * 0.25, 0, 0.44), (0, R(90), 0), seg=16), "brass", bevel=0.01)
        tur.add(bolt((sx * 0.2, -0.122, 0.3), (R(90), 0, 0), r=0.018), "iron", bevel=0.007, seg=2)
    turret = tur.to_object("Twin_Turret", coll, mats, root, (0, 0, pivot_z))

    elev = empty("Twin_Elevation", coll, turret, (0, 0, 0.44), (R(-36), 0, 0))
    body = Part()
    body.add(box((0.3, 0.42, 0.22), (0, 0.04, 0.0)), "iron", bevel=0.03)
    body.add(cyl(0.05, 0.36, (0, 0, 0), (0, R(90), 0), seg=16), "iron", bevel=0.01)
    body.add(box((0.32, 0.06, 0.24), (0, -0.18, 0.0)), "iron", bevel=0.02)
    body.add(box((0.18, 0.2, 0.05), (0, 0.08, 0.125)), "brass", bevel=0.014)
    body.add(cyl(0.035, 0.03, (0, 0.08, 0.155), seg=12), "iron", bevel=0.01, seg=2)
    body.add(box((0.24, 0.05, 0.16), (0, 0.27, -0.01)), "iron", bevel=0.015)
    for bx in (-0.13, 0.13):
        for bz in (-0.085, 0.085):
            body.add(bolt((bx, -0.212, bz), (R(90), 0, 0), r=0.016), "brass", bevel=0.006, seg=2)
    gun = body.to_object("Twin_Body", coll, mats, elev)

    prof = [(-0.02, 0.0), (-0.02, 0.062), (0.1, 0.062), (0.11, 0.046), (0.22, 0.046), (0.225, 0.056),
            (0.27, 0.056), (0.275, 0.046), (0.44, 0.044), (0.445, 0.066), (0.55, 0.066), (0.56, 0.05),
            (0.56, 0.03), (0.47, 0.03), (0.47, 0.0)]
    def barrel_mat(c):
        t = -c.y
        return "brass" if 0.22 < t < 0.28 and math.hypot(c.x, c.z) > 0.04 else "iron"
    barrels, flashes, lights, puffsets = [], [], [], []
    for side, sx in (("L", -0.085), ("R", 0.085)):
        bp = Part()
        bp.add(lathe(prof, rot=(R(90), 0, 0), seg=24), "iron", bevel=0.006, seg=2, angle=40, mat_fn=barrel_mat)
        for i in range(4):
            a = R(45 + 90 * i)
            bp.add(box((0.02, 0.06, 0.02), (math.cos(a) * 0.066, -0.5, math.sin(a) * 0.066), (0, a, 0)),
                   "iron", bevel=0.005, seg=2)
        b = bp.to_object(f"Twin_Barrel_{side}", coll, mats, gun, (sx, -0.21, 0.0))
        fl, li = muzzle_flash(f"Twin_MuzzleFlash_{side}", coll, b, (0, -0.565, 0), 0.75, fx["core"], fx["flash"])
        barrels.append(b)
        flashes.append(fl)
        lights.append(li)
        puffsets.append([smoke_puff(f"Twin_Smoke_{side}_{i + 1:02d}", coll, root, fx["smoke"]) for i in range(8)])

    # Seria czterech strzałów na zmianę z lewej i prawej lufy.
    shots = (8, 14, 20, 26)
    base_rot = elev.rotation_euler.x
    ekeys = [(1, base_rot), (5, base_rot - R(2))]
    skeys = [(1, (1, 1, 1)), (5, (1.02, 1.02, 0.96))]
    for n, f in enumerate(shots):
        ekeys += [(f, base_rot - R(1)), (f + 1, base_rot - R(5)), (f + 3, base_rot - R(1.5))]
        skeys += [(f, (1.0, 1.0, 1.0)), (f + 1, (1.03, 1.03, 0.95)), (f + 3, (0.99, 0.99, 1.01))]
    ekeys += [(shots[-1] + 10, base_rot + R(1.5)), (shots[-1] + 20, base_rot), (FRAMES, base_rot)]
    skeys += [(shots[-1] + 10, (1, 1, 1)), (FRAMES, (1, 1, 1))]
    for fr, rx in ekeys:
        key(elev, "rotation_euler", fr, (rx, 0, 0), "LINEAR" if fr in shots else "BEZIER")
    for fr, s in skeys:
        key(turret, "scale", fr, s, "LINEAR" if fr in shots else "BEZIER")
    for b in barrels:
        key(b, "location", 1, b.location.copy())
    origins = {}
    for i in range(2):
        origins[i] = muzzle_world(flashes[i])
    for n, f in enumerate(shots):
        i = n % 2
        b = barrels[i]
        x = b.location.x
        key(b, "location", f, (x, -0.21, 0), "LINEAR")
        key(b, "location", f + 1, (x, -0.21 + 0.11, 0))
        key(b, "location", f + 5, (x, -0.21, 0))
        flash_keys(flashes[i], lights[i], f + 1, 45.0)
        o, d = origins[i]
        smoke_keys(puffsets[i][(n // 2) * 4:(n // 2) * 4 + 4], o, d, f + 1, 0.11, 0.55, life=24)
    for b in barrels:
        key(b, "location", FRAMES, (b.location.x, -0.21, 0))
    return root, mats


# ---------------------------------------------------------------- UV i wypiek

def select_only(objs, active=None):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def unwrap(meshes):
    select_only(meshes)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=R(60), island_margin=0.006, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.006)
    bpy.ops.object.mode_set(mode="OBJECT")


def dilate(img, steps):
    """Rozlewa kolor wysp UV na puste piksele (margines dla mipmap), potem zeruje przezroczystość."""
    px = np.empty(TEX * TEX * 4, np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(TEX, TEX, 4)
    filled = px[:, :, 3] > 0.5
    rgb = px[:, :, :3] * filled[:, :, None]
    for _ in range(steps):
        acc = np.zeros_like(rgb)
        cnt = np.zeros(filled.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    m = np.roll(np.roll(filled, dy, 0), dx, 1)
                    acc += np.roll(np.roll(rgb, dy, 0), dx, 1) * m[:, :, None]
                    cnt += m
        grow = ~filled & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        filled |= grow
    px[:, :, :3] = rgb
    px[:, :, 3] = 1.0
    img.pixels.foreach_set(px.ravel())


def bake_maps(prefix, meshes, mats, texdir):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.render.bake.margin = 0
    images = {}
    passes = (("BaseColor", "color", "sRGB", 96), ("Roughness", "rough", "Non-Color", 48),
              ("Metallic", "metal", "Non-Color", 1), ("Normal", "normal", "Non-Color", 48))
    for label, src, space, samples in passes:
        img = bpy.data.images.new(f"{prefix}_{label}", TEX, TEX, alpha=True, float_buffer=False)
        img.colorspace_settings.name = space
        img.pixels.foreach_set(np.zeros(TEX * TEX * 4, np.float32))
        for m in mats:
            info = MATINFO[m.name]
            nt = m.node_tree
            info["tex"].image = img
            nt.nodes.active = info["tex"]
            for l in list(info["out"].inputs["Surface"].links):
                nt.links.remove(l)
            if src == "normal":
                nt.links.new(info["bsdf"].outputs[0], info["out"].inputs["Surface"])
            else:
                for l in list(info["emit"].inputs["Color"].links):
                    nt.links.remove(l)
                nt.links.new(info[src], info["emit"].inputs["Color"])
                nt.links.new(info["emit"].outputs[0], info["out"].inputs["Surface"])
        scene.cycles.samples = samples
        select_only(meshes)
        print(f"  wypiek {prefix} {label}...", flush=True)
        # Margines zero: przy kilku obiektach w jednym atlasie margines jednego zamazuje drugi.
        bpy.ops.object.bake(type="NORMAL" if src == "normal" else "EMIT", normal_space="TANGENT",
                            use_clear=False, margin=0)
        dilate(img, 16)
        img.filepath_raw = os.path.join(texdir, f"{prefix}_{label}.png")
        img.file_format = "PNG"
        img.save()
        images[label] = img
    for m in mats:
        info = MATINFO[m.name]
        for l in list(info["out"].inputs["Surface"].links):
            m.node_tree.links.remove(l)
        m.node_tree.links.new(info["bsdf"].outputs[0], info["out"].inputs["Surface"])

    # Mapa dla Unity URP Lit: RGB = metaliczność, A = gładkość (1 - szorstkość).
    n = TEX * TEX * 4
    rough = np.empty(n, np.float32)
    metal = np.empty(n, np.float32)
    images["Roughness"].pixels.foreach_get(rough)
    images["Metallic"].pixels.foreach_get(metal)
    ms = metal.reshape(-1, 4).copy()
    ms[:, 3] = 1.0 - rough.reshape(-1, 4)[:, 0]
    out = bpy.data.images.new(f"{prefix}_MetallicSmoothness", TEX, TEX, alpha=True)
    out.colorspace_settings.name = "Non-Color"
    out.pixels.foreach_set(ms.ravel())
    out.filepath_raw = os.path.join(texdir, f"{prefix}_MetallicSmoothness.png")
    out.file_format = "PNG"
    out.save()
    return images


def baked_material(prefix, images):
    mat = bpy.data.materials.new(f"M_{prefix}")
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    def tex(label, y):
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = images[label]
        node.location = (-500, y)
        return node
    nt.links.new(tex("BaseColor", 300).outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex("Roughness", 0).outputs["Color"], bsdf.inputs["Roughness"])
    nt.links.new(tex("Metallic", -250).outputs["Color"], bsdf.inputs["Metallic"])
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tex("Normal", -500).outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def apply_material(meshes, mat):
    for ob in meshes:
        me = ob.data
        me.materials.clear()
        me.materials.append(mat)
        me.polygons.foreach_set("material_index", [0] * len(me.polygons))


# ---------------------------------------------------------------- scena podglądu

def build_stage(coll):
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
    deck = bpy.context.active_object
    deck.name = "Stage_Deck"
    for c in deck.users_collection:
        c.objects.unlink(deck)
    coll.objects.link(deck)
    mat = bpy.data.materials.new("Stage_DeckWood")
    k = NodeKit(mat)
    coord = k.new("ShaderNodeTexCoord").outputs["Object"]
    brick = k.new("ShaderNodeTexBrick", offset=0.37, offset_frequency=1, squash=1.0, squash_frequency=1)
    brick.inputs["Color1"].default_value = col("#5a2f17")
    brick.inputs["Color2"].default_value = col("#7a4322")
    brick.inputs["Mortar"].default_value = col("#1c0d06")
    brick.inputs["Scale"].default_value = 1.0
    brick.inputs["Mortar Size"].default_value = 0.008
    brick.inputs["Brick Width"].default_value = 2.6
    brick.inputs["Row Height"].default_value = 0.42
    k.link(coord, brick.inputs["Vector"])
    wave = k.new("ShaderNodeTexWave", wave_type="BANDS", bands_direction="Y")
    wave.inputs["Scale"].default_value = 2.0
    wave.inputs["Distortion"].default_value = 9.0
    wave.inputs["Detail"].default_value = 3.0
    k.link(coord, wave.inputs["Vector"])
    c = k.mix(k.remap(wave.outputs["Fac"], 0.2, 0.9, 0.0, 0.35), brick.outputs["Color"], col("#9b5a2c"))
    bsdf = k.new("ShaderNodeBsdfPrincipled")
    k.link(c, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    out = k.new("ShaderNodeOutputMaterial")
    k.link(bsdf.outputs[0], out.inputs["Surface"])
    deck.data.materials.append(mat)

    sun = bpy.data.lights.new("Stage_Sun", "SUN")
    sun.energy = 3.2
    sun.color = (1.0, 0.82, 0.62)
    sun.angle = R(6)
    so = bpy.data.objects.new("Stage_Sun", sun)
    so.rotation_euler = (R(48), R(-18), R(-35))
    coll.objects.link(so)
    fill = bpy.data.lights.new("Stage_Fill", "AREA")
    fill.energy = 220
    fill.size = 4
    fill.color = (0.6, 0.72, 1.0)
    fo = bpy.data.objects.new("Stage_Fill", fill)
    fo.location = (3.5, -3.0, 3.0)
    fo.rotation_euler = (R(55), 0, R(50))
    coll.objects.link(fo)
    lamp = bpy.data.lights.new("Stage_Lantern", "POINT")
    lamp.energy = 120
    lamp.color = (1.0, 0.6, 0.28)
    lamp.shadow_soft_size = 0.3
    lo = bpy.data.objects.new("Stage_Lantern", lamp)
    lo.location = (2.4, 1.6, 1.6)
    coll.objects.link(lo)

    cam = bpy.data.cameras.new("Stage_Camera")
    cam.lens = 42
    co = bpy.data.objects.new("Stage_Camera", cam)
    co.location = (0.35, -4.1, 3.0)
    co.rotation_euler = (R(58), 0, R(4))
    coll.objects.link(co)
    scene = bpy.context.scene
    scene.camera = co
    world = bpy.data.worlds.new("Stage_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = col("#3a2a20")
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world
    return deck, co


# ---------------------------------------------------------------- eksport

def hierarchy(root):
    return [root] + list(root.children_recursive)


def export(root, outdir):
    objs = [o for o in hierarchy(root) if o.type in {"MESH", "EMPTY"}]
    keep = root.location.copy()
    root.location = (0, 0, 0)
    select_only(objs, root)
    name = root.name
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, f"{name}.fbx"), use_selection=True,
                             object_types={"MESH", "EMPTY"}, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=False,
                             bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             path_mode="RELATIVE", embed_textures=False)
    bpy.ops.export_scene.gltf(filepath=os.path.join(outdir, f"{name}.glb"), export_format="GLB",
                              use_selection=True, export_animation_mode="SCENE", export_lights=False,
                              export_cameras=False, export_apply=True)
    root.location = keep


def render_setup(w, h, samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Punchy"
    scene.render.image_settings.file_format = "PNG"


def make_gif(fdir, path):
    """Klatki PNG -> zapętlony GIF (ffmpeg z własną paletą), klatki są potem usuwane."""
    import shutil
    import subprocess
    if not shutil.which("ffmpeg"):
        print("brak ffmpeg, klatki zostają w", fdir)
        return
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", os.path.join(fdir, "f_%04d.png"),
                    "-vf", "split[a][b];[a]palettegen=max_colors=200[p];[b][p]paletteuse=dither=bayer:bayer_scale=4",
                    "-loop", "0", path], check=True)
    shutil.rmtree(fdir)


def main():
    global TEX
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="art/cannons")
    ap.add_argument("--no-gif", action="store_true")
    ap.add_argument("--gif-samples", type=int, default=24)
    ap.add_argument("--tex", type=int, default=TEX)
    args = ap.parse_args(argv)
    TEX = args.tex
    out = os.path.abspath(args.out)
    texdir = os.path.join(out, "textures")
    rdir = os.path.join(out, "renders")
    for d in (out, texdir, rdir):
        os.makedirs(d, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Fire"  # nazwa klipu animacji w FBX/GLB
    scene.render.fps = FPS
    scene.frame_start = 1
    scene.frame_end = FRAMES
    scene.unit_settings.system = "METRIC"
    coll = scene.collection
    cannons = bpy.data.collections.new("Cannons")
    stage = bpy.data.collections.new("Stage")
    coll.children.link(cannons)
    coll.children.link(stage)

    fx = dict(flash=fx_material("M_MuzzleFlash", "#ff6a10", 3.0), core=fx_material("M_MuzzleCore", "#ffd76a", 6.0),
              smoke=fx_material("M_Smoke", "#b9ae9f", 0.0, rough=1.0))
    scene.frame_set(1)
    twin, twin_mats = build_twin(cannons, fx)
    heavy, heavy_mats = build_heavy(cannons, fx)

    for root, mats, prefix in ((twin, twin_mats, "Cannon_Twin"), (heavy, heavy_mats, "Cannon_Heavy")):
        meshes = [o for o in hierarchy(root) if o.type == "MESH" and o.data.materials[0].name in MATINFO]
        scene.frame_set(1)
        unwrap(meshes)
        images = bake_maps(prefix, meshes, mats, texdir)
        apply_material(meshes, baked_material(prefix, images))
        for m in mats:
            bpy.data.materials.remove(m)
        export(root, out)

    twin.location = (-0.85, 0.15, 0)
    heavy.location = (0.85, -0.05, 0)
    heavy.rotation_euler = (0, 0, R(-55))
    twin.rotation_euler = (0, 0, R(40))
    build_stage(stage)

    render_setup(1600, 900, 96)
    scene.frame_set(1)
    scene.render.filepath = os.path.join(rdir, "preview.png")
    bpy.ops.render.render(write_still=True)
    scene.frame_set(14)
    scene.render.filepath = os.path.join(rdir, "preview_fire.png")
    bpy.ops.render.render(write_still=True)

    for img in bpy.data.images:
        if img.filepath_raw:
            img.filepath = bpy.path.relpath(img.filepath_raw, start=out)
    bpy.ops.file.pack_all()  # .blend z teksturami w środku, otwiera się bez folderu textures/
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "cannons.blend"), compress=True, relative_remap=True)

    if not args.no_gif:
        render_setup(640, 360, args.gif_samples)
        fdir = os.path.join(rdir, "frames")
        os.makedirs(fdir, exist_ok=True)
        scene.render.filepath = os.path.join(fdir, "f_")
        bpy.ops.render.render(animation=True)
        make_gif(fdir, os.path.join(rdir, "fire.gif"))
    print("gotowe:", out)


if __name__ == "__main__":
    main()
