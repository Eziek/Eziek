"""Generatory Żaru (GDD §9 p. 109): piec Żaru (spala drewno na Żar, 3 klatki płomienia) i kryształ Żaru (sam powoli
oddaje Żar, także bez pracy, i powiększa słój). Zaczepienie u dołu w środku, piksele emisyjne w *_glow.png.
Podgląd: python3 ember_gens.py -> docs/img/generatory_zaru.png
"""
import os
from sprites import Sprite
from familiars import rim, pts, to_img

STONE = ['p0', 'b1', 'b2', 'b3']


def kiln(f=0):
    """Piec Żaru: kopułowy piec z kamienia, żarzący się otwór, komin z iskrami, stos drewna z boku."""
    s = Sprite(24, 26, 12, 25)
    s.blob(12, 17, 8.5, 8, ['b1', 'b2', 'b2', 'b3'], clip_y=25)          # kopuła pieca
    for y in range(11, 25, 3):                                           # spoiny kamieni
        for x in range(5, 20, 4):
            if (x + y // 3) % 2 == 0 and (x, y) in s.px:
                s.set(x, y, 'b1')
    s.rect(15, 3, 18, 11, 'b2'); pts(s, [(15, y) for y in range(3, 12)], 'b1'); s.rect(14, 2, 19, 3, 'b3')   # komin
    s.blob(12, 21.5, 3.6, 3.4, ['p0', 'p0', 'e0'], clip_y=25)            # otwór
    flames = [[(11, 22), (12, 21), (13, 22), (12, 23), (11, 24), (13, 24)],
              [(10, 23), (12, 20), (13, 21), (12, 22), (11, 24), (14, 24)],
              [(11, 21), (12, 22), (13, 23), (10, 24), (12, 24), (13, 22)]][f]
    s.rect(9, 23, 15, 24, 'fx')                                          # żar na palenisku (świeci)
    pts(s, flames, 'fl'); s.set(12, 22 + (f % 2), 'fL')                  # płomień (świeci)
    pts(s, [(16 + f % 2, 1), (17 - f % 2, 0)][:1 + f % 2], 'fx')         # iskra nad kominem
    for y, (x0, x1) in ((24, (0, 5)), (22, (1, 4))):                     # drewno obok
        s.rect(x0, y, x1, y + 1, 'e3'); s.set(x1, y, 'e1')
    rim(s, 'b3', skip=('o', 'p0', 'e0', 'fx', 'fl', 'fL', 'e1', 'e3'))
    s.outline()
    return s


def crystal():
    """Kryształ Żaru: ciemny kamienny cokół z runami, nad nim pękaty, żarzący się kryształ."""
    s = Sprite(16, 26, 8, 25)
    s.rect(3, 19, 12, 25, 'b2'); pts(s, [(3, y) for y in range(19, 26)], 'b1'); s.rect(2, 18, 13, 19, 'b3')   # cokół
    pts(s, [(6, 22), (7, 21), (9, 22), (10, 23)], 'or')                  # runy (przygaszone)
    for y in range(4, 18):                                               # kryształ: romb z fasetami
        w = (y - 3) if y < 11 else (18 - y)
        w = max(1, min(w, 5))
        for x in range(8 - w, 8 + w):
            k = 'fx' if x < 8 else 'or'
            if x == 8 - w or x == 7:
                k = 'fl'
            s.set(x, y, k)
    pts(s, [(7, 6), (6, 9), (7, 12)], 'fL')                              # błyski (świecą)
    pts(s, [(2, 17), (13, 17)], 'b3')
    s.outline()
    return s


if __name__ == "__main__":
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    ims = [to_img(kiln(f)) for f in range(3)] + [to_img(crystal())]
    cw = max(i.width for i in ims) + 4
    ch = max(i.height for i in ims) + 2
    sheet = Image.new("RGBA", (cw * len(ims), ch), (44, 60, 44, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im, (i * cw + (cw - im.width) // 2, ch - im.height))
    out = os.path.join(ROOT, "docs", "img", "generatory_zaru.png")
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(out)
    print("ok:", out)
