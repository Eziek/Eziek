"""Strefy (cmentarz, bagno, ruiny): podłoża i dekoracje + usuwanie dekoracji (animacje, pnie, gruz, cząsteczki, materiały)."""
import math, random
import sprites3 as S3          # paleta mroczna + emisyjna
import sprites
from sprites import Sprite, GREEN, EARTH, STONE
from sprites3 import RGB

BONE = ['b3', 'fo', 'cr']
MOSS = ['g0', 'g1', 'g2']
WOOD = EARTH
DSTONE = ['p0', 'p1', 'b1', 'b3']


def h(x, y, s=0):
    """Deterministyczny hash 0..1 dla piksela (stały dla miejsca w świecie)."""
    n = (x * 374761393 + y * 668265263 + s * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) / 0xFFFFFFFF


def vnoise(x, y, cell=6, s=0):
    gx, gy = x / cell, y / cell
    x0, y0 = int(math.floor(gx)), int(math.floor(gy))
    fx, fy = gx - x0, gy - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = h(x0, y0, s), h(x0 + 1, y0, s)
    c, d = h(x0, y0 + 1, s), h(x0 + 1, y0 + 1, s)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


# ---------------------------------------------------------------- podłoża (kolor piksela świata)
def ground(zone, x, y):
    if zone == 'cemetery':
        n = vnoise(x, y, 7, 1)
        k = 'e1' if n > 0.68 else ('g0' if n < 0.25 else 'g1')
        r = h(x, y, 2)
        if r > 0.985: k = 'p1'                     # kamyki
        elif r > 0.978 and k == 'e1': k = 'b3'     # kosteczki
        return k
    if zone == 'swamp':
        n = vnoise(x, y, 6, 3)
        if n > 0.66:
            k = 'b1' if vnoise(x, y, 3, 4) < 0.7 else 'b2'   # kałuże
            if n > 0.66 and n < 0.69: k = 'e0'               # brzeg błota
            return k
        k = 'g1' if n > 0.35 else 'g0'
        if h(x, y, 5) > 0.97: k = 'e1'
        return k
    if zone == 'ruins':
        row = y // 6
        bx = (x + (3 if row % 2 else 0)) // 7
        ex = (x + (3 if row % 2 else 0)) % 7
        ey = y % 6
        if vnoise(x, y, 8, 7) > 0.72:
            return 'g1' if h(x, y, 8) > 0.3 else 'g2'        # mech wchodzi na płyty
        if ex == 0 or ey == 0:
            return 'p0'                                         # fugi
        k = 'b1' if h(bx, row, 9) < 0.5 else 'p1'
        if ex == 1 and ey == 1: k = 'b3'
        if h(x, y, 10) > 0.97: k = 'p0'                         # pęknięcia
        return k
    return 'g2'


def ground_tile(zone, ox=0, oy=0):
    s = Sprite(16, 16, 0, 0)
    for y in range(16):
        for x in range(16):
            s.set(x, y, ground(zone, ox + x, oy + y))
    return s


# ---------------------------------------------------------------- cmentarz
def obelisk():
    s = Sprite(12, 26, 6, 24)
    for y in range(4, 22):
        half = 1 + (y - 4) // 7
        for x in range(6 - half - 1, 6 + half + 1):
            s.set(x, y, 'fo' if x < 6 else ('b3' if x == 6 else 'b1'))
    s.set(6, 3, 'fo'); s.set(5, 3, 'b3')
    s.rect(2, 22, 10, 24, 'b1'); s.rect(2, 22, 9, 22, 'b3')
    for y in (9, 14): s.set(5, y, 'p1')
    return s.outline()


def wooden_cross():
    s = Sprite(12, 18, 6, 16)
    for y in range(3, 16):
        x = 6 + (y - 3) // 6 - 1
        s.set(x, y, 'e2'); s.set(x + 1, y, 'e1')
    s.line(2, 7, 9, 6, 'e2'); s.line(2, 8, 9, 7, 'e1')
    s.set(3, 8, 'go')                                         # gwóźdź / tabliczka
    s.outline()
    s.set(5, 16, 'e1'); s.set(7, 16, 'e1')
    return s


def open_grave():
    s = Sprite(20, 14, 10, 12)
    s.blob(10, 8, 7.5, 3.6, ['o', 'e0', 'e0'])               # dół
    s.blob(16, 4.5, 4, 2.4, EARTH)                            # kopczyk ziemi
    s.line(2, 2, 5, 9, 'e2'); s.rect(1, 1, 3, 2, 'b3')        # łopata
    s.outline()
    return s


