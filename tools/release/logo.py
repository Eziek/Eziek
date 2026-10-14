"""Logo Misty Packland w pixel arcie: „PACKLAND” grubym krojem, nad nim „MISTY” cieńszym, z kłębami mgły i latarnią.

Logo to znak towarowy, więc ma ten sam napis we wszystkich językach (GDD §16.5) - wyjątek od zasady „bez tekstu w grafikach”.
Krój rysowany ręcznie (litery 5 x 7), gruby wariant: każdy piksel poszerzony o jeden w prawo.

Uruchom: python3 tools/release/logo.py  ->  docs/img/logo_podglad.png
"""
import os
from PIL import Image
from icon import PAL

GLYPHS = {
    'M': ["X...X", "XX.XX", "X.X.X", "X.X.X", "X...X", "X...X", "X...X"],
    'I': ["XXX", ".X.", ".X.", ".X.", ".X.", ".X.", "XXX"],
    'S': [".XXXX", "X....", "X....", ".XXX.", "....X", "....X", "XXXX."],
    'T': ["XXXXX", "..X..", "..X..", "..X..", "..X..", "..X..", "..X.."],
    'Y': ["X...X", "X...X", ".X.X.", "..X..", "..X..", "..X..", "..X.."],
    'P': ["XXXX.", "X...X", "X...X", "XXXX.", "X....", "X....", "X...."],
    'A': [".XXX.", "X...X", "X...X", "XXXXX", "X...X", "X...X", "X...X"],
    'C': [".XXXX", "X....", "X....", "X....", "X....", "X....", ".XXXX"],
    'K': ["X...X", "X..X.", "X.X..", "XX...", "X.X..", "X..X.", "X...X"],
    'L': ["X....", "X....", "X....", "X....", "X....", "X....", "XXXXX"],
    'N': ["X...X", "XX..X", "X.X.X", "X..XX", "X...X", "X...X", "X...X"],
    'D': ["XXXX.", "X...X", "X...X", "X...X", "X...X", "X...X", "XXXX."],
}


def text_mask(word, bold, gap):
    """Zbiór pikseli napisu (x, y) i szerokość."""
    pts, x = set(), 0
    for ch in word:
        rows = GLYPHS[ch]
        w = len(rows[0])
        for y, row in enumerate(rows):
            for dx, c in enumerate(row):
                if c == 'X':
                    pts.add((x + dx, y))
                    if bold:
                        pts.add((x + dx + 1, y))
        x += w + (1 if bold else 0) + gap
    return pts, x - gap


# odcienie liter od góry: blady pergamin -> brudny szary (jak jasne czapy mgły)
RAMP_BIG = ['m', 'm', 'f4', 'f4', 'f3', 'f3', 'f2']
RAMP_SMALL = ['f4', 'f4', 'f3', 'f3', 'f3', 'f2', 'f2']


def logo(transparent=True):
    """Logo w natywnych pikselach (RGBA). Obrys 1 px, cień 1 px w dół."""
    big, bw = text_mask("PACKLAND", True, 1)
    small, sw = text_mask("MISTY", False, 2)
    W = max(bw, sw) + 4
    sx = (W - sw) // 2
    oy = 10                                   # „PACKLAND” pod „MISTY”
    px = {}
    for (x, y) in small:
        px[(x + sx, y + 1)] = RAMP_SMALL[y]
    for (x, y) in big:
        px[(x + 2, y + oy)] = RAMP_BIG[y]
    # mała latarnia z żarem po lewej i prawej stronie „MISTY” (to, co rozświetla mgłę)
    for lx in (sx - 6, sx + sw + 3):
        for (dx, dy, k) in ((0, 1, 'l0'), (1, 1, 'l0'), (0, 2, 'l1'), (1, 2, 'l1'), (0, 3, 'l0'), (1, 3, 'l0')):
            px[(lx + dx, dy + 1)] = k
    # obrys i cień
    solid = set(px)
    for (x, y) in list(solid):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            q = (x + dx, y + dy)
            if q not in px:
                px[q] = 'o'
    for (x, y) in list(px):
        q = (x + 1, y + 1)
        if q not in px:
            px[q] = 'n0'
    xs = [p[0] for p in px]; ys = [p[1] for p in px]
    x0, y0 = min(xs), min(ys)
    im = Image.new("RGBA", (max(xs) - x0 + 1, max(ys) - y0 + 1), (0, 0, 0, 0))
    for (x, y), k in px.items():
        im.putpixel((x - x0, y - y0), PAL[k] + (255,))
    return im


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    im = logo()
    bg = Image.new("RGBA", (im.width + 16, im.height + 16), PAL['n1'] + (255,))
    bg.alpha_composite(im, (8, 8))
    bg.resize((bg.width * 6, bg.height * 6), Image.NEAREST).save(os.path.join(here, "..", "..", "docs", "img", "logo_podglad.png"))
    print("ok", im.size)
