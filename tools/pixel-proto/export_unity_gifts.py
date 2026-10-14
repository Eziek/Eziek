"""Eksport ikon Darów Mgły (gift_icons.py) do Unity: UI/Icons/icon_gift_<id>.png + wpisy w pivots.json.

Uruchom z tools/pixel-proto:  python3 export_unity_gifts.py   (dopisuje do pivots.json, potem w Unity Wyspa → 2)
"""
import os, json
import ui_kit as K
from sprites3 import RGB
from gift_icons import GIFT_ICONS
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}

out = os.path.join(SPR, "UI", "Icons")
os.makedirs(out, exist_ok=True)
for gid, rows in GIFT_ICONS.items():
    s = K.from_map(rows)
    im = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
    for (x, y), k in s.px.items():
        im.putpixel((x, y), RGB[k] + (255,))
    name = "icon_gift_" + gid
    im.save(os.path.join(out, name + ".png"))
    items[name] = {"name": name, "x": 0.5, "y": 0.5}

with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(GIFT_ICONS)} ikon darów, {len(items)} wpisów w pivots.json")
