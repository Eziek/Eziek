"""Domki chowańców i budowle ogników (GDD §9 p. 103). Punkt zaczepienia u dołu w środku (jak budowle na kafelku).

Domki: kocie posłanie, grzęda kruka, oczko wodne ropuchy, budka nietoperzy, latarenka ćmy, dziupla sowy,
mszysty kamień ślimaka, buda lisa-widma. Ogniki: ognisko (3 klatki płomieni) i sennik.
Piksele emisyjne idą do *_glow.png. Podgląd: python3 homes.py -> docs/img/domki.png
"""
import os
from sprites import Sprite
from sprites3 import RGB, EMISSIVE
from familiars import rim, pts, to_img

WOOD = ['e1', 'e2', 'e3']
WICKER = ['e1', 'e2', 'e3', 'go']
STONE = ['b1', 'b2', 'b3', 'fo']
MOSS = ['g1', 'g2', 'g3']
WATER = ['b0', 'b1', 'b2']


def cat_bed():
    s = Sprite(20, 12, 10, 11)
    s.blob(10, 7.6, 8.4, 3.6, WICKER)                                    # wiklinowy kosz (ścianki)
    for x in range(3, 18, 2):
        s.set(x, 8, 'e1'); s.set(x + 1, 10, 'e1')                        # splot
    s.blob(10, 5.4, 7.0, 1.9, ['e0', 'e1', 'e1'])                        # wnętrze kosza
    s.blob(10, 5.6, 5.6, 1.3, ['p1', 'p2', 'mu'])                        # poduszka w środku
    pts(s, [(x, 3) for x in range(5, 16)], 'go')                         # brzeg kosza
    pts(s, [(3, 4), (4, 4), (16, 4), (17, 4)], 'go')
    s.outline()
    return s


def raven_perch():
    s = Sprite(16, 24, 8, 23)
    s.rect(7, 6, 8, 21, 'e2'); pts(s, [(7, y) for y in range(6, 22)], 'e1')   # słup
    s.rect(2, 5, 13, 5, 'e2'); pts(s, [(x, 4) for x in range(3, 13)], 'e3')   # poprzeczka
    pts(s, [(1, 4), (0, 3), (14, 4), (15, 3), (4, 6), (11, 6)], 'e2')       # sęki
    s.blob(8, 22, 4.5, 1.6, STONE)                                       # kamienie u podstawy
    pts(s, [(12, 7), (12, 8), (12, 9)], 'cr')                            # pióro na sznurku
    s.set(12, 6, 'e1')
    s.outline()
    return s


def toad_pond():
    s = Sprite(24, 12, 12, 11)
    s.blob(12, 6.5, 10.4, 4.0, ['b2', 'b3', 'fo', 'fo'])                 # obmurowanie z kamieni
    s.blob(12, 6.3, 8.6, 2.9, ['b0', 'b1', 'b1', 'b2'])                  # woda (ciemna, z odblaskami)
    pts(s, [(7, 5), (8, 5), (9, 5), (15, 8), (16, 8)], 'fo')            # odblaski
    for x in range(3, 22, 3):
        s.set(x, 9 if x % 2 else 3, 'b2')                                # spoiny kamieni
    s.blob(15.5, 5.8, 2.6, 1.3, MOSS)                                    # liść grzybieni
    s.set(16, 5, 'b1')                                                   # wcięcie liścia
    pts(s, [(4, 3), (4, 2), (4, 1), (5, 0), (3, 2), (3, 1)], 'g2')       # trzcina
    pts(s, [(5, 0), (5, 1)], 'e2')
    s.outline()
    return s


def bat_box():
    s = Sprite(16, 26, 8, 25)
    s.rect(7, 13, 8, 25, 'e1'); pts(s, [(8, y) for y in range(13, 26)], 'e2')   # słup
    s.rect(3, 5, 12, 13, 'e2')                                           # skrzynka
    for x in range(4, 13, 3):
        pts(s, [(x, y) for y in range(6, 13)], 'e1')                     # deski
    for i in range(6):
        pts(s, [(7 - i, 1 + i), (8 + i, 1 + i)], 'e3')                   # dach
        s.rect(8 - i, 1 + i, 7 + i, 1 + i, 'e1')
    pts(s, [(1, 6), (14, 6)], 'e3')
    s.rect(5, 12, 10, 12, 'p0')                                          # szpara wlotu
    rim(s, 'e3', skip=('o', 'p0'))
    s.outline()
    return s


