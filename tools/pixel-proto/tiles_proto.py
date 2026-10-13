"""Prototyp rozbudowy wyspy kafelkami 16x16: maska z kafelków -> zaokrąglenie (morfologia) -> klif, piana, woda, mgła.
Pokazuje: start, wyspę po kilku zakupach, tryb rozbudowy (ceny, znaleziska), strefę odkrytą we mgle."""
import os, math, random
import numpy as np
from PIL import Image
import mockups as M                 # Canvas, ikony, font (ustawia paletę i scenę)
import ui_kit as K
import sprites3 as S3
import sprites4 as S4
import sprites
from sprites import Sprite, RGB
from water import Water, BAYER

OUT = M.OUT
W, H, T = 272, 200, 16
OX, OY = 8, 4                        # przesunięcie siatki kafelków w kadrze
CLIFF = ['g1', 'e2', 'e2', 'e2', 'e1', 'e1', 'p1', 'p1', 'p0']


def disk(r):
    return [(dx, dy) for dy in range(-r, r + 1) for dx in range(-r, r + 1) if dx * dx + dy * dy <= r * r + r]


def erode(m, r):
    out = m.copy()
    for dx, dy in disk(r):
        out &= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


def dilate(m, r):
    out = m.copy()
    for dx, dy in disk(r):
        out |= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


def tile_rect(c, r):
    return OX + c * T, OY + r * T


def plot16():
    s = Sprite(16, 16, 0, 0)
    for y in range(1, 15):
        for x in range(1, 15):
            if (x in (1, 14)) and (y in (1, 14)):
                continue
            k = 'e1'
            if y % 4 == 2: k = 'e0'
            elif y % 4 == 3 and x % 4: k = 'e2'
            if y == 1: k = 'e0'
            s.set(x, y, k)
    return s


