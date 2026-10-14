"""Sprite'y do systemów z GDD §11: rośliny nocy, hybryda, automatyka, budowle, strefy, morze, zdarzenia.
Rozmiar zgodny z placeholderami. Paleta mroczna (+ kolory emisyjne)."""
import math
import sprites
from sprites import Sprite, GREEN, EARTH, ORANGE, STONE, _plant
from run_dark_palette import DARK

EXTRA = {
    'fl': "#ffe08a", 'fx': "#f08a3a",   # płomień
    'wi': "#5fcfb8", 'wl': "#d2fff0",   # upiorne światło
    'mu': "#7a4f9a", 'ml': "#d49af0",   # grzyby
}
for d in (DARK, EXTRA):
    sprites.PAL.update(d)
    sprites.RGB.update({k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for k, v in d.items()})
RGB = sprites.RGB

EMISSIVE = {'fl', 'fx', 'wi', 'wl', 'ml'}

NIGHT_LEAF = ['p0', 'b1', 'b2', 'b3']
DARK_LEAF = ['g0', 'g1', 'g2']
BONE = ['b1', 'b3', 'fo', 'cr']
FUNGUS = ['p0', 'p1', 'p2', 'co']
BLOOD = ['o', 'p2', 'p2', 'co']
IRON = ['o', 'p0', 'p1', 'b3']
PALE = ['b3', 'fo', 'cr', 'cr']


def mound(s, seed_col=('go', 'cr')):
    s.blob(9, 20, 3.5, 1.8, EARTH, clip_y=20)
    if seed_col:
        s.set(9, 18, seed_col[0]); s.set(9, 17, seed_col[1])


def with_mound(s):
    m = _plant(); mound(m, None); m.outline('e0')
    for p, k in m.px.items():
        if p not in s.px or s.px[p] == 'o':
            s.px[p] = k
    return s


def night_seed():
    s = _plant(); mound(s, ('b3', 'fo'))
    return s.outline('e0')


# ---------------- rośliny nocy (fazy 1-3) ----------------
def moonflower(stage):
    s = _plant()
    if stage == 1:
        s.blob(7.5, 16.5, 1.4, 3, NIGHT_LEAF, ang=-0.45); s.blob(10.5, 16.5, 1.4, 3, NIGHT_LEAF, ang=0.45)
        s.outline()
    elif stage == 2:
        s.line(9, 19, 9, 10, 'b1')
        s.blob(6.5, 15, 1.6, 3, NIGHT_LEAF, ang=-0.8); s.blob(11.5, 14, 1.6, 3, NIGHT_LEAF, ang=0.8)
        s.blob(9, 8.5, 1.8, 2.8, ['p1', 'mu', 'fo'])
        s.outline()
    else:
        s.line(9, 19, 9, 11, 'b1')
        s.blob(6, 16, 1.7, 3.2, NIGHT_LEAF, ang=-0.9); s.blob(12, 15, 1.7, 3.2, NIGHT_LEAF, ang=0.9)
        for i in range(5):
            a = -math.pi / 2 + i * 2 * math.pi / 5
            s.blob(9 + 3 * math.cos(a), 8 + 2.6 * math.sin(a), 2.0, 1.5, PALE, ang=a)
        s.outline()
        s.set(9, 8, 'wl'); s.set(8, 8, 'wi'); s.set(9, 7, 'wi'); s.set(10, 8, 'wi'); s.set(9, 9, 'wi')
    return with_mound(s)


def _pumpkin_body(s, ramp, rib):
    s.blob(9, 15.5, 6.5, 4.8, ramp, clip_y=20)
    for rx in (6, 9, 12):
        for y in range(12, 20):
            if (rx, y) in s.px and s.px[(rx, y)] in (ramp[2], ramp[3]):
                s.set(rx, y, rib)


def _ghost_face(s):
    for x, y in ((6, 14), (7, 14), (7, 13), (11, 14), (12, 14), (11, 13)):
        s.set(x, y, 'wi')
    for x in (7, 9, 11):
        s.set(x, 17, 'wi')
    for x in (8, 10):
        s.set(x, 18, 'wl')


