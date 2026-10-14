"""Ikona gry Misty Packland (pixel art, paleta gry): okrągła plakietka z nocnym morzem, wyspą z latarnią, księżycem i mgłą.

Dwa projekty: 32 px (z niego 32, 64, 128, 256, 512, 1024 przez powiększenie x N, piksele ostre) i uproszczony 16 px
(16 i 48 = 16 x 3), bo w 16 px detal z 32 px zlewa się w plamę.

Uruchom: python3 tools/release/icon.py   (wywołuje go też tools/release/release_art.py)
"""
import math
from PIL import Image

PAL = {
    'o': (0x0f, 0x0a, 0x10),   # obrys
    'n0': (0x12, 0x1c, 0x27), 'n1': (0x1f, 0x30, 0x44), 'n2': (0x30, 0x48, 0x5f),   # noc, morze
    'g0': (0x14, 0x1f, 0x1c), 'g1': (0x21, 0x34, 0x2c), 'g2': (0x34, 0x4a, 0x3b), 'g3': (0x56, 0x6d, 0x52),   # trawa
    'e0': (0x20, 0x17, 0x1c), 'e1': (0x35, 0x26, 0x29), 'e2': (0x52, 0x3b, 0x35),   # klif
    'p1': (0x33, 0x26, 0x3a),
    'm': (0xd8, 0xcc, 0xab), 'm2': (0xa9, 0xa0, 0x88),   # księżyc
    'f0': (0x2a, 0x31, 0x3c), 'f1': (0x3f, 0x47, 0x51), 'f2': (0x5c, 0x60, 0x63), 'f3': (0x83, 0x80, 0x76), 'f4': (0xa9, 0xa0, 0x88),   # mgła
    'l0': (0xf0, 0x8a, 0x3a), 'l1': (0xff, 0xe0, 0x8a), 'lh': (0x7a, 0x4a, 0x2a),   # żar latarni, poświata
    'w': (0x56, 0x34, 0x47),
    'lg': (0x6d, 0x6a, 0x3e),   # trawa w świetle latarni
}


class Grid:
    def __init__(self, n):
        self.n = n
        self.px = {}

    def set(self, x, y, k):
        if 0 <= x < self.n and 0 <= y < self.n:
            self.px[(x, y)] = k

    def get(self, x, y):
        return self.px.get((x, y))

    def stamp(self, rows, x0, y0, keys):
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                if ch != '.':
                    self.set(x0 + dx, y0 + dy, keys[ch])

    def image(self):
        im = Image.new("RGBA", (self.n, self.n), (0, 0, 0, 0))
        for (x, y), k in self.px.items():
            im.putpixel((x, y), PAL[k] + (255,))
        return im


def disc(g, cx, cy, r, inside):
    for y in range(g.n):
        for x in range(g.n):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            if dx * dx + dy * dy <= r * r:
                inside(x, y, dx, dy)


def outline(g, in_badge):
    """Obrys 1 px wokół wszystkiego, co narysowane (na zewnątrz)."""
    pts = list(g.px)
    for (x, y) in pts:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in g.px:
                g.set(x + dx, y + dy, 'o')


def icon32():
    g = Grid(32)
    C, R = 16.0, 15.0
    # niebo i morze nocą: jaśniejsze ku górze, pas wody pod horyzontem
    def sky(x, y, dx, dy):
        # noc: jaśniej u góry, przejście w kratkę (dithering) w połowie
        k = 'n1' if y < 15 or (y < 17 and (x + y) % 2 == 0) else 'n0'
        g.set(x, y, k)
    disc(g, C, C, R, sky)
    # księżyc (sierp) w prawym górnym rogu
    disc(g, 22.5, 8.5, 4.2, lambda x, y, dx, dy: g.set(x, y, 'm'))
    disc(g, 20.6, 7.4, 3.6, lambda x, y, dx, dy: g.set(x, y, 'n1'))
    for (x, y) in ((24, 10), (25, 9), (23, 11)):
        if g.get(x, y) == 'm':
            g.set(x, y, 'm2')
    for (x, y) in ((7, 6), (12, 4), (27, 15), (5, 11)):   # gwiazdy
        g.set(x, y, 'm2')
    # wyspa: wierzch trawy i klif
    for y in range(14, 25):
        for x in range(5, 28):
            dx, dy = (x + 0.5 - 16.0) / 10.6, (y + 0.5 - 17.8) / 3.4
            if dy <= 0.25 and dx * dx + dy * dy <= 1.0:
                k = 'g2'
                if dy < -0.6 or (dx < -0.6 and dy < 0):
                    k = 'g3'
                elif dy > 0.0:
                    k = 'g1'
                g.set(x, y, k)
    cliff = ['g1', 'e2', 'e2', 'e1', 'e1', 'p1', 'e0']
    for x in range(6, 27):
        top = max(y for y in range(14, 25) if g.get(x, y) in ('g1', 'g2', 'g3'))
        depth = 7 - int(abs(x + 0.5 - 16.0) / 3.2)
        for d in range(1, max(2, depth)):
            k = cliff[min(d - 1, len(cliff) - 1)]
            if d == 3 and x % 4 == 1:
                k = 'e0'
            g.set(x, top + d, k)
    # drzewo (okrągłe, z prawej)
    g.stamp(["..ggg..",
             ".gGGGg.",
             "gGHHGGg",
             "gGHGGgg",
             ".gGGgg.",
             "..ttt..",
             "...t..."], 18, 9, {'g': 'g1', 'G': 'g2', 'H': 'g3', 't': 'e1'})
    # latarnia na słupie (z lewej) i jej poświata na trawie
    for (x, y) in ((8, 15), (9, 15), (13, 15), (14, 15), (10, 16), (11, 16), (12, 16), (9, 14), (13, 14),
                   (10, 15), (11, 15), (12, 15), (8, 16), (14, 16), (10, 17), (12, 17)):
        if g.get(x, y) in ('g2', 'g3', 'g1'):
            g.set(x, y, 'lg')   # ciepłe światło latarni na trawie
    g.stamp([".oo.",
             "oLLo",
             "oMMo",
             "oLLo",
             ".oo.",
             "..w.",
             "..w.",
             "..w.",
             ".www"], 10, 6, {'o': 'o', 'L': 'l0', 'M': 'l1', 'w': 'w'})
    for (x, y) in ((9, 8), (14, 8), (11, 5), (12, 5)):
        g.set(x, y, 'lh')
    # mgła: kłęby na dole plakietki, jasne czapy od lewej-góry
    # dwie chmury po bokach i mały kłąb w środku, rysowane ręcznie (kule w 32 px wyglądają jak kamienie)
    keys = {'H': 'f4', 'h': 'f3', 'm': 'f2', 'd': 'f1'}
    clouds = [
        (19, 20, ["......HHH...",
                  "...HHHhhhH..",
                  "..HhhhhhhhH.",
                  ".HhhmmmhhhhH",
                  "HhmmmmmmmmhH",
                  "hmmmmmmmmmmd",
                  "dmmmmmmmmmdd",
                  ".dddddddddd."]),
        (1, 22, ["....HHH.......",
                 "..HHhhhH.HHH..",
                 ".HhhhhhhHhhhH.",
                 "HhhhmmhhhhhhhH",
                 "hmmmmmmmmmmmhd",
                 "dmmmmmmmmmmmdd",
                 ".ddddddddddd.."]),
        (11, 27, ["..HHH.HH..",
                  ".HhhhHhhH.",
                  "Hhmmmmmmhd",
                  "dddddddddd"]),
    ]
    for x0, y0, rows in clouds:
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                x, y = x0 + dx, y0 + dy
                ddx, ddy = x + 0.5 - C, y + 0.5 - C
                if ch != '.' and ddx * ddx + ddy * ddy <= R * R:
                    g.set(x, y, keys[ch])
    outline(g, None)
    return g.image()


