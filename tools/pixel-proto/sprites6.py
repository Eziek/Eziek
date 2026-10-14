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


# ---------------- hybrydy (mutacje): kolory rodziców, faza 3 z własnym akcentem ----------------
import copy

TO_NIGHT = {'g0': 'p0', 'g1': 'b1', 'g2': 'b2', 'g3': 'b3'}
TO_ASH = {'g0': 'p0', 'g1': 'b1', 'g2': 'b3', 'g3': 'fo', 'go': 'b3', 'cr': 'fo'}
TO_WOLF = {'g1': 'g0', 'g2': 'g1', 'g3': 'g2', 'p2': 'p1', 'co': 'p2', 'or': 'mu', 'go': 'ml'}
TO_MOON = {'g0': 'p0', 'g1': 'b1', 'g2': 'b2', 'g3': 'b3', 'p2': 'fo', 'co': 'cr'}
TO_BLOOD = {'g1': 'p1', 'b3': 'p2', 'fo': 'co', 'go': 'fx'}


def recolor(spr, mapping):
    out = copy.deepcopy(spr)
    out.px = {p: mapping.get(k, k) for p, k in spr.px.items()}
    return out


def moon_poppy(stage):
    """Księżycowy mak (mak + księżycowy kwiat): blade płatki, środek świeci."""
    s = recolor(poppy(stage), TO_MOON)
    if stage == 3:
        s.set(9, 7, 'wl'); s.set(8, 7, 'wi'); s.set(10, 7, 'wi')
    return s


def ash_wheat(stage):
    """Popielne zboże (zboże + trupi grzyb): szare kłosy z żarzącymi się końcami."""
    s = recolor(sprites.wheat(stage), TO_ASH)
    if stage == 3:
        for p in sorted((p for p, k in s.px.items() if k != 'o'), key=lambda p: p[1])[:6]:
            s.px[p] = 'fx'                                    # px ma już przesunięcie, więc bez set()
    return s


def wolf_carrot(stage):
    """Wilcza marchew (marchew + wilcza jagoda): ciemna nać, fioletowy korzeń z jagodą."""
    s = recolor(sprites.carrot(stage), TO_WOLF)
    if stage == 3:
        s.set(9, 5, 'ml'); s.set(5, 7, 'ml')
    return s


def scream_garlic(stage):
    """Wrzeszczący czosnek (czosnek + mandragora): główka z otwartymi ustami i świecącymi oczami."""
    s = recolor(garlic(stage), TO_NIGHT)
    if stage == 3:
        s.set(8, 17, 'wi'); s.set(10, 17, 'wi'); s.set(9, 19, 'o')
    return s


def blood_wormwood(stage):
    """Krwawy piołun (piołun + krwawa róża, tylko w pełnię): czerwone pióra z rozżarzonymi kwiatkami."""
    return recolor(wormwood(stage), TO_BLOOD)


HYBRIDS = {
    'moonpoppy': (moon_poppy, 'night'), 'ashwheat': (ash_wheat, 'day'), 'wolfcarrot': (wolf_carrot, 'day'),
    'screamgarlic': (scream_garlic, 'night'), 'bloodwormwood': (blood_wormwood, 'night'),
}


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


# ---------------- ryba wyskakująca z wody (animacja otoczenia) ----------------
def fish_jump(up):
    """Mała srebrnoszara ryba w skoku: up = nos w górę (wyskok), inaczej nos w dół (powrót do wody). Patrzy w prawo."""
    s = Sprite(14, 10, 7, 5)
    ang = -0.55 if up else 0.55
    s.blob(7, 5, 4.2, 2.0, ['b1', 'b2', 'b3', 'fo'], ang=ang)
    dy = 2 if up else -2                                   # ogon po przeciwnej stronie niż nos
    s.set(2, 5 + dy, 'b2'); s.set(1, 5 + dy - 1, 'b2'); s.set(1, 5 + dy + 1, 'b2'); s.set(2, 5 + dy - 1, 'b1')
    s.set(8, 4 if up else 6, 'b0')                         # płetwa
    s.outline()
    s.set(10, 3 if up else 6, 'o')                         # oko
    return s


