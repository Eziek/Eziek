"""Chowańce (GDD §9 p. 102): kot, kruk, ropucha, nietoperz (narysowane od nowa, więcej póz) i nowe: ćma księżycowa,
sowa, ślimak, lis-widmo. Wszystkie patrzą w prawo (lewo: flipX w kodzie), punkt zaczepienia u dołu w środku.
Kształty z elips z cieniowaniem (światło z lewej góry), jaśniejszy brzeg u góry, żeby ciemne zwierzęta było widać
na ciemnej trawie, i obrys. Piksele emisyjne (oczy, plamki ćmy, ogon lisa) idą do *_glow.png.

POSES: kolejność póz każdego chowańca (taka sama w grze: Familiars.FamiliarDefs).
Podgląd: python3 familiars.py  -> docs/img/chowance.png
"""
import os, math
from sprites import Sprite
from sprites3 import RGB, EMISSIVE
from PIL import Image

# kolory emisyjne dodane w sprites6 (gn, gN, mL, fL, cW, wW) - ładujemy je, gdy moduł jest używany osobno
for _k, _hex in (('gn', '8fd16a'), ('gN', 'd6ffb0'), ('mL', 'f2dcff'), ('fL', 'fff6c8'), ('cW', 'dfe8ff'), ('wW', 'ffffff')):
    RGB.setdefault(_k, tuple(int(_hex[i:i + 2], 16) for i in (0, 2, 4)))
    EMISSIVE.add(_k)

BLACK = ['p0', 'p1', 'p2']           # czarna sierść z fioletowym połyskiem
FEATHER = ['p0', 'p1', 'b2']         # kruk: granatowe pióra
TOAD = ['g0', 'g1', 'g2', 'g3']
BAT = ['p0', 'p1', 'p2']
OWL = ['e1', 'e2', 'e3', 'go']
SHELL = ['e1', 'e2', 'e3', 'go']
SLIME = ['b1', 'b2', 'b3', 'fo']
GHOST = ['b1', 'b2', 'b3', 'cW']     # lis-widmo: niebieskawa mgła, najjaśniejszy ton świeci
MOTH = ['p1', 'p2', 'mu', 'b3', 'fo']


def rim(s, k, skip=('o',)):
    """Jaśniejszy brzeg: piksel kształtu bez sąsiada nad sobą dostaje kolor k (przed obrysem)."""
    for (x, y), v in list(s.px.items()):
        if v in skip or v in EMISSIVE:
            continue
        if (x, y - 1) not in s.px:
            s.px[(x, y)] = k


def pts(s, points, k):
    for x, y in points:
        s.set(x, y, k)


# ---------------------------------------------------------------- kot
CAT_POSES = ('sit0', 'sit1', 'blink', 'walk0', 'walk1', 'walk2', 'walk3', 'sleep0', 'sleep1', 'groom', 'stretch', 'pounce')


