import math, random
from PIL import Image
from sprites import *
from sheet import render

import os
OUT = os.environ.get("OUT", os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
W, H = 272, 200
OX, OY = 24, 22   # przesunięcie świata (więcej morza wokół wyspy)
FRAMES = 48
rnd = random.Random(7)

# ---------------- teren (statyczny) ----------------
cx, cy, RX, RY = 112 + OX, 72 + OY, 94, 50
land = set()
for y in range(H):
    for x in range(W):
        t = math.atan2(y - cy, x - cx)
        k = 1 + 0.045 * math.sin(3 * t + 0.5) + 0.03 * math.sin(7 * t + 1)
        if ((x - cx) / (RX * k)) ** 2 + ((y - cy) / (RY * k)) ** 2 <= 1:
            land.add((x, y))

CLIFF = ['g1', 'e2', 'e2', 'e2', 'e1', 'e1', 'p1', 'p1', 'p0']
cliff = {}
for (x, y) in land:
    if (x, y + 1) not in land:
        for d in range(1, len(CLIFF) + 1):
            if (x, y + d) in land:
                break
            k = CLIFF[d - 1]
            if d == 2 and rnd.random() < 0.3: k = 'g1'
            if 3 <= d <= 7 and rnd.random() < 0.08: k = 'e0'
            cliff[(x, y + d)] = k
solid = land | set(cliff)
outline_px = set()
for (x, y) in solid:
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        p = (x + dx, y + dy)
        if p not in solid: outline_px.add(p)
blocked = solid | outline_px
shadow = set()
for (x, y) in outline_px:
    for d in range(1, 4):
        p = (x, y + d)
        if p not in blocked: shadow.add(p)


def ring(src, excl):
    out = set()
    for (x, y) in src:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            p = (x + dx, y + dy)
            if p not in excl: out.add(p)
    return out

ring1 = ring(outline_px, blocked)
ring2 = ring(ring1, blocked | ring1)

surface = {}
for p in land:
    surface[p] = 'g2'
    if (p[0], p[1] + 1) not in land: surface[p] = 'g1'
TUFT = [".##.##.", "#..#..#"]
TUFT_S = [".##.", "#..#"]
for _ in range(34):
    t = TUFT if rnd.random() < 0.5 else TUFT_S
    x0, y0 = rnd.randrange(W), rnd.randrange(H)
    pts = [(x0 + i, y0 + j) for j, r in enumerate(t) for i, c in enumerate(r) if c == '#']
    if all((x, y + 2) in land and (x, y + 3) in land for x, y in pts):
        for p in pts: surface[p] = 'g1'
for _ in range(16):
    x, y = rnd.randrange(W), rnd.randrange(H)
    if (x, y + 3) in land and (x, y) in land:
        surface[(x, y)] = rnd.choice(['cr', 'co', 'go'])

waves = [(rnd.randrange(W), rnd.randrange(H), rnd.choice((2, 3, 4))) for _ in range(45)]
deep = [(rnd.randrange(W), rnd.randrange(H), rnd.choice((2, 3, 5))) for _ in range(30)]

# ---------------- sprite'y ----------------
def img(s):
    return render(s), s.ax, s.ay

S = {
    'plot': img(plot()), 'seed': img(seed()),
    'c1': img(carrot(1)), 'c2': img(carrot(2)), 'c3': img(carrot(3)),
    'w1': img(wheat(1)), 'w2': img(wheat(2)), 'w3': img(wheat(3)),
    'p1': img(pumpkin(1)), 'p2': img(pumpkin(2)), 'p3': img(pumpkin(3)),
    'tree': img(round_tree()), 'spruce': img(spruce()), 'bush': img(bush()),
    'rock': img(rock()), 'crate': img(crate()), 'coin': img(coin()),
    'item': img(carrot_item()), 'spark': [img(sparkle(i)) for i in range(4)],
}

from water import Water, BAYER as WATER_BAYER
WATER = Water(W, H, blocked, shadow, ring1, ring2, FRAMES)

PX0, PY0 = 70 + OX, 56 + OY
def plot_pos(col, row):
    return PX0 + col * 20, PY0 + row * 17

LAYOUT = [['c3', 'c3', 'w3', 'w2'], ['p3', 'p2', 'c2', 'seed']]
OBJECTS = [('tree', 40, 66, 22), ('tree', 170, 50, 22), ('spruce', 46, 104, 14), ('spruce', 186, 92, 14),
           ('bush', 84, 114, 16), ('rock', 150, 114, 14), ('crate', 160, 80, 18), ('rock', 30, 80, 12)]
OBJECTS = [(n, x + OX, y + OY, w) for n, x, y, w in OBJECTS]

PALETTE_KEYS = list(PAL)
PAL_FLAT = [c for k in PALETTE_KEYS for c in RGB[k]]
PAL_IDX = {RGB[k]: i for i, k in enumerate(PALETTE_KEYS)}


def draw(frame, spr, X, Y, sway=None, sx=1.0, sy=1.0, dy=0):
    im, ax, ay = spr
    if sx != 1.0 or sy != 1.0:
        nw, nh = max(1, round(im.width * sx)), max(1, round(im.height * sy))
        im = im.resize((nw, nh), Image.NEAREST)
        ax, ay = round(ax * sx), round(ay * sy)
    for y in range(im.height):
        shift = sway(ay - y, im.height) if sway else 0
        row = im.crop((0, y, im.width, y + 1))
        frame.alpha_composite(row, (X - ax + shift, Y - ay + y + dy)) if 0 <= Y - ay + y + dy < H and X - ax + shift >= 0 \
            else _safe(frame, row, X - ax + shift, Y - ay + y + dy)


def _safe(frame, row, x, y):
    if not (0 <= y < H): return
    for i in range(row.width):
        if 0 <= x + i < W:
            p = row.getpixel((i, 0))
            if p[3]: frame.putpixel((x + i, y), p)


def sway_fn(f, phase, amp=1.0, speed=2):
    s = math.sin(2 * math.pi * speed * f / FRAMES + phase)
    return lambda h, total: int(round(amp * s * max(0, h) / total))


def put(frame, x, y, k):
    if 0 <= x < W and 0 <= y < H:
        frame.putpixel((x, y), RGB[k] + (255,))


def text_img(t):
    return img(text(t))


# sparkles: (start, x, y)
SPARK_SPOTS = [(1, 0), (2, 0), (0, 1)]
sparks = []
for i in range(12):
    col, row = SPARK_SPOTS[i % 3]
    px, py = plot_pos(col, row)
    sparks.append((i * 4 + rnd.randrange(3), px + rnd.randrange(3, 16), py - rnd.randrange(4, 14)))

HX, HY = plot_pos(0, 0)
HCX, HCY = HX + 9, HY + 10
COIN_TARGET = (7, 8)
coin_starts = [13, 15, 17]
COIN_T = 9
dirt = [(rnd.uniform(-1.2, 1.2), rnd.uniform(-2.2, -1.2)) for _ in range(5)]
GX, GY = plot_pos(1, 1)
leaves_burst = [(math.cos(a) * 1.6, math.sin(a) * 1.6 - 0.8) for a in [i * math.pi / 3 + 0.3 for i in range(6)]]


def bez(p0, p1, p2, t):
    return tuple((1 - t) ** 2 * a + 2 * (1 - t) * t * b + t * t * c for a, b, c in zip(p0, p1, p2))


def make_frame(f, ui=True):
    fr = Image.new("RGBA", (W, H), RGB['b1'] + (255,))
    px = fr.load()
    # woda (warstwy)
    if WATER is not None:
        WATER.draw(lambda x, y, k: put(fr, x, y, k), f)
    for p in outline_px: put(fr, *p, 'o')
    for p, k in cliff.items(): put(fr, *p, k)
    for p, k in surface.items(): put(fr, *p, k)
    # grządki
    for row in range(2):
        for col in range(4):
            x, y = plot_pos(col, row)
            fr.alpha_composite(S['plot'][0], (x, y))
    # cienie obiektów
    for name, x, y, sw in OBJECTS:
        for dx in range(-sw // 2, sw // 2 + 1):
            for d in (-1, 0, 1):
                if abs(dx) <= sw // 2 - abs(d) * 2:
                    put(fr, x + dx, y + d, 'g1')
    # lista rysowania (sortowanie po y)
    items = []
    for name, x, y, sw in OBJECTS:
        amp = 1.0 if name in ('tree', 'spruce') else 0.0
        items.append((y, lambda x=x, y=y, name=name, amp=amp: draw(
            fr, S[name], x, y, sway_fn(f, x * 0.05, amp, 1) if amp else None)))
    for row in range(2):
        for col in range(4):
            x, y = plot_pos(col, row)
            bx, by = x + 9, y + 10
            key = LAYOUT[row][col]
            if (col, row) == (0, 0):
                if f >= 10: continue
            if (col, row) == (1, 1):
                if f >= 34:
                    sx, sy = {34: (0.85, 1.3), 35: (1.15, 0.85)}.get(f, (1.0, 1.0))
                    items.append((by, lambda bx=bx, by=by, sx=sx, sy=sy: draw(fr, S['p3'], bx, by, None, sx, sy)))
                    continue
            sway = sway_fn(f, bx * 0.15, 1.0, 2) if key[0] in 'cwp' and key != 'seed' else None
            items.append((by, lambda key=key, bx=bx, by=by, sway=sway: draw(fr, S[key], bx, by, sway)))
    for _, fn in sorted(items, key=lambda t: t[0]):
        fn()

    # --- efekty: zbiór marchwi ---
    if 10 <= f <= 11:
        draw(fr, S['c3'], HCX, HCY, None, dy=-2 if f == 10 else -5)
    if 12 <= f <= 13:
        draw(fr, S['item'], HCX, HCY - (8 if f == 12 else 11))
    if 11 <= f <= 17:
        t = f - 11
        for vx, vy in dirt:
            put(fr, round(HCX + vx * t), round(HCY - 2 + vy * t + 0.35 * t * t), 'e2')
    # iskierki
    for start, x, y in sparks:
        i = f - start
        if 0 <= i < 4 and not (x < HX + 18 and y < HY + 8 and f >= 10):
            draw(fr, S['spark'][i], x, y)
    # pop wzrostu dyni
    if 34 <= f <= 41:
        t = f - 34
        for i, (vx, vy) in enumerate(leaves_burst):
            x, y = round(GX + 9 + vx * t), round(GY + 2 + vy * t + 0.25 * t * t)
            put(fr, x, y, 'g3' if i % 2 else 'g2'); put(fr, x + 1, y, 'g1')
    # spadający liść
    if f < 40:
        ly = min(34 + f, 64) + OY
        lx = (30 + round(2 * math.sin(f * 0.6)) if ly < 64 + OY else 30) + OX
        put(fr, lx, ly, 'g3'); put(fr, lx + 1, ly, 'g2')

    if os.environ.get("DARK") and not os.environ.get("NO_FF"):
        fl = random.Random(5)
        for i in range(10):
            x0, y0 = fl.randrange(30, 190), fl.randrange(30, 120)
            ph = fl.randrange(FRAMES)
            x = x0 + round(3 * math.sin((f + ph) * 0.25)); y = y0 + round(2 * math.cos((f + ph) * 0.18))
            if (f + ph) % 16 < 11:
                put(fr, x, y, 'cr')
                if (f + ph) % 16 in (3, 4, 5, 6):
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)): put(fr, x + dx, y + dy, 'go')
    # mgła: tylko nad morzem, gęstnieje ku krawędziom okna, pasma dryfują (pętla co FRAMES klatek)
    T = 2 * math.pi * f / FRAMES
    for (x, y) in WATER.base:
        e = min(x, y, W - 1 - x, H - 1 - y)
        base = max(0.0, 1 - e / 30)
        if base <= 0: continue
        n = (0.5 + 0.28 * math.sin(2 * math.pi * x / 68 + T) * math.sin(2 * math.pi * y / 29 + 0.7)
             + 0.22 * math.sin(2 * math.pi * (x / 45 + y / 90) - 2 * T))
        v = base * (0.45 + 0.7 * n)
        th = WATER_BAYER[y % 4][x % 4] / 16
        if v > 0.85: put(fr, x, y, 'fo')
        elif v > 0.62: put(fr, x, y, 'fo' if th < (v - 0.62) / 0.23 else 'b3')
        elif v > 0.35 and th < (v - 0.35) / 0.27: put(fr, x, y, 'b3')
    if not ui:
        return fr
    # --- UI ---
    coins = 125 + sum(1 for s0 in coin_starts if f >= s0 + COIN_T)
    bump = any(f == s0 + COIN_T for s0 in coin_starts)
    draw(fr, S['coin'], 7, 10 - (1 if bump else 0))
    ti = text_img(str(coins))
    draw(fr, ti, 14, 5 - (1 if bump else 0))
    for s0 in coin_starts:
        t = (f - s0) / COIN_T
        if 0 <= t < 1:
            x, y = bez((HCX, HCY - 12), (HCX - 30, HCY - 50), COIN_TARGET, t)
            draw(fr, S['coin'], round(x), round(y) + 4)
    bx, by, bw = W - 50, 4, 44
    for x in range(bx, bx + bw):
        for y in range(by, by + 7):
            put(fr, x, y, 'o' if x in (bx, bx + bw - 1) or y in (by, by + 6) else 'p0')
    fill = 22 + f // 3
    for x in range(bx + 1, bx + 1 + fill):
        for y in range(by + 1, by + 6):
            put(fr, x, y, 'g3' if y == by + 1 else ('g1' if y == by + 5 else 'g2'))
    if f % 4 < 2:
        for y in range(by + 1, by + 6): put(fr, bx + fill, y, 'cr')
    # ikona "aktywność" (zegar)
    ck = Sprite(9, 9, 4, 8, off=(1, 1))
    ck.blob(3.5, 3.5, 3.4, 3.4, ['b3', 'fo', 'fo'])
    ck.set(3, 1, 'o'); ck.set(3, 2, 'o'); ck.set(3, 3, 'o'); ck.set(4, 3, 'o'); ck.set(5, 3, 'o')
    draw(fr, img(ck.outline()), bx - 6, by + 7)
    return fr


