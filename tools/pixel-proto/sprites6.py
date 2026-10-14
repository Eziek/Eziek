"""Sprite'y Etapu 6b: sześć nowych roślin (3 dnia, 3 nocy), ikony ich nasion i duszek-handlarz.
Te same zasady co sprites3 (paleta mroczna + emisja, siatka 16 px, gruby kontur)."""
import math
import sprites
import sprites3 as S3
from sprites import Sprite, GREEN, _plant
from sprites3 import with_mound, NIGHT_LEAF, DARK_LEAF, PALE, BLOOD

SILVER = ['g1', 'b3', 'fo', 'fo']
ROOT = ['e1', 'e2', 'e3', 'cr']
BERRY = ['o', 'p0', 'mu', 'ml']


# ---------------- rośliny dnia ----------------
def garlic(stage):
    """Czosnek: wąskie liście, w fazie 3 biała główka przy ziemi."""
    s = _plant()
    if stage == 1:
        s.blob(8, 16, 0.9, 3, GREEN, ang=-0.2); s.blob(10, 16, 0.9, 3, GREEN, ang=0.2)
    elif stage == 2:
        for cx, a in ((6.5, -0.45), (8.5, -0.12), (10.5, 0.15), (12.5, 0.45)):
            s.blob(cx, 13, 0.9, 5.5, GREEN, ang=a)
    else:
        for cx, a in ((6, -0.5), (8, -0.15), (10.5, 0.15), (12.5, 0.5)):
            s.blob(cx, 10.5, 0.9, 6, GREEN, ang=a)
        s.blob(9, 18, 3.6, 3, PALE, clip_y=20)
        s.set(9, 15, 'b3'); s.set(7, 18, 'b3'); s.set(11, 18, 'b3')
    s.outline()
    return with_mound(s)


def poppy(stage):
    """Mak: łodyga z pąkiem, w fazie 3 ciemnoczerwony kwiat z czarnym środkiem."""
    s = _plant()
    if stage == 1:
        s.blob(7.5, 16.5, 1.6, 2.6, GREEN, ang=-0.6); s.blob(10.5, 16.5, 1.6, 2.6, GREEN, ang=0.6)
    else:
        top = 9 if stage == 2 else 8
        s.line(9, 19, 9, top + 2, 'g1')
        s.blob(6.5, 16, 1.6, 2.8, GREEN, ang=-0.8); s.blob(11.5, 15, 1.6, 2.8, GREEN, ang=0.8)
        if stage == 2:
            s.blob(9, top, 1.6, 2.2, ['g1', 'g2', 'g3'])
        else:
            s.blob(9, top, 4.2, 3.2, BLOOD)
            s.rect(8, top - 1, 10, top, 'o'); s.set(9, top - 1, 'p0')
    s.outline()
    return with_mound(s)


def wormwood(stage):
    """Piołun: srebrzysty, pierzasty krzaczek."""
    s = _plant()
    tufts = {1: [(8, 16, 1.6, 2.5), (10.5, 16.5, 1.4, 2)],
             2: [(6, 14, 2, 4), (9, 12, 2.2, 5), (12, 14, 2, 4)],
             3: [(4.5, 13, 2.4, 5), (7.5, 10, 2.4, 6.5), (10.5, 9.5, 2.4, 7), (13.5, 12.5, 2.4, 5)]}[stage]
    for i, (cx, cy, rx, ry) in enumerate(tufts):
        s.blob(cx, cy, rx, ry, SILVER, ang=(i - len(tufts) / 2) * 0.25)
    if stage == 3:
        for x, y in ((7, 5), (11, 4), (4, 9), (14, 8)):
            s.set(x, y, 'go')                                   # drobne żółte kwiatki
    s.outline()
    return with_mound(s)


# ---------------- rośliny nocy ----------------
def mandrake(stage):
    """Mandragora: rozeta ciemnych liści, w fazie 3 wystaje korzeń z twarzą (oczy świecą)."""
    s = _plant()
    if stage == 1:
        s.blob(7, 16.5, 2.2, 1.4, NIGHT_LEAF, ang=-0.3); s.blob(11, 16.5, 2.2, 1.4, NIGHT_LEAF, ang=0.3)
        s.outline()
        return with_mound(s)
    if stage == 2:
        for cx, cy, a in ((5, 14, -0.7), (9, 12, 0), (13, 14, 0.7)):
            s.blob(cx, cy, 1.9, 3.6, NIGHT_LEAF, ang=a)
        s.outline()
        return with_mound(s)
    for cx, cy, a in ((4.5, 9, -0.8), (8, 7, -0.25), (11, 7, 0.25), (14, 9, 0.8)):
        s.blob(cx, cy, 1.9, 4, NIGHT_LEAF, ang=a)
    s.blob(9.5, 16.5, 4.2, 4, ROOT, clip_y=21)
    s.line(6, 20, 4, 21, 'e2'); s.line(13, 20, 15, 21, 'e2')       # nóżki korzenia
    s.outline()
    s.set(8, 15, 'wi'); s.set(11, 15, 'wi'); s.set(9, 18, 'o'); s.set(10, 18, 'o')
    return s