def moth_lantern():
    s = Sprite(12, 24, 6, 23)
    pts(s, [(6, y) for y in range(10, 24)], 'e2'); pts(s, [(5, y) for y in range(12, 24)], 'e1')   # kij
    s.rect(3, 3, 8, 9, 'e1')                                             # klatka latarenki
    s.rect(4, 4, 7, 8, 'ml'); s.rect(5, 5, 6, 7, 'mL')                   # blade światło (świeci)
    pts(s, [(5, 1), (6, 1), (5, 2), (6, 2), (2, 3), (9, 3)], 'e2')       # uchwyt i daszek
    s.blob(3, 21, 2.0, 1.6, MOSS); s.blob(9, 21.5, 2.2, 1.5, MOSS)       # kępki
    pts(s, [(2, 19), (9, 19), (10, 20)], 'mu')                           # fioletowe kwiatki
    s.outline()
    return s


def owl_hollow():
    s = Sprite(20, 26, 10, 25)
    s.blob(10, 15, 6.6, 10.5, WOOD)                                      # pień
    s.blob(10, 24, 8.5, 1.8, WOOD)                                       # korzenie
    pts(s, [(5, 4), (6, 3), (8, 4), (12, 3), (14, 4), (13, 5)], 'e3')    # złamany wierzch
    s.blob(10, 11, 2.6, 3.0, ['p0', 'p0', 'p1'])                         # dziupla
    s.blob(5, 18, 1.6, 1.2, MOSS); s.blob(15, 20, 1.8, 1.2, MOSS)        # mech
    for y in range(15, 23, 3):
        s.set(8, y, 'e1'); s.set(12, y + 1, 'e1')                        # kora
    rim(s, 'e3', skip=('o', 'p0', 'p1'))
    s.outline()
    return s


def snail_stone():
    s = Sprite(20, 13, 10, 12)
    s.blob(10, 8, 8.5, 4.2, STONE)                                       # kamień
    s.blob(9, 5.2, 6.5, 1.8, MOSS)                                       # mech na wierzchu
    pts(s, [(4, 3), (4, 2), (15, 4), (15, 3)], 'cr')                     # grzybki
    pts(s, [(3, 2), (5, 2), (4, 1), (14, 3), (16, 3), (15, 2)], 'co')
    pts(s, [(7, 9), (13, 10), (11, 7)], 'wl')                            # krople rosy (świecą)
    s.outline()
    return s


def fox_kennel():
    s = Sprite(22, 22, 11, 21)
    s.rect(4, 9, 17, 21, 'e2')                                           # ściany
    for x in range(5, 17, 3):
        pts(s, [(x, y) for y in range(10, 22)], 'e1')                    # deski
    for i in range(9):
        s.rect(11 - i, 1 + i, 10 + i, 1 + i, 'e1')                       # dach
        pts(s, [(11 - i, 1 + i), (10 + i, 1 + i)], 'e3')
    pts(s, [(1, 9), (20, 9), (2, 9), (19, 9)], 'e3')                     # okap
    s.blob(10.5, 17.5, 3.2, 4.2, ['p0', 'p0', 'p1'], clip_y=21)          # wejście
    pts(s, [(9, 18), (12, 18), (10, 20), (11, 19)], 'wi')                # widmowy blask w środku (świeci)
    s.rect(9, 11, 12, 12, 'go')                                          # tabliczka
    rim(s, 'e3', skip=('o', 'p0', 'p1', 'wi'))
    s.outline()
    return s


BONFIRE_FRAMES = 3


