"""Eksport generatorów Żaru (ember_gens.py) do Unity: Objects/ember_kiln_<0-2>.png, ember_crystal.png (+ _glow),
wpisy w pivots.json (pivot = środek dołu). Uruchom z tools/pixel-proto:  python3 export_unity_ember.py, potem Wyspa → 2."""
import os, json
from PIL import Image
from sprites3 import RGB, EMISSIVE
import ember_gens as G

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
out = os.path.join(SPR, "Objects")


def to_img(spr, only=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if only is None or k in only:
            im.putpixel((x, y), RGB[k] + (255,))
    return im


def save(spr, name):
    to_img(spr).save(os.path.join(out, name + ".png"))
    pv = {"x": round((spr.ax + 0.5) / spr.w, 5), "y": round((spr.h - 1 - spr.ay) / spr.h, 5)}
    items[name] = dict(name=name, **pv)
    if any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(os.path.join(out, name + "_glow.png"))
        items[name + "_glow"] = dict(name=name + "_glow", **pv)


for f in range(3):
    save(G.kiln(f), f"ember_kiln_{f}")
save(G.crystal(), "ember_crystal")
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print("ok: generatory Żaru")
