"""Ozdobniki na wodzie (GDD §9 p. 105): liście lilii wodnych, kwitnące lilie (blada i mglista, świecąca nocą),
opadłe liście, rzęsa i gałązka. Widziane lekko z góry (spłaszczone elipsy), z cieniem na wodzie (o piksel w prawo i w dół).
Podgląd: python3 water_flora.py -> docs/img/woda_ozdobniki.png
"""
import math, os, random
from sprites import Sprite
import sprites3 as S3  # noqa: F401  (paleta mroczna i kolory emisyjne)

PAD = ['g0', 'g1', 'g2', 'g3']


def shadow(s, k='b0'):
    """Cień na wodzie: kształt przesunięty o piksel w prawo i w dół, tylko tam, gdzie nic nie ma."""
    for (x, y) in list(s.px):
        for dx, dy in ((1, 1), (0, 1)):
            q = (x + dx, y + dy)
            if q not in s.px and 0 <= q[0] < s.w and 0 <= q[1] < s.h:
                s.px[q] = k
    return s


def pad(s, cx, cy, rx, ry, notch, seed):
    """Liść lilii: spłaszczony krąg z wcięciem (klin od środka), żyłki, jaśniejszy brzeg od góry."""
    rnd = random.Random(seed)
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            dx, dy = (x - cx) / rx, (y - cy) / ry
            d = dx * dx + dy * dy
            if d > 1.0:
                continue
            a = math.atan2(dy * ry, dx * rx)
            diff = abs((a - notch + math.pi) % (2 * math.pi) - math.pi)
            if diff < 0.5 and d > 0.02:
                continue                                  # wcięcie liścia
            k = PAD[2]
            if dy < -0.45 and d > 0.45:
                k = PAD[3]                                # jaśniejszy brzeg od światła
            elif dy > 0.4 and d > 0.5:
                k = PAD[1]                                # spód w cieniu
            if d < 0.7 and rnd.random() < 0.08:
                k = PAD[1]
            s.set(x, y, k)
    for ang in (notch + 2.2, notch + 3.1, notch + 4.0):   # żyłki od środka
        for t in (0.35, 0.65):
            s.set(int(round(cx + math.cos(ang) * rx * t)), int(round(cy + math.sin(ang) * ry * t)), PAD[1])


def lily_pads(v=0):
    if v == 0:
        s = Sprite(14, 9, 7, 4)
        pad(s, 6.5, 4, 5.4, 3.2, -0.5, 1)
    elif v == 1:
        s = Sprite(18, 11, 9, 5)
        pad(s, 6, 4.5, 4.6, 2.8, 0.4, 2)
        pad(s, 12.5, 6.5, 3.6, 2.2, -2.4, 3)
    else:
        s = Sprite(16, 10, 8, 5)
        pad(s, 5, 3.5, 3.4, 2.1, -0.9, 4)
        pad(s, 11, 4, 3, 1.9, 2.6, 5)
        pad(s, 8, 7, 2.6, 1.6, -2.0, 6)
    s.outline('b0')
    return shadow(s, 'b0')


def lily_flower(v=0):
    """Kwitnąca lilia: liść i kwiat. v 0 = blada (kremowa), v 1 = mglista (fiolet, nocą świeci)."""
    s = Sprite(18, 13, 9, 7)
    pad(s, 8, 8, 6.2, 3.4, 0.9, 7 + v)
    petal, mid, tip, core = (('cr', 'fo', 'cr', 'go') if v == 0 else ('ml', 'mu', 'wl', 'fl'))
    cx, cy = 8, 6
    # zewnętrzne płatki leżą płasko (spłaszczona gwiazda), wewnętrzne sterczą do góry
    for i in range(8):
        a = i * math.pi / 4 + 0.2
        for t in (1, 2, 3):
            x, y = int(round(cx + math.cos(a) * t * 1.25)), int(round(cy + math.sin(a) * t * 0.7))
            s.set(x, y, mid if t == 3 or math.sin(a) > 0.3 else petal)
    for x, y, k in ((7, 4, petal), (9, 4, petal), (8, 3, tip), (6, 5, petal), (10, 5, petal), (7, 3, petal), (9, 3, mid)):
        s.set(x, y, k)
    s.set(8, 5, core); s.set(7, 5, core); s.set(8, 4, core); s.set(9, 5, mid)
    s.outline('b0')
    return shadow(s, 'b0')


def leaf(v=0):
    """Opadły liść, przyciemnione barwy jesieni."""
    s = Sprite(9, 7, 4, 3)
    ramp = (['e1', 'e2', 'e3', 'or'] if v == 0 else ['e1', 'e2', 'p2', 'co'])
    ang = (-0.5, 0.7)[v]
    s.blob(4, 3, 3.2, 1.6, ramp, ang=ang)
    s.line(1, 3 + (1 if v == 0 else -1), 7, 3 - (1 if v == 0 else -1), ramp[0])   # nerw
    s.outline('b0')
    return shadow(s, 'b0')


def duckweed(v=0):
    """Rzęsa: rozsypane drobne listki."""
    s = Sprite(14, 8, 7, 4)
    rnd = random.Random(30 + v)
    for _ in range(16 + v * 4):
        x = int(7 + rnd.gauss(0, 3.0)); y = int(4 + rnd.gauss(0, 1.3))
        if 1 <= x < 13 and 1 <= y < 7:
            s.set(x, y, rnd.choice(['g2', 'g2', 'g3', 'g1']))
            if rnd.random() < 0.4 and x + 1 < 13:
                s.set(x + 1, y, 'g2')
    return shadow(s, 'b0')


def twig():
    s = Sprite(14, 6, 7, 3)
    s.line(1, 4, 12, 2, 'e2'); s.line(2, 4, 12, 3, 'e1')
    s.line(8, 3, 10, 0, 'e2'); s.set(10, 0, 'g2'); s.set(11, 0, 'g1')
    s.outline('b0')
    return shadow(s, 'b0')


FLORA = {
    'lily_pads_0': lambda: lily_pads(0), 'lily_pads_1': lambda: lily_pads(1), 'lily_pads_2': lambda: lily_pads(2),
    'lily_pale': lambda: lily_flower(0), 'lily_mist': lambda: lily_flower(1),
    'leaf_0': lambda: leaf(0), 'leaf_1': lambda: leaf(1),
    'duckweed_0': lambda: duckweed(0), 'duckweed_1': lambda: duckweed(1), 'twig': twig,
}


if __name__ == "__main__":
    from familiars import to_img
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    ims = [to_img(fn()) for fn in FLORA.values()]
    cw = max(i.width for i in ims) + 4
    ch = max(i.height for i in ims) + 4
    sheet = Image.new("RGBA", (cw * len(ims), ch), (48, 72, 95, 255))
    for i, im in enumerate(ims):
        sheet.alpha_composite(im, (i * cw + (cw - im.width) // 2, (ch - im.height) // 2))
    out = os.path.join(ROOT, "docs", "img", "woda_ozdobniki.png")
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(out)
    print("ok:", out)
