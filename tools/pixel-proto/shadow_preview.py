"""Podgląd słońca i cieni (SunShadows.cs, DayNightLighting.cs) bez Unity: drzewo, krzak i kamień na trawie o kilku porach
doby, z kolorem światła globalnego i cieniem ściętym jak w grze (te same wzory). W nocy cień od lampy.
Uruchom z tools/pixel-proto:  python3 shadow_preview.py  -> docs/img/cienie_pory_dnia.png
"""
import os, math
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
OUT = os.path.join(ROOT, "docs", "img", "cienie_pory_dnia.png")
PPU = 16
DAY = 2400.0

NIGHT = (0.48, 0.55, 0.95)
KEYS = [(0, NIGHT, .34), (5.2, NIGHT, .34), (6, (.62, .55, .92), .42), (6.6, (1, .62, .66), .66), (7.3, (1, .84, .62), .9),
        (8, (1, .97, .92), 1), (10, (1, 1, 1), 1), (16.5, (1, 1, 1), 1), (17.8, (1, .93, .8), 1), (18.6, (1, .82, .56), .95),
        (19.3, (1, .6, .45), .76), (19.9, (.78, .52, .86), .52), (20.7, NIGHT, .34), (24, NIGHT, .34)]


def sample(hour):
    for i in range(1, len(KEYS)):
        if hour <= KEYS[i][0]:
            a, b = KEYS[i - 1], KEYS[i]
            t = (hour - a[0]) / (b[0] - a[0]) if b[0] > a[0] else 0
            t = t * t * (3 - 2 * t)
            return tuple(a[1][k] + (b[1][k] - a[1][k]) * t for k in range(3)), a[2] + (b[2] - a[2]) * t
    return NIGHT, .34


def second_of(hour):
    return (hour - 6) / 14 * DAY if 6 <= hour < 20 else DAY + ((hour - 20) % 24) / 10 * 1200


def shadow_dir(hour, lamp=None, at=None):
    s = second_of(hour)
    if s < DAY:
        u = s / DAY
        x, h = math.cos(u * math.pi), math.sin(u * math.pi)
        length = 0.35 + 1.5 * (1 - h) ** 1.5
        return (-x * length + 0.15, 0.3 + 0.1 * h), 0.3 * min(1, h / 0.12)
    if lamp is not None:
        dx, dy = at[0] - lamp[0], at[1] - lamp[1]
        dy = -dy                                  # obraz: y w dół, świat: y w górę
        d = math.hypot(dx, dy) / PPU              # odległość w jednostkach świata
        r = 3.0
        if d > r:
            return (-0.6, 0.3), 0.1
        k = 1 - d / r
        stretch = 0.5 + 1.2 * (1 - k)
        return (dx / (d * PPU) * stretch, dy / (d * PPU) * stretch), 0.42 * k ** 0.7
    return (-0.6, 0.3), 0.1


def load(name):
    im = Image.open(os.path.join(SPR, "Objects", name + ".png")).convert("RGBA")
    return im


def draw_shadow(canvas, sprite, base, d, alpha):
    """Punkt sprite'a (x, y od podstawy, y w górę) -> (x + d.x * y, d.y * y); obraz y w dół."""
    w, h = sprite.size
    dx, dy = d
    if abs(dy) < 0.18:
        dy = -0.18 if dy < 0 else 0.18
    bx, by = base
    sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    px = sprite.load()
    out = sh.load()
    # odwzorowanie odwrotne: dla piksela celu (X, Y) szukamy (x, y) sprite'a
    for Y in range(canvas.size[1]):
        for X in range(canvas.size[0]):
            yw = (by - Y)                       # y w górę od podstawy (piksele)
            yl = yw / dy
            xl = (X - bx) - dx * yl
            sx, sy = int(math.floor(xl + w / 2)), int(math.floor(h - 1 - yl))
            if 0 <= sx < w and 0 <= sy < h and yl >= 0 and px[sx, sy][3] > 0:
                out[X, Y] = (10, 5, 20, int(255 * alpha))
    canvas.alpha_composite(sh)


def scene(hour, night_lamp=False):
    W, H = 120, 70
    im = Image.new("RGBA", (W, H), (58, 74, 52, 255))
    d = ImageDraw.Draw(im)
    for i in range(0, W, 7):
        d.point((i, (i * 13) % H), fill=(66, 84, 58, 255))
    objs = [("tree_round", (32, 52)), ("bush", (66, 58)), ("rock", (92, 54))]
    lamp = (60, 30) if night_lamp else None
    for name, base in objs:
        spr = load(name)
        dirv, a = shadow_dir(hour, lamp, base)
        draw_shadow(im, spr, base, dirv, a)
    for name, base in objs:
        spr = load(name)
        im.alpha_composite(spr, (base[0] - spr.size[0] // 2, base[1] - spr.size[1]))
    if lamp:
        d.ellipse((lamp[0] - 2, lamp[1] - 2, lamp[0] + 2, lamp[1] + 2), fill=(255, 200, 120, 255))
    col, inten = sample(hour)
    px = im.load()
    for y in range(H):
        for x in range(W):
            r, g, b, a = px[x, y]
            k = max(0.3, inten)
            light = 0
            s = second_of(hour)
            if s < DAY:
                u = s / DAY
                sx, sh = math.cos(u * math.pi), math.sin(u * math.pi)
                low = max(0, min(1, 1 - sh / 0.6)) * max(0, min(1, sh / 0.08))
                side = (x / W) if sx < 0 else (1 - x / W)   # jaśniej od strony słońca
                light = 0.45 * low * (1 - side) * 0.8
            if lamp:
                dd = math.hypot(x - lamp[0], y - lamp[1]) / 48
                light = max(0, 1 - dd) * 0.9
            px[x, y] = (min(255, int(r * (col[0] * k + light))), min(255, int(g * (col[1] * k + light * 0.75))),
                        min(255, int(b * (col[2] * k + light * 0.45))), a)
    return im


if __name__ == "__main__":
    frames = [(6.4, False), (7.2, False), (10, False), (13, False), (17, False), (18.8, False), (19.5, False), (23, True)]
    tiles = [scene(h, n) for h, n in frames]
    sheet = Image.new("RGBA", (tiles[0].width * 4 + 12, tiles[0].height * 2 + 4), (20, 16, 24, 255))
    for i, t in enumerate(tiles):
        sheet.alpha_composite(t, ((i % 4) * (t.width + 4), (i // 4) * (t.height + 4)))
    sheet = sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT)
    print("ok:", OUT)