def cat(pose):
    s = Sprite(17, 14, 8, 13)
    if pose.startswith('sleep'):
        breathe = 0.25 if pose == 'sleep1' else 0.0
        s.blob(7.5, 10.3 - breathe, 4.8, 2.6 + breathe, BLACK)           # kłębek
        s.blob(12.2, 9.6, 2.4, 2.1, BLACK)                               # głowa wtulona
        pts(s, [(11, 7), (11, 6), (12, 7), (14, 7), (14, 6), (13, 7)], 'p1')   # uszy
        pts(s, [(3, 13), (4, 13), (5, 13), (6, 13), (7, 13), (8, 13), (9, 13), (10, 12)], 'p1')   # ogon wokół łapek
        rim(s, 'p2')
        s.outline()
        pts(s, [(11, 10), (12, 10)], 'p2')                               # zamknięte oko
        pts(s, [(4, 9), (5, 8), (6, 8)], 'p2')                           # połysk grzbietu
        return s
    if pose in ('sit0', 'sit1', 'blink', 'groom'):
        s.blob(7.5, 9.5, 3.6, 3.4, BLACK)                                # tułów
        hy = 6.2 if pose == 'groom' else 4.6
        hx = 10.5 if pose == 'groom' else 9.5
        s.blob(hx, hy, 2.8, 2.5, BLACK)                                  # głowa (przodem)
        ex = int(hx)
        top = int(hy - 2.4)
        pts(s, [(ex - 2, top), (ex - 2, top - 1), (ex - 1, top)], 'p1')  # lewe ucho
        pts(s, [(ex + 2, top), (ex + 2, top - 1), (ex + 1, top)], 'p1')  # prawe ucho
        pts(s, [(9, 12), (9, 13), (11, 12), (11, 13)], 'p1')             # przednie łapy
        if pose == 'groom':
            pts(s, [(12, 9), (12, 8), (11, 8)], 'p1')                    # uniesiona łapka przy pyszczku
        tail = [(4, 12), (3, 12), (2, 11), (1, 10), (1, 9), (2, 8)] if pose != 'sit1' else \
               [(4, 12), (3, 12), (2, 12), (1, 11), (0, 10), (0, 9)]
        pts(s, tail, 'p1')
        rim(s, 'p2')
        s.outline()
        ey = int(hy)
        if pose == 'blink' or pose == 'groom':
            pts(s, [(ex - 1, ey), (ex + 1, ey)], 'p2')
        else:
            pts(s, [(ex - 1, ey), (ex + 1, ey)], 'fl')                   # oczy
        s.set(ex, ey + 1, 'co')                                          # nosek
        if pose == 'groom':
            s.set(ex + 1, ey + 2, 'co')                                  # język
        return s
    if pose == 'stretch':
        s.blob(8, 10.5, 5.2, 1.8, BLACK, ang=0.25)                       # tułów nisko z przodu
        s.blob(13, 10.5, 2.2, 1.9, BLACK)                                # głowa przy ziemi
        pts(s, [(12, 8), (14, 8)], 'p1')
        pts(s, [(14, 13), (15, 13), (12, 13)], 'p1')                     # wyciągnięte łapy
        pts(s, [(4, 12), (4, 13), (5, 13)], 'p1')                        # tylne łapy
        pts(s, [(3, 8), (2, 7), (2, 6), (3, 5)], 'p1')                   # ogon w górę
        rim(s, 'p2')
        s.outline()
        s.set(14, 10, 'p2')                                              # zmrużone oko
        return s
    # chód i skok: tułów poziomo, głowa z boku
    up = 2 if pose == 'pounce' else 0
    s.blob(7.5, 9 - up, 4.8, 2.3, BLACK)
    s.blob(12.3, 6.5 - up, 2.4, 2.2, BLACK)
    pts(s, [(11, 4 - up), (11, 3 - up), (13, 4 - up), (13, 3 - up), (14, 4 - up)], 'p1')   # uszy
    tail = [(3, 8), (2, 7), (1, 6), (1, 5), (2, 4)] if pose != 'pounce' else [(3, 7), (2, 7), (1, 7), (0, 6)]
    pts(s, tail, 'p1')
    if pose == 'pounce':
        pts(s, [(12, 9), (13, 10), (14, 10), (4, 9), (3, 10), (2, 11)], 'p1')   # łapy wyciągnięte w skoku
    else:
        legs = {'walk0': [(4, 12), (10, 12)], 'walk1': [(5, 12), (9, 12)],
                'walk2': [(6, 12), (8, 12)], 'walk3': [(5, 12), (9, 12)]}[pose]
        for x, y in legs:
            pts(s, [(x, y - 1), (x, y), (x, y + 1)], 'p1')
        back = {'walk0': [(9, 13), (5, 13)], 'walk1': [(10, 13), (4, 13)],
                'walk2': [(11, 13), (3, 13)], 'walk3': [(10, 13), (4, 13)]}[pose]
        for x, y in back:
            pts(s, [(x, y - 2), (x, y - 1), (x, y)], 'p0')
    rim(s, 'p2')
    s.outline()
    pts(s, [(13, 6 - up)], 'fl')                                         # oko z profilu
    s.set(14, 7 - up, 'co')
    return s


