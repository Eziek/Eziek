"""Próbka 32 px w OBECNYM stylu gry (paleta mroczna, gruby kontur, ziemny klif) + nowa woda:
- głębia: dno z kamieniami i wodorostami jako osobna warstwa z paralaksą i załamaniem światła (falowanie wierszy),
- światełka pod wodą (świecące drobinki przy dnie, pulsują i dryfują),
- fale, które idą do brzegu, rosną i załamują się w pianę,
- kręgi na wodzie jak na filmiku, mgła chibi z ciemną obwódką, cała w oknie (bez ucięcia na krawędzi).

Uruchom z katalogu głównego repo:  python3 tools/art32/probka32.py
Wynik: docs/img/art32_woda.gif (dzień | noc, animacja), docs/img/art32_klatka.png (klatka x3), docs/img/art32_zoom.png
"""
import os, sys, math
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pixel-proto"))
import px
from px import C, Canvas, bayer, value_noise, dist_out, outline, shade_blob, hexc
from run_dark_palette import DARK

EXTRA = {
    'fl': "#ffe08a", 'fx': "#f08a3a", 'wi': "#5fcfb8", 'wl': "#d2fff0", 'mu': "#7a4f9a", 'ml': "#d49af0",
    # nowa woda: granat w stronę filmiku, ale w tonacji obecnej palety (b0-b3)
    'ws0': "#2a556d", 'ws1': "#214659", 'ws2': "#1a3a4f", 'ws3': "#142d42", 'ws4': "#0f2335", 'ws5': "#0b1a29",
    'wbed': "#0c1d2c", 'wbed2': "#10283a", 'wshadow': "#08121d",
    'wring': "#3f8ea3", 'wring2': "#86c9d4", 'wfoam': "#d6eef0", 'wcrest': "#2f6f82",
    'ul0': "#1f5a66", 'ul1': "#3fa59e", 'ul2': "#9ef0dc",
    'fog_out': "#232833", 'fog_dark': "#4d5765", 'fog_0': "#6b7584", 'fog_1': "#848e9b", 'fog_hi': "#a3abb5",
}
for k, v in list(DARK.items()) + list(EXTRA.items()):
    C[k] = hexc(v)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
T, CLIFF, WATER = 32, 22, 38
W, H = 400, 360
OX, OY = 120, 100
TILES = ({(c, r) for c in range(4) for r in range(3)} - {(3, 2), (0, 0)}) | {(4, 1), (1, 3)}
rng = np.random.default_rng(5)
ys, xs = np.mgrid[0:H, 0:W]
bay = bayer(H, W)


