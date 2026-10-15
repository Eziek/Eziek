"""Eksport domków chowańców i budowli ogników (homes.py) do Unity: Objects/home_<chowaniec>.png, bonfire_<0-2>.png,
dreamhouse.png, piksele emisyjne w *_glow.png, wpisy w pivots.json (pivot = środek dołu).
Uruchom z tools/pixel-proto:  python3 export_unity_homes.py   (potem w Unity: Wyspa → 2)
"""
import os, json
import homes as H
from familiars import to_img
from sprites3 import EMISSIVE

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
DIR = os.path.join(SPR, "Objects")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}


def save(spr, name):
    pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
    to_img(spr).save(os.path.join(DIR, name + ".png"))
    items[name] = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
    if any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(os.path.join(DIR, name + "_glow.png"))
        items[name + "_glow"] = {"name": name + "_glow", "x": items[name]["x"], "y": items[name]["y"]}


n = 0
for kind, fn in H.HOMES.items():
    save(fn(), "home_" + kind); n += 1
for f in range(H.BONFIRE_FRAMES):
    save(H.bonfire(f), f"bonfire_{f}"); n += 1
save(H.dream_house(), "dreamhouse"); n += 1
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {n} budowli")
