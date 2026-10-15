"""Wyspy fabularne (GDD §13.11): szczątki na kafelkach wysepek, rusztowanie odbudowanej części i budowle po odbudowie.

Cztery historie: zatopiona kaplica (Kaplica Dzwonu), rozbita strażnica (Strażnica Łańcucha), szczątki tartaku
(Tartak rozbitków), zawalone obserwatorium (Gwiezdne obserwatorium). Punkt zaczepienia u dołu w środku, piksele
emisyjne w *_glow.png. Podgląd: python3 story_ruins.py -> docs/img/wyspy_fabularne.png
"""
import math, os
from sprites import Sprite
from familiars import rim, pts, to_img

WOOD = ['e1', 'e2', 'e3']
STONE = ['p0', 'b1', 'b2', 'b3']
MOSS = ['g1', 'g2', 'g3']
SITES = ('chapel', 'watch', 'sawmill', 'observatory')


def stones(s, cx, cy, n, seed, ramp=STONE, r=(1.4, 2.4)):
    """Kupka kamieni (gruz) wokół (cx, cy)."""
    for i in range(n):
        a = (i * 2.4 + seed) % 6.283
        d = (i * 0.37 + seed * 0.11) % 1.0
        x, y = cx + math.cos(a) * d * 5.5, cy + math.sin(a) * d * 2.2
        rr = r[0] + (r[1] - r[0]) * ((i * 0.61 + seed) % 1.0)
        s.blob(x, y, rr, rr * 0.75, ramp)


def moss(s, k='g2', step=5, seed=0):
    """Mech na wierzchu kamieni."""
    for (x, y), v in list(s.px.items()):
        if v in ('b2', 'b3') and (x, y - 1) not in s.px and (x * 3 + y + seed) % step == 0:
            s.px[(x, y)] = k


# ---------------------------------------------------------------- szczątki (2 warianty na historię)

def ruin(site, v=0):
    s = Sprite(18, 18, 9, 17)
    if site == 'chapel':
        stones(s, 9, 14.5, 6, 1 + v)
        if v == 0:                                                       # złamany łuk i przegniłe deski
            s.rect(3, 6, 5, 15, 'b2'); pts(s, [(3, y) for y in range(6, 16)], 'b1')
            pts(s, [(5, 5), (6, 4), (7, 4)], 'b3')
            for x in (9, 12, 14):
                s.line(x, 15, x + 2, 10, 'e1'); s.set(x + 2, 10, 'e2')
        else:                                                            # dzwon w błocie
            s.blob(9, 15, 7, 2.2, ['e0', 'e1', 'e1'])
            s.blob(9, 11.5, 3.6, 3.4, ['p0', 'or', 'go'], clip_y=14)
            pts(s, [(6, 14), (7, 14), (11, 14), (12, 14)], 'or'); s.set(10, 13, 'p0')
        moss(s, seed=v)
    elif site == 'watch':
        stones(s, 9, 14.5, 7, 3 + v)
        if v == 0:                                                       # kikut wieży
            s.rect(4, 5, 13, 15, 'b2')
            for y in range(5, 16, 3):
                pts(s, [(x, y) for x in range(4, 14)], 'b1')
            for x in (4, 7, 10, 13):
                s.set(x, 4, 'b3')
            pts(s, [(5, 3), (12, 2), (12, 3)], 'b3')
            s.rect(8, 9, 9, 12, 'p0')                                    # strzelnica
        else:                                                            # pęknięte lustro sygnałowe
            s.blob(9, 11, 4.6, 3.6, ['e1', 'go', 'go'])
            s.blob(9, 11, 3.2, 2.4, ['b1', 'b2', 'fo'])
            s.line(7, 9, 11, 13, 'p0')
        moss(s, seed=v)
    elif site == 'sawmill':
        if v == 0:                                                       # złamane koło
            for k in range(8):
                a = k * math.pi / 4
                if k in (2, 3):
                    continue
                s.line(9, 10, int(round(9 + math.cos(a) * 6)), int(round(10 + math.sin(a) * 5)), 'e2')
            for k in range(28):
                a = k / 28 * 6.283
                if 1.4 < a < 2.6:
                    continue
                s.set(int(round(9 + math.cos(a) * 6)), int(round(10 + math.sin(a) * 5)), 'e1')
            s.blob(9, 10, 1.4, 1.2, ['e0', 'e1'])
            s.blob(9, 16, 7, 1.6, ['e0', 'e1', 'e2'])
        else:                                                            # deski i piła
            for y, (x0, x1) in ((15, (2, 15)), (13, (3, 13)), (11, (5, 14))):
                s.rect(x0, y, x1, y + 1, 'e2'); pts(s, [(x, y) for x in range(x0, x1 + 1)], 'e3')
            for x in range(6, 14):
                s.set(x, 9, 'fo'); s.set(x, 8 if x % 2 else 9, 'b3')     # zęby piły
            s.rect(4, 8, 5, 9, 'e1')
    else:                                                                # obserwatorium
        stones(s, 9, 14.5, 6, 5 + v)
        if v == 0:                                                       # skorupy kopuły
            for k in range(14):
                a = math.pi + k / 13 * math.pi
                if 4 < k < 8:
                    continue
                s.set(int(round(9 + math.cos(a) * 7)), int(round(13 + math.sin(a) * 7)), 'b3')
                s.set(int(round(9 + math.cos(a) * 6)), int(round(13 + math.sin(a) * 6)), 'b2')
        else:                                                            # przewrócona luneta
            s.line(3, 14, 14, 9, 'go'); s.line(3, 15, 14, 10, 'or')
            s.blob(15, 9.5, 1.6, 1.4, ['or', 'go']); s.set(15, 9, 'fo')
        moss(s, seed=v)
    s.outline()
    return s


