"""Eksport roślin sezonowych (GDD §9 p. 129): fazy 0-3 i ikony nasion 16 roślin z seasonal_crops.py.

Uruchom z tools/pixel-proto:  python3 export_unity_seasonal_crops.py   (dopisuje do pivots.json; przed pad_sway.py,
potem export_unity_stages.py dorobi kiełek i niedojrzałą, a na końcu export_unity_seasons.py)
"""
import os, json
import sprites
import sprites3 as S3
import seasonal_crops as SC
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
    """Jak w export_unity_auto: PNG + wpis pivota, piksele emisyjne osobno w *_glow.png."""
    d = os.path.join(SPR, sub)
    os.makedirs(d, exist_ok=True)
    if pivot is None:
        pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
    to_img(spr).save(os.path.join(d, name + ".png"))
    items[name] = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
    glow_path = os.path.join(d, name + "_glow.png")
    if glow and any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(glow_path)
        items[name + "_glow"] = {"name": name + "_glow", "x": items[name]["x"], "y": items[name]["y"]}
    elif os.path.exists(glow_path):
        os.remove(glow_path)
        items.pop(name + "_glow", None)


for cid, (fn, night, season) in SC.CROPS.items():
    save(S3.night_seed() if night else sprites.seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")

K.SEED_SYMBOLS.update(SC.SEED_SYMBOLS)
for name in SC.SEED_SYMBOLS:
    save(K.seed_packet(name), "UI/Icons", "icon_seed_" + name, pivot=(0.5, 0.5), glow=False)

with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(SC.CROPS)} roślin sezonowych, {len(items)} wpisów w pivots.json")