# ---------------------------------------------------------------- kruk
RAVEN_POSES = ('sit0', 'sit1', 'peck', 'caw', 'fly0', 'fly1', 'fly2')


def raven(pose):
    s = Sprite(15, 13, 7, 12)
    if pose.startswith('fly'):
        s.blob(7, 7, 3.6, 2.0, FEATHER)
        s.blob(11, 5.8, 1.9, 1.7, FEATHER)
        wing = {'fly0': (6, 3.4, 1.4, 3.0, 0.5), 'fly1': (5, 5.6, 3.8, 1.2, 0.15), 'fly2': (6, 9.6, 1.4, 2.6, -0.5)}[pose]
        wx, wy, wrx, wry, wa = wing
        s.blob(wx, wy, wrx, wry, FEATHER, ang=wa)                        # skrzydło
        pts(s, [(wx - 1, int(wy)), (wx - 2, int(wy) + (1 if pose == 'fly2' else -1 if pose == 'fly0' else 0))], 'p0')   # lotki
        pts(s, [(2, 7), (1, 8), (2, 8)], 'p1')                           # ogon
        rim(s, 'b2')
        s.outline()
        pts(s, [(13, 6), (14, 6)], 'fo')                                 # dziób
        s.set(11, 5, 'ml')                                               # oko
        return s
    peck = pose == 'peck'
    caw = pose == 'caw'
    s.blob(6.5, 8, 3.2, 2.7, FEATHER, ang=-0.35)                         # tułów
    hx, hy = (10.5, 9.0) if peck else (9.8, 4.3 if caw else 5.0)
    s.blob(hx, hy, 2.0, 1.8, FEATHER)                                    # głowa
    tail = [(3, 10), (2, 11), (1, 11)] if pose != 'sit1' else [(3, 10), (2, 10), (1, 9)]
    pts(s, tail, 'p1')
    pts(s, [(6, 11), (6, 12), (8, 11), (8, 12)], 'fo')                   # nóżki
    rim(s, 'b2')
    s.outline()
    bx, by = int(hx) + 2, int(hy)
    if caw:
        pts(s, [(bx, by - 1), (bx + 1, by - 2), (bx, by + 1), (bx + 1, by + 2)], 'fo')   # otwarty dziób
    else:
        pts(s, [(bx, by), (bx + 1, by)], 'fo')
    s.set(int(hx), int(hy) - 1, 'ml')                                    # fioletowe oko
    s.set(5, 7, 'b3')                                                    # srebrne piórko
    return s


# ---------------------------------------------------------------- ropucha
TOAD_POSES = ('sit', 'blink', 'croak', 'crouch', 'jump', 'tongue')


def toad(pose):
    s = Sprite(15, 11, 7, 10)
    if pose == 'jump':
        s.blob(7.5, 5, 4.0, 2.4, TOAD, ang=-0.2)
        pts(s, [(3, 7), (2, 8), (1, 9), (11, 7), (12, 8), (13, 9)], 'g1')   # wyprostowane łapy
    else:
        low = 0.6 if pose == 'crouch' else 0.0
        s.blob(7.5, 6.8 + low, 4.6, 3.0 - low, TOAD)
        pts(s, [(3, 10), (4, 10), (11, 10), (12, 10)], 'g1')             # łapy
        if pose == 'croak':
            s.blob(10.5, 8.6, 1.9, 1.5, ['cr', 'cr', 'fo'])              # nadęty worek
    for x, y in [(5, 6), (7, 7), (4, 8), (8, 5), (6, 8)]:
        s.set(x, y - (2 if pose == 'jump' else 0), 'e3')                 # brodawki
    rim(s, 'g3')
    s.outline()
    ey = 2 if pose == 'jump' else 4
    if pose == 'blink':
        pts(s, [(6, ey), (10, ey)], 'g2')
    else:
        pts(s, [(6, ey), (10, ey)], 'go')                                # złote oczy
        pts(s, [(6, ey - 1), (10, ey - 1)], 'g2')
    if pose == 'tongue':
        pts(s, [(12, 6), (13, 5), (14, 4)], 'co')                        # język po świetlika
    return s


