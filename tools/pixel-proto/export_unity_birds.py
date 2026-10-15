"""Eksport nowych ptaków (birds2.py) do Unity: Critters/bird_<rodzaj>_<poza>.png (sit0, sit1, fly0, fly1), wpisy
w pivots.json (pivot = środek dołu). Wrona i zięba zostają z export_unity_auto.py. Uruchom z tools/pixel-proto:
python3 export_unity_birds.py, potem Wyspa → 2."""
import os, json
from PIL import Image
from sprites3 import RGB
import birds2 as B

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
out = os.path.join(SPR, "Critters")

for kind, fn in B.BIRDS.items():
    for pose in B.POSES:
        spr = fn(pose)
        im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
        for (x, y), k in spr.px.items():
            im.putpixel((x, y), RGB[k] + (255,))
        name = f"bird_{kind}_{pose}"
        im.save(os.path.join(out, name + ".png"))
        items[name] = {"name": name, "x": round((spr.ax + 0.5) / spr.w, 5), "y": round((spr.h - 1 - spr.ay) / spr.h, 5)}
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(B.BIRDS)} rodzajów ptaków")