# ---------------- skład (nadwyżka drewna i kamienia na monety) ----------------
def storehouse():
    """Skład: stos polan, kupka kamieni i ważąca je waga z mosiężnymi szalami. 16 px szerokości (kafelek)."""
    s = Sprite(16, 22, 8, 20)
    for i, (y0, x0, n) in enumerate(((17, 1, 3), (14, 2, 2), (11, 3, 1))):   # polana (czoła słojów)
        for k in range(n):
            cx, cy = x0 + k * 3 + 1, y0 + 1
            s.blob(cx, cy, 1.6, 1.6, ['e1', 'e2', 'e3'])
            s.set(cx, cy, 'e1')
    s.blob(12.5, 18, 2.6, 1.8, ['p1', 'b1', 'b3']); s.blob(10.5, 18.5, 1.8, 1.4, ['p1', 'b1', 'b2'])
    s.blob(12, 15.8, 1.8, 1.4, ['p1', 'b2', 'b3'])
    s.rect(12, 5, 12, 13, 'e1')                                       # słupek wagi
    s.rect(8, 5, 15, 5, 'go'); s.set(12, 4, 'go')                     # belka
    for x, y in ((8, 6), (8, 7), (15, 6), (15, 7)):
        s.set(x, y, 'e1')
    s.rect(7, 8, 9, 8, 'go'); s.rect(14, 8, 15, 8, 'go')              # szale
    s.set(8, 7, 'e2'); s.set(12, 14, 'e0')
    return s.outline()


# ---------------- ikony nasion (do ui_kit.SEED_SYMBOLS) ----------------
SEED_SYMBOLS = {
    'garlic': [".m.m.", "..m..", ".www.", "wwzww", ".www."],
    'poppy': [".c.c.", "c1c1c", ".c.c.", "..m..", ".mm.."],
    'wormwood': ["s.z.s", ".szs.", "..s..", ".sms.", "..m.."],
    'mandrake': [".m.m.", "..m..", ".jhj.", "jWhWj", ".h.h."],
    'belladonna': [".1U1.", "1UMU1", ".1U1.", "..k..", ".kk.."],
    'graveivy': ["..W..", ".WhW.", "..h..", ".WhW.", "..h.."],
    'moonpoppy': [".z.z.", "zWzWz", ".z.z.", "..d..", ".dd.."],
    'ashwheat': ["..X..", ".sXs.", "..s..", ".sss.", "..b.."],
    'wolfcarrot': ["...MU", "..3U.", ".33..", "32...", "2...."],
    'screamgarlic': [".d.d.", "..d..", ".www.", "wWoWw", ".www."],
    'bloodwormwood': ["3.X.3", ".3c3.", "..3..", ".3k3.", "..k.."],
}