def icon16():
    g = Grid(16)
    C, R = 8.0, 7.5
    disc(g, C, C, R, lambda x, y, dx, dy: g.set(x, y, 'n1' if y < 7 else 'n0'))
    for (x, y) in ((11, 3), (12, 3), (12, 4), (13, 4), (12, 5)):   # sierp księżyca
        g.set(x, y, 'm')
    # wyspa
    for x in range(3, 13):
        g.set(x, 9, 'g2' if 4 <= x <= 11 else 'g1')
    for x in range(4, 12):
        g.set(x, 8, 'g3' if x < 7 else 'g2')
    for x in range(4, 12):
        g.set(x, 10, 'e2')
    for x in range(5, 11):
        g.set(x, 11, 'e1')
    # drzewo
    g.stamp([".gg.", "gGGg", ".gg."], 8, 4, {'g': 'g1', 'G': 'g3'})
    g.set(9, 7, 'e1')
    # latarnia
    g.stamp(["LL", "MM", ".w"], 5, 4, {'L': 'l0', 'M': 'l1', 'w': 'w'})
    g.set(6, 7, 'w')
    g.set(4, 5, 'lh'); g.set(7, 5, 'lh')
    # mgła
    for (x, y, k) in ((2, 11, 'f3'), (3, 11, 'f4'), (1, 12, 'f2'), (2, 12, 'f3'), (3, 12, 'f3'), (4, 12, 'f3'),
                      (5, 12, 'f4'), (6, 12, 'f4'), (7, 13, 'f3'), (8, 13, 'f3'), (9, 12, 'f4'), (10, 12, 'f4'),
                      (11, 12, 'f3'), (12, 11, 'f4'), (13, 11, 'f3'), (12, 12, 'f3'), (13, 12, 'f2'), (14, 11, 'f2'),
                      (3, 13, 'f2'), (4, 13, 'f2'), (5, 13, 'f3'), (6, 13, 'f3'), (9, 13, 'f3'), (10, 13, 'f2'),
                      (11, 13, 'f2'), (5, 14, 'f2'), (6, 14, 'f2'), (7, 14, 'f2'), (8, 14, 'f2'), (9, 14, 'f1'), (10, 14, 'f1')):
        dx, dy = x + 0.5 - C, y + 0.5 - C
        if dx * dx + dy * dy <= R * R:
            g.set(x, y, k)
    outline(g, None)
    return g.image()


def scaled(im, n):
    return im.resize((im.width * n, im.height * n), Image.NEAREST)


def icon_set():
    """Rozmiar -> obraz. Rozmiary jak w Unity (Standalone) i w pliku .ico."""
    a, b = icon32(), icon16()
    out = {16: b, 24: None, 32: a, 48: scaled(b, 3), 64: scaled(a, 2), 128: scaled(a, 4), 256: scaled(a, 8),
           512: scaled(a, 16), 1024: scaled(a, 32)}
    out[24] = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    out[24].paste(b, (4, 4))   # 24 px: projekt 16 px z marginesem (24/16 to nie całość)
    return out


if __name__ == "__main__":
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    s = icon_set()
    sheet = Image.new("RGBA", (16 * 8 + 32 * 8 + 24, 32 * 8), (40, 40, 44, 255))
    sheet.paste(scaled(s[16], 8), (0, 0))
    sheet.paste(s[256], (16 * 8 + 24, 0))
    sheet.save(os.path.join(here, "..", "..", "docs", "img", "ikona_podglad.png"))
    print("ok")