# ---------------------------------------------------------------- nietoperz
BAT_POSES = ('fly0', 'fly1', 'fly2', 'hang0', 'hang1')


def bat(pose):
    s = Sprite(17, 11, 8, 10)
    if pose.startswith('hang'):
        sway = 1 if pose == 'hang1' else 0
        s.blob(8 + sway * 0.5, 5.5, 2.0, 3.4, BAT)
        pts(s, [(7, 1), (9, 1)], 'p1')                                   # pazurki
        pts(s, [(6, 3), (6, 4), (6, 5), (10, 3), (10, 4), (10, 5)], 'p1')   # skrzydła owinięte
        rim(s, 'p2')
        s.outline()
        pts(s, [(7 + sway, 7), (9 + sway, 7)], 'fx')                     # oczy w dół
        return s
    s.blob(8.5, 5.5, 1.9, 2.0, BAT)
    pts(s, [(7, 3), (7, 2), (10, 3), (10, 2)], 'p1')                     # uszy
    wing = {'fly0': [(6, 4), (5, 3), (4, 2), (3, 1), (2, 2), (1, 3), (5, 4), (4, 3), (3, 3), (2, 4),
                     (11, 4), (12, 3), (13, 2), (14, 1), (15, 2), (16, 3), (12, 4), (13, 3), (14, 3), (15, 4)],
            'fly1': [(6, 5), (5, 5), (4, 5), (3, 5), (2, 5), (1, 6), (3, 6), (5, 6),
                     (11, 5), (12, 5), (13, 5), (14, 5), (15, 5), (16, 6), (14, 6), (12, 6)],
            'fly2': [(6, 6), (5, 7), (4, 8), (3, 8), (2, 7), (5, 8), (4, 7),
                     (11, 6), (12, 7), (13, 8), (14, 8), (15, 7), (12, 8), (13, 7)]}[pose]
    pts(s, wing, 'p1')
    rim(s, 'p2')
    s.outline()
    pts(s, [(8, 5), (10, 5)], 'fx')
    return s


# ---------------------------------------------------------------- ćma księżycowa
MOTH_POSES = ('fly0', 'fly1', 'fly2', 'rest')


def moth(pose):
    s = Sprite(17, 13, 8, 12)
    if pose == 'rest':
        s.blob(8.5, 8, 4.4, 3.6, MOTH)                                   # skrzydła złożone w daszek
        pts(s, [(8, 3), (7, 2), (6, 1), (10, 3), (11, 2), (12, 1)], 'fo')   # czułki
        rim(s, 'cr')
        s.outline()
        pts(s, [(6, 8), (11, 8)], 'ml')                                  # plamki-oczy
        pts(s, [(7, 9), (10, 9)], 'mL')
        return s
    span = {'fly0': (3.8, 5.0, 4), 'fly1': (5.6, 3.4, 6), 'fly2': (4.0, 2.6, 8)}[pose]
    rx, ry, cy = span
    s.blob(8.5 - rx * 0.75, cy, rx * 0.8, ry, MOTH, ang=0.3)             # lewe skrzydło
    s.blob(8.5 + rx * 0.75, cy, rx * 0.8, ry, MOTH, ang=-0.3)            # prawe skrzydło
    s.blob(8.5, 7, 1.1, 3.0, ['e0', 'e1', 'e2'])                         # tułów (puszysty)
    pts(s, [(8, 3), (7, 2), (6, 1), (9, 3), (10, 2), (11, 1)], 'fo')
    rim(s, 'cr')
    s.outline()
    lx, rx2 = int(8.5 - rx * 0.8), int(8.5 + rx * 0.8)
    pts(s, [(lx, int(cy)), (rx2, int(cy))], 'mL')                        # świecące plamki
    pts(s, [(lx, int(cy) + 1), (rx2, int(cy) + 1)], 'ml')
    return s


