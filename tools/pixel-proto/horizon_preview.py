"""Podgląd sylwetek na horyzoncie (GDD §9 p. 120): mapy znaków czytane z Visual/HorizonSights.cs (jedno źródło),
wąż morski liczony jak PaintSerpent. Tło: głęboka woda. Uruchom: python3 horizon_preview.py -> docs/img/horyzont.png
"""
import math
import os
import re
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = open(os.path.join(ROOT, "Assets/_Project/Scripts/Visual/HorizonSights.cs"), encoding="utf-8").read()
COL = {'o': (15, 10, 16), 'd': (11, 9, 16), 'm': (31, 48, 68), 'f': (132, 142, 155), 's': (86, 105, 122),
       'w': (146, 171, 177), 'g': (86, 105, 122), 'h': (132, 142, 155), 'k': (51, 38, 58), 'l': (242, 184, 102)}
BG = (18, 28, 39)   # głęboka woda (b0)


def maps():
    out = {}
    for name, body in re.findall(r'static readonly string\[\] (\w+) =\s*\{(.*?)\};', SRC, re.S):
        out[name] = re.findall(r'"([^"]*)"', body)
    return out


def draw(rows, alpha=1.0):
    h, w = len(rows), len(rows[0])
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c in COL:
                im.putpixel((x, y), COL[c] + (int(255 * alpha),))
    return im


def serpent(p, w=46, h=11):
    g = [['.'] * w for _ in range(h)]

    def put(x, y, c):
        if 0 <= x < w and 0 <= y < h:
            g[h - 1 - y][x] = c
    for i in range(3):
        cx = 6 + 11 * i
        hgt = round(5.5 * max(0.0, math.sin(p - i * 0.9)))
        if hgt <= 0:
            continue
        for dx in range(-5, 6):
            top = round(hgt * math.sqrt(max(0.0, 1 - (dx / 5.2) ** 2)))
            for y in range(1, top + 1):
                put(cx + dx, y, 'm' if y == top else 'd')
            if top > 0:
                put(cx + dx, 0, 'd')
        put(cx - 6, 0, 'f'); put(cx + 6, 0, 'f'); put(cx - 7, 0, 'f')
    head = math.sin(p - 3.0)
    if head > 0.1:
        hy = round(6 * min(1.0, head * 1.6))
        for y in range(hy + 1):
            put(40, y, 'd'); put(41, y, 'm' if y == hy else 'd')
        put(42, hy, 'd'); put(43, hy, 'd'); put(42, hy + 1, 'm'); put(41, hy + 1, 'm'); put(42, hy - 1, 'd')
        put(42, hy + 1, 'w')
        put(39, 0, 'f'); put(43, 0, 'f')
    return [''.join(r) for r in g]


if __name__ == "__main__":
    m = maps()
    frames = [draw(m[n]) for n in ("WhaleRipple", "WhaleHump", "WhaleBack", "WhaleSpout0", "WhaleSpout1", "WhaleFluke", "WhaleFlukeLow")]
    frames += [draw(serpent(p)) for p in (1.0, 2.2, 3.4, 4.4)]
    frames.append(draw(m["ShipMap"], 0.6))
    cw = max(f.width for f in frames) + 4
    ch = max(f.height for f in frames) + 4
    cols = 4
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new("RGBA", (cw * cols, ch * rows), BG + (255,))
    for i, f in enumerate(frames):
        x, y = (i % cols) * cw, (i // cols) * ch
        sheet.alpha_composite(f, (x + (cw - f.width) // 2, y + ch - 2 - f.height))
    out = os.path.join(ROOT, "docs", "img", "horyzont.png")
    sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(out)
    print("ok:", out)
