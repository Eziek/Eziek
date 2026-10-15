"""Dodatkowe fazy wzrostu roślin (6 faz zamiast 4): z gotowych PNG faz 0-3 powstają dwie nowe.

  crop_<id>_shoot.png   kiełek: nasiono w kopczyku z maleńkim pędem (kolor z liści fazy 1)
  crop_<id>_unripe.png  niedojrzała: kształt dojrzałej, ale owoc/kwiat w kolorach liści (dojrzewa = zmienia kolor)

Kolejność w grze (WyspaSetup.StageSprites): 0, shoot, 1, 2, unripe, 3. Niedojrzała nie świeci (bez _glow).
Działa na PNG po pad_sway.py (kopiuje pivot razem z polem "pad", więc pad_sway ich nie poszerza drugi raz).
Uruchom z tools/pixel-proto, po export_unity*.py i pad_sway.py:  python3 export_unity_stages.py
"""
import os, json, glob
from collections import Counter
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
CROPS = os.path.join(SPR, "Crops")
PIVOTS = os.path.join(SPR, "pivots.json")


def pixels(im):
    return [im.getpixel((x, y)) for y in range(im.height) for x in range(im.width)]


def colors(im):
    return {p[:3] for p in pixels(im) if p[3] > 0}


def soil_colors(seed):
    # kopczyk ziemi: kolory nasiona, które występują kilka razy (samo ziarenko to 1-2 piksele)
    n = Counter(p[:3] for p in pixels(seed) if p[3] > 0)
    return {c for c, k in n.items() if k >= 3}


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def outline_color(im):
    # obrys: najciemniejszy kolor, który leży na krawędzi kształtu
    return min(colors(im), key=lum)


def shoot(seed, sprout, ax):
    """Nasiono + pęd 2 px z listkami po bokach, obrysowany jak reszta."""
    out = seed.copy()
    w, h = out.size
    ol = outline_color(seed)
    soil = soil_colors(seed)
    leaf = sorted({p[:3] for p in pixels(sprout) if p[3] > 0 and p[:3] not in soil and p[:3] != ol}, key=lum)
    if not leaf:
        return out
    stem, tip = leaf[max(0, len(leaf) - 2)], leaf[-1]   # dwa najjaśniejsze kolory liści
    # wierzch kopczyka w kolumnie zaczepienia (pierwszy kolor niebędący obrysem od góry)
    top = next((y for y in range(h) if seed.getpixel((ax, y))[3] > 0 and seed.getpixel((ax, y))[:3] != ol), h - 3)
    new = {(ax, top - 1): stem, (ax, top - 2): stem, (ax - 1, top - 3): tip, (ax + 1, top - 3): tip, (ax, top - 3): stem}
    for (x, y), c in new.items():
        out.putpixel((x, y), c + (255,))
    for (x, y) in new:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and out.getpixel((nx, ny))[3] == 0:
                out.putpixel((nx, ny), ol + (255,))
    return out


def unripe(ripe, growing, seed):
    """Dojrzała z owocem w kolorach liści fazy 2 (dobór po jasności, w palecie gry)."""
    ol = outline_color(ripe)
    soil = soil_colors(seed)
    keep = colors(growing) | soil | {ol}
    foliage = sorted(colors(growing) - soil - {ol}, key=lum)
    fruit = sorted(colors(ripe) - keep, key=lum)
    remap = {}
    if foliage and fruit:
        # jasności owocu rozkładamy na całą rampę liści (najciemniejszy owoc -> najciemniejszy liść)
        for i, c in enumerate(fruit):
            t = i / max(1, len(fruit) - 1)
            remap[c] = foliage[min(len(foliage) - 1, round(t * (len(foliage) - 1)))]
    changed = sum(1 for p in pixels(ripe) if p[3] > 0 and p[:3] in remap)
    if changed < 4:
        # owoc w kolorach liści (np. grzyby, piołun): niedojrzała jest o ton ciemniejsza, przy dojrzewaniu jaśnieje
        plant = sorted(colors(ripe) - soil - {ol}, key=lum)
        dist = lambda a, b: sum((x - y) ** 2 for x, y in zip(a, b))
        remap = {c: min(plant[:i], key=lambda d: dist(c, d)) for i, c in enumerate(plant) if i > 0}   # najbliższy ciemniejszy
    out = ripe.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] > 0 and p[:3] in remap:
                px[x, y] = remap[p[:3]] + (p[3],)
    return out


items = {it["name"]: it for it in json.load(open(PIVOTS))["items"]}
ids = sorted({os.path.basename(p)[5:-6] for p in glob.glob(os.path.join(CROPS, "crop_*_3.png"))})
for cid in ids:
    ims = [Image.open(os.path.join(CROPS, f"crop_{cid}_{i}.png")).convert("RGBA") for i in range(4)]
    piv = items.get(f"crop_{cid}_0")
    ax = int(piv["x"] * ims[0].width) if piv else ims[0].width // 2
    for name, im, src in ((f"crop_{cid}_shoot", shoot(ims[0], ims[1], ax), f"crop_{cid}_0"),
                          (f"crop_{cid}_unripe", unripe(ims[3], ims[2], ims[0]), f"crop_{cid}_3")):
        im.save(os.path.join(CROPS, name + ".png"))
        base = dict(items.get(src, {"x": 0.5, "y": 0.0}))
        base["name"] = name
        items[name] = base
with open(PIVOTS, "w") as f:
    json.dump({"items": sorted(items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: fazy kiełek i niedojrzała dla {len(ids)} roślin")