def belladonna(stage):
    """Wilcza jagoda: ciemne liście, fioletowe kielichy, w fazie 3 lśniące jagody."""
    s = _plant()
    if stage == 1:
        s.blob(7.5, 16, 1.5, 2.6, DARK_LEAF, ang=-0.5); s.blob(10.5, 16, 1.5, 2.6, DARK_LEAF, ang=0.5)
    else:
        s.line(9, 19, 9, 9, 'g0')
        for cx, cy, a in ((6, 15, -0.9), (12, 13.5, 0.9), (6.5, 10.5, -0.7), (11.5, 9, 0.7)):
            s.blob(cx, cy, 1.7, 3, DARK_LEAF, ang=a)
        bells = ((8, 12), (11, 16)) if stage == 2 else ((7, 13), (11, 11), (10, 16))
        for x, y in bells:
            s.blob(x + 0.5, y, 1.4, 1.6, ['p0', 'p1', 'mu'])
        if stage == 3:
            for x, y in ((5, 8), (13, 6), (8, 6)):
                s.blob(x + 0.5, y + 0.5, 1.5, 1.5, BERRY)
    s.outline()
    if stage == 3:
        for x, y in ((5, 8), (13, 6), (8, 6)):
            s.set(x, y, 'ml')                                 # odblask jagód (emisja)
    return with_mound(s)


def grave_ivy(stage):
    """Cmentarny bluszcz: pnie się po kołku, w fazie 3 upiorne, świecące liście."""
    s = _plant()
    top = {1: 15, 2: 10, 3: 4}[stage]
    s.rect(9, top, 9, 20, 'e2'); s.set(10, top, 'e1')                 # kołek
    leaf = NIGHT_LEAF if stage < 3 else ['g0', 'b2', 'wi', 'wl']
    for i, y in enumerate(range(18, top, -3)):
        x = 7 if i % 2 == 0 else 11
        s.blob(x + 0.5, y, 1.8, 1.4, leaf, ang=-0.5 if x < 9 else 0.5)
    s.outline()
    return with_mound(s)


CROPS_DAY = {'garlic': garlic, 'poppy': poppy, 'wormwood': wormwood}
CROPS_NIGHT = {'mandrake': mandrake, 'belladonna': belladonna, 'graveivy': grave_ivy}


# ---------------- duszek-handlarz ----------------
def merchant_spirit(f):
    """Złoty duszek z sakiewką: lata do skrzyń i sprzedaje plony (Etap 6b)."""
    s = Sprite(16, 22, 8, 21)
    dy = 0   # unoszenie robi kod (płynnie), klatki zmieniają tylko płomień
    s.blob(8, 8 + dy, 3.4, 4, ['fx', 'fl', 'fl'])
    for x, y in ((7, 3), (8, 2), (9, 3)):
        if f % 2 or y == 3: s.set(x, y + dy, 'fl')
    for x, y in ((6, 12), (8, 13), (10, 12)):
        if (x + f) % 2 == 0 or y == 13: s.set(x, y + dy, 'fx')
    s.blob(11.5, 14 + dy, 2.4, 2.2, ['e1', 'e2', 'e3'])            # sakiewka
    s.set(11, 12 + dy, 'e1'); s.set(12, 15 + dy, 'go')
    s.outline('e0')
    s.set(7, 8 + dy, 'o'); s.set(9, 8 + dy, 'o')                   # oczy
    for x in range(6, 12):
        s.set(x, 20, 'e1')                                          # cień
    return s


# ---------------- ikony nasion (do ui_kit.SEED_SYMBOLS) ----------------
SEED_SYMBOLS = {
    'garlic': [".m.m.", "..m..", ".www.", "wwzww", ".www."],
    'poppy': [".c.c.", "c1c1c", ".c.c.", "..m..", ".mm.."],
    'wormwood': ["s.z.s", ".szs.", "..s..", ".sms.", "..m.."],
    'mandrake': [".m.m.", "..m..", ".jhj.", "jWhWj", ".h.h."],
    'belladonna': [".1U1.", "1UMU1", ".1U1.", "..k..", ".kk.."],
    'graveivy': ["..W..", ".WhW.", "..h..", ".WhW.", "..h.."],
}


