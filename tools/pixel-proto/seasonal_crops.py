"""Rośliny sezonowe (GDD §9 p. 129): po 4 na porę roku (2 dnia, 2 nocy), do siania tylko w swojej porze.
Te same zasady co sprites6 (paleta mroczna + emisja, płótno 22x26, ziemia w y=20, gruby kontur), fazy 1-3
(faza 0 = nasiono, kiełek i niedojrzałą dorabia export_unity_stages.py). Piksele emisyjne (wi, wl, fl, fx, ml) świecą nocą.
Podgląd: python3 seasonal_crops.py -> docs/img/rosliny_sezonowe.png
"""
import os
import sprites
import sprites3 as S3
from sprites import Sprite, GREEN, _plant
from sprites3 import with_mound, NIGHT_LEAF, DARK_LEAF

FROST = ['b0', 'b1', 'b3', 'fo']
KALE = ['g0', 'b1', 'g1', 'b3']
PURPLE = ['p0', 'p1', 'p2']
PALEPINK = ['p2', 'co', 'fo', 'cr']
AMBER = ['e2', 'or', 'go']
PUFF = ['b3', 'fo', 'cr']


def _sprout(s, ramp=GREEN):
    s.blob(7.5, 16.5, 1.4, 2.8, ramp, ang=-0.45); s.blob(10.5, 16.5, 1.4, 2.8, ramp, ang=0.45)


# ---------------- wiosna ----------------
def radish(stage):
    """Rzodkiew: kępka liści, w fazie 3 czerwona bulwa wystaje z ziemi."""
    s = _plant()
    if stage == 1:
        _sprout(s)
    elif stage == 2:
        for cx, a in ((6.5, -0.6), (9, 0), (11.5, 0.6)):
            s.blob(cx, 13.5, 1.6, 4, GREEN, ang=a)
    else:
        for cx, a in ((5.5, -0.7), (8, -0.2), (10.5, 0.2), (13, 0.7)):
            s.blob(cx, 11.5, 1.7, 4.5, GREEN, ang=a)
        s.blob(9, 18, 2.8, 2.4, ['p2', 'co', 'co'], clip_y=21)
        s.set(9, 20, 'fo')
    s.outline()
    return with_mound(s)


def rhubarb(stage):
    """Krwisty rabarbar: wielkie liście na krwistych łodygach."""
    s = _plant()
    if stage == 1:
        _sprout(s, DARK_LEAF)
        s.set(9, 18, 'p2')
    elif stage == 2:
        s.line(7, 19, 6, 13, 'p2'); s.line(11, 19, 12, 12, 'p2')
        s.blob(5.5, 11, 2.6, 2.4, DARK_LEAF, ang=-0.4); s.blob(12.5, 10, 2.6, 2.4, DARK_LEAF, ang=0.4)
    else:
        for x0, x1, y1 in ((8, 5, 10), (9, 9, 7), (10, 13, 9)):
            s.line(x0, 19, x1, y1 + 2, 'co'); s.line(x0 + 1, 19, x1 + 1, y1 + 2, 'p2')
        s.blob(4.5, 8.5, 3.2, 2.6, DARK_LEAF, ang=-0.3); s.blob(13.5, 7.5, 3.2, 2.6, DARK_LEAF, ang=0.3)
        s.blob(9.5, 5.5, 3.2, 2.6, ['g0', 'g1', 'g2', 'g3'])
    s.outline()
    return with_mound(s)


def wolfsbane(stage):
    """Tojad: wysoka łodyga z fioletowymi kapturkami kwiatów, w fazie 3 najwyższy pączek lekko świeci."""
    s = _plant()
    if stage == 1:
        _sprout(s, DARK_LEAF)
    else:
        top = 9 if stage == 2 else 4
        s.line(9, 19, 9, top, 'g0')
        s.blob(6, 16, 1.8, 2.6, DARK_LEAF, ang=-0.8); s.blob(12, 15, 1.8, 2.6, DARK_LEAF, ang=0.8)
        hoods = ((9, top + 1),) if stage == 2 else ((9, top + 1), (7.5, top + 4), (10.5, top + 6), (8, top + 9))
        for x, y in hoods:
            s.blob(x + 0.5, y, 1.5, 1.6, PURPLE)
    s.outline()
    if stage == 3:
        s.set(9, 4, 'ml')
    return with_mound(s)


