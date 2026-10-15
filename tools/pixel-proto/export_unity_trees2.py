"""Eksport nowych drzew (trees2.py) do Unity: Objects/tree_round, tree_spruce, bush, Zones/dead_tree, klatki przewracania
okrągłego drzewa (Removal/tree_round_fall_*) i drzewa owocowe Trees/tree_<gatunek>_<faza> (+ _glow, + tree_<gatunek>_fall_*).
Nadpisuje grafiki z export_unity.py / export_unity_trees.py / export_unity_zones.py, więc uruchamiaj po nich, a potem
pad_sway.py (wpisy pivotów są tu zapisywane bez pola "pad", żeby margines na kołysanie doszedł od nowa) i export_unity_stages.py.
    python3 tools/pixel-proto/export_unity_trees2.py
"""
import os, json
from PIL import Image
from sprites3 import RGB, EMISSIVE
import sprites4 as S4
import sprites5 as S5
import trees2 as T

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}


def to_img(spr, only=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if only is None or k in only:
            im.putpixel((x, y), RGB[k] + (255,))
    return im


def save(spr, sub, name, glow=True):
    d = os.path.join(SPR, sub)
    os.makedirs(d, exist_ok=True)
    pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
    to_img(spr).save(os.path.join(d, name + ".png"))
    items[name] = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
    gpath = os.path.join(d, name + "_glow.png")
    if glow and any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(gpath)
        items[name + "_glow"] = {"name": name + "_glow", "x": items[name]["x"], "y": items[name]["y"]}
    elif os.path.exists(gpath):                     # stara poświata bez pokrycia w nowym rysunku
        os.remove(gpath)
        if os.path.exists(gpath + ".meta"):
            os.remove(gpath + ".meta")
        items.pop(name + "_glow", None)


FALL = (0, 12, 35, 70)
rt = T.round_tree(0)
save(rt, "Objects", "tree_round")
save(T.spruce(0), "Objects", "tree_spruce")
save(T.bush(0), "Objects", "bush")
save(T.dead_tree(0), "Zones", "dead_tree")
for i, a in enumerate(FALL):
    save(S4.rotated(rt, a, (rt.ax, rt.ay)), "Removal", f"tree_round_fall_{i}", glow=False)
for sid in S5.SPECIES:
    for st in range(4):
        save(T.fruit_tree(sid, st), "Trees", f"tree_{sid}_{st}")
    adult = T.fruit_tree(sid, 2)
    for i, a in enumerate(FALL):
        save(S4.rotated(adult, a, (adult.ax, adult.ay)), "Trees", f"tree_{sid}_fall_{i}", glow=False)
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: drzewa trees2, {len(items)} wpisów w pivots.json")
