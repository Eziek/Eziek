"""Drzewa od nowa (GDD §9 p. 105): korona z kęp liści z teksturą (jaśniejsze kępki, ciemne prześwity, cień pod kępami,
listki wystające poza obrys), grubszy pień z korą, korzeniami i gałęzią wchodzącą w koronę.
Dekoracyjne: okrągłe drzewo, świerk, krzak, martwe drzewo. Owocowe: te same gatunki i fazy co sprites5.tree,
z nową koroną i pniem. Podgląd: python3 trees2.py -> docs/img/drzewa.png
"""
import math, os, random
from sprites import Sprite, GREEN, EARTH
import sprites5 as S5
import sprites3 as S3  # noqa: F401  (paleta mroczna i kolory emisyjne)

LEAF = ['g0', 'g1', 'g2', 'g3', 'g4']
PINE = ['b0', 'b1', 'g1', 'b2', 'g2']
BARK = ['e0', 'e1', 'e2', 'e3']


def _h(x, y, seed):
    v = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    v = ((v ^ (v >> 13)) * 1274126177) & 0xFFFFFFFF
    return (v ^ (v >> 16)) / 4294967296.0


def texture(s, ramp, seed, strength=1.0):
    """Tekstura liści: kępki o jeden ton jaśniejsze (góra-lewo), prześwity o ton ciemniejsze, ciemny dół kęp."""
    keys = set(ramp)
    pts = [(p, k) for p, k in s.px.items() if k in keys]
    for (x, y), k in pts:
        i = ramp.index(k)
        n = (_h(x // 2, y // 2, seed) + _h((x + 1) // 3, (y + 2) // 3, seed + 7)) / 2
        below_empty = (x, y + 1) not in s.px
        if n > 1 - 0.22 * strength and i < len(ramp) - 1 and (x, y - 1) in s.px:
            i += 1                                   # jaśniejsza kępka listków
        elif n < 0.18 * strength and i > 0:
            i -= 1                                   # prześwit w koronie
        if below_empty and i > 0:
            i -= 1                                   # spód kępy w cieniu
        s.px[(x, y)] = ramp[i]


def tufts(s, ramp, seed, count=14):
    """Listki wystające poza obrys korony (głównie u góry i po bokach), żeby sylwetka nie była gładką kulą."""
    keys = set(ramp)
    rnd = random.Random(seed)
    edge = [(x, y) for (x, y), k in s.px.items() if k in keys and (x, y - 1) not in s.px]
    side = [(x, y) for (x, y), k in s.px.items() if k in keys and ((x - 1, y) not in s.px or (x + 1, y) not in s.px)]
    rnd.shuffle(edge); rnd.shuffle(side)
    for x, y in edge[:count]:
        s.set(x, y - 1, ramp[min(len(ramp) - 1, 2 + rnd.randint(0, 1))])
    for x, y in side[:count // 2]:
        dx = -1 if (x - 1, y) not in s.px else 1
        s.set(x + dx, y, ramp[1 + rnd.randint(0, 1)])


def trunk(s, x, y0, y1, w, ramp=BARK, roots=True):
    """Pień zwężający się ku górze, kora (pionowe pasma), korzenie rozchodzące się u dołu."""
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(1, y1 - y0)
        ww = max(2, int(round(w - (1 - t) * 1)))
        x0 = x - ww // 2
        for i in range(ww):
            k = ramp[3] if i == 0 else ramp[2] if i < ww - 1 else ramp[0]
            if ww > 2 and i == ww // 2 and (y * 3 + x) % 5 == 0:
                k = ramp[1]                           # słoje kory
            s.set(x0 + i, y, k)
    if roots:
        for dx, k in ((-w // 2 - 1, ramp[2]), (-w // 2 - 2, ramp[1]), (w - w // 2, ramp[1]), (w - w // 2 + 1, ramp[0])):
            s.set(x + dx, y1, k)
        s.set(x - w // 2 - 1, y1 - 1, ramp[2]); s.set(x + w - w // 2, y1 - 1, ramp[0])


def branch(s, x0, y0, x1, y1, k):
    s.line(x0, y0, x1, y1, k)


def canopy(s, clusters, ramp, seed, lo=-0.15):
    for cx, cy, rx, ry in clusters:
        s.blob(cx, cy, rx, ry, ramp, lo=lo)
    texture(s, ramp, seed)
    tufts(s, ramp, seed)


# ---------------------------------------------------------------- dekoracyjne

def round_tree(variant=0):
    rnd = random.Random(11 + variant)
    s = Sprite(34, 42, 17, 40)
    trunk(s, 17, 25, 39, 4)
    branch(s, 16, 27, 11, 21, 'e1'); branch(s, 18, 26, 23, 20, 'e2')     # gałęzie znikające w koronie
    jit = lambda: rnd.uniform(-1.2, 1.2)
    canopy(s, [(9 + jit(), 20, 7, 5.5), (25 + jit(), 20, 7, 5.5), (17, 13 + jit(), 10.5, 8.5),
               (11, 11 + jit(), 5.5, 4.5), (23, 10 + jit(), 5.5, 4.5), (17, 23, 8, 4.5)], LEAF, 31 + variant)
    return s.outline()


def spruce(variant=0):
    s = Sprite(24, 42, 12, 40)
    trunk(s, 12, 32, 39, 3, roots=True)
    tiers = [(2, 12, 4.5), (7, 19, 6.5), (13, 26, 8.5), (19, 33, 10.5)]
    for t, (top, bot, hw) in enumerate(tiers):
        for y in range(top, bot + 1):
            w = (y - top + 1) / (bot - top + 1) * hw
            for x in range(int(12 - w), int(12 + w) + 1):
                rel = (x - 12) / max(w, 1)
                k = PINE[3] if rel < -0.45 else PINE[2] if rel < 0.1 else PINE[1]
                if y == bot:
                    k = PINE[0]                          # dolna krawędź piętra w cieniu
                s.set(x, y, k)
        # poszarpany dół piętra: igły zwisają nierówno
        for x in range(int(12 - hw), int(12 + hw) + 1, 2):
            if (x + t + variant) % 3:
                s.set(x, bot + 1, PINE[1])
    texture(s, PINE, 53 + variant, 0.7)
    s.set(11, 1, PINE[4]); s.set(12, 0, PINE[3])
    return s.outline()


def bush(variant=0):
    s = Sprite(20, 15, 10, 13)
    canopy(s, [(6, 9, 4.6, 3.8), (14, 9, 4.6, 3.8), (10, 6, 5.2, 4.4), (10, 11, 6, 2.6)], LEAF[:4], 71 + variant)
    rnd = random.Random(5 + variant)
    for _ in range(5):                                    # jagody
        x, y = rnd.randint(4, 15), rnd.randint(5, 11)
        if (x, y) in s.px:
            s.set(x, y, 'co'); s.set(x + 1, y, 'p2'); s.set(x, y - 1, 'ro')
    return s.outline()


def dead_tree(variant=0):
    s = Sprite(32, 44, 16, 42)

    def limb(x, y, ang, length, w, depth):
        x2, y2 = x + math.cos(ang) * length, y + math.sin(ang) * length
        n = max(2, int(length * 2))
        for i in range(n + 1):
            t = i / n
            px, py = x + (x2 - x) * t, y + (y2 - y) * t
            for dw in range(w):
                s.set(int(round(px)) + dw, int(round(py)), 'e3' if dw == 0 else 'e1' if dw < w - 1 else 'e0')
        if depth > 0:
            limb(x2, y2, ang - 0.6, length * 0.64, max(1, w - 1), depth - 1)
            limb(x2, y2, ang + 0.5, length * 0.58, max(1, w - 1), depth - 1)
            if depth == 2:
                limb(x2, y2, ang - 0.05, length * 0.5, 1, 0)
    limb(15, 41, -math.pi / 2 + 0.05, 14, 4, 3)
    for x, k in ((10, 'e1'), (11, 'e2'), (12, 'e2'), (19, 'e1'), (20, 'e0')):
        s.set(x, 41, k)                                   # korzenie
    s.set(16, 30, 'p0'); s.set(16, 31, 'p0')               # dziupla
    return s.outline()


# ---------------------------------------------------------------- owocowe (gatunki i fazy jak sprites5)

def fruit_tree(species, stage, seed=1):
    """Faza 0 sadzonka, 1 młode, 2 dorosłe, 3 owocujące. Kształty koron gatunków z sprites5, z nową teksturą i pniem."""
    sp = S5.SPECIES[species]
    L = sp['leaf']
    tseed = sum(map(ord, species)) * 7
    if stage == 0:
        s = Sprite(14, 16, 7, 14)
        trunk(s, 7, 7, 13, 2, sp['trunk'], roots=False)
        s.blob(4.5, 6, 2.2, 1.4, L, ang=-0.5); s.blob(9.5, 5, 2.2, 1.4, L, ang=0.5); s.blob(7, 3, 1.6, 2.1, L)
        texture(s, L, tseed, 0.6)
        s.outline()
        s.blob(7, 14, 3.5, 1.5, EARTH, clip_y=14)
        return s
    if stage == 1:
        s = Sprite(26, 32, 13, 30)
        trunk(s, 13, 17, 29, 3, sp['trunk'])
        S5._canopy(s, sp, 13, 12, 0.6)
        texture(s, L, tseed)
        tufts(s, L, tseed, 8)
        return s.outline()
    s = Sprite(36, 44, 18, 42)
    if sp['shape'] == 'bush':                             # kilka rozchodzących się pni
        r = sp['trunk']
        for x1, y1 in ((11, 21), (18, 19), (25, 21)):
            s.line(18, 40, x1, y1, r[2]); s.line(19, 40, x1 + 1, y1, r[1])
        s.rect(16, 37, 20, 41, r[1]); s.rect(16, 37, 16, 41, r[2])
        s.set(15, 41, r[2]); s.set(21, 41, r[0])
    else:
        tw = 3 if sp['shape'] in ('airy', 'oval') else 4
        trunk(s, 18, 23, 41, tw, sp['trunk'])
        branch(s, 17, 27, 12, 20, sp['trunk'][1]); branch(s, 19, 26, 24, 19, sp['trunk'][2])
    S5._canopy(s, sp, 18, 15, 1.0)
    texture(s, L, tseed)
    tufts(s, L, tseed, 12)
    if stage == 3:
        S5._fruits(s, sp, 18, 15, 1.0, seed)
    return s.outline()


if __name__ == "__main__":
    from familiars import to_img
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    ims = [to_img(round_tree(0)), to_img(round_tree(1)), to_img(spruce()), to_img(bush()), to_img(dead_tree())]
    for sid in S5.SPECIES:
        ims += [to_img(fruit_tree(sid, st)) for st in range(4)]
    cw = max(i.width for i in ims) + 2
    ch = max(i.height for i in ims) + 2
    cols = 9
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new("RGBA", (cw * cols, ch * rows), (52, 70, 48, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im, ((i % cols) * cw + (cw - im.width) // 2, (i // cols) * ch + ch - im.height))
    out = os.path.join(ROOT, "docs", "img", "drzewa.png")
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(out)
    print("ok:", out)