def fernflower(stage):
    """Kwiat paproci: zwinięte pióropusze liści, w fazie 3 w środku płonący kwiat (świeci)."""
    s = _plant()
    if stage == 1:
        s.blob(8, 16, 1.2, 2.6, NIGHT_LEAF, ang=-0.3); s.blob(10, 16, 1.2, 2.6, NIGHT_LEAF, ang=0.3)
        s.set(8, 13, 'b3'); s.set(10, 13, 'b3')
    else:
        fr = ((4, 12, -1.0), (6.5, 9, -0.5), (11.5, 9, 0.5), (14, 12, 1.0)) if stage == 3 else ((6, 13, -0.6), (12, 13, 0.6))
        for cx, cy, a in fr:
            s.blob(cx, cy, 1.4, 5 if stage == 3 else 4, NIGHT_LEAF, ang=a)
        if stage == 3:
            s.blob(9, 11, 2.4, 2.4, ['e2', 'or', 'go'])
    s.outline()
    if stage == 3:
        for x, y, k in ((9, 9, 'fx'), (8, 10, 'fl'), (10, 10, 'fl'), (9, 11, 'fx'), (8, 12, 'fl'), (10, 12, 'fl'), (9, 12, 'fl')):
            s.set(x, y, k)
    return with_mound(s)


# ---------------- lato ----------------
def lavender(stage):
    """Lawenda cieni: cienkie łodyżki zakończone fioletowymi kłosami."""
    s = _plant()
    if stage == 1:
        _sprout(s, DARK_LEAF)
    else:
        stems = ((6, 9), (9, 6), (12, 9)) if stage == 3 else ((7, 12), (11, 12))
        for x, top in stems:
            s.line(9, 19, x, top + 3, 'g1')
            s.blob(x + 0.5, top + 1, 1.0, 2.4 if stage == 3 else 1.5, ['p0', 'p1', 'p2'] if stage == 3 else ['g1', 'g2'])
        s.blob(7, 17, 1.4, 2, DARK_LEAF, ang=-0.8); s.blob(11, 17, 1.4, 2, DARK_LEAF, ang=0.8)
    s.outline()
    return with_mound(s)


def sunflower(stage):
    """Żałobny słonecznik: wysoki, w fazie 3 pochylona tarcza z przygaszonymi płatkami."""
    s = _plant()
    if stage == 1:
        _sprout(s)
    else:
        top = 9 if stage == 2 else 5
        s.line(9, 19, 9, top + 2, 'g1'); s.line(10, 19, 10, top + 3, 'g0')
        s.blob(6, 15, 2, 2.6, GREEN, ang=-0.9); s.blob(13, 13, 2, 2.6, GREEN, ang=0.9)
        if stage == 2:
            s.blob(9.5, top, 1.8, 1.8, ['g0', 'g1', 'g2'])
        else:
            s.blob(10, top, 4.2, 3.6, AMBER)          # płatki
            s.blob(10.5, top + 0.5, 2.2, 1.9, ['o', 'e0', 'e1'])   # ciemna tarcza
    s.outline()
    return with_mound(s)


