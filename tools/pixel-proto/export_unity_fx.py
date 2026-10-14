"""Eksport do Unity dla Etapu 3a (żywa wyspa): woda pod shader, mgła, sprite'y cząsteczek.

Uruchom z tools/pixel-proto:  python3 export_unity_fx.py   (po export_unity.py; dopisuje do pivots.json)

- Ground/water_base.png: statyczna woda (głębia, cień klifu, piana) - to, co widać, gdy shader nie działa.
- Ground/water_data.png: dane dla shaderów wody i mgły (import bez sRGB):
    R = odległość od brzegu w pikselach x4 (0..255), G = klasa piksela x32
    (0 ląd, 1 płycizna b2, 2 środek b1, 3 głębia b0, 4 najgłębsza p0, 5 cień klifu, 6 piana 1, 7 piana 2).
- Art/water_layout.json: ścieżki ryb (ósemki) i miejsce potwora w głębinie, liczone tak jak w prototypie (water.py).
- Fx/*: drobne sprite'y cząsteczek (iskierki, świetliki, ziemia, listki).
"""
import os, json
import daynight            # paleta mroczna (jak export_unity.py)
import anim
import export_unity_ui as E
from sprites import Sprite, RGB, sparkle
from PIL import Image

W, H = anim.W, anim.H
WATER = anim.WATER
CLASS = {'b2': 1, 'b1': 2, 'b0': 3, 'p0': 4}

base = Image.new("RGBA", (W, H), (0, 0, 0, 0))
data = Image.new("RGBA", (W, H), (0, 0, 0, 255))
for (x, y), k in WATER.base.items():
    cls = CLASS[k]
    col = k
    if (x, y) in anim.shadow:
        cls, col = 5, 'b0'
    if (x, y) in anim.ring1:
        cls, col = 6, 'fo'
    if (x, y) in anim.ring2:
        cls, col = 7, 'b2'
    base.putpixel((x, y), RGB[col] + (255,))
    d = min(255, round(WATER.dist[y][x] * 4))
    data.putpixel((x, y), (d, cls * 32, 0, 255))
gdir = os.path.join(E.SPR, "Ground")
base.save(os.path.join(gdir, "water_base.png"))
data.save(os.path.join(gdir, "water_data.png"))
for n in ("water_base", "water_data"):
    E.items[n] = {"name": n, "x": 0.5, "y": 0.5}

layout = {
    "width": W, "height": H, "frames": anim.FRAMES,
    "fish": [{"cx": cx, "cy": cy, "rx": round(rx, 3), "ry": round(ry, 3), "phase": round(ph, 4), "glow": bool(g)}
             for cx, cy, rx, ry, ph, g in WATER.fish],
    "beast": {"x": WATER.beast[0], "y": WATER.beast[1]},
}
with open(os.path.join(E.ART, "water_layout.json"), "w") as f:
    json.dump(layout, f, indent=2)


def px(w, h, pts):
    s = Sprite(w, h, w // 2, h // 2)
    for x, y, k in pts:
        s.set(x, y, k)
    return s


fx = {
    "fx_sparkle_0": sparkle(0), "fx_sparkle_1": sparkle(1), "fx_sparkle_2": sparkle(2),
    "fx_firefly_0": px(3, 3, [(1, 1, 'cr')]),
    "fx_firefly_1": px(3, 3, [(1, 1, 'cr'), (0, 1, 'go'), (2, 1, 'go'), (1, 0, 'go'), (1, 2, 'go')]),
    "fx_soil_0": px(1, 1, [(0, 0, 'e2')]),
    "fx_soil_1": px(2, 1, [(0, 0, 'e1'), (1, 0, 'e2')]),
    "fx_leaf_0": px(2, 1, [(0, 0, 'g3'), (1, 0, 'g1')]),
    "fx_leaf_1": px(2, 1, [(0, 0, 'g2'), (1, 0, 'g1')]),
    "fx_leaf_fall": px(2, 1, [(0, 0, 'g3'), (1, 0, 'g2')]),
}
for name, spr in fx.items():
    E.save(spr, "Fx", name, pivot=(0.5, 0.5), glow=False)

with open(E.PIVOTS, "w") as f:
    json.dump({"items": sorted(E.items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: woda + {len(fx)} cząsteczek, {len(layout['fish'])} ryb; {len(E.items)} wpisów w pivots.json")
