"""Drzewa owocowe (§15): 5 gatunków x 4 fazy (sadzonka, młode, dorosłe, owocujące), ikony owoców, ikona sadzonki."""
import math, random
import sprites3 as S3          # paleta mroczna + emisyjna
from sprites import Sprite, EARTH
import ui_kit as K
import sprites

# dodatkowe kolory dla drzew: jaśniejsze, nasłonecznione liście i odblask owoców
for k, v in {'g4': "#7a9564", 'ro': "#d9725a"}.items():
    sprites.PAL[k] = v
    sprites.RGB[k] = tuple(int(v[i:i + 2], 16) for i in (1, 3, 5))

SPECIES = {
    # trunk: rampa kory, leaf: rampa liści, shape: kształt korony, fruit: (kolory owocu: ciemny, środek, światło), n: liczba owoców
    'apple':     dict(name="Jabłoń", trunk=['e0', 'e1', 'e2', 'e3'], leaf=['g0', 'g1', 'g2', 'g3', 'g4'], shape='round',
                      fruit=('p2', 'ro', 'or'), n=7),
    'elder':     dict(name="Czarny bez", trunk=['e0', 'e1', 'e2', 'e2'], leaf=['g0', 'g1', 'g2', 'g3'], shape='bush',
                      fruit=('o', 'p2', 'mu'), n=9),
    'ghostpear': dict(name="Upiorna grusza", trunk=['b1', 'b3', 'fo', 'fo'], leaf=['p0', 'b1', 'b2', 'b3', 'fo'], shape='oval',
                      fruit=('b2', 'wi', 'wl'), n=6),
    'rowan':     dict(name="Krwawa jarzębina", trunk=['e0', 'e1', 'e2', 'e3'], leaf=['g0', 'g1', 'p2', 'co', 'or'], shape='airy',
                      fruit=('p2', 'ro', 'or'), n=8),
    'moon':      dict(name="Drzewo księżyca", trunk=['b1', 'b3', 'fo', 'fo'], leaf=['p0', 'p1', 'mu', 'fo', 'cr'], shape='weep',
                      fruit=('fo', 'wl', 'wl'), n=6),
}


def _trunk(s, sp, x0, y0, y1, w):
    r = sp['trunk']
    for y in range(y0, y1 + 1):
        for i in range(w):
            k = r[3] if i == 0 else (r[2] if i < w - 1 else r[1])
            if w > 2 and i == w - 1: k = r[0]
            s.set(x0 + i, y, k)


def _canopy(s, sp, cx, cy, scale):
    L = sp['leaf']
    sh = sp['shape']
    if sh == 'round':
        blobs = [(-7, 6, 7, 6), (7, 6, 7, 6), (0, -1, 10, 9), (0, 9, 8, 5)]
    elif sh == 'bush':
        blobs = [(-9, 4, 6, 5), (9, 4, 6, 5), (-3, 0, 8, 6), (5, -1, 7, 6), (0, 6, 9, 4)]
    elif sh == 'oval':
        blobs = [(0, -2, 6, 11), (-4, 4, 4, 6), (4, 3, 4, 6)]
    elif sh == 'airy':
        blobs = [(-7, 2, 4, 3.5), (6, 1, 4, 3.5), (-2, -5, 5, 4), (4, -7, 3.5, 3), (-5, 7, 4, 3), (5, 7, 4, 3), (0, 1, 4, 4)]
    else:  # weep - mała korona, długie zwisające gałązki
        blobs = [(0, -4, 8, 5), (-5, -1, 5, 4), (5, -1, 5, 4)]
    for dx, dy, rx, ry in blobs:
        s.blob(cx + dx * scale, cy + dy * scale, max(1.5, rx * scale), max(1.5, ry * scale), L)
    if sh == 'weep':                                     # zwisające gałązki jak u wierzby płaczącej
        for i, x0 in enumerate(range(int(cx - 12 * scale), int(cx + 12 * scale) + 1, 2)):
            if abs(x0 - cx) < 2 * scale: continue
            ln = int((10 + (i * 7) % 9) * scale)
            out = 1 if x0 > cx else -1
            y0 = int(cy - 1 * scale)
            for j in range(ln):
                x = x0 + out * (j // max(1, int(6 * scale)))
                s.set(x, y0 + j, L[3] if j % 4 == 0 else (L[2] if j % 2 else L[1]))


def _fruits(s, sp, cx, cy, scale, seed):
    rnd = random.Random(seed)
    dark, mid, light = sp['fruit']
    cand = [p for p, k in s.px.items() if k in sp['leaf'] and abs(p[0] - cx) < 11 * scale and abs(p[1] - cy) < 9 * scale]
    rnd.shuffle(cand)
    placed = []
    for (x, y) in cand:
        if len(placed) >= sp['n']: break
        if all(abs(x - a) + abs(y - b) > 5 for a, b in placed):
            placed.append((x, y))
            if sp['shape'] in ('bush', 'airy'):             # grona drobnych jagód
                for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
                    s.set(x + dx, y + dy, mid if (dx + dy) % 2 else dark)
                s.set(x, y, light)
            else:
                for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)): s.set(x + dx, y + dy, mid)
                s.set(x, y, light); s.set(x + 1, y + 2, dark)