# ---------------------------------------------------------------- sowa
OWL_POSES = ('sit', 'blink', 'look', 'hoot', 'fly0', 'fly1')


def owl(pose):
    s = Sprite(15, 16, 7, 15)
    if pose.startswith('fly'):
        up = pose == 'fly0'
        s.blob(3.5, 6 if up else 10, 3.0, 1.5, OWL, ang=0.6 if up else -0.5)    # skrzydła szeroko
        s.blob(11.5, 6 if up else 10, 3.0, 1.5, OWL, ang=-0.6 if up else 0.5)
        s.blob(7.5, 8, 2.8, 3.0, OWL)
        rim(s, 'e3')
        s.outline()
        pts(s, [(6, 7), (9, 7)], 'fl')
        s.set(7, 8, 'go')
        return s
    puff = 0.5 if pose == 'hoot' else 0.0
    s.blob(7.5, 10, 3.6 + puff, 4.4 + puff, OWL)                         # tułów
    s.blob(7.5, 5.2, 3.4, 2.8, OWL)                                      # głowa
    pts(s, [(4, 2), (4, 3), (11, 2), (11, 3)], 'e2')                     # pędzelki uszu
    for x, y in [(6, 10), (9, 11), (7, 12), (8, 9)]:
        s.set(x, y, 'cr')                                                # jasne cętki na piersi
    pts(s, [(6, 15), (9, 15)], 'go')                                     # szpony
    rim(s, 'e3')
    s.outline()
    shift = -1 if pose == 'look' else 0
    if pose == 'blink':
        pts(s, [(5, 5), (6, 5), (9, 5), (10, 5)], 'e3')
    else:
        pts(s, [(5 + shift, 5), (6 + shift, 5), (9 + shift, 5), (10 + shift, 5)], 'go')   # tarcze oczu
        pts(s, [(6 + shift, 5), (9 + shift, 5)], 'fl')                   # źrenice świecą
    s.set(7 + (0 if shift == 0 else -1), 6, 'e3')                        # dziób
    if pose == 'hoot':
        s.set(7, 7, 'p0')
    return s


# ---------------------------------------------------------------- ślimak
SNAIL_POSES = ('crawl0', 'crawl1', 'hide')


def snail(pose):
    s = Sprite(17, 11, 8, 10)
    stretch = 1 if pose == 'crawl1' else 0
    if pose != 'hide':
        s.blob(9 + stretch * 0.5, 9.2, 6.0 + stretch, 1.4, SLIME)        # ciało (stopa)
        s.blob(13.8 + stretch, 7.6, 1.5, 1.6, SLIME)                     # głowa
        pts(s, [(13 + stretch, 5), (13 + stretch, 4), (15 + stretch, 5), (15 + stretch, 4), (16 + stretch, 3)], 'b3')   # czułki
    s.blob(7.5, 6, 4.0, 3.8, SHELL)                                      # muszla
    rim(s, 'go', skip=('o', 'b1', 'b2', 'b3', 'fo'))
    s.outline()
    pts(s, [(7, 5), (8, 5), (8, 6), (7, 7), (6, 6), (6, 4), (8, 3), (9, 4), (9, 7)], 'e1')   # spirala
    if pose != 'hide':
        pts(s, [(13 + stretch, 3), (16 + stretch, 2)], 'wl')             # świecące końce czułków
    return s


# ---------------------------------------------------------------- lis-widmo
FOX_POSES = ('sit0', 'sit1', 'walk0', 'walk1', 'walk2', 'walk3', 'curl')