def ghost_pumpkin(stage):
    s = _plant()
    if stage == 1:
        s.line(9, 19, 9, 16, 'b1')
        s.blob(7, 15.5, 2.2, 1.6, NIGHT_LEAF, ang=-0.3); s.blob(11, 15.5, 2.2, 1.6, NIGHT_LEAF, ang=0.3)
        s.outline()
        return with_mound(s)
    if stage == 2:
        s.blob(5, 16, 3.4, 2.6, NIGHT_LEAF); s.blob(13, 15.5, 3.4, 2.6, NIGHT_LEAF); s.blob(9, 13, 3.4, 2.8, NIGHT_LEAF)
        s.blob(10, 18.5, 2.4, 1.9, ['b3', 'fo', 'cr'], clip_y=20)
        s.outline()
        return with_mound(s)
    s.blob(3.5, 17, 3, 2.4, NIGHT_LEAF)
    _pumpkin_body(s, BONE, 'b3')
    s.line(9, 10, 10, 8, 'e1'); s.set(10, 10, 'e2')
    s.blob(13, 9.5, 2.4, 1.6, NIGHT_LEAF, ang=0.3)
    s.outline()
    _ghost_face(s)
    return s


def hybrid_pumpkin():
    """Dynia-upiór (hybryda dyni i upiornej dyni): pomarańczowa z upiorną twarzą."""
    s = _plant()
    s.blob(3.5, 17, 3, 2.4, GREEN)
    _pumpkin_body(s, ORANGE, 'co')
    s.line(9, 10, 10, 8, 'e1'); s.set(10, 10, 'e2')
    s.blob(13, 9.5, 2.4, 1.6, NIGHT_LEAF, ang=0.3)
    s.outline()
    _ghost_face(s)
    return s


def corpse_mushroom(stage):
    s = _plant()
    shrooms = {1: [(7, 17, 1.8, 2), (11, 18, 1.5, 1)],
               2: [(6, 15, 2.6, 4), (11, 16, 2.2, 3), (14, 18, 1.5, 1)],
               3: [(9, 8, 4.8, 11), (4, 14, 2.6, 5), (14, 13, 3, 6)]}[stage]
    for cx, cy, r, stem in shrooms:
        s.rect(int(cx), int(cy), int(cx) + (1 if r > 2.5 else 0), int(cy) + stem, 'fo')
        s.set(int(cx), int(cy) + stem, 'b3')
    for cx, cy, r, stem in shrooms:
        s.blob(cx + 0.5, cy, r, r * 0.62, FUNGUS, clip_y=int(cy))
        for i in range(-int(r) + 1, int(r)):
            if (int(cx) + i, int(cy) + 1) not in s.px:
                s.set(int(cx) + i, int(cy) + 1, 'p0')     # blaszki pod kapeluszem
    if stage == 3:
        for x, y in ((7, 5), (10, 4), (12, 6), (6, 7), (3, 12), (14, 11)):
            if (x, y) in s.px: s.set(x, y, 'cr')
    s.outline()
    return with_mound(s)


def blood_rose(stage):
    s = _plant()
    if stage == 1:
        s.line(9, 19, 9, 15, 'g0')
        s.blob(7.5, 15.5, 1.3, 2.4, DARK_LEAF, ang=-0.6); s.blob(10.5, 15, 1.3, 2.4, DARK_LEAF, ang=0.6)
    else:
        top = 10 if stage == 2 else 9
        s.line(9, 19, 9, top, 'g0'); s.line(10, 19, 10, top + 2, 'g1')
        for x, y in ((8, 16), (11, 14), (8, 12)):
            s.set(x, y, 'p1')                                       # kolce
        s.blob(6.5, 15, 1.6, 3, DARK_LEAF, ang=-0.9); s.blob(12.5, 13.5, 1.6, 3, DARK_LEAF, ang=0.9)
        if stage == 2:
            s.blob(9.5, 8, 1.8, 2.6, BLOOD)
        else:
            s.blob(9.5, 7, 4.4, 3.6, BLOOD)
            for x, y in ((9, 6), (10, 6), (10, 7), (9, 8), (8, 7), (11, 8)):
                s.set(x, y, 'p2')                                   # spirala płatków
            s.set(8, 5, 'co'); s.set(7, 6, 'or')
    s.outline()
    return with_mound(s)


