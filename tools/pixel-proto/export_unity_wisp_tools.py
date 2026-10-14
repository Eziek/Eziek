"""Eksport narzędzi ogników (wisp_tools.py) do Unity: Automation/tool_<nazwa>_<klatka>.png (+ _glow dla pikseli emisyjnych)
i wpisy w pivots.json (pivot = punkt chwytu). Uruchom z tools/pixel-proto:  python3 export_unity_wisp_tools.py, potem Wyspa → 2.
"""
import os, json
import ui_kit as K
from sprites3 import RGB, EMISSIVE
from wisp_tools import TOOLS
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


for name, tool in TOOLS.items():
    gx, gy = tool["grip"]
    for f, rows in enumerate(tool["frames"]):
        s = K.from_map(rows)
        piv = {"x": round((gx + 1 + 0.5) / s.w, 5), "y": round((s.h - 1 - (gy + 1) + 0.5) / s.h, 5)}
        fn = f"tool_{name}_{f}"
        to_img(s).save(os.path.join(out, fn + ".png"))
        items[fn] = {"name": fn, **piv}
        if any(k in EMISSIVE for k in s.px.values()):
            to_img(s, EMISSIVE).save(os.path.join(out, fn + "_glow.png"))
            items[fn + "_glow"] = {"name": fn + "_glow", **piv}

with open(PIVOTS, "w") as fp:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, fp, indent=2)
print("ok:", len(TOOLS), "narzędzi")
