"""Eksport nowych assetów do Unity: rośliny nocy, hybryda, obiekty §11, ikony, elementy UI (9-slice), font.

Uruchom z tools/pixel-proto:  python3 export_unity_ui.py   (po export_unity.py - dopisuje do pivots.json)
Obiekty z pikselami emisyjnymi dostają drugi plik *_glow.png (tylko emisja) - w Unity na materiale Unlit.
"""
import os, json
import sprites3 as S3
import ui_kit as K
from sprites3 import RGB, EMISSIVE
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
SPR = os.path.join(ART, "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")

items = {}
if os.path.exists(PIVOTS):
    for it in json.load(open(PIVOTS))["items"]:
        items[it["name"]] = it


def to_img(spr, only=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if only is not None and k not in only:
            continue
        im.putpixel((x, y), RGB[k] + (255,))
    return im


def save(spr, sub, name, pivot=None, border=None, glow=True):
    d = os.path.join(SPR, sub)
    os.makedirs(d, exist_ok=True)
    if pivot is None:
        pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
    to_img(spr).save(os.path.join(d, name + ".png"))
    entry = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
    if border:
        entry["border"] = list(border)          # L, B, R, T w pikselach
    items[name] = entry
    if glow and any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(os.path.join(d, name + "_glow.png"))
        items[name + "_glow"] = {"name": name + "_glow", "x": entry["x"], "y": entry["y"]}


# --- rośliny nocy + hybryda ---
for cid, (_, fn) in S3.CROPS_NIGHT.items():
    save(S3.night_seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")
import sprites
save(sprites.seed(), "Crops", "crop_hybridpumpkin_0")
for st in (1, 2):
    save(sprites.pumpkin(st), "Crops", f"crop_hybridpumpkin_{st}")
save(S3.hybrid_pumpkin(), "Crops", "crop_hybridpumpkin_3")

# --- obiekty ---
for cat, objs in S3.OBJECTS.items():
    for name, fn in objs.items():
        save(fn(), cat, name)

# --- UI: ikony, 9-slice, proste ---
for name, ic in K.all_icons().items():
    save(ic, "UI/Icons", "icon_" + name, pivot=(0.5, 0.5), glow=False)
for name, (fn, l, b, r, t) in K.NINE_SLICE.items():
    save(fn(), "UI/Kit", name, pivot=(0.5, 0.5), border=(l, b, r, t), glow=False)
for name, fn in K.SIMPLE_UI.items():
    pv = (0.0, 1.0) if name == 'ui_cursor' else (0.5, 0.5)
    save(fn(), "UI/Kit", name, pivot=pv, glow=False)

# --- font ---
fonts = os.path.join(ART, "Fonts")
os.makedirs(fonts, exist_ok=True)
K.build_ttf(os.path.join(fonts, "WyspaPixel.ttf"))

with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(items)} wpisów w pivots.json")
