"""Monumenty wysp fabularnych (GDD §13.11): jeden obiekt na 3x2 kafelkach (52x64 px, zaczepienie u dołu w środku).

Cztery historie: Kaplica Dzwonu, Strażnica Łańcucha, Tartak rozbitków, Gwiezdne obserwatorium. Każdy monument ma 5 faz:
0 = ruina (niski, poszarpany kikut, gruz, mech), 1-3 = odbudowa (mur rośnie od dołu, rusztowanie, stos materiałów),
4 = gotowy (świecące okna w *_glow). Fazy 0-3 liczone z gotowego rysunku, więc każdy monument rośnie „sam z siebie”.
Podgląd: python3 monuments.py -> docs/img/monumenty.png
"""
import math, os, random
from sprites import Sprite
from sprites3 import EMISSIVE
from familiars import rim, pts, to_img

W, H, AX, AY = 52, 64, 26, 63
SITES = ('chapel', 'watch', 'sawmill', 'observatory')
PHASES = 4


def wall(s, x0, y0, x1, y1, k='b2', mortar='b1', brick=5):
    """Mur z cegieł: spoiny poziome co 3 px, pionowe przesunięte co rząd."""
    s.rect(x0, y0, x1, y1, k)
    for y in range(y0, y1 + 1):
        if (y - y0) % 3 == 2:
            pts(s, [(x, y) for x in range(x0, x1 + 1)], mortar)
        else:
            off = 0 if ((y - y0) // 3) % 2 == 0 else brick // 2
            pts(s, [(x, y) for x in range(x0 + off, x1 + 1, brick)], mortar)


def roof(s, cx, top, half, rows, ramp=('p1', 'p2')):
    for i in range(rows):
        w = int(half * (i + 1) / rows)
        s.rect(cx - w, top + i, cx + w, top + i, ramp[i % 2])


def arch_window(s, cx, y0, w, h, k='fl', k2='fL', frame='p0'):
    s.rect(cx - w // 2 - 1, y0 - 1, cx + (w - 1) // 2 + 1, y0 + h, frame)
    s.rect(cx - w // 2, y0 + 1, cx + (w - 1) // 2, y0 + h - 1, k)
    pts(s, [(x, y0) for x in range(cx - w // 2 + 1, cx + (w - 1) // 2)], k)
    s.set(cx, y0 + h // 2, k2)


def door(s, cx, y1, w=6, h=9):
    s.rect(cx - w // 2, y1 - h + 2, cx + (w - 1) // 2, y1, 'p0')
    pts(s, [(x, y1 - h + 1) for x in range(cx - w // 2 + 1, cx + (w - 1) // 2)], 'p0')
    pts(s, [(cx - w // 2, y) for y in range(y1 - h + 3, y1 + 1)], 'e0')


def steps(s, cx, y1, w=14):
    for i in range(2):
        s.rect(cx - w // 2 + i * 2, y1 - i, cx + w // 2 - i * 2, y1 - i, 'b3' if i else 'b2')


def moss(s, step=5, seed=0):
    for (x, y), v in list(s.px.items()):
        if v in ('b2', 'b3') and (x, y - 1) not in s.px and (x * 3 + y + seed) % step == 0:
            s.px[(x, y)] = 'g2'


# ---------------------------------------------------------------- gotowe monumenty

def chapel():
    s = Sprite(W, H, AX, AY)
    for x in (6, 45):                                                    # przypory
        wall(s, x - 2, 40, x + 1, 62, 'b2'); pts(s, [(x - 2, 39), (x - 1, 38), (x, 38), (x + 1, 39)], 'b3')
    wall(s, 8, 34, 43, 62)                                               # nawa
    roof(s, 26, 18, 20, 17)                                              # dwuspadowy dach
    wall(s, 21, 6, 30, 34, 'b2')                                         # dzwonnica
    roof(s, 26, 0, 6, 7)
    s.rect(23, 9, 28, 15, 'p0')                                          # okno dzwonnicy
    s.blob(25.5, 12.5, 2.2, 2.4, ['or', 'go', 'go'], clip_y=15)          # dzwon
    s.blob(25.5, 25, 4.5, 4.5, ['ml', 'ml', 'mL'])                       # rozeta (świeci)
    pts(s, [(25, y) for y in range(21, 30)] + [(x, 25) for x in range(21, 31)], 'p0')
    for cx in (14, 37):
        arch_window(s, cx, 42, 4, 9, 'ml', 'mL')                         # witraże (świecą)
    door(s, 26, 61, 7, 11)
    steps(s, 26, 63, 16)
    moss(s, 6)
    s.outline()
    return s


def watch():
    s = Sprite(W, H, AX, AY)
    wall(s, 3, 42, 48, 62)                                               # mur obwodowy
    for x in range(3, 49, 4):
        s.rect(x, 39, x + 1, 41, 'b3')                                   # blanki
    for cx in (8, 43):                                                   # baszty
        wall(s, cx - 5, 32, cx + 4, 62, 'b2')
        for x in range(cx - 5, cx + 5, 3):
            s.rect(x, 29, x + 1, 31, 'b3')
        arch_window(s, cx, 40, 2, 5)
    wall(s, 18, 8, 33, 50, 'b2')                                         # wieża główna
    for x in range(18, 34, 3):
        s.rect(x, 5, x + 1, 7, 'b3')
    s.rect(20, 0, 31, 6, 'e1'); s.blob(25.5, 3, 4.5, 2.6, ['go', 'fl', 'fL'])   # lustro sygnałowe (świeci)
    for y in (16, 28):
        arch_window(s, 25, y, 3, 6)
    door(s, 25, 61, 7, 10)
    moss(s, 5, 1)
    s.outline()
    return s


def sawmill():
    s = Sprite(W, H, AX, AY)
    s.rect(16, 30, 49, 62, 'e2')                                         # hala z desek
    for x in range(16, 50, 3):
        pts(s, [(x, y) for y in range(30, 63)], 'e1')
    for i in range(16):                                                  # dach
        s.rect(13 + i // 2, 29 - i, 52 - 1 - i // 2, 29 - i, 'p1' if i % 2 else 'p2')
    s.rect(42, 4, 45, 18, 'b2'); s.rect(41, 3, 46, 4, 'b3')              # komin
    s.rect(28, 48, 37, 62, 'p0'); pts(s, [(x, 48) for x in range(28, 38)], 'e3')   # wrota
    for cx in (22, 44):
        s.rect(cx - 2, 38, cx + 1, 41, 'fl'); s.set(cx, 39, 'fL')        # okna (świecą)
    for k in range(48):                                                  # wielkie koło wodne
        a = k / 48 * 6.283
        s.set(int(round(11 + math.cos(a) * 10)), int(round(48 + math.sin(a) * 10)), 'e1')
        s.set(int(round(11 + math.cos(a) * 9)), int(round(48 + math.sin(a) * 9)), 'e2')
    for k in range(10):
        a = k * math.pi / 5
        s.line(11, 48, int(round(11 + math.cos(a) * 9)), int(round(48 + math.sin(a) * 9)), 'e3')
    s.blob(11, 48, 1.8, 1.8, ['e0', 'e1'])
    s.blob(11, 61, 10, 2.2, ['b1', 'b2', 'fo'])                          # spieniona woda
    for y, (x0, x1) in ((61, (38, 51)), (59, (40, 50)), (57, (42, 49))):   # stos bali
        s.rect(x0, y, x1, y + 1, 'e3'); s.set(x1, y, 'e1')
    s.outline()
    return s


def observatory():
    s = Sprite(W, H, AX, AY)
    for x0, x1 in ((2, 15), (36, 49)):                                   # skrzydła
        wall(s, x0, 44, x1, 62)
        roof(s, (x0 + x1) // 2, 37, 8, 7)
        arch_window(s, (x0 + x1) // 2, 50, 3, 6, 'ml', 'mL')
    wall(s, 13, 26, 38, 62)                                              # wieża
    pts(s, [(13, y) for y in range(26, 63)], 'b1'); pts(s, [(38, y) for y in range(26, 63)], 'b3')
    s.blob(25.5, 25, 14, 13, ['p1', 'p2', 'mu'], clip_y=26)              # kopuła
    s.rect(24, 12, 26, 26, 'p0')                                         # szczelina
    s.line(25, 18, 40, 4, 'go'); s.line(26, 18, 41, 4, 'or'); s.line(26, 19, 41, 5, 'or')   # luneta
    s.blob(42, 3.5, 2.2, 2, ['go', 'fl', 'fL'])                          # soczewka (świeci)
    for x, y in ((16, 17), (33, 16), (21, 14), (30, 20)):
        s.set(x, y, 'cW')                                                # gwiazdki na kopule (świecą)
    for cx in (19, 32):
        arch_window(s, cx, 34, 3, 7, 'ml', 'mL')
    door(s, 25, 61, 7, 11)
    steps(s, 25, 63, 14)
    moss(s, 6, 2)
    s.outline()
    return s


FINAL = {'chapel': chapel, 'watch': watch, 'sawmill': sawmill, 'observatory': observatory}


# ---------------------------------------------------------------- fazy

def _dark(s):
    """Okna niegotowej budowli nie świecą: piksele emisyjne na ciemne."""
    for p, k in list(s.px.items()):
        if k in EMISSIVE:
            s.px[p] = 'p0'


def _top(final):
    return min(y for (x, y) in final.px)


def _cols(final):
    xs = [x for (x, y) in final.px]
    return min(xs), max(xs)


def scaffold(s, x0, x1, ytop, ybot, rnd):
    """Rusztowanie: słupy po bokach, pomosty co 7 px, zastrzały, wciągarka u góry."""
    for x in (x0, x1):
        s.rect(x, ytop, x, ybot, 'e2'); s.set(x, ytop, 'e3')
    for y in range(ytop + 2, ybot, 9):
        s.rect(x0 - 1, y, x1 + 1, y, 'e3')
        pts(s, [(x, y + 1) for x in range(x0, x1 + 1, 5)], 'e1')
    for y in range(ytop + 2, ybot - 9, 9):
        s.line(x0, y + 9, x0 + 6, y, 'e1'); s.line(x1, y + 9, x1 - 6, y, 'e1')
    s.rect(x0 + 2, ytop - 3, x0 + 2, ytop, 'e2'); s.rect(x0 + 2, ytop - 3, x0 + 8, ytop - 3, 'e2')   # żuraw
    pts(s, [(x0 + 8, y) for y in range(ytop - 2, ytop + 4)], 'fo')                                   # lina
    s.rect(x0 + 7, ytop + 4, x0 + 9, ytop + 6, 'b2')                                                # podnoszony blok


def materials(s, rnd):
    """Stos materiałów przy budowie: deski i ciosane kamienie."""
    for i, (x0, x1) in enumerate(((0, 9), (1, 8))):
        s.rect(x0, 62 - i * 2, x1, 63 - i * 2, 'e3'); s.set(x1, 62 - i * 2, 'e1')
    for cx, cy in ((46, 62), (49, 61), (47, 59)):
        s.rect(cx - 1, cy - 1, cx + 1, cy, 'b3'); s.set(cx + 1, cy, 'b1')


def rubble(s, rnd, n=9):
    for _ in range(n):
        x = rnd.uniform(4, W - 5); y = rnd.uniform(57, 62)
        r = rnd.uniform(1.4, 2.8)
        s.blob(x, y, r, r * 0.75, ['p0', 'b1', 'b2', 'b3'])


def phase(site, k):
    final = FINAL[site]()
    if k >= PHASES:
        return final
    rnd = random.Random(sum(map(ord, site)) * 7 + k)
    top, bottom = _top(final), AY
    x0, x1 = _cols(final)
    s = Sprite(W, H, AX, AY)
    if k == 0:
        # ruina: niski kikut z poszarpaną górą i dziurami, gruz dookoła, mech
        base = bottom - (bottom - top) * 0.3
        a, b = rnd.uniform(0, 6.28), rnd.uniform(0, 6.28)
        # łagodnie poszarpana góra (dwie fale), kilka wyrw w murze
        cut = {x: base + 5 * math.sin(x * 0.28 + a) + 2.5 * math.sin(x * 0.83 + b) for x in range(W)}
        holes = [(rnd.uniform(8, W - 8), rnd.uniform(base + 4, bottom - 6), rnd.uniform(1.5, 3)) for _ in range(3)]
        for (x, y), v in final.px.items():
            if y >= round(cut[x]) and not any((x - hx) ** 2 + ((y - hy) * 1.4) ** 2 < hr * hr for hx, hy, hr in holes):
                s.px[(x, y)] = v
        _dark(s)
        for (x, y), v in list(s.px.items()):
            if v in ('o',) and (x, y - 1) not in s.px:
                s.px[(x, y)] = 'b1'
        rubble(s, rnd, 12)
        for (x, y), v in list(s.px.items()):
            if (x, y - 1) not in s.px and v not in ('o',) and rnd.random() < 0.45:
                s.px[(x, y)] = 'g2' if rnd.random() < 0.6 else 'g1'
        s.outline()
        return s
    # odbudowa: mur rośnie od dołu (45%, 65%, 85% wysokości), świeża górna krawędź, rusztowanie, materiały
    frac = 0.25 + 0.2 * k
    cut = bottom - (bottom - top) * frac
    for (x, y), v in final.px.items():
        if y >= cut:
            s.px[(x, y)] = v
    _dark(s)
    for (x, y), v in list(s.px.items()):
        if (x, y - 1) not in s.px and v != 'o':
            s.px[(x, y)] = 'b3' if v.startswith('b') else 'e3'          # świeżo ułożona krawędź
    scaffold(s, max(1, x0 + 3), min(W - 2, x1 - 3), int(cut) - 6, bottom - 1, rnd)
    materials(s, rnd)
    s.outline()
    return s


if __name__ == "__main__":
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    rows = [[to_img(phase(site, k)) for k in range(PHASES + 1)] for site in SITES]
    cw, ch = W + 6, H + 4
    sheet = Image.new("RGBA", (cw * (PHASES + 1), ch * len(SITES)), (44, 60, 44, 255))
    for j, row in enumerate(rows):
        for i, im in enumerate(row):
            sheet.alpha_composite(im, (i * cw + 3, j * ch + 2))
    out = os.path.join(ROOT, "docs", "img", "monumenty.png")
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(out)
    print("ok:", out)
