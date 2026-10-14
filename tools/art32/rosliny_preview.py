"""Przed / po: obecne rośliny i obiekty 16 px (powiększone x2), wersja 32 px z hd2x i ta sama w żywszych kolorach.
Uruchom:  python3 tools/art32/rosliny_preview.py  → docs/img/art32_rosliny.png
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "pixel-proto"))
from PIL import Image, ImageDraw
import sprites
import sprites3 as S3
import sprites6 as S6
from hd2x import hd2x, to_image, EXTRA_COLORS, hexrgb

RGB = dict(sprites.RGB)
for k, v in EXTRA_COLORS.items():
    RGB[k] = hexrgb(v)
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
import colorsys
VIVID = {}
for k, (r, g, b) in RGB.items():   # żywsze rośliny: więcej nasycenia i jasności poza konturem i najciemniejszymi tonami
    hh, ss, vv = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if k not in ('o',) and vv > 0.12:
        ss, vv = min(1, ss * 1.22), min(1, vv * 1.14 + 0.02)
    VIVID[k] = tuple(int(round(c * 255)) for c in colorsys.hsv_to_rgb(hh, ss, vv))

items = [
    ("marchew", lambda: sprites.carrot(3)), ("zboże", lambda: sprites.wheat(3)), ("dynia", lambda: sprites.pumpkin(3)),
    ("czosnek", lambda: S6.garlic(3)), ("mak", lambda: S6.poppy(3)), ("piołun", lambda: S6.wormwood(3)),
    ("ks. kwiat", lambda: S3.moonflower(3)), ("trupi grzyb", lambda: S3.corpse_mushroom(3)), ("upiorna dynia", lambda: S3.ghost_pumpkin(3)),
    ("krwawa róża", lambda: S3.blood_rose(3)), ("wilcza jagoda", lambda: S6.belladonna(3)), ("mandragora", lambda: S6.mandrake(3)),
    ("drzewo", sprites.round_tree), ("świerk", sprites.spruce), ("krzak", sprites.bush), ("kamień", sprites.rock),
]
SC = 3
tiles = []
for i, (name, fn) in enumerate(items):
    spr = fn()
    from hd2x import grid_of
    before = to_image(grid_of(spr), RGB).resize((spr.w * 2 * SC, spr.h * 2 * SC), Image.NEAREST)
    after = to_image(hd2x(spr, RGB, seed=i + 1), RGB, SC)
    tiles.append((before, after, to_image(hd2x(spr, VIVID, seed=i + 1), VIVID, SC)))
cols = 4
cw = max(b.width + a.width + v.width + 24 for b, a, v in tiles) + 16
ch = max(max(b.height, a.height) for b, a, v in tiles) + 16
rows = (len(tiles) + cols - 1) // cols
sheet = Image.new('RGBA', (cols * cw, rows * ch), (52, 74, 60, 255))   # tło jak trawa
d = ImageDraw.Draw(sheet)
for i, (b, a, v) in enumerate(tiles):
    x, y = (i % cols) * cw + 8, (i // cols) * ch + 8
    sheet.alpha_composite(b, (x, y + ch - 16 - b.height))
    sheet.alpha_composite(a, (x + b.width + 12, y + ch - 16 - a.height))
    sheet.alpha_composite(v, (x + b.width + a.width + 24, y + ch - 16 - v.height))
    d.line([(x + b.width + 6, y + 4), (x + b.width + 6, y + ch - 20)], fill=(40, 58, 47), width=2)
out = os.path.join(ROOT, 'docs', 'img', 'art32_rosliny.png')
sheet.save(out)
print('ok', sheet.size)
