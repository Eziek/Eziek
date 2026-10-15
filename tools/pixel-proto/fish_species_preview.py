"""Podgląd gatunków ryb pod wodą (GDD §9 p. 112): port FishPixel z Water.shader, kilka klatek każdego gatunku.
python3 tools/pixel-proto/fish_species_preview.py -> docs/img/ryby_gatunki.png"""
import math, os
from PIL import Image

SCHOOL = [(0, 0), (6, 1), (2, 3), (8, 4)]


def fish_row(shape, row):
    if row == 0: return 0x30
    if row == 2: return 0x3FE if shape == 0 else 0x3FD
    return 0x1F9 if shape == 0 else 0x1FA


def fish_pixel(kind, lx, ly, right, t, f, i):
    if kind == 1:
        for j, (ox, oy) in enumerate(SCHOOL):
            mx, my = lx - ox, ly - oy - round(0.6 * math.sin(t * 2 + j * 1.7))
            if 0 <= mx < 4 and 0 <= my < 2:
                k = mx if right else 3 - mx
                row = 0x6 if my == 0 else (0xF if (f + j) % 2 == 0 else 0xE)
                if (row >> k) & 1: return 1
        return 0
    if kind == 2:
        if not (0 <= lx < 16 and 0 <= ly < 5): return 0
        k = lx if right else 15 - lx
        c = 2 + round(1.3 * math.sin(k * 0.55 - t * 5 + i))
        thick = 2 if k >= 12 else (1 if k >= 1 else 0)
        return 1 if c <= ly < c + thick else 0
    if kind == 3:
        if not (0 <= lx < 14 and -1 <= ly < 7): return 0
        k = lx if right else 13 - lx
        wing = 3.0 + 0.9 * math.sin(t * 2.2 + i)
        bx, by = (k - 8.5) / 5, (ly - 2.5) / wing
        if bx * bx + by * by <= 1 and abs(bx) + abs(by) <= 1.25: return 1
        return 1 if k < 4 and ly == 2 else 0
    if kind == 4:
        if not (0 <= lx < 15 and 0 <= ly < 5): return 0
        k = lx if right else 14 - lx
        bx, by = (k - 8.5) / 6, (ly - 2) / 2.2
        if bx * bx + by * by <= 1: return 1
        dy = ly - 2 + (0 if (f + i) % 4 < 2 else (1 if ly < 2 else 0))
        if k < 0.5: return 1 if abs(dy) in (1, 2) else 0
        if k < 1.5: return 1 if abs(dy) <= 1 else 0
        return 1 if k < 2.5 and dy == 0 else 0
    if kind == 5:
        if not (0 <= lx < 7 and 0 <= ly < 6): return 0
        pulse = 0.4 * math.sin(t * 3 + i)
        bx, by = (lx - 3) / (3 + pulse), (ly - 1.5) / 1.6
        if ly <= 2 and bx * bx + by * by <= 1: return 2
        if ly >= 3:
            for j in (1, 3, 5):
                if lx == j + round(0.6 * math.sin(t * 2 + ly + j)): return 2
        return 0
    if kind == 6:
        if not (-1 <= lx < 11 and -1 <= ly < 5): return 0
        k = lx if right else 9 - lx
        bx, by = (k - 4.5) / 4.5, (ly - 2.5) / 1.8
        if bx * bx + by * by <= 1: return 1
        if k == 8 and ly == 0: return 1
        return 3 if k == 9 and ly == -1 else 0
    if not (0 <= lx < 10 and 0 <= ly < 4): return 0
    k = lx if right else 9 - lx
    return 1 if (fish_row(0 if (f + i) % 4 < 2 else 1, ly) >> k) & 1 else 0


if __name__ == "__main__":
    B1, O, LIGHT, GLOW = (31, 48, 68), (15, 10, 16), (48, 72, 95), (95, 207, 184)
    cw, ch, frames = 20, 10, 4
    im = Image.new("RGB", (cw * frames, ch * 7), B1)
    for kind in range(7):
        for fr in range(frames):
            t = fr * 0.35
            for y in range(ch):
                for x in range(cw):
                    tone = fish_pixel(kind, x - 2, y - 2, fr % 2 == 0, t, fr, kind)
                    if tone:
                        im.putpixel((fr * cw + x, kind * ch + y), O if tone == 1 else LIGHT if tone == 2 else GLOW)
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    out = os.path.join(ROOT, "docs", "img", "ryby_gatunki.png")
    im.resize((im.width * 6, im.height * 6), Image.NEAREST).save(out)
    print("ok:", out)