def nightjasmine(stage):
    """Nocny jaśmin: ciemny krzaczek z białymi gwiazdkami kwiatów (świecą)."""
    s = _plant()
    if stage == 1:
        _sprout(s, DARK_LEAF)
        s.outline()
        return with_mound(s)
    bush = ((6, 14, 2.6, 3), (12, 14, 2.6, 3), (9, 11, 3, 3.2)) if stage == 3 else ((7.5, 15, 2.4, 2.6), (10.5, 15, 2.4, 2.6))
    for cx, cy, rx, ry in bush:
        s.blob(cx, cy, rx, ry, DARK_LEAF)
    s.outline()
    stars = ((6, 12), (9, 9), (12, 12), (8, 14), (11, 15)) if stage == 3 else ((8, 14),)
    for x, y in stars:
        s.set(x, y, 'wi' if stage == 3 else 'fo')
    return with_mound(s)


def lantern(stage):
    """Lampion: łukowata łodyga z wiszącymi pomarańczowymi koszyczkami, w środku tli się owoc (świeci)."""
    s = _plant()
    if stage == 1:
        _sprout(s)
        s.outline()
        return with_mound(s)
    s.line(9, 19, 9, 9, 'g1'); s.line(9, 9, 13, 7, 'g1'); s.line(9, 11, 5, 9, 'g1')
    s.blob(6.5, 15, 1.8, 2.4, GREEN, ang=-0.8); s.blob(11.5, 14, 1.8, 2.4, GREEN, ang=0.8)
    pods = ((13.5, 10), (5, 12)) if stage == 3 else ((13.5, 10),)
    for x, y in pods:
        s.blob(x, y, 1.8, 2.2, ['p2', 'or', 'go'] if stage == 3 else ['g0', 'g1', 'g2'])
    s.outline()
    if stage == 3:
        for x, y in pods:
            s.set(int(x), y, 'fl')
    return with_mound(s)


# ---------------- jesień ----------------
def turnip(stage):
    """Rzepa: liście i biało-fioletowa bulwa."""
    s = _plant()
    if stage == 1:
        _sprout(s)
    else:
        for cx, a in ((6.5, -0.6), (9, 0), (11.5, 0.6)):
            s.blob(cx, 12 if stage == 3 else 14, 1.6, 4, GREEN, ang=a)
        if stage == 3:
            s.blob(9, 17.5, 3.2, 2.8, ['p1', 'p2', 'fo', 'cr'], clip_y=21)
    s.outline()
    return with_mound(s)


def heather(stage):
    """Wrzos: niski, gęsty krzaczek obsypany drobnymi kwiatkami."""
    s = _plant()
    if stage == 1:
        _sprout(s, DARK_LEAF)
        s.outline()
        return with_mound(s)
    for cx, cy, rx, ry in ((6, 16, 2.6, 2.8), (12, 16, 2.6, 2.8), (9, 13.5, 3, 3.2)) if stage == 3 else ((9, 16, 3, 2.6),):
        s.blob(cx, cy, rx, ry, DARK_LEAF)
    if stage == 3:
        for x, y in ((5, 15), (7, 12), (9, 11), (11, 13), (13, 15), (8, 15), (10, 16), (6, 17), (12, 14)):
            s.set(x, y, 'p2' if (x + y) % 2 else 'co')
    s.outline()
    return with_mound(s)


def puffball(stage):
    """Czarcia purchawka: blade kule grzybów, w fazie 3 największa sypie świecącymi zarodnikami."""
    s = _plant()
    if stage == 1:
        s.blob(9, 17.5, 1.6, 1.4, PUFF)
    elif stage == 2:
        s.blob(8, 16.5, 2.2, 2, PUFF); s.blob(11.5, 17.5, 1.4, 1.3, PUFF)
    else:
        s.blob(8.5, 15, 3.6, 3.4, PUFF); s.blob(13, 17, 1.8, 1.7, PUFF); s.blob(5, 17.5, 1.3, 1.2, PUFF)
        s.set(8, 12, 'e1'); s.set(9, 12, 'e1')            # pęknięty czubek
    s.outline()
    if stage == 3:
        for x, y in ((7, 9), (10, 8), (8, 7), (11, 10)):
            s.set(x, y, 'ml')
    return with_mound(s)


