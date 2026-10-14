"""Margines na kołysanie (po teście gracza: przy wietrze widać było pionową kreskę, która schodzi w dół).

Shader kołysania przesuwa górne wiersze sprite'a w bok o kilka pikseli (z podmuchem do ok. 5). Sprite kończy się tuż przy
liściach, więc przesunięte piksele wychodziły poza obrazek i były ucinane równą pionową krawędzią. Ten skrypt dokłada
przezroczysty margines PAD px po lewej i prawej sprite'om, które się kołyszą (rośliny, drzewa, krzaki), i przelicza pivot.

Uruchom po skryptach export_unity*.py (jest idempotentny: dopisuje do pivots.json pole "pad" i nie dokłada drugi raz):
    python3 tools/pixel-proto/pad_sway.py
"""
import os, json, glob
from PIL import Image

PAD = 6
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
# kołyszą się: rośliny na grządkach, drzewa owocowe, dekoracje z sway = true (WyspaSetup)
FILES = sorted(glob.glob(os.path.join(SPR, "Crops", "crop_*.png")) + glob.glob(os.path.join(SPR, "Trees", "tree_*.png"))
               + [os.path.join(SPR, "Objects", n + s + ".png") for n in ("tree_round", "tree_spruce", "bush") for s in ("", "_glow")])

data = json.load(open(PIVOTS))
items = {it["name"]: it for it in data["items"]}
done = 0
for path in FILES:
    if not os.path.exists(path):
        continue
    name = os.path.splitext(os.path.basename(path))[0]
    it = items.get(name)
    if it is None or it.get("pad", 0) >= PAD:
        continue
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    out = Image.new("RGBA", (w + 2 * PAD, h), (0, 0, 0, 0))
    out.paste(im, (PAD, 0))
    out.save(path)
    it["x"] = round((it["x"] * w + PAD) / (w + 2 * PAD), 5)
    it["pad"] = PAD
    done += 1
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: margines {PAD} px dla {done} sprite'ów")
