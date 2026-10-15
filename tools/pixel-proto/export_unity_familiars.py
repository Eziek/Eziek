"""Eksport chowańców (familiars.py) do Unity: Critters/familiar_<rodzaj>_<poza>.png, piksele emisyjne w *_glow.png,
wpisy w pivots.json (pivot = punkt zaczepienia u dołu). Usuwa grafiki starych póz, których już nie ma.
Uruchom z tools/pixel-proto:  python3 export_unity_familiars.py   (potem w Unity: Wyspa → 2)
"""
import os, json, glob
import familiars as F
from sprites3 import EMISSIVE

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
DIR = os.path.join(SPR, "Critters")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}

made = set()
for kind, (fn, poses) in F.FAMILIARS.items():
    for pose in poses:
        spr = fn(pose)
        name = f"familiar_{kind}_{pose}"
        pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
        F.to_img(spr).save(os.path.join(DIR, name + ".png"))
        items[name] = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
        made.add(name)
        glow = os.path.join(DIR, name + "_glow.png")
        if any(k in EMISSIVE for k in spr.px.values()):
            F.to_img(spr, EMISSIVE).save(glow)
            items[name + "_glow"] = {"name": name + "_glow", "x": items[name]["x"], "y": items[name]["y"]}
            made.add(name + "_glow")

# stare pozy (np. familiar_cat_sleep, familiar_bat_hang): pliki, .meta i wpisy pivotów
removed = 0
for path in glob.glob(os.path.join(DIR, "familiar_*.png")):
    name = os.path.splitext(os.path.basename(path))[0]
    if name not in made:
        for f in (path, path + ".meta"):
            if os.path.exists(f):
                os.remove(f)
        items.pop(name, None)
        removed += 1
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(made)} sprite'ów chowańców, usunięte stare: {removed}")
