"""Eksport placeholderów do Unity: PNG (paleta mroczna) + pivots.json + prototype_layout.json.

Uruchom z katalogu tools/pixel-proto:  python3 export_unity.py
Wynik trafia do Assets/_Project/Art/ w katalogu głównym repo.
"""
import os, json
import daynight            # ustawia paletę mroczną + kolory emisyjne, buduje scenę z anim.py
import anim
from sprites import (Sprite, RGB, seed, carrot, wheat, pumpkin, round_tree, spruce, bush, rock,
                     crate, coin, plot)
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
SPR = os.path.join(ART, "Sprites")
W, H = anim.W, anim.H
pivots = []


def save(img, sub, name, pivot):
    os.makedirs(os.path.join(SPR, sub), exist_ok=True)
    img.save(os.path.join(SPR, sub, name + ".png"))
    pivots.append({"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)})


def to_img(spr, keys=None, remap=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if keys is not None and k not in keys:
            continue
        if remap and k in remap:
            k = remap[k]
        im.putpixel((x, y), RGB[k] + (255,))
    return im


def anchor_pivot(spr):
    # punkt zaczepienia (ax, ay) = dolna krawędź piksela ay, środek kolumny ax; Unity liczy y od dołu
    return ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)


def export_sprite(spr, sub, name):
    save(to_img(spr), sub, name, anchor_pivot(spr))


# --- rośliny (faza 0 = nasiono) ---
for cname, fn in (("carrot", carrot), ("wheat", wheat), ("pumpkin", pumpkin)):
    export_sprite(seed(), "Crops", f"crop_{cname}_0")
    for st in (1, 2, 3):
        export_sprite(fn(st), "Crops", f"crop_{cname}_{st}")

# --- obiekty ---
for name, fn in (("tree_round", round_tree), ("tree_spruce", spruce), ("bush", bush), ("rock", rock), ("crate", crate)):
    export_sprite(fn(), "Objects", name)
save(to_img(plot()), "Ground", "plot", (0.5, 0.5))
export_sprite(coin(), "UI", "coin")

# --- lampy: podstawa (emisja zgaszona) + osobny sprite "glow" z samymi pikselami emisyjnymi ---
LAMP_IDS = {daynight.lantern_post: "lantern", daynight.skull_candle: "skull",
            daynight.wisp_cage: "wisp", daynight.mushrooms: "mushrooms"}
lamps_layout = []
for fn, x, y, (lx, ly), col, r in daynight.LAMPS:
    lid = LAMP_IDS[fn]
    spr = fn(0)
    pv = anchor_pivot(spr)
    save(to_img(spr, remap=daynight.OFF), "Lamps", f"lamp_{lid}", pv)
    save(to_img(spr, keys=daynight.EMISSIVE), "Lamps", f"lamp_{lid}_glow", pv)
    lamps_layout.append({"id": lid, "sprite": f"lamp_{lid}", "x": x, "y": y, "lightX": lx, "lightY": ly,
                         "radius": r, "r": col[0] / 1.25, "g": col[1] / 1.25, "b": col[2] / 1.25})

# --- teren: wyspa (z cieniami obiektów) i woda (statyczna klatka) ---
island = Image.new("RGBA", (W, H), (0, 0, 0, 0))
def put_i(x, y, k):
    if 0 <= x < W and 0 <= y < H: island.putpixel((x, y), RGB[k] + (255,))
for p in anim.outline_px: put_i(*p, 'o')
for p, k in anim.cliff.items(): put_i(*p, k)
for p, k in anim.surface.items(): put_i(*p, k)
for name, x, y, sw in anim.OBJECTS:
    for dx in range(-sw // 2, sw // 2 + 1):
        for d in (-1, 0, 1):
            if abs(dx) <= sw // 2 - abs(d) * 2: put_i(x + dx, y + d, 'g1')
save(island, "Ground", "island", (0.5, 0.5))

water = Image.new("RGBA", (W, H), (0, 0, 0, 0))
def put_w(x, y, k):
    if 0 <= x < W and 0 <= y < H: water.putpixel((x, y), RGB[k] + (255,))
anim.WATER.draw(put_w, 0)
save(water, "Ground", "water", (0.5, 0.5))

# --- układ sceny (współrzędne w pikselach kadru, y w dół) ---
plots = []
for row in range(2):
    for col in range(4):
        px_, py_ = anim.plot_pos(col, row)
        plots.append({"row": row, "col": col, "x": px_ + 9, "y": py_ + 7, "cropX": px_ + 9, "cropY": py_ + 10})
NAME = {"tree": "tree_round", "spruce": "tree_spruce", "bush": "bush", "rock": "rock", "crate": "crate"}
layout = {
    "width": W, "height": H, "ppu": 16,
    "plots": plots,
    "objects": [{"sprite": NAME[n], "x": x, "y": y} for n, x, y, _ in anim.OBJECTS],
    "lamps": lamps_layout,
}
os.makedirs(ART, exist_ok=True)
with open(os.path.join(ART, "prototype_layout.json"), "w") as f:
    json.dump(layout, f, indent=2)
with open(os.path.join(SPR, "pivots.json"), "w") as f:
    json.dump({"items": pivots}, f, indent=2)
print(f"ok: {len(pivots)} sprite'ów -> {SPR}")