def bonfire(f):
    """Ognisko ogników: krąg kamieni, skrzyżowane polana, turkusowo-złote płomienie (3 klatki)."""
    s = Sprite(20, 18, 10, 17)
    for cx, cy in ((3, 14.5), (6, 16), (10, 16.5), (14, 16), (17, 14.5), (5, 13), (15, 13)):
        s.blob(cx, cy, 1.7, 1.2, STONE)                                  # kamienie
    s.line(5, 15, 15, 12, 'e2'); s.line(5, 12, 15, 15, 'e1')             # polana
    s.line(5, 14, 15, 11, 'e3')
    flames = [
        [(10, 3), (9, 4), (10, 4), (11, 5), (9, 5), (10, 5), (8, 6), (9, 6), (10, 6), (11, 6), (12, 7), (8, 7), (9, 7),
         (10, 7), (11, 7), (7, 8), (8, 8), (9, 8), (10, 8), (11, 8), (12, 8), (8, 9), (9, 9), (10, 9), (11, 9), (12, 9),
         (8, 10), (9, 10), (10, 10), (11, 10), (12, 10), (9, 11), (10, 11), (11, 11)],
        [(9, 2), (9, 3), (10, 4), (9, 4), (11, 4), (10, 5), (11, 5), (8, 6), (9, 6), (10, 6), (11, 6), (7, 7), (8, 7),
         (9, 7), (10, 7), (11, 7), (12, 7), (8, 8), (9, 8), (10, 8), (11, 8), (12, 8), (7, 9), (8, 9), (9, 9), (10, 9),
         (11, 9), (8, 10), (9, 10), (10, 10), (11, 10), (12, 10), (9, 11), (10, 11), (11, 11)],
        [(11, 3), (10, 4), (11, 4), (9, 5), (10, 5), (11, 5), (12, 6), (9, 6), (10, 6), (11, 6), (8, 7), (9, 7), (10, 7),
         (11, 7), (12, 7), (8, 8), (9, 8), (10, 8), (11, 8), (12, 8), (13, 9), (8, 9), (9, 9), (10, 9), (11, 9), (12, 9),
         (7, 10), (8, 10), (9, 10), (10, 10), (11, 10), (12, 10), (9, 11), (10, 11), (11, 11)],
    ][f % 3]
    for x, y in flames:
        s.set(x, y, 'wi')                                                # turkusowy płomień (kolor ogników)
    core = [(p) for p in flames if p[1] >= 7 and 8 <= p[0] <= 11]
    for x, y in core:
        s.set(x, y, 'wl')
    for x, y in [(9, 10), (10, 10), (10, 9), (11, 10)]:
        s.set(x, y, 'fl')                                                # złoty żar u dołu
    sparks = [[(6, 2), (13, 1)], [(14, 3), (7, 0)], [(5, 4), (12, 0)]][f % 3]
    for x, y in sparks:
        s.set(x, y, 'fl')                                                # iskry
    s.outline()
    return s


def dream_house():
    """Sennik: okrągła chatka odpoczynku ogników, stożkowy dach z gwiazdką, świecące okno, księżyc na dachu."""
    s = Sprite(22, 28, 11, 27)
    s.blob(11, 19.5, 7.6, 7.6, ['p1', 'p2', 'mu'], clip_y=27)            # okrągłe ściany
    for i in range(10):
        s.rect(11 - i, 3 + i, 11 + i, 3 + i, 'b1')                       # stożkowy dach
        pts(s, [(11 - i, 3 + i)], 'b2')
    pts(s, [(11, 1), (11, 2), (10, 2), (12, 2)], 'fl')                   # gwiazdka na szczycie (świeci)
    pts(s, [(7, 9), (7, 10), (8, 11), (7, 8)], 'cW')                     # sierp księżyca (świeci)
    s.blob(11, 24.2, 2.4, 3.2, ['p0', 'p0', 'p1'], clip_y=27)            # drzwi
    s.blob(15.5, 17.5, 1.6, 1.6, ['fl', 'fl', 'fL'])                     # okrągłe okno (świeci)
    s.blob(6.5, 18, 1.2, 1.4, ['fl', 'fl', 'fL'])
    rim(s, 'mu', skip=('o', 'p0', 'fl', 'fL', 'cW', 'b1', 'b2'))
    s.outline()
    return s


HOMES = {
    'cat': cat_bed, 'raven': raven_perch, 'toad': toad_pond, 'bat': bat_box,
    'moth': moth_lantern, 'owl': owl_hollow, 'snail': snail_stone, 'fox': fox_kennel,
}

if __name__ == "__main__":
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    ims = [to_img(fn()) for fn in HOMES.values()] + [to_img(bonfire(f)) for f in range(3)] + [to_img(dream_house())]
    cw = max(i.width for i in ims) + 4
    ch = max(i.height for i in ims) + 2
    sheet = Image.new("RGBA", (cw * len(ims), ch), (44, 60, 44, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im, (i * cw + (cw - im.width) // 2, ch - im.height))
    out = os.path.join(ROOT, "docs", "img", "domki.png")
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(out)
    print("ok:", out)