def crypt():
    s = Sprite(34, 36, 17, 34)
    for y in range(12, 33):
        for x in range(4, 30):
            k = 'b1' if x < 26 else 'p1'
            if (y - 12) % 5 == 0 or ((x + (y // 5) * 3) % 8 == 0): k = 'p0'
            s.set(x, y, k)
    for y in range(4, 12):                                     # dach
        half = 15 - (11 - y) * 1.6
        for x in range(int(17 - half), int(17 + half) + 1):
            s.set(x, y, 'p1' if x > 17 else 'p2')
    s.set(17, 2, 'b3'); s.set(17, 3, 'b3'); s.set(16, 3, 'b3'); s.set(18, 3, 'b3')   # krzyż na dachu
    s.rect(13, 20, 21, 32, 'o')                                # drzwi
    for y in range(20, 33): s.set(17, y, 'p0')
    s.set(15, 26, 'go'); s.set(19, 26, 'go')
    s.rect(11, 18, 23, 19, 'b3')                               # nadproże
    for x, y in ((6, 30), (7, 31), (27, 29), (28, 31), (5, 14)): s.set(x, y, 'g1')
    return s.outline()


def skull_pile():
    s = Sprite(16, 12, 8, 10)
    for cx, cy in ((4.5, 7.5), (11, 7.5), (7.8, 4.5)):
        s.blob(cx, cy, 2.8, 2.5, BONE)
    s.outline()
    for cx, cy in ((4, 7), (10, 7), (7, 4)):
        s.set(cx, cy, 'o'); s.set(cx + 2, cy, 'o')
    return s


def candles():
    s = Sprite(14, 12, 7, 10)
    for x, hgt in ((3, 4), (6, 6), (9, 3), (11, 5)):
        for y in range(10 - hgt, 10): s.set(x, y, 'cr')
        s.set(x + 1, 9, 'fo')
    s.outline()
    for x, hgt in ((3, 4), (6, 6), (9, 3), (11, 5)):
        s.set(x, 9 - hgt, 'fl'); s.set(x, 8 - hgt, 'fx')
    for x in range(2, 13): s.set(x, 10, 'cr' if x % 3 else 'fo')    # wosk
    return s


def iron_gate():
    s = Sprite(30, 26, 15, 24)
    for px in (2, 25):
        s.rect(px, 6, px + 2, 24, 'b1'); s.rect(px, 6, px, 24, 'b3')
        s.rect(px - 1, 4, px + 3, 5, 'b3')
    for x in range(6, 25, 3):
        top = 9 + int(abs(x - 15) / 3)
        for y in range(top, 24): s.set(x, y, 'p1')
        s.set(x, top - 1, 'b3')
    for x in range(5, 25):
        s.set(x, 14, 'p1'); s.set(x, 22, 'p1')
    return s.outline()


def thorns():
    s = Sprite(18, 14, 9, 12)
    rnd = random.Random(4)
    for _ in range(9):
        x0, y0 = 9 + rnd.randint(-2, 2), 12
        a = -math.pi / 2 + rnd.uniform(-1.1, 1.1)
        L = rnd.uniform(5, 10)
        x1, y1 = x0 + math.cos(a) * L, y0 + math.sin(a) * L
        s.line(x0, y0, int(x1), int(y1), 'p1')
        s.set(int(x1), int(y1), 'p2')
    return s.outline()


def crow(f):
    s = Sprite(12, 10, 6, 9)
    s.blob(6, 5, 3, 2.4, ['o', 'p0', 'p1'])
    s.blob(8.5, 3.2, 1.6, 1.5, ['o', 'p0', 'p1'])
    s.set(10, 3, 'go'); s.set(11, 3, 'go')
    if f == 1:
        s.line(5, 4, 1, 1, 'p0'); s.line(6, 4, 3, 0, 'p0')     # skrzydło w górę
    else:
        s.line(2, 6, 5, 6, 'p0')                               # ogon
    s.set(5, 8, 'go'); s.set(7, 8, 'go')
    s.outline()
    s.set(9, 3, 'fl')
    return s


# ---------------------------------------------------------------- bagno
def mangrove():
    s = Sprite(34, 38, 17, 36)
    for i, (x0, dx) in enumerate(((10, -5), (14, -2), (20, 3), (24, 6))):    # korzenie
        s.line(17, 26, x0 + dx, 35, 'e1'); s.line(17, 27, x0 + dx + 1, 35, 'e0')
    for y in range(14, 30):
        sh = int(2 * math.sin(y * 0.4))
        s.set(16 + sh, y, 'e2'); s.set(17 + sh, y, 'e1'); s.set(18 + sh, y, 'e0')
    for cx, cy, rx, ry in ((10, 12, 7, 5), (24, 11, 7, 5), (17, 7, 9, 6)):
        s.blob(cx, cy, rx, ry, ['g0', 'g0', 'g1', 'g2'])
    for x in (7, 11, 21, 26, 15):                               # zwisający mech
        for y in range(14, 14 + (x % 5) + 4): s.set(x, y, 'g1' if y % 2 else 'g0')
    return s.outline()


def lily_pads():
    s = Sprite(20, 12, 10, 10)
    for cx, cy, r in ((6, 6, 3.4), (13, 4.5, 2.6), (14, 9, 2.2)):
        s.blob(cx, cy, r, r * 0.6, MOSS)
        s.set(int(cx + r * 0.3), int(cy), 'b1')                 # nacięcie liścia
    s.outline('b0')
    s.set(6, 5, 'ml'); s.set(7, 5, 'mu'); s.set(6, 4, 'ml')    # kwiatek (lekko świeci)
    return s


def rotten_log():
    s = Sprite(28, 14, 14, 12)
    for y in range(6, 12):
        for x in range(2, 26):
            k = 'e1' if y > 7 else 'e2'
            if y == 11: k = 'e0'
            if (x * 7 + y) % 9 == 0: k = 'e0'
            s.set(x, y, k)
    s.blob(25, 9, 2, 3, ['e0', 'e1', 'e2'])
    s.set(25, 9, 'e0')
    for x in range(4, 22, 5): s.set(x, 6, 'g1')
    for cx in (7, 12, 18):
        s.blob(cx, 4.5, 1.8, 1.2, ['mu', 'mu', 'ml'], clip_y=5)
        s.set(cx, 5, 'fo')
    return s.outline()


def bog_bubbles(f):
    s = Sprite(12, 10, 6, 9)
    pts = [((3, 7), (8, 8)), ((3, 6), (8, 7), (5, 4)), ((3, 4), (8, 6), (5, 2))][f]
    for x, y in pts:
        for dx, dy in ((1, 0), (0, 1), (1, 1), (0, 0)):
            pass
        s.set(x, y, 'g2'); s.set(x + 1, y, 'g1'); s.set(x, y - 1, 'g3')
    return s


def cattails():
    s = Sprite(16, 22, 8, 20)
    for x, hgt, lean in ((3, 14, -1), (6, 18, 0), (9, 16, 1), (12, 12, 1), (8, 10, 0)):
        s.line(x, 20, x + lean, 20 - hgt, 'g1'); s.set(x + lean, 20 - hgt, 'g2')
    for x, hgt, lean in ((6, 18, 0), (9, 16, 1), (3, 14, -1)):
        s.rect(x + lean, 20 - hgt + 2, x + lean, 20 - hgt + 6, 'e1')
        s.set(x + lean, 20 - hgt + 2, 'e2')
    s.blob(4, 17, 2.4, 1.4, MOSS, ang=-0.4); s.blob(12, 16, 2.4, 1.4, MOSS, ang=0.4)
    return s.outline()


def frog(f):
    s = Sprite(10, 8, 5, 7)
    s.blob(5, 5, 3, 2 if f == 0 else 1.6, ['g0', 'g1', 'g2'])
    s.set(3, 3, 'g2'); s.set(7, 3, 'g2')
    s.outline()
    s.set(3, 2, 'fl'); s.set(7, 2, 'fl')
    if f == 1:
        s.blob(5, 6.6, 1.6, 1.1, ['co', 'co', 'or'])          # nadmuchane gardło
    return s


# ---------------------------------------------------------------- ruiny
def broken_wall():
    s = Sprite(34, 24, 17, 22)
    top = [10, 9, 9, 8, 6, 6, 5, 7, 8, 8, 6, 4, 4, 5, 8, 10, 12, 12, 13, 15, 16, 16]
    for x in range(4, 30):
        t = top[min(len(top) - 1, (x - 4))] if x - 4 < len(top) else 18
        for y in range(t, 22):
            row = y // 4
            k = 'b1' if x < 27 else 'p1'
            if y % 4 == 0 or (x + row * 3) % 6 == 0: k = 'p0'
            s.set(x, y, k)
    for x, y in ((8, 9), (9, 10), (14, 6), (20, 14), (12, 20)): s.set(x, y, 'g1')
    return s.outline()


def fallen_column():
    s = Sprite(32, 14, 16, 12)
    for y in range(5, 11):
        for x in range(4, 27):
            k = 'fo' if y == 5 else ('b3' if y < 8 else 'b1')
            if x % 5 == 0 and y < 10: k = 'p1'
            s.set(x, y, k)
    s.blob(4.5, 8, 2.2, 3.2, ['p1', 'b1', 'b3'])
    for x, y in ((28, 10), (29, 9), (27, 11)): s.set(x, y, 'b3')
    for x in (9, 18, 22): s.set(x, 10, 'g1')
    return s.outline()


def statue():
    s = Sprite(18, 34, 9, 32)
    s.rect(3, 26, 15, 31, 'b1'); s.rect(3, 26, 15, 26, 'b3')
    for y in range(9, 26):
        half = 3 + (y - 9) / 6
        for x in range(int(9 - half), int(9 + half) + 1):
            s.set(x, y, 'b3' if x < 8 else ('b1' if x < 11 else 'p1'))
    s.blob(9, 6, 3, 3.4, ['p1', 'b1', 'b3'])
    s.rect(8, 6, 10, 8, 'o')                                     # pusty kaptur
    s.line(12, 10, 14, 17, 'p0')                                 # pęknięcie
    for x, y in ((5, 25), (6, 24), (12, 27)): s.set(x, y, 'g1')
    return s.outline()


def stone_stairs():
    s = Sprite(24, 18, 12, 16)
    for i in range(4):
        y0 = 4 + i * 3
        for y in range(y0, y0 + 3):
            for x in range(2 + i, 22 - i):
                s.set(x, y, 'b3' if y == y0 else ('b1' if y == y0 + 1 else 'p1'))
    s.rect(2, 16, 21, 16, 'p0')
    for x, y in ((4, 7), (19, 10)): s.set(x, y, 'g1')
    return s.outline()


def rubble():
    s = Sprite(18, 10, 9, 8)
    for cx, cy, r in ((5, 6, 2.6), (10, 5, 3), (14, 7, 2), (8, 7.5, 1.6)):
        s.blob(cx, cy, r, r * 0.75, DSTONE)
    return s.outline()


def brazier():
    s = Sprite(14, 22, 7, 20)
    s.rect(6, 10, 8, 19, 'b1'); s.rect(4, 19, 10, 20, 'b3')
    s.blob(7, 8, 5, 2.2, ['p0', 'p1', 'b3'], clip_y=9)
    s.outline()
    for x, y in ((5, 5), (7, 3), (9, 5), (6, 4), (8, 4), (7, 5)): s.set(x, y, 'fx')
    s.set(7, 4, 'fl'); s.set(6, 5, 'fl'); s.set(8, 5, 'fl'); s.set(7, 2, 'fx')
    return s


def rune_tablet():
    s = Sprite(16, 20, 8, 18)
    s.rect(3, 4, 12, 17, 'b1'); s.rect(3, 4, 12, 4, 'b3'); s.rect(12, 4, 12, 17, 'p1')
    for x, y in ((6, 7), (7, 8), (8, 7), (6, 10), (8, 10), (7, 11), (7, 13), (6, 14), (8, 14)): s.set(x, y, 'wi')
    s.set(4, 16, 'g1'); s.set(11, 15, 'g1')
    s.outline()
    return s


def vine_pillar():
    s = sprites3_pillar = S3.broken_pillar()
    for y in range(8, 25):
        x = 4 + int(2.5 * math.sin(y * 0.7)) + 2
        if (x, y) in s.px and s.px[(x, y)] != 'o': s.set(x, y, 'g1' if y % 3 else 'g2')
    return s


# ---------------------------------------------------------------- usuwanie dekoracji
def rotated(spr, deg, pivot):
    """Obrót pixel art (najbliższy sąsiad) wokół punktu zaczepienia - klatki przewracania."""
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    px, py = pivot
    out = Sprite(spr.w + 40, spr.h + 10, spr.ax + 20, spr.ay)
    for y in range(out.h):
        for x in range(out.w):
            # punkt docelowy -> źródło
            dx, dy = x - (px + 20), y - py
            sx = ca * dx + sa * dy + px
            sy = -sa * dx + ca * dy + py
            k = spr.px.get((int(round(sx)), int(round(sy))))
            if k: out.px[(x, y)] = k
    return out


def stump(kind='round'):
    s = Sprite(16, 12, 8, 10)
    w = {'round': 4, 'spruce': 3, 'dead': 3}[kind]
    s.blob(8, 8.5, w + 1.5, 2.2, ['e0', 'e1', 'e2'])
    s.rect(8 - w, 4, 8 + w - 1, 8, 'e1'); s.rect(8 - w, 4, 8 - w + 1, 8, 'e2')
    s.blob(8, 4, w, 1.4, ['e2', 'e3', 'go'])
    s.set(8, 4, 'e1'); s.set(7, 4, 'e2')                      # słoje
    if kind == 'round':
        s.set(4, 9, 'e1'); s.set(12, 9, 'e1')
    return s.outline()


def rock_cracked():
    s = sprites.rock()
    s = Sprite(s.w, s.h, s.ax, s.ay); r = sprites.rock(); s.px = dict(r.px)
    for x, y in ((7, 3), (7, 4), (8, 5), (8, 6), (9, 7), (6, 6), (5, 7)):
        if (x, y) in s.px and s.px[(x, y)] != 'o': s.px[(x, y)] = 'o'
    return s


def gravestone_broken():
    s = Sprite(14, 18, 7, 16)
    s.rect(3, 11, 10, 16, 'b3'); s.rect(10, 11, 10, 16, 'b1')
    s.line(3, 11, 10, 9, 'b3')
    s.blob(11, 15, 2.4, 1.4, STONE)
    for x in range(2, 12): s.set(x, 16, 'g1')
    return s.outline()


def bush_cut():
    s = Sprite(18, 10, 9, 8)
    for x in range(4, 15, 2):
        s.line(x, 8, x + (1 if x % 4 else -1), 5, 'e1'); s.set(x + (1 if x % 4 else -1), 5, 'g1')
    s.blob(9, 8, 6, 1.2, ['g0', 'g1', 'g1'])
    return s.outline()


def particle(kind, f=0):
    s = Sprite(6, 6, 3, 5)
    if kind == 'woodchip':
        s.set(2, 2, 'e3'); s.set(3, 2, 'e2'); s.set(3, 3, 'e1')
    elif kind == 'leaf':
        s.set(2, 2, 'g3'); s.set(3, 3, 'g2'); s.set(3, 2, 'g2')
    elif kind == 'stonechip':
        s.set(2, 2, 'fo'); s.set(3, 2, 'b3'); s.set(2, 3, 'b1')
    elif kind == 'dust':
        r = (1, 2, 2)[f]
        for x in range(6):
            for y in range(6):
                d = math.hypot(x - 2.5, y - 2.5)
                if d <= r and (f < 2 or (x + y) % 2 == 0): s.set(x, y, 'fo' if d < r - 0.8 else 'b3')
    return s


def icon_material(kind):
    import ui_kit as K
    if kind == 'wood':
        return K.from_map(["..........", ".hhhhhh...", "hjjjjjjh..", "hjfhfjjhf.", "hjjjjjjhf.",
                           ".hhhhhhff.", "..hhhhhh..", ".hjjjjjjh.", ".hjjfhjjhf", "..hhhhhhf."])
    return K.from_map(["..........", "...2222...", "..2sszz2..", ".2ssszzz2.", ".2sssszz2.",
                       ".2bsssss2.", "..2bbbb2..", "...2222...", "..........", ".........."])


ZONE_DECOR = {
    'cemetery': {'obelisk': obelisk, 'wooden_cross': wooden_cross, 'open_grave': open_grave, 'crypt': crypt,
                 'skull_pile': skull_pile, 'candles': candles, 'iron_gate': iron_gate, 'thorns': thorns,
                 'crow_0': lambda: crow(0), 'crow_1': lambda: crow(1)},
    'swamp': {'mangrove': mangrove, 'lily_pads': lily_pads, 'rotten_log': rotten_log, 'cattails': cattails,
              'bog_bubbles_0': lambda: bog_bubbles(0), 'bog_bubbles_1': lambda: bog_bubbles(1),
              'bog_bubbles_2': lambda: bog_bubbles(2), 'frog_0': lambda: frog(0), 'frog_1': lambda: frog(1)},
    'ruins': {'broken_wall': broken_wall, 'fallen_column': fallen_column, 'statue': statue, 'stone_stairs': stone_stairs,
              'rubble': rubble, 'brazier': brazier, 'rune_tablet': rune_tablet, 'vine_pillar': vine_pillar},
}


def removal_sets():
    tree = sprites.round_tree()
    sets = {
        'tree_round_fall': [rotated(tree, a, (tree.ax, tree.ay)) for a in (0, 12, 35, 70)],
        'stumps': [stump('round'), stump('spruce'), stump('dead')],
        'remains': [rock_cracked(), rubble(), gravestone_broken(), bush_cut()],
        'particles': [particle('woodchip'), particle('leaf'), particle('stonechip'),
                      particle('dust', 0), particle('dust', 1), particle('dust', 2)],
        'icons': [icon_material('wood'), icon_material('stone')],
    }
    return sets
