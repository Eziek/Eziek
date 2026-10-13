"""Paleta + proceduralne sprite'y w stylu referencji (gruby ciemny kontur, hue-shift, okrągłe kształty)."""
import math

PAL = {
    'o':  "#1e121f",  # kontur (prawie czarny, śliwkowy - jak w referencjach)
    'p0': "#2e2236", 'p1': "#51395B", 'p2': "#8B4263",            # śliwka (z palety)
    'co': "#CC6058", 'or': "#e8894f", 'go': "#f2b866", 'cr': "#f6e3a1",  # ciepłe
    'e0': "#4a2a33", 'e1': "#6e3b3a", 'e2': "#9a5a45", 'e3': "#c48a5a",  # ziemia/drewno
    'g0': "#1f4a46", 'g1': "#2e7a55", 'g2': "#4fa760", 'g3': "#9ccf6a",  # zieleń
    'b0': "#2f4a6e", 'b1': "#477495", 'b2': "#63A3B4", 'b3': "#92ABB1", 'fo': "#d4e6e0",  # woda (z palety)
}
RGB = {k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for k, v in PAL.items()}

GREEN = ['g0', 'g1', 'g2', 'g3']
SPRUCE = ['g0', 'b1', 'b2', 'b3']
EARTH = ['e0', 'e1', 'e2', 'e3']
ORANGE = ['p2', 'co', 'or', 'go']
WHEAT = ['e2', 'go', 'cr']
STONE = ['p1', 'b1', 'b3', 'fo']
GOLD = ['e2', 'go', 'cr']

_l = (-0.6, -0.75, 0.55)
_n = math.sqrt(sum(v * v for v in _l))
LIGHT = tuple(v / _n for v in _l)


class Sprite:
    def __init__(self, w, h, ax=None, ay=None, off=(0, 0)):
        self.w, self.h = w, h
        self.offx, self.offy = off
        self.ax = w // 2 if ax is None else ax
        self.ay = h - 1 if ay is None else ay
        self.px = {}

    def set(self, x, y, k):
        x, y = x + self.offx, y + self.offy
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[(x, y)] = k

    def rect(self, x0, y0, x1, y1, k):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, k)

    def line(self, x0, y0, x1, y1, k):
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, k)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy: err += dy; x0 += sx
            if e2 <= dx: err += dx; y0 += sy

    def blob(self, cx, cy, rx, ry, ramp, ang=0.0, lo=-0.15, hi=1.0, clip_y=None):
        ca, sa = math.cos(ang), math.sin(ang)
        R = int(max(rx, ry)) + 2
        for y in range(int(cy) - R, int(cy) + R + 1):
            if clip_y is not None and y > clip_y:
                continue
            for x in range(int(cx) - R, int(cx) + R + 1):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                u = (dx * ca + dy * sa) / rx
                v = (-dx * sa + dy * ca) / ry
                d = u * u + v * v
                if d > 1:
                    continue
                z = math.sqrt(1 - d)
                nx, ny = u * ca - v * sa, u * sa + v * ca
                l = nx * LIGHT[0] + ny * LIGHT[1] + z * LIGHT[2]
                t = (l - lo) / (hi - lo)
                self.set(x, y, ramp[max(0, min(len(ramp) - 1, int(t * len(ramp))))])

    def outline(self, k='o'):
        add = {}
        for (x, y) in self.px:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                p = (x + dx, y + dy)
                if p not in self.px and 0 <= p[0] < self.w and 0 <= p[1] < self.h:
                    add[p] = k
        self.px.update(add)
        return self


# ---------- obiekty ----------
def round_tree():
    s = Sprite(32, 40, 16, 38)
    for y in range(24, 37):
        for x in range(14, 18):
            s.set(x, y, ['e3', 'e2', 'e1', 'e0'][x - 14])
    s.set(13, 36, 'e2'); s.set(18, 36, 'e0'); s.set(13, 35, 'e2'); s.set(18, 35, 'e1')
    s.blob(9, 19, 7, 6, GREEN)
    s.blob(23, 19, 7, 6, GREEN)
    s.blob(16, 12, 10, 9, GREEN)
    s.blob(16, 22, 8, 5, GREEN, lo=0.0)
    for x, y in ((12, 7), (13, 6), (10, 9), (19, 9)):
        s.set(x, y, 'g3')
    return s.outline()