# ---------------- ikony UI (Etap 6b+): kategorie koła nasion, wyjście, zasypanie grządki ----------------
ICON_MAPS = {
    'sun': ["....F.....", ".F.....F..", "...XXX....", "..XFFFX...", "F.XFLFX.F.",
            "..XFFFX...", "...XXX....", ".F.....F..", "....F.....", ".........."],
    'moon': ["..sss.....", ".szzs.....", "szz.......", "sz........", "sz.....L..",
             "szz.......", ".szzzzs...", "..ssss....", "..........", ".........."],
    'exit': [".hhhhh....", ".hjjjh....", ".hj.jh....", ".hj.jh.w..", ".hjgjhwww.",
             ".hj.jh.w..", ".hj.jh....", ".hjjjh....", ".hhhhh....", ".........."],
    'fill': [".......jj.", "......jj..", ".....jj...", "....jj....", "..sss.....",
             ".szzs.....", ".szs......", "..s.......", "hhhhhhh...", ".........."],
    # menu główne (Etap 6d+): trzy deski jak w księdze zapisów
    'menu': ["..........", ".hhhhhhh..", ".hjjjjjh..", ".hhhhhhh..", ".hjjjjjh..",
             ".hhhhhhh..", ".hjjjjjh..", ".hhhhhhh..", "..........", ".........."],
}


# ---------------- ptaki (prośba gracza: życie na wyspie) ----------------
def bird(kind, pose):
    """kind: 'crow' (wrona) albo 'finch' (mały ptaszek). pose: sit0, sit1 (dziób w dół), fly0 (skrzydła w górze), fly1 (w dole).
    Patrzy w prawo; w lewo odbija kod (flipX)."""
    s = Sprite(12, 10, 6, 9)
    body = ['o', 'p0', 'p1'] if kind == 'crow' else ['e1', 'e2', 'e3']
    head = body if kind == 'crow' else ['e0', 'p1', 'p2']
    beak = 'go' if kind == 'crow' else 'or'
    peck = pose == 'sit1'
    fly = pose.startswith('fly')
    by = 5 if not fly else 4
    s.blob(5.5, by, 3, 2.3 if kind == 'crow' else 2.0, body)
    hx, hy = (8.5, by - 2.0) if not peck else (9.0, by + 0.5)
    s.blob(hx, hy, 1.6 if kind == 'crow' else 1.4, 1.5, head)
    bx, byk = int(hx + 1.6), int(round(hy))
    s.set(bx, byk, beak)
    if kind == 'crow':
        s.set(bx + 1, byk, beak)
    if kind == 'finch':
        s.set(6, by + 1, 'cr'); s.set(5, by + 1, 'cr')     # jasny brzuszek
    s.line(1, by + 1, 3, by, body[1])                      # ogon
    if pose == 'fly0':
        s.line(5, by - 1, 2, 0, body[1]); s.line(6, by - 1, 4, 0, body[1])
    elif pose == 'fly1':
        s.line(5, by + 1, 2, 9, body[1]); s.line(6, by + 1, 4, 9, body[1])
    else:
        s.set(5, 8, beak); s.set(7, 8, beak)               # nóżki
    s.outline()
    s.set(int(hx), int(round(hy)) - 1 if not peck else int(round(hy)), 'fl' if kind == 'crow' else 'cr')   # oko
    return s


BIRD_POSES = ('sit0', 'sit1', 'fly0', 'fly1')


# ---------- wprowadzenie (Etap 6d): tablica z karteczkami i strzałka celu ----------
def notice_board(note=True):
    """Krzywa tablica ogłoszeń na dwóch słupkach, 16 px szerokości z obrysem (jak kafelek). note: przybita karteczka."""
    s = Sprite(16, 22, 8, 20)
    for x in (3, 12):                                   # słupki
        s.rect(x, 9, x, 19, 'e1')
        s.set(x, 19, 'e0')
    for y in range(4, 12):                              # deski tablicy
        for x in range(1, 15):
            k = 'e3' if y in (4, 8) else ('e1' if y in (7, 11) else 'e2')
            if x in (1, 14): k = 'e1'
            s.set(x, y, k)
    s.set(1, 4, 'e2'); s.set(14, 11, 'e0')              # nierówne rogi
    s.rect(2, 3, 13, 3, 'e1')                           # daszek
    s.set(1, 3, 'e0'); s.set(14, 3, 'e0')
    # stara, wyblakła karteczka zawsze w rogu
    s.rect(10, 6, 12, 9, 'fo'); s.set(12, 9, 'e2'); s.set(11, 7, 'b3')
    if note:
        s.rect(4, 5, 8, 10, 'cr')                       # karteczka z pergaminu
        s.set(8, 10, 'go'); s.set(4, 10, 'go')          # podwinięte rogi
        for y in (7, 9):
            s.rect(5, y, 7, y, 'e3')                    # „pismo”
        s.set(6, 5, 'co')                               # gwóźdź
    return s.outline()


def intro_arrow():
    """Mała złota strzałka nad celem wprowadzenia (unosi się w kodzie)."""
    s = Sprite(7, 7, 3, 6)
    for y, half in ((0, 1), (1, 1), (2, 1), (3, 3), (4, 2), (5, 1), (6, 0)):
        for x in range(3 - half, 4 + half):
            s.set(x, y, 'go' if x > 3 - half else 'cr')
    return s.outline()