def scaffold():
    """Odbudowana część (wspólna): rusztowanie z nowych belek wokół świeżo ułożonego muru."""
    s = Sprite(18, 22, 9, 21)
    s.rect(4, 10, 13, 20, 'b2')                                          # nowy mur
    for y in range(10, 21, 3):
        pts(s, [(x, y) for x in range(4, 14)], 'b1')
    for y in range(11, 21, 3):
        pts(s, [(x, y) for x in range(5 + (y % 2) * 2, 14, 4)], 'b1')
    pts(s, [(x, 9) for x in range(4, 14)], 'b3')
    for x in (2, 15):                                                    # słupy rusztowania
        s.rect(x, 3, x, 21, 'e2'); s.set(x, 3, 'e3')
    for y in (8, 15):
        s.rect(1, y, 16, y, 'e3'); pts(s, [(x, y + 1) for x in range(2, 16, 4)], 'e1')
    s.line(3, 14, 14, 9, 'e1')                                           # zastrzał
    s.rect(6, 6, 9, 8, 'e2'); pts(s, [(6, 6), (7, 6), (8, 6), (9, 6)], 'e3')   # skrzynka z narzędziami
    s.outline()
    return s


# ---------------------------------------------------------------- budowle po odbudowie

def chapel():
    """Kaplica Dzwonu: kamienna kaplica z drewnianą dzwonnicą, dzwon i witraż świecą."""
    s = Sprite(30, 40, 15, 39)
    s.rect(5, 20, 24, 39, 'b2')
    for y in range(20, 40, 3):
        pts(s, [(x, y) for x in range(5, 25)], 'b1')
    for i in range(10):                                                  # dach
        s.rect(15 - i - 1, 11 + i, 15 + i, 11 + i, 'p1'); pts(s, [(15 - i - 1, 11 + i)], 'p2')
    s.rect(11, 3, 18, 11, 'e2'); pts(s, [(11, y) for y in range(3, 12)], 'e1')   # dzwonnica
    for i in range(4):
        s.rect(14 - i, i - 1 if i else 0, 15 + i, i - 1 if i else 0, 'p1')
    s.blob(14.5, 7.5, 2.2, 2.4, ['or', 'go', 'go'], clip_y=9)            # dzwon
    s.blob(15, 27, 3.4, 3.8, ['ml', 'ml', 'mL'], clip_y=31)              # witraż (świeci)
    pts(s, [(15, y) for y in range(23, 31)], 'p0'); pts(s, [(x, 27) for x in range(12, 19)], 'p0')
    s.blob(15, 36.5, 2.4, 3.2, ['p0', 'p0', 'e0'], clip_y=39)            # drzwi
    for x in (7, 22):
        s.blob(x, 26, 1.0, 1.6, ['fl', 'fl', 'fL'])                      # okienka
    moss(s, 'g2', 4)
    rim(s, 'b3', skip=('o', 'p0', 'ml', 'mL', 'fl', 'fL', 'p1', 'p2', 'or', 'go', 'e1', 'e2'))
    s.outline()
    return s


