"""Przerysowanie obecnych sprite'ów 16 px na 32 px (wybór gracza: „jak obecne 16 px, tylko większe”, bez „klusek”,
więcej detalu, lepsze kolory).

Kroki dla każdego sprite'a (na kluczach palety, nie na RGB):
1. Scale2x (EPX) - powiększenie, które zachowuje pikselowe kształty i wygładza skosy zamiast robić schodki 2x2.
2. Cienki kontur: zewnętrzny obrys zostaje 1 px, wewnętrzna połowa podwójnej linii dostaje najciemniejszy kolor sąsiada
   (selout), linie wewnętrzne (żebra dyni, granice liści) zostają 1 px.
3. Bryła: piksele przy górno-lewej krawędzi o stopień jaśniejsze, przy dolno-prawej o stopień ciemniejsze.
4. Przejścia: na granicy dwóch odcieni tej samej rampy kolor pośredni (więcej odcieni niż w 16 px).
5. Faktura: kreski na liściach, słoje na drewnie, plamki na kamieniu, odblaski na owocach.
"""
import numpy as np

# rampy obecnej palety (ciemny → jasny); dodatkowe jaśniejsze szczyty dla kontrastu z trawą (uwaga gracza o kolorach)
RAMPS = [
    ['gs', 'g0', 'g1', 'g2', 'g3', 'g4'],
    ['e0', 'e1', 'e2', 'e3', 'e4'],
    ['p2', 'co', 'or', 'go', 'cr'],
    ['p0', 'p1', 'p2'],
    ['b0', 'b1', 'b2', 'b3', 'fo', 'b4'],
    ['p1', 'mu', 'ml'],
    ['wi', 'wl'],
    ['fx', 'fl'],
]
# paleta dark fantasy dla roślin i obiektów (uwaga gracza: kolory bardziej mroczne): chłodniej, mniej nasycenia,
# zieleń w stronę oliwki i szarości, pomarańcz w rdzę, jasne szczyty przygaszone
DARK_FANTASY = {
    'o': '#0b080c', 'gs': '#121a1b',
    'g0': '#121a19', 'g1': '#1c2a25', 'g2': '#2a3a30', 'g3': '#3e4d3a', 'g4': '#536147',
    'e0': '#1a1215', 'e1': '#2b1e21', 'e2': '#432f2b', 'e3': '#5c4234', 'e4': '#735540',
    'p0': '#17111a', 'p1': '#2a1f2e', 'p2': '#46263a',
    'co': '#6e3234', 'or': '#8a4630', 'go': '#a06d3f', 'cr': '#b8a684',
    'b0': '#0f151d', 'b1': '#1a2633', 'b2': '#29394a', 'b3': '#465564', 'fo': '#68727e', 'b4': '#868e98',
    'mu': '#4f3364', 'ml': '#8f6aa8', 'wi': '#4fae9c', 'wl': '#b4e3d6', 'fx': '#c8702f', 'fl': '#e8c071',
}
EXTRA_COLORS = {'g4': '#7d955e', 'e4': '#8d6c55', 'b4': '#b6bec7', 'gs': '#1b2a2c'}


def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def build_steps(rgb):
    """Dla każdego klucza: (jaśniejszy, ciemniejszy) w jego rampie."""
    up, down = {}, {}
    for r in RAMPS:
        r = [k for k in r if k in rgb]
        for i, k in enumerate(r):
            up.setdefault(k, r[i + 1] if i + 1 < len(r) else k)
            down.setdefault(k, r[i - 1] if i > 0 else k)
    return up, down


def grid_of(spr):
    g = [[None] * spr.w for _ in range(spr.h)]
    for (x, y), k in spr.px.items():
        if 0 <= x < spr.w and 0 <= y < spr.h:
            g[y][x] = k
    return g


def scale2x(g):
    h, w = len(g), len(g[0])
    out = [[None] * (w * 2) for _ in range(h * 2)]
    get = lambda x, y: g[y][x] if 0 <= x < w and 0 <= y < h else None
    for y in range(h):
        for x in range(w):
            p = g[y][x]
            a, b, c, d = get(x, y - 1), get(x + 1, y), get(x - 1, y), get(x, y + 1)
            e0 = a if (c == a and c != d and a != b) else p
            e1 = b if (a == b and a != c and b != d) else p
            e2 = c if (d == c and d != b and c != a) else p
            e3 = d if (b == d and b != a and d != c) else p
            out[2 * y][2 * x], out[2 * y][2 * x + 1], out[2 * y + 1][2 * x], out[2 * y + 1][2 * x + 1] = e0, e1, e2, e3
    return out


def lum(rgb, k):
    r, g, b = rgb[k]
    return 0.3 * r + 0.59 * g + 0.11 * b