def tree(species, stage, seed=1):
    """stage: 0 sadzonka, 1 młode, 2 dorosłe, 3 owocujące."""
    sp = SPECIES[species]
    if stage == 0:
        s = Sprite(14, 16, 7, 14)
        _trunk(s, sp, 6, 6, 13, 1)
        L = sp['leaf']
        s.blob(4.5, 6, 2, 1.3, L, ang=-0.5); s.blob(9.5, 5, 2, 1.3, L, ang=0.5); s.blob(7, 3, 1.5, 2, L)
        s.outline()
        s.blob(7, 14, 3.5, 1.5, EARTH, clip_y=14)
        return s
    if stage == 1:
        s = Sprite(24, 30, 12, 28)
        _trunk(s, sp, 11, 16, 27, 2)
        _canopy(s, sp, 12, 11, 0.55)
        s.outline()
        return s
    s = Sprite(34, 42, 17, 40)
    tw = 2 if sp['shape'] in ('airy', 'oval') else 3
    if sp['shape'] == 'bush':                         # kilka rozchodzących się pni
        r = sp['trunk']
        for x1, y1 in ((10, 20), (17, 18), (24, 20)):
            s.line(17, 39, x1, y1, r[2]); s.line(18, 39, x1 + 1, y1, r[1])
        s.rect(15, 36, 19, 39, r[1]); s.rect(15, 36, 15, 39, r[2])
    else:
        _trunk(s, sp, 17 - tw // 2, 22, 39, tw)
        s.set(17 - tw // 2 - 1, 39, sp['trunk'][2]); s.set(17 + tw - tw // 2, 39, sp['trunk'][0])
    _canopy(s, sp, 17, 14, 1.0)
    if stage == 3:
        _fruits(s, sp, 17, 14, 1.0, seed)
    return s.outline()


# ---------------------------------------------------------------- ikony
FRUIT_ICONS = {
    'apple': ["....fm....", "...fmm....", "..cccrc...", ".ccrrccc..", ".crrcccc..", ".ccccccc..",
              ".ccccccc..", "..ccccc...", "...ccc....", ".........."],
    'elder': ["...m......", "..mmf.....", "...f......", "..f.f.....", ".2U.2U....", "2U12U12U..",
              ".2U12U1...", "..2U12U...", "...2U1....", ".........."],
    'ghostpear': ["....f.....", "....f.....", "...zWz....", "...WLW....", "..zWWWz...", "..WWLWWz..",
                  ".zWWWWWz..", ".zWWWWWz..", "..zWWWz...", "...zzz...."],
    'rowan': ["....m.....", "...mmf....", "....f.....", "..f...f...", ".cr.cr.cr.", "crccrccrc.",
              ".cc.cc.cc.", "..cr.cr...", "..cc.cc...", ".........."],
    'moon': ["..........", "...LLL....", "..LwwL....", ".LwL......", ".Lw.......", ".Lw.......",
             ".LwL....L.", "..LwwLLwL.", "...LLLLL..", ".........."],
}
SAPLING_ICON = ["....m.....", "..mmm.mm..", "...mmmm...", "....fm....", "....f.....",
                ".hhhhhhh..", "..hjjjh...", "..hjjjh...", "...hhh....", ".........."]


def fruit_icon(species):
    return K.from_map(FRUIT_ICONS[species])


def sapling_icon():
    return K.from_map(SAPLING_ICON)


# Świecące owoce rozjaśniają okolicę w nocy (jak mała lampa): (kolor RGB-mnożnik, promień px, przesunięcie od zaczepienia)
TREE_LIGHT = {
    'ghostpear': ((0.45, 1.15, 1.05), 34, (0, -24)),
    'moon': ((0.9, 0.85, 1.25), 40, (0, -20)),
}