# ---------------- automatyka ----------------
def wisp_gardener(f, bob=True):
    """bob=False: bez podskoku w klatkach, klatki zmieniają tylko płomień."""
    s = Sprite(16, 20, 8, 19)
    dy = (0, -1, 0, 1)[f % 4] if bob else 0
    s.blob(8, 10 + dy, 3.6, 4.2, ['wi', 'wi', 'wl'])
    for i, (x, y) in enumerate(((7, 5), (8, 4), (9, 5), (8, 3))):
        if i < 3 or f % 2: s.set(x, 4 + dy - (4 - y) + 1, 'wl')
    for x, y in ((6, 14), (8, 15), (10, 14), (8, 16)):        # rozwiany "ogon"
        if (x + f) % 2 == 0 or y == 15: s.set(x, y + dy, 'wi')
    s.outline('b1')
    s.set(7, 9 + dy, 'o'); s.set(10, 9 + dy, 'o')             # oczy
    s.set(6, 18, 'b1'); s.set(7, 18, 'b1'); s.set(8, 18, 'b1'); s.set(9, 18, 'b1'); s.set(10, 18, 'b1')  # cień
    return s


def harvest_crate(fill):
    """fill: 0 pusta, 1 do połowy, 2 pełna. Dokładnie 16 px szerokości z obrysem (kafelek), więc sąsiednie skrzynie
    stykają się bokami zamiast na siebie nachodzić."""
    s = Sprite(16, 18, 8, 16)
    if fill >= 1:
        s.blob(5, 6.5, 2.4, 2.4, ORANGE); s.blob(10.5, 6.5, 2.3, 2.1, ['p1', 'b3', 'fo'])
    if fill >= 2:
        s.blob(8, 4.5, 2.4, 2.4, ORANGE); s.blob(12.2, 4.8, 1.9, 2.0, ['g1', 'g2', 'g3']); s.blob(3.6, 5, 1.8, 1.9, ['p0', 'p1', 'p2'])
    s.rect(1, 6, 14, 7, 'e0')
    for y in range(8, 16):
        for x in range(1, 15):
            k = 'e2' if y in (8, 12) else ('e1' if y in (11, 15) else 'e3')
            if x in (1, 14): k = 'e1'
            s.set(x, y, k)
    for y in range(8, 16):
        s.set(5, y, 'e1'); s.set(10, y, 'e1')
    return s.outline()


def merchant_chest(open_=False):
    s = Sprite(22, 20, 11, 18)
    body_top = 9
    for y in range(body_top, 18):
        for x in range(2, 20):
            k = 'e2' if y > body_top else 'e3'
            if y == 17: k = 'e1'
            s.set(x, y, k)
    for x in (5, 16):
        for y in range(body_top, 18): s.set(x, y, 'p1')
    if open_:
        s.rect(3, 4, 18, 8, 'e0')
        s.rect(3, 2, 18, 3, 'e2')
        for x in range(5, 17, 3): s.set(x, 6, 'go'); s.set(x + 1, 7, 'fl')
    else:
        for y in range(4, 9):
            w = 9 - max(0, 5 - y)
            for x in range(11 - w, 11 + w):
                s.set(x, y, 'e3' if y == 4 else 'e2')
        s.rect(9, 5, 12, 5, 'o')                                  # szczelina na monety
        for x in (5, 16):
            for y in range(5, 9): s.set(x, y, 'p1')
    s.rect(9, 10, 12, 13, 'cr'); s.set(10, 11, 'wi'); s.set(11, 11, 'wi'); s.set(10, 12, 'o'); s.set(11, 12, 'wi')  # zamek-oko
    return s.outline()


def rune_stone():
    s = Sprite(14, 20, 7, 18)
    s.blob(7, 10, 4.4, 7.5, STONE, clip_y=17)
    s.rect(3, 15, 11, 17, 'b1')
    for y in range(6, 13): s.set(7, y, 'wi')
    for x, y in ((6, 7), (5, 8), (8, 9), (9, 10), (6, 11)): s.set(x, y, 'wi')
    s.set(7, 6, 'wl')
    s.outline()
    for x in (4, 9): s.set(x, 16, 'g1')
    return s