def spruce():
    s = Sprite(22, 40, 11, 38)
    for y in range(30, 37):
        s.set(10, y, 'e2'); s.set(11, y, 'e1')
    tiers = [(2, 12, 5), (8, 20, 7), (15, 30, 9)]
    for top, bot, hw in tiers:
        for y in range(top, bot + 1):
            w = (y - top + 1) / (bot - top + 1) * hw
            for x in range(int(11 - w), int(11 + w) + 1):
                rel = (x - 11) / max(w, 1)
                k = 'b2' if rel < -0.35 else ('b1' if rel < 0.3 else 'g0')
                if y == bot and rel > -0.6: k = 'g0'
                s.set(x, y, k)
    s.set(9, 6, 'b3'); s.set(8, 13, 'b3'); s.set(6, 21, 'b3'); s.set(7, 22, 'b3')
    return s.outline()


def rock():
    s = Sprite(18, 14, 9, 12, off=(1, 1))
    s.blob(7, 6.5, 6, 4.2, STONE)
    s.blob(11.5, 8, 3, 2.5, STONE, lo=-0.05)
    return s.outline()


def bush():
    s = Sprite(18, 14, 9, 12)
    s.blob(5.5, 8, 4.5, 4, GREEN)
    s.blob(12.5, 8, 4.5, 4, GREEN)
    s.blob(9, 5.5, 5, 4.5, GREEN)
    for x, y in ((5, 7), (11, 5), (13, 9), (8, 9)):
        s.set(x, y, 'co'); s.set(x + 1, y, 'p2'); s.set(x, y - 1, 'cr')
    return s.outline()


def crate():
    s = Sprite(20, 18, 10, 16)
    for x, y in ((6, 4), (11, 3)):
        s.blob(x + 1.5, y + 2.5, 2.5, 2.5, ORANGE)
    s.blob(15, 5, 2.5, 2.2, ['g1', 'g2', 'g3'])
    s.blob(4.5, 5, 2.2, 2, ORANGE)
    s.rect(1, 6, 18, 7, 'e0')
    for y in range(8, 16):
        for x in range(1, 19):
            k = 'e2' if y in (8, 12) else ('e1' if y in (11, 15) else 'e3')
            if x in (1, 18): k = 'e1'
            s.set(x, y, k)
    for y in range(8, 16):
        s.set(5, y, 'e1'); s.set(14, y, 'e1')
    return s.outline()


def coin():
    s = Sprite(9, 9, 4, 8, off=(1, 1))
    s.blob(3.5, 3.5, 3.4, 3.4, GOLD)
    s.set(3, 2, 'e2'); s.set(3, 3, 'e2'); s.set(3, 4, 'e2')
    return s.outline()


def carrot_item():
    s = Sprite(10, 13, 5, 12)
    s.blob(5, 7.5, 2.6, 4.5, ORANGE)
    s.set(4, 10, 'co'); s.set(5, 11, 'co')
    s.blob(3.5, 2.5, 1.2, 2.5, GREEN, ang=-0.5)
    s.blob(6.5, 2.5, 1.2, 2.5, GREEN, ang=0.5)
    return s.outline()


# ---------- rośliny: canvas 18x24, podstawa (ziemia) w y=20 ----------
def _plant():
    return Sprite(22, 26, 11, 22, off=(2, 2))


def _mound(s):
    s.blob(9, 20, 3.5, 1.8, EARTH, clip_y=20)


def seed():
    s = _plant()
    _mound(s)
    s.set(9, 18, 'go'); s.set(9, 17, 'cr')
    return s.outline('e0')


def _leaf(s, cx, cy, rx, ry, ang):
    s.blob(cx, cy, rx, ry, GREEN, ang=ang)


def carrot(stage):
    s = _plant()
    if stage == 1:
        _leaf(s, 7.5, 16.5, 1.4, 3, -0.45); _leaf(s, 10.5, 16.5, 1.4, 3, 0.45)
        _mound(s)
    elif stage == 2:
        for cx, a in ((5.5, -0.8), (7.5, -0.3), (10.5, 0.3), (12.5, 0.8)):
            _leaf(s, cx, 13.5, 1.8, 4.5, a)
        _mound(s)
    else:
        for cx, cy, a in ((4.5, 11, -0.85), (6.5, 9, -0.35), (9, 8, 0.0), (11.5, 9, 0.35), (13.5, 11, 0.85)):
            _leaf(s, cx, cy, 2.1, 5.5, a)
        s.blob(9, 19, 4.3, 3.2, ORANGE, clip_y=20)
        s.set(7, 18, 'co'); s.set(10, 19, 'co')
    return s.outline()


