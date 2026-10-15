"""Eksport monumentów wysp fabularnych (monuments.py) do Unity: Story/monument_<historia>_<faza 0-4>.png (+ _glow dla
gotowego), wpisy w pivots.json (pivot = środek dołu). Usuwa grafiki starszych szczątków (ruin_*, story_*).
Uruchom z tools/pixel-proto:  python3 export_unity_story.py, potem Wyspa → 2."""
import os, json, glob
from PIL import Image
from sprites3 import RGB, EMISSIVE
import monuments as M

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
out = os.path.join(SPR, "Story")
os.makedirs(out, exist_ok=True)
for old in glob.glob(os.path.join(out, "ruin_*")) + glob.glob(os.path.join(out, "story_*")):
    os.remove(old)
for name in [n for n in items if n.startswith(("ruin_", "story_"))]:
    del items[name]


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


for site in M.SITES:
    for k in range(M.PHASES + 1):
        save(M.phase(site, k), f"monument_{site}_{k}")
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: monumenty -> {out}")
