"""Etap 7: przerobienie grafik świata z 16 px na 32 px (pixel art, paleta dark fantasy) - zaakceptowany styl gracza.

Czyta obecne PNG 16 px (z export_unity*.py + pad_sway.py), zamienia kolory z powrotem na klucze palety, robi hd2x
(crisp: Scale2x, czysty kontur, bryła, łuski liści) i zapisuje PNG 32 px w tym samym miejscu. Poświaty (*_glow) liczy
od nowa z pikseli emisyjnych nowego obrazka. Pivoty w pivots.json są ułamkami, więc się nie zmieniają; wpis dostaje
"hd": 2 (importer ustawia wtedy PPU 32, czyli rozmiar w świecie zostaje ten sam).

Kolejność:  export_unity*.py → pad_sway.py → art32/convert32.py   (skrypt pomija grafiki już przerobione)
"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "pixel-proto"))
from PIL import Image
import sprites
import sprites3 as S3
import sprites6 as S6   # noqa: F401  (dopisuje kolory emisyjne do palety)
from hd2x import hd2x, EXTRA_COLORS, DARK_FANTASY, hexrgb

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIVOTS = os.path.join(SPR, "pivots.json")
FOLDERS = ["Crops", "Trees", "Objects", "Automation", "Critters", "Zones", "Removal", "Events", "Sea", "Lamps", "Fx"]
GROUND = ["plot", "plot_tile"]           # reszta Ground to dane i stare podglądy wyspy
SKIP = {"intro_arrow"}                    # strzałka wprowadzenia: element interfejsu, zostaje ostra 16 px

SRC = dict(sprites.RGB)                   # klucze → RGB (paleta gry, w tym kolory emisyjne)
for k, v in EXTRA_COLORS.items():
    SRC[k] = hexrgb(v)
REV = {}
for k, rgb in SRC.items():
    REV.setdefault(tuple(rgb), k)         # pierwszy klucz wygrywa (bez duplikatów kolorów w palecie)
DST = dict(SRC)
for k, v in DARK_FANTASY.items():
    DST[k] = hexrgb(v)
EMISSIVE = set(S3.EMISSIVE)


class Grid:
    """Sprite z kluczami palety odczytany z PNG (zgodny z tym, czego oczekuje hd2x)."""
    def __init__(self, im):
        self.w, self.h = im.size
        self.px = {}
        px = im.load()
        for y in range(self.h):
            for x in range(self.w):
                r, g, b, a = px[x, y]
                if a < 128:
                    continue
                key = REV.get((r, g, b))
                if key is None:                       # kolor spoza palety: osobny klucz bez rampy
                    key = "#%02x%02x%02x" % (r, g, b)
                    SRC[key] = DST[key] = (r, g, b)
                    REV[(r, g, b)] = key
                self.px[(x, y)] = key


def render(g, only_emissive=False):
    h, w = len(g), len(g[0])
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            k = g[y][x]
            if k is None or (only_emissive and k not in EMISSIVE):
                continue
            im.putpixel((x, y), tuple(DST[k]) + (255,))
    return im


def main(force=False):
    data = json.load(open(PIVOTS))
    items = {it["name"]: it for it in data["items"]}
    paths = []
    for f in FOLDERS:
        d = os.path.join(SPR, f)
        if os.path.isdir(d):
            paths += [os.path.join(d, n) for n in sorted(os.listdir(d)) if n.endswith(".png") and not n.endswith("_glow.png")]
    paths += [os.path.join(SPR, "Ground", n + ".png") for n in GROUND]
    done = 0
    for i, path in enumerate(paths):
        name = os.path.splitext(os.path.basename(path))[0]
        it = items.get(name)
        if not os.path.exists(path) or name in SKIP or it is None or (it.get("hd", 1) >= 2 and not force):
            continue
        im = Image.open(path).convert("RGBA")
        g = hd2x(Grid(im), SRC, seed=i + 1, crisp=True)
        render(g).save(path)
        glow_path = path[:-4] + "_glow.png"
        if os.path.exists(glow_path):   # poświata tylko tam, gdzie była (np. wrony celowo bez niej)
            gl = render(g, only_emissive=True)
            if gl.getbbox() is not None:
                gl.save(glow_path)
                gi = items.setdefault(name + "_glow", {"name": name + "_glow", "x": it["x"], "y": it["y"]})
                gi["hd"] = 2
        it["hd"] = 2
        if "border" in it:
            it["border"] = [b * 2 for b in it["border"]]
        done += 1
    with open(PIVOTS, "w") as f:
        json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
    print(f"ok: 32 px dla {done} grafik")


if __name__ == "__main__":
    main(force="--force" in sys.argv)