# ---------------- ikony UI (Etap 6b+): kategorie koła nasion, wyjście, zasypanie grządki ----------------
ICON_MAPS = {
    # losowe sadzenie (kostka) i prestiż (perła mgły)
    'dice': [".wwwwwwwz.", ".w1www1wz.", ".wwwwwwwz.", ".www1wwwz.", ".wwwwwwwz.",
             ".w1www1wz.", ".wwwwwwwz.", ".zzzzzzzz.", "..........", ".........."],
    'pearl': ["..........", "...zzzz...", "..zLwwzz..", ".zLwwwzzs.", ".zwwwzzzs.",
              ".zwwzzzss.", "..zzzzss..", "...ssss...", "..........", ".........."],
    'sun': ["....F.....", ".F.....F..", "...XXX....", "..XFFFX...", "F.XFLFX.F.",
            "..XFFFX...", "...XXX....", ".F.....F..", "....F.....", ".........."],
    'moon': ["..sss.....", ".szzs.....", "szz.......", "sz........", "sz.....L..",
             "szz.......", ".szzzzs...", "..ssss....", "..........", ".........."],
    'exit': [".hhhhh....", ".hjjjh....", ".hj.jh....", ".hj.jh.w..", ".hjgjhwww.",
             ".hj.jh.w..", ".hj.jh....", ".hjjjh....", ".hhhhh....", ".........."],
    'fill': [".......jj.", "......jj..", ".....jj...", "....jj....", "..sss.....",
             ".szzs.....", ".szs......", "..s.......", "hhhhhhh...", ".........."],
    # wskaźnik pracy w pasku HUD: klawiatura i mysz (zapalają się, gdy gra widzi input)
    'keys': ["..........", "..........", "hhhhhhhhh.", "hjhjhjhjh.", "hhhhhhhhh.",
             "hjhjhjhjh.", "hhhhhhhhh.", "hhjjjjjhh.", "hhhhhhhhh.", ".........."],
    'mouse': ["...hhh....", "..hjhjh...", ".hjjhjjh..", ".hjjhjjh..", ".hhhhhhh..",
              ".hjjjjjh..", ".hjjjjjh..", "..hjjjh...", "...hhh....", ".........."],
    # drzewko ulepszeń: lekkie skrzydła, pojemne skrzynie, nocna rosa
    'wing': ["..........", "......sss.", "....sszzs.", "...szzzs..", "..szzzs...",
             ".szzss....", "szzs......", "sss.......", "..........", ".........."],
    'crate': ["..........", ".hhhhhhhh.", ".hjjjjjjh.", ".hhhhhhhh.", ".hjhjjhjh.",
              ".hjjhhjjh.", ".hjhjjhjh.", ".hhhhhhhh.", "..........", ".........."],
    'drop': ["....W.....", "....W.....", "...WLW....", "...WLW....", "..WLLLW...",
             "..WLLLW...", "..WWLWW...", "...WWW....", "..........", ".........."],
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


# ---------- kocioł alchemiczny (po Etapie 6d) ----------
def cauldron(state='idle', f=0):
    """Żeliwny kocioł na trzech nóżkach, 16 px szerokości z obrysem. state: idle (zimny, ciemny wywar),
    brew (ogień pod spodem, turkusowy wywar z bąblami, klatki 0-2), ready (fioletowy, świecący wywar, klatki 0-1)."""
    s = Sprite(16, 20, 8, 18)
    for lx in (3, 12):                                     # nóżki
        for y in (15, 16, 17):
            s.set(lx, y, 'p0')
    if state == 'brew':                                    # ogień pod kotłem (emisyjny)
        base = [(5, 17), (6, 17), (7, 17), (8, 17), (9, 17), (10, 17)]
        tips = [((5, 16), (7, 15), (8, 16), (10, 15), (7, 14)), ((6, 16), (6, 15), (9, 16), (9, 15), (8, 14)),
                ((5, 15), (7, 16), (8, 15), (10, 16), (9, 14))][f % 3]
        for x, y in base:
            s.set(x, y, 'fx')
        for x, y in tips:
            s.set(x, y, 'fl' if y < 16 else 'fx')
    s.blob(8, 10, 6.6, 4.3, ['o', 'p0', 'p1'])             # brzuch kotła
    for x in range(2, 14):                                 # rant
        s.set(x, 5, 'p1'); s.set(x, 6, 'p0')
    s.set(2, 5, 'p0'); s.set(13, 5, 'p0'); s.set(4, 5, 'b3'); s.set(5, 5, 'b3')
    s.set(4, 9, 'b2'); s.set(4, 10, 'b2'); s.set(5, 8, 'b2')     # odblask na żeliwie
    liquid = {'idle': ('g1', 'g2', None), 'brew': ('wi', 'wl', 'wl'), 'ready': ('mu', 'ml', 'ml')}[state]
    for x in range(3, 13):                                 # powierzchnia wywaru
        s.set(x, 4, liquid[0])
    s.set(5, 4, liquid[1]); s.set(9, 4, liquid[1])
    if state == 'brew':                                    # bąble nad wywarem
        bubbles = [((5, 3), (9, 2)), ((6, 2), (10, 3), (8, 1)), ((4, 3), (8, 3), (7, 0))][f % 3]
        for x, y in bubbles:
            s.set(x, y, liquid[2])
    if state == 'ready':                                   # świecąca para
        for x, y in [((6, 2), (9, 1), (7, 0)), ((7, 2), (8, 1), (10, 0))][f % 2]:
            s.set(x, y, 'ml')
    return s.outline()


# ---------- chowańce (po Etapie 6d): kot, kruk, ropucha, nietoperz ----------
CAT_POSES = ('sit0', 'sit1', 'walk0', 'walk1', 'sleep')


def cat(pose):
    """Czarny kot ze świecącymi oczami, patrzy w prawo (lewo: flipX w kodzie)."""
    s = Sprite(12, 10, 6, 9)
    body = ['o', 'p0', 'p1']
    if pose == 'sleep':
        s.blob(6, 7, 4.2, 2.2, body)                          # zwinięty w kłębek
        s.line(2, 8, 5, 9, 'p0'); s.set(9, 6, 'p0'); s.set(10, 5, 'p0')   # ogon, ucho
        s.outline()
        s.set(8, 7, 'p2'); s.set(9, 7, 'p2')                  # zamknięte oczy
        return s
    if pose.startswith('sit'):
        s.blob(5.5, 6.5, 2.6, 2.6, body)                      # tułów
        s.blob(7.5, 3.2, 2.0, 1.8, body)                      # głowa
        s.set(6, 1, 'p0'); s.set(9, 1, 'p0')                  # uszy
        tail = [(3, 8), (2, 7), (1, 6), (1, 5)] if pose == 'sit0' else [(3, 8), (2, 8), (1, 7), (0, 7)]
        for x, y in tail:
            s.set(x, y, 'p0')
        s.set(5, 9, 'p0'); s.set(7, 9, 'p0')                  # łapki
        s.outline()
        s.set(7, 3, 'fl'); s.set(9, 3, 'fl')                  # oczy
        return s
    # chód: tułów poziomo, łapki na przemian
    s.blob(5.5, 6, 3.6, 1.8, body)
    s.blob(9, 4, 1.8, 1.7, body)
    s.set(8, 2, 'p0'); s.set(10, 2, 'p0')
    for x, y in [(1, 4), (1, 5), (2, 5)]:
        s.set(x, y, 'p0')
    legs = [(3, 8), (7, 8)] if pose == 'walk0' else [(4, 8), (8, 8)]
    for x, y in legs:
        s.set(x, y, 'p0'); s.set(x, y + 1, 'p0')
    s.outline()
    s.set(9, 4, 'fl'); s.set(10, 4, 'fl')
    return s


def raven(pose):
    """Kruk-chowaniec: jak wrona, ale z fioletowym, świecącym okiem i srebrnym piórkiem."""
    s = bird('crow', pose)
    for p, k in list(s.px.items()):
        if k == 'fl':
            s.px[p] = 'ml'
    s.set(5, 6 if not pose.startswith('fly') else 5, 'b3')
    return s


TOAD_POSES = ('sit', 'jump')


def toad(pose):
    """Ropucha z brodawkami i złotymi oczami."""
    s = Sprite(12, 9, 6, 8)
    ramp = ['g0', 'g1', 'e2']
    if pose == 'sit':
        s.blob(6, 5.5, 4.2, 2.6, ramp)
        s.set(3, 8, 'g0'); s.set(9, 8, 'g0')
    else:
        s.blob(6, 4, 3.6, 2.4, ramp)
        s.line(2, 6, 1, 8, 'g0'); s.line(10, 6, 11, 8, 'g0')    # wyprostowane łapy
    for x, y in [(5, 5), (7, 6), (4, 6)]:
        s.set(x, y if pose == 'sit' else y - 1, 'g3')          # brodawki
    s.outline()
    ey = 3 if pose == 'sit' else 2
    s.set(4, ey, 'go'); s.set(8, ey, 'go')
    return s


BAT_POSES = ('fly0', 'fly1', 'hang')


def bat(pose):
    """Nietoperz z czerwonymi oczkami (emisyjne). hang: wisi głową w dół."""
    s = Sprite(12, 8, 6, 7)
    if pose == 'hang':
        s.blob(6, 3.5, 1.6, 2.6, ['o', 'p0', 'p1'])
        s.set(5, 0, 'p0'); s.set(7, 0, 'p0')                   # pazurki u góry
        s.outline()
        s.set(5, 5, 'fx'); s.set(7, 5, 'fx')
        return s
    s.blob(6, 4, 1.6, 1.6, ['o', 'p0', 'p1'])
    if pose == 'fly0':
        wing = [(4, 3), (3, 2), (2, 1), (1, 2), (8, 3), (9, 2), (10, 1), (11, 2), (3, 3), (9, 3)]
    else:
        wing = [(4, 4), (3, 5), (2, 6), (1, 5), (8, 4), (9, 5), (10, 6), (11, 5), (3, 4), (9, 4)]
    for x, y in wing:
        s.set(x, y, 'p0')
    s.set(5, 2, 'p0'); s.set(7, 2, 'p0')                       # uszy
    s.outline()
    s.set(5, 4, 'fx'); s.set(7, 4, 'fx')
    return s


# ---------- rój ogników: gniazdo i ogniki w kolorach zawodów ----------
# nowe kolory emisyjne (świecą jak płomień ognika)
from sprites3 import RGB, EMISSIVE  # noqa: E402
for _k, _hex in (('gn', '8fd16a'), ('gN', 'd6ffb0'), ('mL', 'f2dcff'), ('fL', 'fff6c8'), ('cW', 'dfe8ff'), ('wW', 'ffffff')):
    RGB[_k] = tuple(int(_hex[i:i + 2], 16) for i in (0, 2, 4))
    EMISSIVE.add(_k)

WISP_KINDS = {
    'gardener': ('wi', 'wl'),      # turkus: zbiera plony
    'sower': ('gn', 'gN'),         # zieleń: sadzi
    'woodcutter': ('fx', 'fl'),    # pomarańcz: drewno i kamień
    'alchemist': ('ml', 'mL'),     # fiolet: kocioł
    'lamplighter': ('fl', 'fL'),   # złoto: lampy
    'gatherer': ('cW', 'wW'),      # biel: gwiazdy, owoce, błyskotki
}


def wisp_kind(kind, f):
    """Ognik w kolorze zawodu (ten sam kształt co ognik-ogrodnik, płomień w innym kolorze)."""
    body, light = WISP_KINDS[kind]
    s = S3.wisp_gardener(f, bob=False)
    for p, k in list(s.px.items()):
        if k == 'wi':
            s.px[p] = body
        elif k == 'wl':
            s.px[p] = light
    return s


def wisp_nest(f=0):
    """Gniazdo ogników: wypróchniały pień z trzema świecącymi dziuplami, 16 px szerokości z obrysem."""
    s = Sprite(16, 22, 8, 20)
    s.blob(8, 13, 5.6, 7.2, ['e0', 'e1', 'e2'])            # pień
    for x in range(3, 14):                                 # postrzępiony wierzch
        if x % 3 != 1:
            s.set(x, 6, 'e2')
    s.set(2, 19, 'e1'); s.set(1, 20, 'e0'); s.set(13, 19, 'e1'); s.set(14, 20, 'e0')   # korzenie
    for y in range(8, 19, 3):                              # słoje kory
        s.set(5, y, 'e3'); s.set(11, y + 1, 'e3')
    s.outline()
    glow = [('wi', 'wl'), ('wl', 'wi')][f % 2]
    for i, (x, y) in enumerate(((7, 10), (10, 14), (6, 16))):   # dziuple z blaskiem
        s.set(x, y, 'o'); s.set(x + 1, y, glow[i % 2]); s.set(x, y + 1, glow[(i + 1) % 2])
    s.set(9, 4, 'wl')                                      # iskierka nad gniazdem
    return s