def catseye(stage):
    """Kocie oczy: ciemny krzak, w fazie 3 pary bursztynowych jagód jak świecące oczy w mroku."""
    s = _plant()
    if stage == 1:
        _sprout(s, NIGHT_LEAF)
        s.outline()
        return with_mound(s)
    for cx, cy, rx, ry in ((6, 14, 2.8, 3.2), (12, 14, 2.8, 3.2), (9, 10.5, 3.2, 3.4)) if stage == 3 else ((9, 15, 3.2, 3),):
        s.blob(cx, cy, rx, ry, NIGHT_LEAF)
    s.outline()
    eyes = ((6, 13), (11, 9), (11, 15)) if stage == 3 else ()
    for x, y in eyes:
        s.set(x, y, 'fx'); s.set(x + 2, y, 'fx')
    return with_mound(s)


# ---------------- zima ----------------
def kale(stage):
    """Jarmuż: kędzierzawa, sinozielona rozeta, rośnie mimo mrozu."""
    s = _plant()
    if stage == 1:
        _sprout(s, KALE)
    else:
        leaves = ((5, 13, -0.8), (8, 11, -0.3), (11, 11, 0.3), (14, 13, 0.8)) if stage == 3 else ((7, 14, -0.5), (11, 14, 0.5))
        for cx, cy, a in leaves:
            s.blob(cx, cy, 2.1, 4.2 if stage == 3 else 3.2, KALE, ang=a)
        if stage == 3:
            for x, y in ((4, 10), (7, 8), (12, 8), (15, 10), (9, 10)):
                s.set(x, y, 'b3')                          # kędziory
    s.outline()
    return with_mound(s)


def hellebore(stage):
    """Ciemiernik: ciemne liście i pochylone, blade kwiaty, które kwitną w śniegu."""
    s = _plant()
    if stage == 1:
        _sprout(s, DARK_LEAF)
    else:
        for cx, cy, a in ((5.5, 15, -0.9), (12.5, 15, 0.9), (9, 13, 0)):
            s.blob(cx, cy, 2, 3, DARK_LEAF, ang=a)
        if stage == 3:
            for x, y in ((6, 9), (12, 8), (9, 6)):
                s.line(9, 13, x, y + 2, 'g0')
                s.blob(x + 0.5, y, 1.8, 1.6, PALEPINK)
                s.set(x, y + 1, 'go')
    s.outline()
    return with_mound(s)


def frostbloom(stage):
    """Szronik: lodowy kwiat z kryształowych płatków, w fazie 3 świeci bladym światłem."""
    s = _plant()
    if stage == 1:
        s.line(9, 19, 9, 16, 'b1'); s.set(8, 16, 'b3'); s.set(10, 16, 'b3')
    else:
        top = 12 if stage == 2 else 8
        s.line(9, 19, 9, top + 2, 'b1')
        s.blob(6.5, 16, 1.4, 2.4, FROST, ang=-0.8); s.blob(11.5, 16, 1.4, 2.4, FROST, ang=0.8)
        if stage == 3:
            for dx, dy in ((0, -3), (-3, 0), (3, 0), (-2, -2), (2, -2), (0, 2)):
                s.line(9, top, 9 + dx, top + dy, 'b3')
            s.blob(9, top, 1.6, 1.6, FROST)
        else:
            s.blob(9, top, 1.4, 1.6, FROST)
    s.outline()
    if stage == 3:
        s.set(9, 8, 'wl'); s.set(9, 5, 'wl'); s.set(6, 8, 'wl'); s.set(12, 8, 'wl')
    return with_mound(s)


