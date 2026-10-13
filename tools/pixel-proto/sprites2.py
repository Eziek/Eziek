"""Wersja 2x detal: rośliny ~32-40 px, drzewo ~64 px. Wewnętrzne linie konturu, ości w zbożu, pierzaste liście marchwi."""
import math
from sprites import Sprite, RGB, PAL, GREEN, EARTH, ORANGE, WHEAT, LIGHT


def plant2():
    return Sprite(40, 48, 20, 44, off=(0, 0))


def mound2(s, cx=20, base=44):
    m = Sprite(s.w, s.h)
    m.blob(cx, base - 1, 6.5, 3.2, EARTH, clip_y=base - 1)
    m.outline('e0')
    for p, k in m.px.items():
        if p not in s.px or s.px[p] == 'o':
            s.px[p] = k


def curve(x0, y0, x1, y1, bend, n=24):
    """punkty na łuku (kwadratowa krzywa Beziera)"""
    mx, my = (x0 + x1) / 2 + bend, (y0 + y1) / 2
    pts = []
    for i in range(n + 1):
        t = i / n
        x = (1 - t) ** 2 * x0 + 2 * (1 - t) * t * mx + t * t * x1
        y = (1 - t) ** 2 * y0 + 2 * (1 - t) * t * my + t * t * y1
        pts.append((round(x), round(y)))
    return pts


def blade(s, x0, y0, x1, y1, bend, w0=2, light='g3', mid='g2', dark='g1'):
    pts = curve(x0, y0, x1, y1, bend)
    for i, (x, y) in enumerate(pts):
        w = w0 if i < len(pts) * 0.55 else 1
        s.set(x, y, mid)
        if w > 1:
            s.set(x + 1, y, dark)
        if i % 5 == 2:
            s.set(x, y, light)


def wheat2(stage):
    s = plant2()
    if stage == 1:
        for x0, x1, h, b in ((17, 14, 9, -1), (20, 20, 12, 0), (23, 26, 9, 1), (19, 16, 7, -2)):
            blade(s, x0, 42, x1, 42 - h, b, w0=1)
        s.outline()
    elif stage == 2:
        # wysokie, wąskie liście + zielone, niedojrzałe kłosy (pionowa sylwetka)
        for x0, x1, h, b in ((15, 8, 14, -3), (25, 32, 13, 3), (18, 12, 19, -2), (23, 29, 18, 2)):
            blade(s, x0, 42, x1, 42 - h, b)
        heads = []
        for x, h in ((16, 30), (20, 34), (24, 31)):
            for i in range(4):
                s.blob(x + (0.2 if i % 2 else 1.4), 42 - h + 1.5 + i * 1.7, 1.3, 1.5, ['g1', 'g2', 'g3'])
            heads.append((x, h))
        s.outline()
        for x, h in heads:   # łodygi bez konturu - lżejsze
            s.line(x, 41, x, 42 - h + 8, 'g3'); s.line(x + 1, 41, x + 1, 42 - h + 8, 'g1')
    else:
        ears = []
        for x, h, lean in ((13, 31, -0.25), (18, 36, -0.08), (23, 34, 0.1), (28, 30, 0.28)):
            top_x = x + lean * 10
            s.line(x, 42, round(top_x), 42 - h + 9, 'go')
            s.line(x + 1, 42, round(top_x) + 1, 42 - h + 10, 'e2')
            ears.append((top_x, 42 - h, lean))
        for x0, x1, h, b in ((15, 9, 12, -2), (26, 32, 11, 2)):
            blade(s, x0, 42, x1, 42 - h, b, light='go', mid='e3', dark='e2')
        for tx, ty, lean in ears:
            for i in range(5):
                gx = tx + (-0.7 if i % 2 else 1.0) + lean * (5 - i)
                s.blob(gx + 0.5, ty + 1.5 + i * 1.8, 1.5, 1.7, WHEAT)
        s.outline()
        for tx, ty, lean in ears:   # ości (bez konturu) - charakterystyczna sylwetka zboża
            for dx, ln in ((-2, 5), (0, 6), (2, 5)):
                s.line(round(tx + lean * 5) + 1, round(ty), round(tx + lean * 5 + dx * 1.6) + 1, round(ty - ln), 'cr')
    mound2(s)
    return s


def frond(s, x0, y0, x1, y1, bend, leaflets=4, size=1.6):
    pts = curve(x0, y0, x1, y1, bend)
    for x, y in pts:
        s.set(x, y, 'g1')
    for i in range(leaflets):
        t = 0.35 + 0.65 * i / max(1, leaflets - 1)
        x, y = pts[int(t * (len(pts) - 1))]
        side = 1 if i % 2 else -1
        ang = math.atan2(y1 - y0, x1 - x0) + math.pi / 2
        s.blob(x + side * 1.8 * math.cos(ang - math.pi / 2 + side * 0.9) * 0.6 + side * 1.2, y - 0.5,
               size * 0.75, size * 1.25, GREEN, ang=side * 0.6)
    s.blob(x1 + 0.5, y1 + 0.5, size * 0.85, size * 1.35, GREEN, ang=(x1 - x0) * 0.05)