def to_p(fr):
    p = Image.new("P", fr.size)
    p.putpalette(PAL_FLAT)
    p.putdata([PAL_IDX.get(c[:3], 0) for c in fr.get_flattened_data()])
    return p


if __name__ == "__main__":
    frames = [make_frame(f) for f in range(FRAMES)]
    big = [to_p(fr).resize((W * 3, H * 3), Image.NEAREST) for fr in frames]
    big[0].save(OUT + "prototyp.gif", save_all=True, append_images=big[1:], duration=100, loop=0, disposal=1)
    frames[5].resize((W * 4, H * 4), Image.NEAREST).save(OUT + "prototyp_klatka.png")

    # wieczór (demo LUT): ta sama klatka, kolory przesunięte w stronę śliwki/błękitu
    ev = frames[5].copy().convert("RGB")
    tint = RGB['p1']
    lut = {}
    def eve(c):
        if c not in lut:
            r, g, b = c
            lum = (r * 0.3 + g * 0.59 + b * 0.11) / 255
            k = 0.55 - 0.2 * lum
            lut[c] = (int(r * (1 - k) + tint[0] * k * 0.9), int(g * (1 - k) * 0.95 + tint[1] * k),
                      int(b * (1 - k) + tint[2] * k * 1.25))
        return lut[c]
    ev.putdata([eve(c) for c in ev.get_flattened_data()])
    fl = random.Random(3)
    for _ in range(18):
        x, y = fl.randrange(W), fl.randrange(20, H - 20)
        if (x, y) in land:
            ev.putpixel((x, y), RGB['cr']); ev.putpixel((x + 1, y), RGB['go'])
    combo = Image.new("RGB", (W * 2 + 4, H), RGB['o'])
    combo.paste(frames[5].convert("RGB"), (0, 0)); combo.paste(ev, (W + 4, 0))
    combo.resize((combo.width * 3, H * 3), Image.NEAREST).save(OUT + "dzien_wieczor.png")
    print("ok")