def essence_jar(size, fill):
    """size 0/1/2 (mały/średni/duży), fill 0..1."""
    w, h = ((12, 18), (14, 22), (18, 26))[size]
    s = Sprite(w, h, w // 2, h - 1)
    cork_h, neck_h = 3, 2
    top = 1
    for y in range(top, top + cork_h):
        for x in range(w // 2 - 2, w // 2 + 2): s.set(x, y, 'e3' if y == top else 'e2')
    for y in range(top + cork_h, top + cork_h + neck_h):
        for x in range(w // 2 - 2, w // 2 + 2): s.set(x, y, 'p0')
    body_top = top + cork_h + neck_h
    body_bot = h - 3
    body_h = body_bot - body_top + 1
    level = body_bot - int(round(body_h * fill)) + 1
    for y in range(body_top, body_bot + 1):
        inset = 2 if y == body_top else (1 if y == body_top + 1 or y == body_bot else 0)
        for x in range(1 + inset, w - 1 - inset):
            if y >= level:
                k = 'fl' if y == level else 'fx'
                if (x * 3 + y * 5) % 11 == 0 and y > level + 1: k = 'fl'
            else:
                k = 'p0'
            if x == 2 + inset and body_top + 1 < y < body_bot - 1 and y < level:
                k = 'b3'                                            # odblask szkła
            s.set(x, y, k)
    s.outline()
    return s


def compost():
    s = Sprite(22, 18, 11, 16)
    s.blob(11, 6, 8, 2.4, ['e0', 'e1', 'g1'])
    for x, y in ((7, 5), (12, 6), (15, 5), (9, 7)): s.set(x, y, 'g2')
    for y in range(7, 16):
        for x in range(2, 20):
            k = 'e2' if (x - 2) % 4 else 'e0'
            if y == 7: k = 'e3'
            s.set(x, y, k)
    s.outline()
    for x, y in ((8, 1), (9, 0), (13, 2), (14, 1)): s.set(x, y, 'b3')  # para
    return s


# ---------------- morze ----------------
def pier():
    s = Sprite(38, 26, 2, 16)
    for y in range(8, 16):
        for x in range(0, 35):
            k = 'e3' if y == 8 else ('e2' if y < 15 else 'e1')
            if x % 4 == 3: k = 'e0'
            s.set(x, y, k)
    for px in (5, 17, 29):
        for y in range(16, 22):
            s.set(px, y, 'e1'); s.set(px + 1, y, 'e0')
    s.outline()
    for px in (5, 17, 29):
        for dx in (-1, 0, 1, 2, 3): s.set(px + dx, 23, 'fo')
    return s


def boat(lantern=True):
    s = Sprite(30, 16, 15, 14)
    for y in range(7, 14):
        half = 13 - max(0, y - 9) * 2
        for x in range(15 - half, 15 + half):
            k = 'e2'
            if y == 7: k = 'e3'
            elif y in (8, 9) and 15 - half + 2 <= x < 15 + half - 2: k = 'e0'
            elif y >= 12: k = 'e1'
            s.set(x, y, k)
    s.rect(13, 8, 16, 8, 'e2')                                  # ławka
    if lantern:
        s.line(24, 7, 24, 2, 'e1'); s.set(23, 2, 'e1')
        s.rect(22, 3, 23, 4, 'fx'); s.set(22, 3, 'fl')
    return s.outline()


def net_buoy():
    s = Sprite(16, 14, 8, 11)
    s.blob(8, 7, 2.6, 2.6, ['p2', 'co', 'or'])
    s.rect(6, 7, 10, 7, 'cr')
    s.line(8, 4, 8, 1, 'e1'); s.set(9, 1, 'co'); s.set(9, 2, 'co')
    s.outline()
    for x, y in ((2, 11), (4, 12), (6, 11), (10, 11), (12, 12), (14, 11), (3, 10), (13, 10)): s.set(x, y, 'b3')
    return s


def lighthouse():
    s = Sprite(26, 58, 13, 56)
    for y in range(18, 56):
        t = (y - 18) / 38
        half = 4 + t * 3.5
        band = ((y - 18) // 6) % 2
        for x in range(int(13 - half), int(13 + half) + 1):
            rel = (x - 13) / half
            if band == 0:
                k = 'cr' if rel < -0.3 else ('fo' if rel < 0.4 else 'b3')
            else:
                k = 'p2' if rel < -0.3 else ('p1' if rel < 0.4 else 'p0')
            s.set(x, y, k)
    s.rect(11, 50, 14, 55, 'e0'); s.set(12, 50, 'e1')             # drzwi
    s.rect(7, 16, 19, 17, 'p1')
    for x in range(7, 20, 2): s.set(x, 15, 'o')                    # balustrada
    for y in range(8, 15):
        for x in range(9, 18):
            s.set(x, y, 'p1' if x in (9, 12, 15, 17) else ('fl' if 11 <= y <= 12 else 'fx'))
    s.blob(13, 6, 5, 3, ['p0', 'p1', 'p2'], clip_y=7)
    s.set(13, 2, 'p1'); s.set(13, 1, 'p1')
    return s.outline()


def altar():
    s = Sprite(26, 22, 13, 20)
    for y in range(9, 12):
        for x in range(2, 24): s.set(x, y, 'fo' if y == 9 else 'b3')
    for y in range(12, 20):
        for x in range(3, 23): s.set(x, y, 'b1' if x < 20 else 'p1')
    for x, y in ((11, 14), (12, 13), (13, 13), (14, 14), (14, 15), (13, 16), (12, 16), (11, 15)):
        s.set(x, y, 'p0')                                              # półksiężyc
    s.set(12, 14, 'b1'); s.set(12, 15, 'b1')
    for cx, hgt in ((5, 4), (9, 3), (19, 5)):
        for y in range(9 - hgt, 9): s.set(cx, y, 'cr')
        s.set(cx + 1, 8, 'fo')
    s.outline()
    for cx, hgt in ((5, 4), (9, 3), (19, 5)):
        s.set(cx, 9 - hgt - 1, 'fl'); s.set(cx, 9 - hgt - 2, 'fx')
    return s


def ghost_merchant():
    s = Sprite(34, 34, 17, 32)
    for y in range(24, 31):
        half = 14 - max(0, y - 26) * 2
        for x in range(17 - half, 17 + half):
            s.set(x, y, 'p1' if y == 24 else ('p0' if y < 29 else 'o'))
    for y in range(6, 24):
        t = (y - 6) / 18
        half = 3 + t * 5
        for x in range(int(16 - half), int(16 + half) + 1):
            rel = (x - 16) / half
            s.set(x, y, 'p1' if rel < -0.4 else 'p0')
    for x in range(10, 23):
        if x % 3 == 0: s.set(x, 23, 'o')                              # postrzępiony płaszcz
    s.blob(16, 8, 3.5, 3.2, ['o', 'p0', 'p1'])
    s.rect(15, 8, 17, 10, 'o')
    s.line(22, 23, 27, 6, 'e1')
    s.line(27, 6, 29, 7, 'e1')
    s.outline()
    s.set(15, 9, 'wi'); s.set(17, 9, 'wi')
    s.rect(29, 8, 30, 10, 'wi'); s.set(29, 9, 'wl')
    return s


# ---------------- strefy ----------------
def gravestone(v):
    s = Sprite(14, 18, 7, 16)
    if v == 0:
        s.rect(3, 6, 10, 16, 'b3'); s.blob(6.5, 6, 3.6, 3.4, STONE, clip_y=6)
        for y in range(6, 16): s.set(10, y, 'b1')
        s.line(5, 8, 7, 11, 'p1')
    elif v == 1:
        s.rect(6, 2, 8, 16, 'b3'); s.rect(3, 5, 11, 7, 'b3')
        for y in range(2, 17): s.set(8, y, 'b1')
        s.rect(3, 7, 11, 7, 'b1')
    else:
        for y in range(7, 17):
            sh = (16 - y) // 4
            for x in range(3 + sh, 10 + sh):
                if not (y < 9 and x > 7 + sh): s.set(x, y, 'b3' if x < 9 + sh else 'b1')
    for x in range(2, 12): s.set(x, 16, 'g1')
    s.set(4, 15, 'g2'); s.set(9, 15, 'g1')
    return s.outline()


def iron_fence():
    s = Sprite(18, 14, 9, 12)
    for x in range(1, 17, 3):
        for y in range(3, 12): s.set(x, y, 'p1')
        s.set(x, 2, 'b3'); s.set(x, 1, 'b3')
    for x in range(1, 17):
        s.set(x, 5, 'p1'); s.set(x, 10, 'p1')
    return s.outline()


def reeds():
    s = Sprite(14, 18, 7, 16)
    for x, h, lean in ((3, 9, -1), (6, 12, 0), (9, 10, 1), (11, 7, 1)):
        s.line(x, 16, x + lean, 16 - h, 'g1'); s.set(x + lean, 16 - h, 'g2')
    for x, h in ((6, 12), (9, 10)):
        s.rect(x + (0 if x == 6 else 1), 16 - h + 1, x + (0 if x == 6 else 1), 16 - h + 4, 'e1')
        s.set(x + (0 if x == 6 else 1), 16 - h + 1, 'e2')
    return s.outline()


def swamp_pool():
    s = Sprite(30, 14, 15, 12)
    s.blob(15, 7, 13, 5, ['g0', 'g0', 'g1'])
    for x, y in ((9, 6), (10, 6), (18, 8), (19, 8), (21, 6)): s.set(x, y, 'b1')
    for x, y in ((12, 8), (22, 5)): s.set(x, y, 'b3')
    return s.outline('e0')


def broken_pillar():
    s = Sprite(14, 28, 7, 26)
    for y in range(7, 24):
        for x in range(3, 11): s.set(x, y, 'fo' if x in (4, 7) else ('b3' if x < 9 else 'b1'))
    for x, cut in zip(range(3, 11), (8, 6, 5, 7, 4, 6, 9, 8)):
        for y in range(7, cut): s.px.pop((x, y), None)
    s.rect(1, 24, 12, 26, 'b3'); s.rect(1, 26, 12, 26, 'b1')
    s.set(5, 15, 'g1'); s.set(6, 16, 'g1'); s.set(9, 20, 'g2')
    return s.outline()


def ruined_arch():
    s = Sprite(36, 32, 18, 30)
    cx, cy = 18, 15
    for y in range(2, 30):
        for x in range(2, 34):
            d = math.hypot(x - cx, (y - cy) * 1.1)
            in_arch = y <= cy and 10 <= d <= 15 and x < 24
            in_left = 3 <= x <= 7 and y > cy - 1
            in_right = 29 <= x <= 33 and y > cy + 6
            if in_arch or in_left or in_right:
                s.set(x, y, 'b3' if (x + y // 3) % 5 else 'b1')
    for x, y in ((6, 20), (31, 25), (12, 6), (5, 26)): s.set(x, y, 'g1')
    return s.outline()


def dead_tree():
    s = Sprite(30, 42, 15, 40)
    def branch(x, y, ang, length, w, depth):
        x2, y2 = x + math.cos(ang) * length, y + math.sin(ang) * length
        n = int(length * 2)
        for i in range(n + 1):
            t = i / n
            px, py = x + (x2 - x) * t, y + (y2 - y) * t
            for dw in range(w):
                s.set(int(round(px)) + dw, int(round(py)), 'e2' if dw == 0 else 'e1')
        if depth > 0:
            branch(x2, y2, ang - 0.55, length * 0.66, max(1, w - 1), depth - 1)
            branch(x2, y2, ang + 0.45, length * 0.6, max(1, w - 1), depth - 1)
    branch(14, 39, -math.pi / 2, 13, 3, 3)
    for x in range(11, 19): s.set(x, 39, 'e1')
    return s.outline()


# ---------------- zdarzenia i cząsteczki ----------------
def monster_eye(f):
    s = Sprite(22, 14, 11, 12)
    s.blob(11, 8, 9, 4.5, ['o', 'p0', 'p1', 'p2'], clip_y=11)
    if f >= 1:
        hh = 1 if f == 1 else 2.6
        s.blob(11, 8, 5, hh, ['fo', 'cr', 'cr'])
        if f == 2:
            s.blob(11, 8, 2.2, 2.2, ['wi', 'wi', 'wl']); s.rect(11, 6, 11, 10, 'o')
    s.outline()
    for x in range(0, 22, 3): s.set(x, 12, 'fo')
    return s


def tentacle(f):
    s = Sprite(16, 34, 8, 32)
    h = (12, 22, 30)[f]
    for i in range(h):
        y = 31 - i
        t = i / 30
        x = 8 + 3 * math.sin(i * 0.22 + 0.5) * (t + 0.2)
        w = max(1, int(3.2 * (1 - t * 0.8)))
        for dx in range(-w, w + 1):
            s.set(int(round(x)) + dx, y, 'p1' if dx < 0 else ('p2' if dx == 0 else 'p0'))
        if i % 4 == 2 and w > 1:
            s.set(int(round(x)) + w, y, 'fo')                      # przyssawki
    s.outline()
    for x in (3, 5, 11, 13): s.set(x, 32, 'fo')
    return s


def falling_star(f):
    s = Sprite(20, 20, 0, 0)
    hx, hy = 6 + f * 4, 4 + f * 3
    for i in range(7):
        x, y = hx - i * 2, hy - i * 1.5
        k = 'wl' if i == 0 else ('fl' if i < 3 else 'fx')
        if 0 <= x < 20 and 0 <= y < 20 and (i < 4 or (i + f) % 2 == 0):
            s.set(int(x), int(y), k)
            if i == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)): s.set(int(x) + dx, int(y) + dy, 'fl')
    return s


def star_shard():
    s = Sprite(12, 14, 6, 13)
    for y in range(1, 12):
        half = (y - 1) * 0.5 if y < 6 else (11 - y) * 0.8
        for x in range(int(6 - half), int(6 + half) + 1):
            s.set(x, y, 'wl' if x < 6 and y < 7 else ('cr' if x <= 6 else 'fo'))
    return s.outline()


def moth(f):
    s = Sprite(7, 6, 3, 5)
    s.set(3, 2, 'e1'); s.set(3, 3, 'e1')
    if f == 0:
        for x, y in ((1, 1), (2, 1), (4, 1), (5, 1), (1, 2), (5, 2)): s.set(x, y, 'cr')
    else:
        for x, y in ((2, 2), (4, 2), (2, 3), (4, 3)): s.set(x, y, 'fo')
    return s


def bubble():
    s = Sprite(5, 5, 2, 4)
    for x, y in ((1, 0), (2, 0), (0, 1), (3, 1), (0, 2), (3, 2), (1, 3), (2, 3)): s.set(x, y, 'fo')
    s.set(1, 1, 'cr')
    return s


# rejestr do arkuszy i eksportu: nazwa -> (kategoria, funkcja bez argumentów)
CROPS_NIGHT = {
    'moonflower': ("Księżycowy kwiat", moonflower),
    'ghostpumpkin': ("Upiorna dynia", ghost_pumpkin),
    'corpseshroom': ("Trupi grzyb", corpse_mushroom),
    'bloodrose': ("Krwawa róża", blood_rose),
}

OBJECTS = {
    'Automation': {
        'wisp_gardener_0': lambda: wisp_gardener(0), 'wisp_gardener_1': lambda: wisp_gardener(1),
        'wisp_gardener_2': lambda: wisp_gardener(2), 'wisp_gardener_3': lambda: wisp_gardener(3),
        'harvest_crate_0': lambda: harvest_crate(0), 'harvest_crate_1': lambda: harvest_crate(1),
        'harvest_crate_2': lambda: harvest_crate(2),
        'merchant_chest': merchant_chest, 'merchant_chest_open': lambda: merchant_chest(True),
        'rune_stone': rune_stone, 'compost': compost,
        'essence_jar_s': lambda: essence_jar(0, 0.6), 'essence_jar_m': lambda: essence_jar(1, 0.6),
        'essence_jar_l': lambda: essence_jar(2, 0.6),
    },
    'Sea': {
        'pier': pier, 'boat': boat, 'net_buoy': net_buoy, 'lighthouse': lighthouse, 'altar': altar,
        'ghost_merchant': ghost_merchant,
    },
    'Zones': {
        'gravestone_0': lambda: gravestone(0), 'gravestone_1': lambda: gravestone(1),
        'gravestone_2': lambda: gravestone(2), 'iron_fence': iron_fence, 'reeds': reeds,
        'swamp_pool': swamp_pool, 'broken_pillar': broken_pillar, 'ruined_arch': ruined_arch,
        'dead_tree': dead_tree,
    },
    'Events': {
        'monster_eye_0': lambda: monster_eye(0), 'monster_eye_1': lambda: monster_eye(1),
        'monster_eye_2': lambda: monster_eye(2),
        'tentacle_0': lambda: tentacle(0), 'tentacle_1': lambda: tentacle(1), 'tentacle_2': lambda: tentacle(2),
        'falling_star_0': lambda: falling_star(0), 'falling_star_1': lambda: falling_star(1),
        'falling_star_2': lambda: falling_star(2), 'star_shard': star_shard,
        'moth_0': lambda: moth(0), 'moth_1': lambda: moth(1), 'bubble': bubble,
    },
}
