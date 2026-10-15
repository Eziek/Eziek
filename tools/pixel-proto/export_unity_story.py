"""Eksport wysp fabularnych (story_ruins.py) do Unity: Story/ruin_<historia>_<0|1>.png, story_scaffold.png,
story_<historia>.png (+ _glow), wpisy w pivots.json (pivot = środek dołu). Uruchom z tools/pixel-proto:
python3 export_unity_story.py, potem Wyspa → 2."""
import os, json
from PIL import Image
from sprites3 import RGB, EMISSIVE
import story_ruins as R

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
out = os.path.join(SPR, "Story")
os.makedirs(out, exist_ok=True)


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


for site in R.SITES:
    for v in (0, 1):
        save(R.ruin(site, v), f"ruin_{site}_{v}")
    save(R.BUILDINGS[site](), f"story_{site}")
save(R.scaffold(), "story_scaffold")
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: wyspy fabularne -> {out}")
