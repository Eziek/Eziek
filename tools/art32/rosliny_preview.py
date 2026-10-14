"""Rośliny i obiekty 32 px na trawie z próbki terenu: obecne 16 px (powiększone) | 32 px, pixel art, paleta dark fantasy.
Uruchom:  python3 tools/art32/rosliny_preview.py  → docs/img/art32_rosliny.png
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "pixel-proto"))
import numpy as np
from PIL import Image
import sprites
import sprites3 as S3
import sprites6 as S6
from hd2x import hd2x, to_image, grid_of, EXTRA_COLORS, DARK_FANTASY, hexrgb
from px import value_noise, bayer

RGB = dict(sprites.RGB)
for k, v in EXTRA_COLORS.items():
    RGB[k] = hexrgb(v)
DF = dict(RGB)
for k, v in DARK_FANTASY.items():
    DF[k] = hexrgb(v)
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

items = [
    ("marchew", lambda: sprites.carrot(3)), ("zboże", lambda: sprites.wheat(3)), ("dynia", lambda: sprites.pumpkin(3)),
    ("czosnek", lambda: S6.garlic(3)), ("mak", lambda: S6.poppy(3)), ("piołun", lambda: S6.wormwood(3)),
    ("ks. kwiat", lambda: S3.moonflower(3)), ("trupi grzyb", lambda: S3.corpse_mushroom(3)), ("upiorna dynia", lambda: S3.ghost_pumpkin(3)),
    ("krwawa róża", lambda: S3.blood_rose(3)), ("wilcza jagoda", lambda: S6.belladonna(3)), ("mandragora", lambda: S6.mandrake(3)),
    ("drzewo", sprites.round_tree), ("świerk", sprites.spruce), ("krzak", sprites.bush), ("kamień", sprites.rock),
]
SC = 3


def grass(w, h, seed):
    """Trawa jak w próbce terenu (g1/g2/g3 w płatach + kępki), w kolorach gry."""
    n = value_noise(h, w, 10, seed)
    bay = bayer(h, w)
    keys = np.where(n + (bay - 0.5) * 0.12 < 0.38, 'g1', np.where(n + (bay - 0.5) * 0.12 > 0.66, 'g3', 'g2'))
    arr = np.zeros((h, w, 3), np.uint8)
    for k in ('g1', 'g2', 'g3'):
        arr[keys == k] = RGB[k]
    rng = np.random.default_rng(seed)
    for _ in range(w * h // 60):
        x, y = rng.integers(1, w - 1), rng.integers(0, h - 3)
        arr[y, x] = RGB['g3']; arr[y + 1, x - 1] = RGB['g3']; arr[y + 1, x + 1] = RGB['g3']
        arr[y + 1, x] = RGB['g1']; arr[y + 2, x] = RGB['g1']
    return Image.fromarray(arr, 'RGB').convert('RGBA')


cells = []
for i, (name, fn) in enumerate(items):
    spr = fn()
    before = to_image(grid_of(spr), RGB)
    before = before.resize((before.width * 2, before.height * 2), Image.NEAREST)
    after = to_image(hd2x(spr, DF, seed=i + 1, crisp=True), DF)
    cells.append((before, after))
cols = 4
cw = max(b.width + a.width for b, a in cells) + 30
ch = max(max(b.height, a.height) for b, a in cells) + 12
rows = (len(cells) + cols - 1) // cols
bg = grass(cols * cw, rows * ch, 5)
for i, (b, a) in enumerate(cells):
    x, y = (i % cols) * cw + 6, (i // cols) * ch + 4
    bg.alpha_composite(b, (x, y + ch - 8 - b.height))
    bg.alpha_composite(a, (x + b.width + 14, y + ch - 8 - a.height))
sheet = bg.resize((bg.width * SC, bg.height * SC), Image.NEAREST)
out = os.path.join(ROOT, 'docs', 'img', 'art32_rosliny.png')
sheet.save(out)
print('ok', sheet.size)