def build(tiles, seed=3):
    """tiles: {(c, r): typ} typ: grass/soil/cemetery/swamp."""
    m = np.zeros((H, W), bool)
    for (c, r) in tiles:
        x, y = tile_rect(c, r)
        m[max(0, y):y + T, max(0, x):x + T] = True
    m = dilate(erode(m, 6), 6)       # zaokrąglenie wypukłych narożników
    m = erode(dilate(m, 5), 5)       # wypełnienie wklęsłych
    # nieregularny brzeg: gładki szum (stały dla danego miejsca świata - kafelek nie zmienia sąsiadów)
    g = np.random.default_rng(seed).random((H // 8 + 2, W // 8 + 2))
    ys, xs = np.mgrid[0:H, 0:W] / 8.0
    y0, x0 = ys.astype(int), xs.astype(int); fy, fx = ys - y0, xs - x0
    noise = (g[y0, x0] * (1 - fx) * (1 - fy) + g[y0, x0 + 1] * fx * (1 - fy)
             + g[y0 + 1, x0] * (1 - fx) * fy + g[y0 + 1, x0 + 1] * fx * fy)
    edge_out = dilate(m, 3) & ~m
    edge_in = m & ~erode(m, 3)
    m = (m | (edge_out & (noise > 0.62))) & ~(edge_in & (noise < 0.3))
    m = erode(dilate(m, 1), 1)
    land = {(int(x), int(y)) for y, x in zip(*np.nonzero(m))}
    rnd = random.Random(seed)
    cliff = {}
    for (x, y) in land:
        if (x, y + 1) not in land:
            for d in range(1, len(CLIFF) + 1):
                if (x, y + d) in land: break
                k = CLIFF[d - 1]
                if d == 2 and rnd.random() < 0.3: k = 'g1'
                if 3 <= d <= 7 and rnd.random() < 0.08: k = 'e0'
                cliff[(x, y + d)] = k
    solid = land | set(cliff)
    outline = {(x + dx, y + dy) for (x, y) in solid for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} - solid
    blocked = solid | outline
    shadow = {(x, y + d) for (x, y) in outline for d in (1, 2, 3)} - blocked
    def ring(src, excl):
        return {(x + dx, y + dy) for (x, y) in src for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} - excl
    r1 = ring(outline, blocked); r2 = ring(r1, blocked | r1)
    water = Water(W, H, blocked, shadow, r1, r2, 48, seed=11)
    surface = {}
    for p in land:
        # granice między typami podłoża rozmyte szumem (bez widocznych kwadratów)
        jx = (S4.vnoise(p[0], p[1], 4, 21) - 0.5) * 9
        jy = (S4.vnoise(p[0], p[1], 4, 22) - 0.5) * 9
        c, r = int((p[0] + jx - OX) // T), int((p[1] + jy - OY) // T)
        t = tiles.get((c, r), tiles.get(((p[0] - OX) // T, (p[1] - OY) // T), 'grass'))
        if t == 'soil': t = 'grass'
        k = S4.ground(t, p[0], p[1]) if t in ('cemetery', 'swamp', 'ruins') else 'g2'
        if (p[0], p[1] + 1) not in land: k = 'g1'
        surface[p] = k
    for _ in range(len(land) // 60):
        x, y = rnd.choice(sorted(land))
        pts = [(x, y), (x + 1, y - 1), (x + 2, y), (x + 3, y - 1), (x + 4, y)]
        if all((px, py + 3) in land for px, py in pts):
            for p in pts: surface[p] = 'g1'
    return dict(land=land, cliff=cliff, outline=outline, water=water, surface=surface, tiles=tiles)


def render(isl, objects=(), fog_from=22, fog_span=40, hidden=()):
    img = Image.new("RGBA", (W, H), RGB['b1'] + (255,))
    c = M.Canvas(img)
    isl['water'].draw(c.put, 0)
    for p in isl['outline']: c.put(*p, 'o')
    for p, k in isl['cliff'].items(): c.put(*p, k)
    for p, k in isl['surface'].items(): c.put(*p, k)
    p16 = plot16()
    for (cc, rr), t in sorted(isl['tiles'].items(), key=lambda kv: kv[0][1]):
        x, y = tile_rect(cc, rr)
        if t == 'soil': c.sprite(p16, x, y)
    for spr, x, y in sorted(objects, key=lambda o: o[2]):
        c.sprite(spr, x - spr.ax, y - spr.ay)
    # mgła liczona od odległości od lądu
    dist = isl['water'].dist
    for (x, y) in isl['water'].base:
        d = dist[y][x]
        v = (d - fog_from) / fog_span + 0.18 * math.sin(x / 9.0) * math.sin(y / 7.0)
        th = BAYER[y % 4][x % 4] / 16
        if v > 0.85: c.put(x, y, 'fo')
        elif v > 0.55: c.put(x, y, 'fo' if th < (v - 0.55) / 0.3 else 'b3')
        elif v > 0.25 and th < (v - 0.25) / 0.3: c.put(x, y, 'b3')
    # coś czeka we mgle: ciemne sylwetki NAD mgłą (cień we mgle), środek lekko ditherowany
    for spr, x, y in hidden:
        for (sx, sy), k in spr.px.items():
            px, py = x - spr.ax + sx, y - spr.ay + sy
            if k == 'o': c.put(px, py, 'b1')
            elif (px + py) % 2 == 0: c.put(px, py, 'b1')
            else: c.put(px, py, 'b3')
    return c


def crops_on(tiles, choices):
    objs = []
    for (cc, rr), t in tiles.items():
        if t == 'soil' and (cc, rr) in choices:
            x, y = tile_rect(cc, rr)
            objs.append((choices[(cc, rr)], x + 8, y + 12))
    return objs


def neighbors(tiles):
    cand = set()
    for (c, r) in tiles:
        for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            p = (c + dc, r + dr)
            if p not in tiles: cand.add(p)
    return sorted(cand)


def price(n_owned, start):
    k = n_owned - start
    return int(30 * 1.15 ** k), 5 + k


def main():
    start = {(c, r): 'grass' for c in range(6, 11) for r in range(4, 8)}
    for c in range(7, 10): start[(c, 5)] = 'soil'
    crops = {(7, 5): sprites.carrot(3), (8, 5): sprites.wheat(2), (9, 5): sprites.carrot(1)}

    b = dict(start)
    for p in [(11, 5), (11, 6), (5, 6), (8, 8), (9, 8), (7, 3), (10, 3), (12, 6)]: b[p] = 'grass'
    for p in [(7, 6), (8, 6), (9, 6)]: b[p] = 'soil'
    crops_b = dict(crops); crops_b.update({(7, 6): sprites.pumpkin(2), (8, 6): sprites.carrot(2), (9, 6): sprites.seed()})

    cc = dict(b)
    for p in [(12, 5), (13, 5), (13, 4), (12, 4), (14, 4), (13, 3), (14, 5), (11, 4), (11, 3), (12, 3),
              (5, 5), (4, 6), (5, 7), (4, 7), (10, 8), (11, 7), (6, 8)]:
        cc[p] = 'grass'
    for p in [(12, 3), (13, 3), (13, 4), (14, 4), (12, 4)]: cc[p] = 'cemetery'
    cc[(4, 7)] = 'swamp'; cc[(5, 7)] = 'swamp'
    for p in [(10, 6), (10, 7)]: cc[p] = 'soil'
    crops_c = dict(crops_b); crops_c.update({(10, 6): S3.ghost_pumpkin(3), (10, 7): S3.moonflower(2)})

    stone = lambda: S3.gravestone(0)
    hidden = [(S3.gravestone(0), 222, 66), (S3.gravestone(1), 236, 60), (S3.dead_tree(), 248, 74),
              (S3.reeds(), 30, 132), (S3.broken_pillar(), 130, 186)]
    def objs_for(stage_tiles, cr, extra=()):
        o = crops_on(stage_tiles, cr)
        o += [(sprites.round_tree(), OX + 6 * T + 4, OY + 5 * T + 2)]
        o += list(extra)
        return o

    panels = []
    isA = build(start); panels.append(("START: 20 KAFELKÓW", render(isA, objs_for(start, crops))))
    isB = build(b); panels.append(("+11 KAFELKÓW", render(isB, objs_for(b, crops_b))))
    graves = [(S3.gravestone(0), OX + 12 * T + 8, OY + 3 * T + 13), (S3.gravestone(1), OX + 13 * T + 9, OY + 4 * T + 12),
              (S3.gravestone(2), OX + 14 * T + 7, OY + 4 * T + 13), (S3.iron_fence(), OX + 13 * T + 8, OY + 3 * T + 4),
              (S3.reeds(), OX + 4 * T + 8, OY + 7 * T + 4), (S3.dead_tree(), OX + 13 * T + 10, OY + 5 * T + 14)]
    isC = build(cc); panels.append(("+28: CMENTARZ I BAGNO ODKRYTE", render(isC, objs_for(cc, crops_c, graves))))
    S = 2
    sheet = Image.new("RGB", (W * S * 3 + 16, H * S + 30), RGB['o'])
    for i, (title, cv) in enumerate(panels):
        K.draw_text(cv.put, 4, 4, title, 'cr', 'o')
        sheet.paste(cv.img.convert("RGB").resize((W * S, H * S), Image.NEAREST), (i * (W * S + 8), 30))
    sheet.save(OUT + "wyspa_etapy.png")

    # tryb rozbudowy na etapie B
    cv = render(isB, objs_for(b, crops_b))
    owned = len(b)
    finds = {(12, 5): 'starshard', (5, 5): 'nightcrop'}
    for i, (c_, r_) in enumerate(neighbors(b)):
        x, y = tile_rect(c_, r_)
        if not (0 <= x < W - 15 and 0 <= y < H - 15): continue
        sel = (c_, r_) == (12, 5)
        k = 'fl' if sel else 'wl'
        for t in range(16):
            if t % 3 != 2 or sel:
                for px, py in ((x + t, y), (x + t, y + 15), (x, y + t), (x + 15, y + t)):
                    cv.put(px, py, k)
        if (c_, r_) in finds:
            cv.text(x + 6, y + 4, "?", 'wi')
        else:
            p, _ = price(owned, len(start))
            txt = str(p)
            cv.text(x + 8 - K.text_width(txt) // 2, y + 4, txt, 'cr')
    p, e = price(owned, len(start))
    tx, ty = W - 112, 24
    cv.nine('ui_tooltip', tx, ty, 108, 44)
    cv.text(tx + 4, ty + 3, "NOWY KAFELEK", 'fl')
    cv.icon('coin', tx + 4, ty + 11); cv.text(tx + 17, ty + 13, str(p))
    cv.icon('essence', tx + 40, ty + 11); cv.text(tx + 53, ty + 13, str(e))
    cv.text(tx + 4, ty + 25, "COŚ BŁYSZCZY WE MGLE...", 'wi')
    cv.text(tx + 4, ty + 34, "LPM: WYRWIJ Z MGŁY", 'b3')
    cv.nine('ui_tooltip', 4, H - 22, 150, 18)
    cv.text(8, H - 18, f"ROZBUDOWA: {owned} KAFELKÓW", 'cr')
    cv.save("wyspa_rozbudowa.png")
    print("ok")


if __name__ == "__main__":
    main()
