"""Eksport assetów Etapu 6b: nowe rośliny (fazy 0-3), ikony nasion, duszek-handlarz.

Uruchom z tools/pixel-proto:  python3 export_unity_auto.py   (po export_unity_ui.py - dopisuje do pivots.json)
"""
import os, json
import sprites
import sprites3 as S3
import sprites6 as S6
import ui_kit as K
from sprites3 import RGB, EMISSIVE
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}


def to_img(spr, only=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if only is None or k in only:
            im.putpixel((x, y), RGB[k] + (255,))
    return im


def save(spr, sub, name, pivot=None, glow=True):
    """Jak w export_unity_ui: PNG + wpis pivota, piksele emisyjne osobno w *_glow.png."""
    d = os.path.join(SPR, sub)
    os.makedirs(d, exist_ok=True)
    if pivot is None:
        pivot = ((spr.ax + 0.5) / spr.w, (spr.h - 1 - spr.ay) / spr.h)
    to_img(spr).save(os.path.join(d, name + ".png"))
    items[name] = {"name": name, "x": round(pivot[0], 5), "y": round(pivot[1], 5)}
    if glow and any(k in EMISSIVE for k in spr.px.values()):
        to_img(spr, EMISSIVE).save(os.path.join(d, name + "_glow.png"))
        items[name + "_glow"] = {"name": name + "_glow", "x": items[name]["x"], "y": items[name]["y"]}


for cid, fn in S6.CROPS_DAY.items():
    save(sprites.seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")
for cid, fn in S6.CROPS_NIGHT.items():
    save(S3.night_seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")

for cid, (fn, kind) in S6.HYBRIDS.items():   # hybrydy z mutacji
    save(S3.night_seed() if kind == 'night' else sprites.seed(), "Crops", f"crop_{cid}_0")
    for st in (1, 2, 3):
        save(fn(st), "Crops", f"crop_{cid}_{st}")

for f in range(4):
    save(S6.merchant_spirit(f), "Automation", f"merchant_spirit_{f}")
    save(S3.wisp_gardener(f, bob=False), "Automation", f"wisp_gardener_{f}")   # bez podskoku: płynny lot w kodzie

for kind in ('crow', 'finch'):
    for pose in S6.BIRD_POSES:
        save(S6.bird(kind, pose), "Critters", f"bird_{kind}_{pose}", glow=False)

for fill in range(3):   # skrzynia na plony 16 px (sąsiednie nie nachodzą na siebie)
    save(S3.harvest_crate(fill), "Automation", f"harvest_crate_{fill}")

for note in (True, False):   # wprowadzenie (Etap 6d)
    save(S6.notice_board(note), "Objects", "notice_board" + ("" if note else "_empty"), glow=False)
save(S6.intro_arrow(), "Fx", "intro_arrow", glow=False)

save(S6.storehouse(), "Objects", "storehouse")
save(S6.fish_jump(True), "Events", "fish_jump_up", glow=False)   # ryba wyskakująca z wody
save(S6.fish_jump(False), "Events", "fish_jump_down", glow=False)   # skład: nadwyżka drewna i kamienia na monety
save(S6.cauldron('idle'), "Objects", "cauldron_idle")   # kocioł alchemiczny
for f in range(3):
    save(S6.cauldron('brew', f), "Objects", f"cauldron_brew_{f}")
for f in range(2):
    save(S6.cauldron('ready', f), "Objects", f"cauldron_ready_{f}")

for pose in S6.CAT_POSES:   # chowańce
    save(S6.cat(pose), "Critters", f"familiar_cat_{pose}")
for pose in S6.BIRD_POSES:
    save(S6.raven(pose), "Critters", f"familiar_raven_{pose}")
for pose in S6.TOAD_POSES:
    save(S6.toad(pose), "Critters", f"familiar_toad_{pose}")
for pose in S6.BAT_POSES:
    save(S6.bat(pose), "Critters", f"familiar_bat_{pose}")

for kind in S6.WISP_KINDS:   # rój ogników
    for f in range(4):
        save(S6.wisp_kind(kind, f), "Automation", f"wisp_{kind}_{f}")
for f in range(2):
    save(S6.wisp_nest(f), "Automation", f"wisp_nest_{f}")

for name, rows in S6.ICON_MAPS.items():
    save(K.from_map(rows), "UI/Icons", "icon_" + name, pivot=(0.5, 0.5), glow=False)

K.SEED_SYMBOLS.update(S6.SEED_SYMBOLS)
for name in S6.SEED_SYMBOLS:
    save(K.seed_packet(name), "UI/Icons", "icon_seed_" + name, pivot=(0.5, 0.5), glow=False)

with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(items)} wpisów w pivots.json")