def wheat(stage):
    s = _plant()
    xs = [4, 7, 10, 13]
    if stage == 1:
        for cx, h, a in ((6, 3, -0.35), (9, 4, 0.0), (12, 3, 0.35)):
            s.blob(cx, 19.5 - h, 1.2, h, GREEN, ang=a)
    elif stage == 2:
        for cx, cy, ry, a in ((5, 13, 6, -0.3), (8, 11, 7.5, -0.08), (11, 12, 7, 0.12), (14, 14, 5.5, 0.35)):
            s.blob(cx, cy, 1.4, ry, GREEN, ang=a)
    else:
        for i, (x, h) in enumerate(zip(xs, (14, 17, 15, 16))):
            s.line(x, 19, x, 19 - h + 5, 'e3'); s.line(x + 1, 19, x + 1, 19 - h + 6, 'e1')
        for i, (x, h) in enumerate(zip(xs, (14, 17, 15, 16))):
            s.blob(x + 0.5, 19 - h + 2.5, 1.6, 3.4, WHEAT, ang=0.15 if i % 2 else -0.15)
    s.outline()
    m = _plant(); _mound(m); m.outline('e0')
    for p, k in m.px.items():
        if p not in s.px or s.px[p] == 'o':
            s.px[p] = k
    return s


def pumpkin(stage):
    s = _plant()
    if stage == 1:
        s.line(9, 19, 9, 16, 'g1')
        s.blob(7, 15.5, 2.2, 1.6, GREEN, ang=-0.3); s.blob(11, 15.5, 2.2, 1.6, GREEN, ang=0.3)
        _mound(s)
    elif stage == 2:
        s.blob(5, 16, 3.4, 2.6, GREEN); s.blob(13, 15.5, 3.4, 2.6, GREEN); s.blob(9, 13, 3.4, 2.8, GREEN)
        s.blob(10, 18.5, 2.4, 1.9, ['g1', 'g2', 'g3'], clip_y=20)
        _mound(s)
    else:
        s.blob(3.5, 17, 3, 2.4, GREEN)
        s.blob(9, 15.5, 6.5, 4.8, ORANGE, clip_y=20)
        for rx in (6, 9, 12):
            for y in range(12, 20):
                if (rx, y) in s.px and s.px[(rx, y)] in ('or', 'go'):
                    s.set(rx, y, 'co')
        s.line(9, 10, 10, 8, 'e1'); s.set(10, 10, 'e2')
        s.blob(13, 9.5, 2.4, 1.6, GREEN, ang=0.3)
    return s.outline()


def plot():
    s = Sprite(18, 14, 0, 0)
    for y in range(14):
        for x in range(18):
            if (x in (0, 17) and y in (0, 13)):
                continue
            k = 'e1'
            if y % 4 == 1: k = 'e0'
            elif y % 4 == 2 and x % 4: k = 'e2'
            if y == 0: k = 'e0'
            s.set(x, y, k)
    return s


def sparkle(f):
    s = Sprite(5, 5, 2, 2)
    if f in (0, 3):
        s.set(2, 2, 'cr')
    elif f == 1:
        s.set(2, 2, 'cr'); [s.set(2 + dx, 2 + dy, 'go') for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
    else:
        s.set(2, 2, 'cr')
        for d in (1, 2):
            for dx, dy in ((d, 0), (-d, 0), (0, d), (0, -d)):
                s.set(2 + dx, 2 + dy, 'cr' if d == 1 else 'go')
    return s


FONT = {
    '0': ["111", "101", "101", "101", "111"], '1': ["110", "010", "010", "010", "111"],
    '2': ["111", "001", "111", "100", "111"], '3': ["111", "001", "011", "001", "111"],
    '4': ["101", "101", "111", "001", "001"], '5': ["111", "100", "111", "001", "111"],
    '6': ["111", "100", "111", "101", "111"], '7': ["111", "001", "010", "010", "010"],
    '8': ["111", "101", "111", "101", "111"], '9': ["111", "101", "111", "001", "111"],
}


def text(txt, k='cr'):
    s = Sprite(len(txt) * 4 + 2, 7, 0, 0)
    for i, ch in enumerate(txt):
        for y, row in enumerate(FONT[ch]):
            for x, v in enumerate(row):
                if v == '1':
                    s.set(1 + i * 4 + x, 1 + y, k)
    return s.outline()