def hd2x(spr, rgb, seed=1, texture=True, crisp=False):
    """Zwraca siatkę kluczy 2x z detalem (lista wierszy, None = przezroczysty).
    crisp=True (uwaga gracza: „bardziej pixel art niż anime”): bez ditheringu przejść, bez losowych plamek i odblasków,
    czarny kontur bez selout, liście jako równe łuski - czyste plamy koloru."""
    rnd = np.random.default_rng(seed)
    up, down = build_steps(rgb)
    g = scale2x(grid_of(spr))
    h, w = len(g), len(g[0])
    get = lambda x, y, gg=None: (gg or g)[y][x] if 0 <= x < w and 0 <= y < h else None
    OUT = ('o', 'e0') if False else ('o',)

    # 2. cienki kontur (selout)
    g2 = [row[:] for row in g]
    for y in range(h):
        for x in range(w):
            if g[y][x] not in OUT:
                continue
            nb = [get(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if any(n is None for n in nb):
                continue                      # zewnętrzny obrys zostaje
            left, upn = get(x - 1, y), get(x, y - 1)
            inner = [n for n in nb if n not in OUT and n is not None]
            if left in OUT or upn in OUT:     # druga połowa podwójnej linii
                if inner:
                    g2[y][x] = min(inner, key=lambda k: lum(rgb, k))
                    g2[y][x] = down.get(g2[y][x], g2[y][x])
    g = g2

    # 3. bryła: światło z lewej-góry
    g3 = [row[:] for row in g]
    for y in range(h):
        for x in range(w):
            k = g[y][x]
            if k is None or k in OUT:
                continue
            ul = [get(x - 1, y), get(x, y - 1)]
            dr = [get(x + 1, y), get(x, y + 1)]
            if crisp:
                if (get(x, y - 1) is None or get(x, y - 1) in OUT) and get(x, y + 1) not in (None,) + OUT:
                    g3[y][x] = up.get(k, k)
                elif (get(x, y + 1) is None or get(x, y + 1) in OUT) and get(x, y - 1) not in (None,) + OUT:
                    g3[y][x] = down.get(k, k)
                continue
            if any(n is None or n in OUT for n in ul) and not any(n is None or n in OUT for n in dr):
                g3[y][x] = up.get(k, k)
            elif any(n is None or n in OUT for n in dr) and not any(n is None or n in OUT for n in ul):
                g3[y][x] = down.get(k, k)
    g = g3

    # 4. przejścia: granica jasny/ciemny tej samej rampy → co drugi piksel pośredni (dithering 1 px)
    g4 = [row[:] for row in g]
    for y in range(h):
        for x in range(w):
            k = g[y][x]
            if k is None or k in OUT:
                continue
            for dx, dy in ((1, 0), (0, 1)):
                n = get(x + dx, y + dy)
                if not crisp and n and n != k and up.get(n) == k and (x + y) % 2 == 0:
                    g4[y][x] = n
                    break
    g = g4

    # 5a. liście w koronach (duże zielone plamy): łuski - jasna górna krawędź, ciemny dół, w przesuniętej siatce
    greens_all = {'g0', 'g1', 'g2', 'g3'}
    n_green = sum(1 for row in g for k in row if k in greens_all)
    if texture and n_green > 140:
        for y in range(h):
            for x in range(w):
                k = g[y][x]
                if k not in ('g1', 'g2', 'g3'):
                    continue
                off = 2 if (y // 3) % 2 else 0
                u, v = (x + off) % 5, y % 3
                if v == 0 and u in (1, 2, 3) and (crisp or rnd.random() < 0.85):
                    g[y][x] = up[k]
                elif v == 2 and u in (0, 4):
                    g[y][x] = down[k]

    # 5. faktura
    if texture and not crisp:
        greens = {'g1', 'g2', 'g3'}
        woods = {'e1', 'e2', 'e3'}
        stones = {'b1', 'b2', 'b3', 'p1'}
        fruit = {'or', 'go', 'co', 'ml', 'wl'}
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                k = g[y][x]
                if k in greens and rnd.random() < 0.12 and g[y][x + 1] == k:
                    g[y][x] = up[k]                          # kreska na liściu (2 px poziomo)
                    if rnd.random() < 0.6:
                        g[y][x + 1] = up[k]
                elif k in woods and (y * 3 + x) % 7 == 0 and g[y + 1][x] == k:
                    g[y][x] = down[k]                        # słój drewna
                elif k in stones and rnd.random() < 0.08:
                    g[y][x] = down[k] if rnd.random() < 0.6 else up[k]
        # odblask na owocach/płatkach: jeden jasny piksel w górnej-lewej części każdej plamy
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                k = g[y][x]
                if k in fruit and g[y - 1][x] in ('o', None) and g[y][x - 1] in ('o', None) and up.get(k, k) != k:
                    g[y + 1][x + 1] = up[up[k]] if up.get(up[k]) else up[k]
    # 6. kontur zależny od światła: od lewej-góry ciemny kolor rampy zamiast czerni (selout), od prawej-dołu czerń
    if crisp:
        return g
    g6 = [row[:] for row in g]
    for y in range(h):
        for x in range(w):
            if g[y][x] not in OUT:
                continue
            below, right = get(x, y + 1), get(x + 1, y)
            inner = [n for n in (below, right) if n is not None and n not in OUT]
            if inner and get(x, y - 1) is None and get(x - 1, y) is None or (inner and (get(x, y - 1) is None) and below not in (None,) + OUT):
                darkest = inner[0]
                while down.get(darkest, darkest) != darkest:
                    darkest = down[darkest]
                g6[y][x] = darkest if darkest not in ('g0',) else 'gs'
    return g6


def to_image(g, rgb, scale=1):
    from PIL import Image
    h, w = len(g), len(g[0])
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            if g[y][x] is not None:
                im.putpixel((x, y), rgb[g[y][x]] + (255,))
    return im.resize((w * scale, h * scale), Image.NEAREST) if scale > 1 else im
