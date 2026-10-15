"""Eksport ozdobników na wodzie (water_flora.py) do Unity: Water/flora_<nazwa>.png (+ _glow dla mglistej lilii),
wpisy w pivots.json (pivot = środek). Uruchom z tools/pixel-proto:  python3 export_unity_water.py, potem Wyspa → 2."""
import os, json
from PIL import Image
from sprites3 import RGB, EMISSIVE
import water_flora as W

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
out = os.path.join(SPR, "Water")
os.makedirs(out, exist_ok=True)


def to_img(spr, only=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if only is None or k in only:
            im.putpixel((x, y), RGB[k] + (255,))
    return im


for name, fn in W.FLORA.items():
    spr, n = fn(), "flora_" + name
    to_img(spr).save(os.path.join(out, n + ".png"))
    items[n] = {"name": n, "x": 0.5, "y": 0.5}
    if any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(os.path.join(out, n + "_glow.png"))
        items[n + "_glow"] = {"name": n + "_glow", "x": 0.5, "y": 0.5}
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(W.FLORA)} ozdobników na wodzie -> {out}")
