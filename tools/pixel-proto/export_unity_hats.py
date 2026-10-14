"""Eksport czapek ogników (wisp_hats.py) do Unity: Automation/hat_<id>.png (+ _glow), pivot = środek dolnej krawędzi.
Uruchom z tools/pixel-proto:  python3 export_unity_hats.py, potem w Unity Wyspa → 2.
"""
import os, json
import ui_kit as K
from sprites3 import RGB, EMISSIVE
from wisp_hats import HATS
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
out = os.path.join(SPR, "Automation")


def to_img(s, only=None):
    im = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
    for (x, y), k in s.px.items():
        if only is None or k in only:
            im.putpixel((x, y), RGB[k] + (255,))
    return im


for hid, rows in HATS.items():
    s = K.from_map(rows)
    fn = "hat_" + hid
    to_img(s).save(os.path.join(out, fn + ".png"))
    items[fn] = {"name": fn, "x": 0.5, "y": 0.0}
    if any(k in EMISSIVE for k in s.px.values()):
        to_img(s, EMISSIVE).save(os.path.join(out, fn + "_glow.png"))
        items[fn + "_glow"] = {"name": fn + "_glow", "x": 0.5, "y": 0.0}

with open(PIVOTS, "w") as fp:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, fp, indent=2)
print("ok:", len(HATS), "czapek")