def mistletoe(stage):
    """Jemioła: kępa na sękatym kiju, w fazie 3 białe, świecące jagody."""
    s = _plant()
    top = {1: 15, 2: 11, 3: 8}[stage]
    s.rect(9, top + 2, 9, 20, 'e1'); s.set(10, top + 4, 'e2')    # sękaty kij
    r = {1: 1.8, 2: 3, 3: 4}[stage]
    s.blob(9, top, r + 0.6, r, ['g0', 'g1', 'g2'])
    s.outline()
    if stage == 3:
        for x, y in ((7, 7), (10, 6), (12, 9), (8, 10), (6, 9)):
            s.set(x, y, 'wi')
    return with_mound(s)


# id -> (funkcja, noc?, pora)
CROPS = {
    'radish': (radish, False, 'spring'), 'rhubarb': (rhubarb, False, 'spring'),
    'wolfsbane': (wolfsbane, True, 'spring'), 'fernflower': (fernflower, True, 'spring'),
    'lavender': (lavender, False, 'summer'), 'sunflower': (sunflower, False, 'summer'),
    'nightjasmine': (nightjasmine, True, 'summer'), 'lanternfruit': (lantern, True, 'summer'),
    'turnip': (turnip, False, 'autumn'), 'heather': (heather, False, 'autumn'),
    'puffball': (puffball, True, 'autumn'), 'catseye': (catseye, True, 'autumn'),
    'kale': (kale, False, 'winter'), 'hellebore': (hellebore, False, 'winter'),
    'frostbloom': (frostbloom, True, 'winter'), 'mistletoe': (mistletoe, True, 'winter'),
}

# ikony nasion (5x5 na torebce, litery jak ui_kit.KEY)
SEED_SYMBOLS = {
    'radish': ["..mm.", ".mm..", ".cc..", "cccc.", ".cz.."],
    'rhubarb': ["kmmk.", "mmmmk", ".c.c.", ".c.c.", ".c.c."],
    'wolfsbane': ["..2..", ".232.", "..k..", ".232.", "..k.."],
    'fernflower': ["b.X.b", ".bFb.", "..F..", ".b.b.", "b...b"],
    'lavender': ["2.2.2", "2.2.2", "m.m.m", ".mmm.", "..m.."],
    'sunflower': [".ggg.", "gfffg", "gfffg", ".ggg.", "..m.."],
    'nightjasmine': ["W.k.W", ".kkk.", "kWkWk", ".kkk.", "..k.."],
    'lanternfruit': ["..m..", ".rrr.", "rrFrr", ".rrr.", "..r.."],
    'turnip': [".m.m.", "..m..", ".222.", "2zzz2", ".zzz."],
    'heather': ["3.c.3", ".k3k.", "c3k3c", ".kkk.", "..h.."],
    'puffball': [".zzz.", "zzzzz", "zzzzs", ".sss.", "..M.."],
    'catseye': [".kkk.", "kXkXk", "kkkkk", "XkXkk", ".kkk."],
    'kale': ["s.s.s", "kskss", ".kkk.", "kskks", "..k.."],
    'hellebore': [".z.z.", "zgzgz", ".kzk.", "..k..", ".kk.."],
    'frostbloom': ["..L..", "s.L.s", ".LLL.", "s.L.s", "..b.."],
    'mistletoe': [".kmk.", "kWmWk", ".mWm.", "..h..", "..h.."],
}


if __name__ == "__main__":
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    RGB = S3.RGB
    cells = []
    for cid, (fn, night, season) in CROPS.items():
        row = []
        for st in (1, 2, 3):
            spr = fn(st)
            im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
            for (x, y), k in spr.px.items():
                im.putpixel((x, y), RGB[k] + (255,))
            row.append(im)
        cells.append(row)
    cw, ch = 24, 28
    cols = 4
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new("RGBA", (cw * 3 * cols, ch * rows), (40, 46, 40, 255))
    for i, row in enumerate(cells):
        for j, im in enumerate(row):
            sheet.alpha_composite(im, ((i % cols) * cw * 3 + j * cw, (i // cols) * ch))
    out = os.path.join(ROOT, "docs", "img", "rosliny_sezonowe.png")
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(out)
    print("ok:", out)
