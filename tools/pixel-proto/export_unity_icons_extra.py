"""Dodatkowe ikony UI (12 px, klucze ui_kit.KEY) eksportowane bez przebudowy całego zestawu:
UI/Icons/icon_<nazwa>.png + wpisy w pivots.json. Uruchom z tools/pixel-proto:  python3 export_unity_icons_extra.py
"""
import os, json
import ui_kit as K
from sprites3 import RGB
from PIL import Image

EXTRA_ICONS = {
    # zapis gry: zwój z woskową pieczęcią
    'save': ["..........", ".hwwwwwwh.", ".hw2222wh.", ".hwwwwwwh.", ".hw222wwh.",
             ".hwwwwcch.", ".hwwwccrc.", ".hhhhhccc.", "......c.c.", ".........."],
    # menu główne: domek z zapalonym oknem
    'home': ["....hh....", "...hffh...", "..hffffh..", ".hffffffh.", "hhhhhhhhhh",
             ".wwwwwwww.", ".wggwwjjw.", ".wggwwjjw.", ".wwwwwjjw.", ".wwwwwjjw."],
}

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
for name, rows in EXTRA_ICONS.items():
    s = K.from_map(rows)
    im = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
    for (x, y), k in s.px.items():
        im.putpixel((x, y), RGB[k] + (255,))
    fn = "icon_" + name
    im.save(os.path.join(SPR, "UI", "Icons", fn + ".png"))
    items[fn] = {"name": fn, "x": 0.5, "y": 0.5}
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print("ok:", ", ".join(EXTRA_ICONS))