def carrot2(stage):
    s = plant2()
    if stage == 1:
        s.line(20, 42, 20, 38, 'g1')
        s.blob(17, 36.5, 2.0, 3.0, GREEN, ang=-0.7); s.blob(23.5, 36.5, 2.0, 3.0, GREEN, ang=0.7)
        s.outline()
    elif stage == 2:
        # pierzaste liście rozchodzące się wachlarzem (szeroka sylwetka)
        for x1, y1, b in ((9, 30, -2), (14, 25, -1), (20, 23, 0), (26, 25, 1), (31, 30, 2)):
            frond(s, 20, 42, x1, y1, b, leaflets=3, size=1.7)
        s.outline()
    else:
        for x1, y1, b in ((5, 26, -3), (10, 18, -2), (16, 14, -1), (24, 14, 1), (30, 18, 2), (35, 26, 3)):
            frond(s, 20, 38, x1, y1, b, leaflets=5, size=2.0)
        s.blob(20, 41.5, 7, 5.2, ORANGE, clip_y=44)
        for y, x0, x1 in ((39, 16, 19), (41, 22, 25), (43, 17, 21)):
            for x in range(x0, x1): s.set(x, y, 'co')
        s.set(16, 38, 'cr'); s.set(17, 37, 'go')
        s.outline()
    mound2(s)
    return s


def tree2():
    s = Sprite(72, 84, 36, 80)
    # pień z korą i korzeniami
    for y in range(48, 78):
        for x in range(31, 41):
            k = ['e0', 'e3', 'e3', 'e2', 'e2', 'e2', 'e1', 'e1', 'e1', 'e0'][x - 31]
            if (x * 7 + y * 3) % 11 == 0 and 32 < x < 40: k = 'e1' if k != 'e1' else 'e0'
            s.set(x, y, k)
    for x0, dx in ((31, -1), (40, 1)):
        for i in range(5):
            for j in range(i + 1):
                s.set(x0 + dx * (j + 1), 77 - (4 - i), 'e1' if dx > 0 else 'e2')
    # korona z klastrów; dolno-prawa krawędź każdego klastra = wewnętrzna linia g0
    clusters = [(20, 40, 12, 10), (52, 40, 12, 10), (36, 46, 14, 9), (24, 26, 12, 11), (48, 26, 12, 11),
                (36, 18, 14, 12), (12, 34, 7, 7), (60, 34, 7, 7), (36, 34, 11, 9)]
    for cx, cy, rx, ry in clusters:
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                u, v = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                d = u * u + v * v
                if d > 1:
                    continue
                z = math.sqrt(1 - d)
                l = u * LIGHT[0] + v * LIGHT[1] + z * LIGHT[2]
                t = (l + 0.15) / 1.15
                k = GREEN[max(0, min(3, int(t * 4)))]
                if d > 0.78 and (u + v) > 0.3 and (x - s.offx, y - s.offy) in s.px:
                    k = 'g0'
                s.set(x, y, k)
    # postrzępiona krawędź liści: małe kulki na faktycznej krawędzi korony (bez pnia)
    canopy = {p for p, k in s.px.items() if k[0] == 'g'}
    edge = sorted(p for p in canopy if any((p[0] + dx, p[1] + dy) not in canopy
                                            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))))
    edge = sorted(edge, key=lambda p: math.atan2(p[1] - 32, p[0] - 36))
    for i, (x, y) in enumerate(edge[::9]):
        if y < 54:
            s.blob(x + 0.5, y + 0.5, 2.4, 2.2, GREEN)
    for x, y in ((26, 14), (27, 13), (22, 20), (34, 9), (35, 8), (44, 16), (16, 30), (30, 24)):
        s.set(x, y, 'g3')
    for x, y in ((24, 15), (33, 10), (43, 18), (15, 31)):
        s.set(x, y, 'cr')
    return s.outline()


def plot2():
    s = Sprite(36, 26, 0, 0)
    for y in range(26):
        for x in range(36):
            if (x in (0, 35) and y in (0, 25)) or (x in (0, 35) and y in (1, 24)) or (x in (1, 34) and y in (0, 25)):
                continue
            k = 'e1'
            if y % 6 in (2, 3): k = 'e0' if y % 6 == 3 else 'e1'
            if y % 6 == 4 and (x * 5) % 7: k = 'e2'
            if y % 6 == 5 and x % 9 == 4: k = 'e3'
            if y <= 1: k = 'e0'
            s.set(x, y, k)
    return s