def dilate(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r + r:
                out |= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


def erode(m, r):
    return ~dilate(~m, r)


# ---------------------------------------------------------------- ląd jak w obecnym malarzu wyspy (tiles_proto), ale 32 px
land = np.zeros((H, W), bool)
for c, r in TILES:
    land[OY + r * T:OY + (r + 1) * T, OX + c * T:OX + (c + 1) * T] = True
land = dilate(erode(land, 10), 10)
land = erode(dilate(land, 8), 8)
n = value_noise(H, W, 12, 3)
land = (land | (dilate(land, 5) & ~land & (n > 0.64))) & ~(land & ~erode(land, 5) & (n < 0.3))
land = erode(dilate(land, 2), 2)
inside = dist_out(~land, 20)

cliff_top = np.full(W, -1)
cliff_h = np.zeros(W, int)
cliff = np.zeros_like(land)
hn = value_noise(1, W, 9, 9)[0]
for x in range(W):
    for y in range(1, H):
        if land[y - 1, x] and not land[y, x]:
            h = CLIFF + int(hn[x] * 5)
            cliff[y:y + h, x] = True
            cliff_top[x], cliff_h[x] = y, h
cliff &= ~land
solid = land | cliff
dshore = dist_out(solid, 100)
wob = value_noise(H, W, 22, 11)
water = (dshore <= WATER + (wob - 0.5) * 16) & ~solid

# ---------------------------------------------------------------- warstwa lądu (statyczna)
L = Canvas(W, H)
emis = np.zeros((H, W), bool)
# trawa: podstawa g2, ciemniejsze i jaśniejsze płaty, kępki, kwiatki, ścieżki ziemi
gn = value_noise(H, W, 10, 5)
gk = np.where(gn + (bay - 0.5) * 0.12 < 0.38, 'g1', np.where(gn + (bay - 0.5) * 0.12 > 0.66, 'g3', 'g2'))
for k in ('g1', 'g2', 'g3'):
    L.fill(land & (gk == k), k)
for _ in range(420):
    x, y = rng.integers(OX - 10, OX + 6 * T), rng.integers(OY - 10, OY + 5 * T)
    if land[y, x] and inside[y, x] > 3:
        tall = rng.random() < 0.4
        L.px(x, y, 'g3'); L.px(x - 1, y + 1, 'g3'); L.px(x + 1, y + 1, 'g3')
        L.px(x, y + 1, 'g1'); L.px(x - 1, y + 2, 'g1'); L.px(x + 1, y + 2, 'g1')
        if tall:
            L.px(x, y - 1, 'g3'); L.px(x + 2, y, 'g2')
for _ in range(40):
    x, y = rng.integers(OX, OX + 6 * T), rng.integers(OY, OY + 5 * T)
    if land[y, x] and inside[y, x] > 4:
        L.px(x, y, ['cr', 'go', 'ml'][rng.integers(0, 3)]); L.px(x, y + 1, 'g1')
L.fill(land & (inside == 1) & ~np.roll(cliff, -1, axis=0), 'g1')
L.fill(outline(land) & ~np.roll(cliff, -1, axis=0) & (inside == 0), 'o')

# klif: warstwy ziemi z pasami, kamienie, korzenie, ciemny dół (jak obecny klif, ale wyższy i z detalem)
strata = value_noise(H, W, 6, 41)
for x in range(W):
    if cliff_top[x] < 0:
        continue
    y0, h = cliff_top[x], cliff_h[x]
    for k in range(h):
        y = y0 + k
        f = k / h
        if k < 2:
            key = 'g1' if k == 0 else 'g0'
        elif k == 2:
            key = 'e3'
        elif f < 0.72:
            band = int((k + strata[y, x] * 3) / 5) % 2
            key = 'e2' if band == 0 else 'e1'
            if (k + int(strata[y, x] * 3)) % 5 == 0:
                key = 'e1' if band == 0 else 'e0'
        elif f < 0.88:
            key = 'p1'
        else:
            key = 'p0'
        L.px(x, y, key)
    L.px(x, y0 + h - 1, 'o')
# kamienie w ścianie klifu
for _ in range(36):
    x = int(rng.integers(OX - 6, OX + 6 * T + 6))
    if 0 <= x < W and cliff_top[x] >= 0:
        y = cliff_top[x] + int(rng.integers(5, cliff_h[x] - 6))
        shade_blob(L, x, y, rng.integers(2, 5), rng.integers(2, 3), ['p0', 'p1', 'b1', 'b2'], out='o', clip=cliff)
# korzenie i zwisająca trawa
for x in range(OX - 6, OX + 6 * T + 6):
    if not (0 <= x < W) or cliff_top[x] < 0:
        continue
    r = rng.random()
    if r < 0.12:
        y = cliff_top[x] + 3
        xx = x
        for k in range(int(rng.integers(4, 10))):
            L.px(xx, y + k, 'e0' if k % 3 else 'p1')
            if k % 3 == 2:
                xx += int(rng.choice([-1, 1]))
    elif r < 0.32:
        for k in range(int(rng.integers(1, 5))):
            L.px(x, cliff_top[x] + k, 'g2' if k == 0 else 'g1')
L.fill(outline(cliff) & ~np.roll(land, 1, axis=0) & ~(np.roll(cliff, -1, axis=0) & False), 'o')


def tile(c, r):
    return OX + c * T, OY + r * T


def soil(c0, c1, r):
    x0, y0 = tile(c0, r)
    x1 = OX + (c1 + 1) * T
    for y in range(y0 + 3, y0 + T - 2):
        for x in range(x0 + 3, x1 - 3):
            fy = (y - y0 - 3) % 8
            k = ['e3', 'e2', 'e2', 'e1', 'e1', 'e1', 'e0', 'e0'][fy]
            if (x * 7 + y * 3) % 11 == 0 and fy in (1, 2):
                k = 'e3'
            if x in (x0 + 3, x1 - 4) or y in (y0 + 3, y0 + T - 3):
                k = 'o' if (x in (x0 + 3, x1 - 4) and y in (y0 + 3, y0 + T - 3)) else 'e0'
            L.px(x, y, k)
    return x0, y0, x1


def pumpkin(x, y):
    """Dynia 2x (obecny styl, więcej detalu: żebra, ogonek, liść, wąs)."""
    shade_blob(L, x, y - 6, 9, 6.5, ['p2', 'co', 'or', 'go'], out='o')
    for dx in (-5, -1, 3):
        for dy in range(-10, -1):
            if 0 <= y + dy < H:
                L.px(x + dx + (1 if dy > -4 else 0), y + dy, 'p2' if dy > -8 else 'co')
    for k in range(4):
        L.px(x, y - 12 - k, 'e1'); L.px(x + 1, y - 12 - k, 'e2')
    L.px(x - 1, y - 16, 'o'); L.px(x + 2, y - 15, 'o')
    shade_blob(L, x + 6, y - 15, 4, 2.5, ['g0', 'g1', 'g2', 'g3'], out='o')
    for k in range(5):
        L.px(x - 3 - k, y - 13 - (k % 2), 'g2')


def carrot(x, y):
    for dx, dy, a in ((-3, -6, -0.6), (0, -8, 0), (3, -6, 0.6), (-1, -5, -0.2), (2, -5, 0.3)):
        shade_blob(L, x + dx, y + dy, 1.6, 4.5, ['g0', 'g1', 'g2', 'g3'], out='o')
    shade_blob(L, x, y - 1, 3.5, 2.2, ['p2', 'co', 'or'], out='o')
    L.px(x - 1, y - 2, 'go')


def round_tree(x, y):
    """Drzewo w obecnym stylu (okrągła korona), 2x i z detalem: kępy liści, prześwity, kora."""
    for k in range(26):
        w = 6 if k < 4 else 4
        for dx in range(w):
            kk = 'e3' if dx == 1 else ('e1' if dx >= w - 2 else 'e2')
            if k % 6 == 3 and dx == 2:
                kk = 'e1'
            L.px(x - w // 2 + dx, y - k, kk)
        L.px(x - w // 2 - 1, y - k, 'o'); L.px(x - w // 2 + w, y - k, 'o')
    for dx in (-5, -4, 4, 5):
        L.px(x + dx, y, 'e1'); L.px(x + dx, y + 1, 'o')
    shade_blob(L, x, y + 2, 13, 3, ['g0'], clip=land)
    blobs = [(-10, -30, 9, 8), (10, -31, 9, 8), (0, -38, 11, 9), (-6, -46, 9, 7), (7, -45, 9, 7), (0, -28, 10, 6), (1, -52, 7, 5)]
    for bx, by, rx, ry in blobs:
        shade_blob(L, x + bx, y + by, rx, ry, ['g0', 'g1', 'g2', 'g2', 'g3'], out='o')
    for _ in range(40):   # pojedyncze jaśniejsze listki na górnych krawędziach kęp
        bx, by, rx, ry = blobs[rng.integers(0, len(blobs))]
        a = rng.uniform(-2.6, -0.5)
        px_, py_ = int(x + bx + math.cos(a) * rx * 0.6), int(y + by + math.sin(a) * ry * 0.6)
        L.px(px_, py_, 'g3')
    for _ in range(6):
        bx, by, rx, ry = blobs[rng.integers(0, len(blobs))]
        L.px(int(x + bx + rng.integers(-3, 4)), int(y + by + rng.integers(1, 4)), 'g0')


def lantern(x, y):
    """Krzywa latarnia (obecny model, 2x): drewniany słup, żelazne ramię, klatka z płomieniem."""
    for k in range(44):
        b = int(max(0, k - 28) * 0.18)
        L.px(x + b - 1, y - k, 'e3'); L.px(x + b, y - k, 'e2'); L.px(x + b + 1, y - k, 'e1')
        L.px(x + b - 2, y - k, 'o'); L.px(x + b + 2, y - k, 'o')
        if k % 7 == 2:
            L.px(x + b, y - k, 'e1')
    shade_blob(L, x, y, 5, 2.5, ['p0', 'p1', 'b1'], out='o')
    top = y - 44
    for dx in range(-1, 12):
        L.px(x + 3 + dx, top + dx // 5, 'p1'); L.px(x + 3 + dx, top + dx // 5 - 1, 'o'); L.px(x + 3 + dx, top + dx // 5 + 1, 'o')
    lx, ly = x + 13, top + 4
    for dy in range(13):
        for dx in range(-5, 6):
            if dy == 0 and abs(dx) > 2:
                continue
            k = 'go'
            if abs(dx) == 5 or dy in (1, 12):
                k = 'o'
            elif abs(dx) <= 1 and 4 <= dy <= 8:
                k = 'cr'
            elif abs(dx) <= 2 and 3 <= dy <= 9:
                k = 'fl'
            elif abs(dx) == 4 or dy in (2, 11):
                k = 'p1'
            if dy == 0:
                k = 'p1'
            L.px(lx + dx, ly + dy, k)
            if k in ('go', 'cr', 'fl'):
                emis[ly + dy, lx + dx] = True
    return lx, ly + 6


def gravestone(x, y):
    shade_blob(L, x, y - 9, 6, 9, ['p1', 'b1', 'b2', 'b3'], out='o')
    for dy in range(-9, 0):
        for dx in range(-6, 7):
            if L.a[y + dy, x + dx] == 0 or abs(dx) == 6:
                continue
    L.fill((xs >= x - 6) & (xs <= x + 6) & (ys >= y - 9) & (ys <= y), 'b1')
    L.fill((xs == x - 6) & (ys >= y - 9) & (ys <= y), 'b2')
    L.fill(((xs == x - 7) | (xs == x + 7)) & (ys >= y - 9) & (ys <= y), 'o')
    L.fill((xs >= x - 7) & (xs <= x + 7) & (ys == y + 1), 'o')
    for dx, dy in ((0, -13), (-1, -11), (1, -11), (0, -10), (0, -9), (0, -8)):
        L.px(x + dx, y + dy, 'p1')   # wyryty krzyż
    for dx in range(-6, 7):
        if rng.random() < 0.6:
            L.px(x + dx, y, 'g1'); L.px(x + dx, y - 1, 'g2' if rng.random() < 0.4 else 'b1')
    shade_blob(L, x, y + 2, 9, 2, ['g0'], clip=land)


def bush(x, y):
    for bx, by, rx, ry in ((-5, -4, 6, 5), (5, -4, 6, 5), (0, -8, 7, 6)):
        shade_blob(L, x + bx, y + by, rx, ry, ['g0', 'g1', 'g2', 'g3'], out='o')
    for dx, dy in ((-4, -8), (3, -11), (6, -5)):
        L.px(x + dx, y + dy, 'co')   # jagody
    shade_blob(L, x, y + 1, 10, 2, ['g0'], clip=land)


def rock(x, y):
    shade_blob(L, x, y + 2, 8, 2, ['g0'], clip=land)
    shade_blob(L, x, y - 3, 7, 5, ['p1', 'b1', 'b2', 'b3', 'fo'], out='o')


x0, y0, x1 = soil(1, 2, 1)
for i, cx in enumerate(range(x0 + 10, x1 - 6, 14)):
    pumpkin(cx, y0 + 14) if i % 2 == 0 else carrot(cx, y0 + 14)
    carrot(cx + 5, y0 + 29) if i % 2 == 0 else pumpkin(cx + 5, y0 + 29)
lamp = lantern(*np.add(tile(3, 0), (6, 28)))
round_tree(*np.add(tile(0, 1), (14, 28)))
gravestone(*np.add(tile(1, 3), (14, 20)))
bush(*np.add(tile(4, 1), (14, 22)))
rock(*np.add(tile(3, 1), (16, 24)))

# ---------------------------------------------------------------- mgła chibi (statyczna), ciemniejsza obwódka, cała w oknie
F = Canvas(W, H)
dout = dist_out(water | solid, 60)
coast = water & (dshore >= WATER - 10)
base_f = ((dout > 0) & (dout <= 7)) | (coast & (dshore >= WATER - 3))
F.fill(base_f, 'fog_0')
cand = np.argwhere(~solid & ((dout > 0) | coast))
for dmin, dmax, rmin, rmax in ((5, 10, 7, 11), (0, 6, 6, 9), (-3, 1, 4, 7)):
    rng.shuffle(cand)
    placed = []
    for y, x in cand[:6000]:
        dd = dout[y, x] if dout[y, x] > 0 else -(WATER - dshore[y, x])
        if not (dmin <= dd <= dmax):
            continue
        r = int(rng.integers(rmin, rmax))
        if any((x - a) ** 2 + (y - b) ** 2 < (0.8 * (r + c)) ** 2 for a, b, c in placed):
            continue
        placed.append((x, y, r))
    for x, y, r in sorted(placed, key=lambda p: p[1]):
        shade_blob(F, x, y, r, r * 0.8, ['fog_dark', 'fog_0', 'fog_0', 'fog_1', 'fog_hi'], out='fog_dark', clip=~solid)
fogm = F.a > 0
F.fill(outline(fogm), 'fog_out')

# ---------------------------------------------------------------- woda: warstwy liczone na klatkę
seabed_n = value_noise(H + 40, W + 40, 8, 61)
weed_n = value_noise(H + 40, W + 40, 3, 62)
rocks_bed = seabed_n > 0.6
weeds = (weed_n > 0.72) & ~rocks_bed
depth_n = value_noise(H, W, 10, 21)
depth = dshore + (depth_n - 0.5) * 8
bands = ['ws0', 'ws1', 'ws2', 'ws3', 'ws4', 'ws5']
bidx = np.zeros((H, W), int)
for e in (4, 9, 15, 22, 30):
    bidx += (depth + (bay - 0.5) * 3 > e).astype(int)
shadow = np.zeros_like(water)
for k in range(1, 7):
    shadow |= np.roll(cliff, k, axis=0)
shadow &= water
shore_n = value_noise(H, W, 14, 71)          # przesunięcie fazy fal wzdłuż brzegu
foam_n = value_noise(H, W, 3, 44)
value_gaps = value_noise(H, W, 9, 45)
lights = []
while len(lights) < 16:
    y, x = rng.integers(0, H), rng.integers(0, W)
    if water[y, x] and 9 <= dshore[y, x] <= 30:
        lights.append((x, y, rng.random() * 6.28, rng.uniform(0.6, 1.4)))
rings = [(70, 230, 0.0), (330, 140, 0.35), (285, 305, 0.7), (180, 64, 0.5)]
FR = 32


def water_frame(f):
    t = f / FR                                   # 0..1 pętla
    rgb = np.zeros((H, W, 3), np.float32)
    for i, k in enumerate(bands):
        rgb[bidx == i] = C[k]
    # dno z paralaksą: przesuwa się wolniej niż powierzchnia, a fale załamują obraz (wiersze falują o 1 px)
    par = int(round(math.sin(2 * math.pi * t) * 2))
    refr = np.round(np.sin(ys * 0.35 + 2 * math.pi * t * 2) * (dshore < 26)).astype(int)
    sx = np.clip(xs + 20 + par + refr, 0, W + 39)
    sy = np.clip(ys + 20, 0, H + 39)
    rb, wd = rocks_bed[sy, sx], weeds[sy, sx]
    vis = water & (dshore > 5) & ~shadow
    darker = {'ws0': 'ws2', 'ws1': 'ws3', 'ws2': 'ws4', 'ws3': 'ws5', 'ws4': 'ws5', 'ws5': 'wbed'}
    for i, k in enumerate(bands):
        sel = vis & rb & (bidx == i)
        rgb[sel] = C[darker[k]]
        sel = vis & wd & (bidx == i) & (bay < 0.5)
        rgb[sel] = C[darker[k]]
    # górna krawędź kamieni na dnie łapie światło (tylko płycej)
    rim = vis & rb & ~np.roll(rb, 1, axis=0) & (dshore < 22)
    rgb[rim] = C['ws1']
    # światełka pod wodą: pulsują i dryfują razem z dnem (paralaksa)
    for lx0, ly0, ph, sp in lights:
        b = 0.5 + 0.5 * math.sin(2 * math.pi * t * sp * 2 + ph)
        lx = int(round(lx0 + par + math.sin(2 * math.pi * t + ph) * 1.5))
        ly = int(round(ly0 + math.cos(2 * math.pi * t + ph) * 1.0))
        r = 1.5 + 2.0 * b
        m = water & ((xs - lx) ** 2 + (ys - ly) ** 2 <= r * r)
        rgb[m & (bay < 0.35 + 0.4 * b)] = C['ul0']
        m2 = water & ((xs - lx) ** 2 + (ys - ly) ** 2 <= (r * 0.45) ** 2)
        rgb[m2] = C['ul1']
        if b > 0.7 and 0 <= ly < H and 0 <= lx < W and water[ly, lx]:
            rgb[ly, lx] = C['ul2']
    rgb[shadow] = C['wshadow']
    # fale idące do brzegu: grzbiet zbliża się (d maleje), przy brzegu gęstnieje i rozbija się w pianę
    for wave in range(2):
        ph = (t + wave * 0.5 + shore_n * 0.35) % 1.0
        dc = 15 * (1 - ph)                         # pozycja grzbietu (odległość od brzegu)
        wdt = 0.6 + 1.4 * np.clip((8 - dc) / 8, 0, 1)
        crest = water & ~shadow & (np.abs(dshore - dc) < wdt) & (dshore >= 2)
        brk = value_gaps > 0.42                    # grzbiet przerywany: fala nie jest równym konturem
        far = crest & (dc > 8) & brk & (foam_n > 0.5)
        mid = crest & (dc <= 8) & (dc > 4) & brk
        near = crest & (dc <= 4) & (foam_n > 0.2)
        rgb[far] = C['wcrest']
        rgb[mid & (foam_n > 0.4)] = C['wring']
        rgb[mid & (foam_n > 0.75)] = C['wring2']
        rgb[near] = C['wfoam']
        # rozbryzg przed grzbietem tuż przy brzegu
        spray = water & (dc < 4) & (dshore < dc) & (dshore >= 1) & (foam_n > 0.5 + dc * 0.1) & ((xs + ys + f) % 3 == 0)
        rgb[spray] = C['wfoam']
        # zostawiona piana cofa się i znika (tuż po rozbiciu: faza bliska 0 po zawinięciu)
        back = water & (ph < 0.25) & (np.abs(dshore - (1 + ph * 12)) < 0.6) & (foam_n > 0.55 + ph) & ~shadow
        rgb[back] = C['wring2']
    # stała, przerywana linia piany przy brzegu
    rgb[water & (dshore == 1) & (foam_n > 0.25)] = C['wfoam']
    rgb[water & (dshore == 2) & (foam_n > 0.62)] = C['wring2']
    # kręgi na wodzie (rosną i bledną)
    for cx, cy, off in rings:
        for k in range(2):
            u = (t + off + k * 0.5) % 1.0
            rx = 3 + u * 16
            segs = np.random.default_rng(cx + k).random(10) > (0.2 + 0.6 * u)
            for a in np.linspace(0, 2 * np.pi, int(rx * 9)):
                if not segs[int(a / (2 * np.pi) * 10) % 10]:
                    continue
                x, y = int(round(cx + math.cos(a) * rx)), int(round(cy + math.sin(a) * rx * 0.48))
                if 0 <= x < W and 0 <= y < H and water[y, x] and not shadow[y, x]:
                    rgb[y, x] = C['wring2'] if (math.sin(a) < -0.5 and u < 0.6) else C['wring']
    return rgb


# ---------------------------------------------------------------- noc
def night_grade(rgb, is_fog, is_water):
    out = rgb * np.array([0.42, 0.47, 0.7]) + np.array([4, 6, 16])
    out[is_fog] = rgb[is_fog] * np.array([0.5, 0.53, 0.68])
    lx, ly = lamp
    d = np.sqrt((xs - lx) ** 2 + ((ys - ly) * 1.2) ** 2)
    light = np.clip(1 - d / 72, 0, 1) ** 0.8
    st = (np.floor(light * 5 + (bay - 0.5) * 0.8) / 5 * 0.92)[..., None]
    out = out * (1 - st) + rgb * np.array([1.06, 0.9, 0.62]) * st + st * np.array([62, 40, 12])   # addytywnie, jak Light2D
    out[emis] = rgb[emis]
    return out


yy, xx = np.where(fogm | water | solid)
cy0, cy1, cx0, cx1 = yy.min() - 6, yy.max() + 7, xx.min() - 6, xx.max() + 7
assert cy0 > 0 and cx0 > 0 and cy1 < H and cx1 < W, "mgła dotyka krawędzi płótna"
alpha = (fogm | water | solid).astype(np.float32)
chk = ((xs // 4 + ys // 4) % 2)[..., None]
bg = np.where(chk, np.array([52, 54, 60]), np.array([62, 64, 70]))
ul_keys = [C['ul0'], C['ul1'], C['ul2']]


def compose(f, night):
    w = water_frame(f)
    rgb = np.zeros((H, W, 3), np.float32)
    rgb[water] = w[water]
    rgb[L.a > 0] = L.rgb[L.a > 0]
    rgb[fogm] = F.rgb[fogm]
    if night:
        glow_ul = np.zeros((H, W), bool)
        for k in ul_keys:
            glow_ul |= (w == k).all(-1) & water & ~fogm
        g = night_grade(rgb, fogm, water)
        g[glow_ul] = w[glow_ul] * 1.15             # światełka pod wodą świecą w nocy
        rgb = g
    out = rgb * alpha[..., None] + bg * (1 - alpha[..., None])
    return out[cy0:cy1, cx0:cx1]


def to_im(a, s):
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGB')
    return im.resize((im.width * s, im.height * s), Image.NEAREST)


frames = []
for f in range(FR):
    d, nt = to_im(compose(f, False), 2), to_im(compose(f, True), 2)
    sheet = Image.new('RGB', (d.width * 2 + 16, d.height), (18, 18, 22))
    sheet.paste(d, (0, 0)); sheet.paste(nt, (d.width + 16, 0))
    frames.append(sheet.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE))
os.makedirs(os.path.join(ROOT, 'docs', 'img'), exist_ok=True)
frames[0].save(os.path.join(ROOT, 'docs', 'img', 'art32_woda.gif'), save_all=True, append_images=frames[1:], duration=110, loop=0)
still = to_im(compose(8, False), 3)
still.save(os.path.join(ROOT, 'docs', 'img', 'art32_klatka.png'))
zx, zy = OX + T - 30 - cx0, OY + 3 * T - 6 - cy0
to_im(compose(8, False)[zy:zy + 80, zx:zx + 130], 5).save(os.path.join(ROOT, 'docs', 'img', 'art32_zoom.png'))
print('ok', frames[0].size, 'okno', cx1 - cx0, 'x', cy1 - cy0)