def watch():
    """Strażnica Łańcucha: smukła kamienna wieża z lustrem sygnałowym, które łapie światło."""
    s = Sprite(24, 44, 12, 43)
    for y in range(12, 44):
        w = 5 + (y - 12) // 9
        s.rect(12 - w, y, 11 + w, y, 'b2')
        s.set(12 - w, y, 'b1')
    for y in range(14, 44, 4):
        pts(s, [(x, y) for x in range(4, 20) if (x, y) in s.px], 'b1')
    for x in range(5, 19):                                               # blanki
        s.rect(x, 9, x, 11, 'b3' if x % 3 else 'b2')
    s.rect(7, 4, 16, 9, 'e1')                                            # osłona lustra
    s.blob(11.5, 6, 3.2, 2.4, ['go', 'fl', 'fL'])                        # lustro sygnałowe (świeci)
    for i in range(3):
        s.rect(11 - i, 1 + i, 12 + i, 1 + i, 'p1')
    s.rect(10, 20, 13, 23, 'fl'); s.rect(11, 21, 12, 22, 'fL')           # okno wartowni (świeci)
    s.rect(10, 38, 13, 43, 'p0')                                         # drzwi
    moss(s, 'g2', 4, 1)
    s.outline()
    return s


def sawmill():
    """Tartak rozbitków: drewniana szopa z kołem wodnym z boku i stosem desek."""
    s = Sprite(34, 34, 17, 33)
    s.rect(9, 17, 30, 33, 'e2')
    for x in range(9, 31, 3):
        pts(s, [(x, y) for y in range(17, 34)], 'e1')
    for i in range(9):                                                   # dach z desek
        s.rect(7 + i, 16 - i, 32 - i, 16 - i, 'p1' if i % 2 else 'p2')
    s.rect(18, 26, 22, 33, 'p0')                                         # wrota
    s.rect(25, 21, 27, 23, 'fl'); s.set(26, 22, 'fL')                   # okienko (świeci)
    for k in range(32):                                                  # koło wodne
        a = k / 32 * 6.283
        s.set(int(round(7 + math.cos(a) * 6)), int(round(25 + math.sin(a) * 6)), 'e1')
    for k in range(8):
        a = k * math.pi / 4
        s.line(7, 25, int(round(7 + math.cos(a) * 6)), int(round(25 + math.sin(a) * 6)), 'e3')
    s.blob(7, 25, 1.3, 1.3, ['e0', 'e1'])
    s.blob(7, 32.5, 6, 1.4, ['b1', 'b2', 'fo'])                          # spieniona woda pod kołem
    for y, (x0, x1) in ((32, (24, 33)), (30, (25, 32))):                 # stos desek
        s.rect(x0, y, x1, y + 1, 'e3')
    s.outline()
    return s


def observatory():
    """Gwiezdne obserwatorium: okrągła wieża z kopułą, luneta i gwiezdne okna świecą."""
    s = Sprite(28, 42, 14, 41)
    s.rect(6, 20, 21, 41, 'b2')
    for y in range(20, 42, 3):
        pts(s, [(x, y) for x in range(6, 22)], 'b1')
    pts(s, [(6, y) for y in range(20, 42)], 'b1'); pts(s, [(21, y) for y in range(20, 42)], 'b3')
    s.blob(13.5, 19, 9.5, 8.5, ['p1', 'p2', 'mu'], clip_y=20)            # kopuła
    s.rect(13, 11, 14, 19, 'p0')                                         # szczelina kopuły
    s.line(14, 14, 23, 5, 'go'); s.line(15, 14, 24, 5, 'or')             # luneta
    s.blob(24.5, 4.5, 1.6, 1.4, ['go', 'fl', 'fL'])                      # soczewka (świeci)
    for x, y in ((10, 27), (17, 27), (13, 33)):
        s.blob(x + 0.5, y, 1.1, 1.6, ['ml', 'ml', 'mL'])                 # okna (świecą)
    s.rect(12, 37, 15, 41, 'p0')
    pts(s, [(9, 13), (18, 12), (20, 15)], 'cW')                          # gwiazdki na kopule (świecą)
    moss(s, 'g2', 5, 2)
    s.outline()
    return s


BUILDINGS = {'chapel': chapel, 'watch': watch, 'sawmill': sawmill, 'observatory': observatory}


if __name__ == "__main__":
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    ims = []
    for site in SITES:
        ims += [to_img(ruin(site, 0)), to_img(ruin(site, 1)), to_img(BUILDINGS[site]())]
    ims.append(to_img(scaffold()))
    cw = max(i.width for i in ims) + 4
    ch = max(i.height for i in ims) + 2
    sheet = Image.new("RGBA", (cw * len(ims), ch), (44, 60, 44, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im, (i * cw + (cw - im.width) // 2, ch - im.height))
    out = os.path.join(ROOT, "docs", "img", "wyspy_fabularne.png")
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(out)
    print("ok:", out)