def fox(pose):
    s = Sprite(19, 14, 9, 13)
    if pose == 'curl':
        s.blob(9.5, 10, 5.6, 2.8, GHOST)
        s.blob(13.5, 9.4, 2.2, 1.8, GHOST)
        pts(s, [(13, 7), (15, 7)], 'b2')
        pts(s, [(3, 11), (3, 10), (4, 9), (5, 12), (6, 12), (7, 12)], 'b3')
        rim(s, 'b3')
        s.outline()
        pts(s, [(3, 10), (4, 9)], 'cW')                                  # świecący koniec ogona
        pts(s, [(14, 9), (15, 9)], 'b3')
        return s
    if pose.startswith('sit'):
        s.blob(8, 9.5, 3.4, 3.6, GHOST)
        s.blob(10.5, 4.8, 2.8, 2.2, GHOST)
        pts(s, [(12, 5), (13, 5), (14, 6)], 'b2')                        # pyszczek
        pts(s, [(9, 2), (9, 1), (8, 1), (12, 2), (12, 1), (11, 1)], 'b2')   # uszy
        pts(s, [(9, 12), (9, 13), (11, 12), (11, 13)], 'b2')
        tail = [(5, 12), (4, 12), (3, 11), (2, 10), (2, 9), (3, 8)] if pose == 'sit0' else \
               [(5, 12), (4, 12), (3, 12), (2, 12), (1, 11), (1, 10)]
        pts(s, tail, 'b3')
        rim(s, 'b3')
        s.outline()
        pts(s, [tail[-1], tail[-2]], 'cW')
        pts(s, [(10, 4), (12, 4)], 'wi')                                 # oczy
        s.set(14, 5, 'p0')                                               # nos
        return s
    s.blob(8.5, 8.5, 5.0, 2.3, GHOST)
    s.blob(13.8, 6.2, 2.4, 2.0, GHOST)
    pts(s, [(15, 6), (16, 7), (17, 7)], 'b2')
    pts(s, [(13, 4), (13, 3), (15, 4), (15, 3)], 'b2')
    pts(s, [(3, 7), (2, 6), (1, 6), (0, 7)], 'b3')                       # ogon na wznak
    legs = {'walk0': [(5, 11), (11, 11)], 'walk1': [(6, 11), (10, 11)],
            'walk2': [(7, 11), (9, 11)], 'walk3': [(6, 11), (10, 11)]}[pose]
    for x, y in legs:
        pts(s, [(x, y), (x, y + 1), (x, y + 2)], 'b2')
    back = {'walk0': [(10, 11), (6, 11)], 'walk1': [(11, 11), (5, 11)],
            'walk2': [(12, 11), (4, 11)], 'walk3': [(11, 11), (5, 11)]}[pose]
    for x, y in back:
        pts(s, [(x, y), (x, y + 1)], 'b1')
    rim(s, 'b3')
    s.outline()
    pts(s, [(0, 7), (1, 6)], 'cW')
    s.set(14, 5, 'wi')
    s.set(17, 7, 'p0')
    return s


FAMILIARS = {
    'cat': (cat, CAT_POSES), 'raven': (raven, RAVEN_POSES), 'toad': (toad, TOAD_POSES), 'bat': (bat, BAT_POSES),
    'moth': (moth, MOTH_POSES), 'owl': (owl, OWL_POSES), 'snail': (snail, SNAIL_POSES), 'fox': (fox, FOX_POSES),
}


def to_img(spr, only=None):
    im = Image.new("RGBA", (spr.w, spr.h), (0, 0, 0, 0))
    for (x, y), k in spr.px.items():
        if only is None or k in only:
            im.putpixel((x, y), RGB[k] + (255,))
    return im


if __name__ == "__main__":
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    rows = []
    for name, (fn, poses) in FAMILIARS.items():
        rows.append([to_img(fn(p)) for p in poses])
    cw, ch = 20, 17
    W = cw * max(len(r) for r in rows)
    sheet = Image.new("RGBA", (W, ch * len(rows)), (44, 60, 44, 255))
    for y, r in enumerate(rows):
        for x, im in enumerate(r):
            sheet.alpha_composite(im, (x * cw + (cw - im.width) // 2, y * ch + ch - im.height))
    out = os.path.join(ROOT, "docs", "img", "chowance.png")
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(out)
    print("ok:", out)
