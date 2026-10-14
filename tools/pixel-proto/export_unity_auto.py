"""Eksport assetów Etapu 6b: nowe rośliny (fazy 0-3), ikony nasion, duszek-handlarz.

Uruchom z tools/pixel-proto:  python3 export_unity_auto.py   (po export_unity_ui.py - dopisuje do pivots.json)
"""
import os, json
import sprites
import sprites3 as S3
import sprites6 as S6
import ui_kit as K
from sprites3 import RGB, EMISSIVE
from PIL import Image

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


def save(spr, sub, name, pivot=None, glow=True):
    """Jak w export_unity_ui: PNG + wpis pivota, piksele emisyjne osobno w *_glow.png."""
    d = os.path.join(SPR, sub)
    os.makedirs(d, exist_ok=True)
    if pivot is None:
        pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
    to_img(spr).save(os.path.join(d, name + ".png"))
    items[name] = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
    if glow and any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(os.path.join(d, name + "_glow.png"))
        items[name + "_glow"] = {"name": name + "_glow", "x": items[name]["x"], "y": items[name]["y"]}


for cid, fn in S6.CROPS_DAY.items():
    save(sprites.seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")
for cid, fn in S6.CROPS_NIGHT.items():
    save(S3.night_seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")

for f in range(4):
    save(S6.merchant_spirit(f), "Automation", f"merchant_spirit_{f}")
    save(S3.wisp_gardener(f, bob=False), "Automation", f"wisp_gardener_{f}")   # bez podskoku: płynny lot w kodzie

for kind in ('crow', 'finch'):
    for pose in S6.BIRD_POSES:
        save(S6.bird(kind, pose), "Critters", f"bird_{kind}_{pose}", glow=False)

for name, rows in S6.ICON_MAPS.items():
    save(K.from_map(rows), "UI/Icons", "icon_" + name, pivot=(0.5, 0.5), glow=False)

K.SEED_SYMBOLS.update(S6.SEED_SYMBOLS)
for name in S6.SEED_SYMBOLS:
    save(K.seed_packet(name), "UI/Icons", "icon_seed_" + name, pivot=(0.5, 0.5), glow=False)

with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(items)} wpisów w pivots.json")
