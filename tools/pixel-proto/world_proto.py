"""Losowy start (wyspa 2x2 o losowym kształcie) i losowy świat 32x32 z ziarna: strefy, znaleziska, dziwne kafelki."""
import math, random
from PIL import Image, ImageDraw, ImageFont
import tiles_proto as TP
import ui_kit as K
import sprites3 as S3
import sprites
from sprites import RGB

OUT = TP.OUT
WORLD = 32
START = [(15, 15), (16, 15), (15, 16), (16, 16)]
ZONES = [('cemetery', 'CMENTARZ'), ('swamp', 'BAGNO'), ('ruins', 'RUINY')]


def price(n):
    """n = kafelki kupione od startu. Łagodna krzywa (kwadratowa) na świat 32x32."""
    return round(10 + 4 * n + 0.06 * n * n), 3 + n // 8


def gen_world(seed):
    rnd = random.Random(seed)
    types = {}
    # strefy: w różnych kierunkach (min. 90 stopni odstępu), 6-12 kafelków od startu, kształt = losowa plama
    angles = []
    while len(angles) < len(ZONES):
        a = rnd.uniform(0, 2 * math.pi)
        if all(abs((a - b + math.pi) % (2 * math.pi) - math.pi) > math.pi / 2 for b in angles):
            angles.append(a)
    zones = []
    for (ztype, name), a in zip(ZONES, angles):
        d = rnd.uniform(6, 12)
        cx, cy = 15.5 + d * math.cos(a), 15.5 + d * math.sin(a)
        size = rnd.randint(8, 14)
        cells = {(int(cx), int(cy))}
        while len(cells) < size:
            c, r = rnd.choice(sorted(cells))
            dc, dr = rnd.choice(((1, 0), (-1, 0), (0, 1), (0, -1)))
            p = (min(WORLD - 2, max(1, c + dc)), min(WORLD - 2, max(1, r + dr)))
            cells.add(p)
        for p in cells: types[p] = ztype
        zones.append((ztype, name, (int(cx), int(cy))))
    # znaleziska: ok. 6% kafelków, gęściej przy strefach; dziwne kafelki: 1 źródło, 1 pradawny kamień
    finds = {}
    loot = ['coins'] * 5 + ['nightseed'] * 3 + ['shard'] * 2 + ['lore'] * 2
    for c in range(WORLD):
        for r in range(WORLD):
            if (c, r) in START: continue
            near = min(math.hypot(c - z[2][0], r - z[2][1]) for z in zones)
            if rnd.random() < (0.12 if near < 4 else 0.05):
                finds[(c, r)] = rnd.choice(loot)
    for special in ('spring', 'monolith'):
        while True:
            p = (rnd.randrange(3, WORLD - 3), rnd.randrange(3, WORLD - 3))
            if p not in types and p not in START and math.hypot(p[0] - 15.5, p[1] - 15.5) > 5:
                finds[p] = special
                break
    return types, finds, zones


def start_island(seed):
    rnd = random.Random(seed)
    tiles = {p: 'grass' for p in START}
    soil = rnd.sample(START, 2)
    for p in soil: tiles[p] = 'soil'
    return tiles, soil


def render_start(seed):
    tiles, soil = start_island(seed)
    TP.OX, TP.OY = 136 - 16 * 16, 100 - 16 * 16
    isl = TP.build(tiles, seed=seed)
    rnd = random.Random(seed + 1)
    objs = [(rnd.choice([sprites.carrot(1), sprites.seed(), sprites.wheat(1)]), *mid(p, 12)) for p in soil]
    grass = [p for p in START if p not in soil]
    deco = rnd.choice([sprites.round_tree(), sprites.rock(), sprites.bush(), S3.dead_tree()])
    objs.append((deco, *mid(rnd.choice(grass), 13)))
    cv = TP.render(isl, objs, fog_from=14, fog_span=30)
    view = cv.img.crop((136 - 72, 100 - 56, 136 + 72, 100 + 56))
    return view


def mid(p, dy):
    x, y = TP.tile_rect(*p)
    return x + 8, y + dy


def minimap(seed, cell=10):
    types, finds, zones = gen_world(seed)
    col = {'cemetery': 'p2', 'swamp': 'g3', 'ruins': 'b3'}
    fcol = {'coins': 'go', 'nightseed': 'fo', 'shard': 'wl', 'lore': 'co', 'spring': 'wi', 'monolith': 'ml'}
    img = Image.new("RGB", (WORLD * cell, WORLD * cell), RGB['b0'])
    d = ImageDraw.Draw(img)
    for c in range(WORLD):
        for r in range(WORLD):
            x, y = c * cell, r * cell
            k = 'g2' if (c, r) in START else col.get(types.get((c, r)), None)
            if k: d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=RGB[k])
            if (c, r) in finds:
                f = finds[(c, r)]
                s = 2 if f in ('spring', 'monolith') else 1
                d.rectangle([x + 4 - s, y + 4 - s, x + 5 + s, y + 5 + s], fill=RGB[fcol[f]])
    for i in range(WORLD + 1):
        d.line([(i * cell, 0), (i * cell, WORLD * cell)], fill=RGB['b1'])
        d.line([(0, i * cell), (WORLD * cell, i * cell)], fill=RGB['b1'])
    return img


def main():
    S = 3
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 14)
    fs = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 11)
    seeds = [11, 23, 42, 77, 128, 404]
    starts = [render_start(s) for s in seeds]
    sw, sh = starts[0].width * S, starts[0].height * S
    sheet = Image.new("RGB", (sw * 3 + 40, sh * 2 + 70), RGB['o'])
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "Losowe starty: wyspa 2x2 (2 grządki + losowy obiekt), okno startowe 144x112 (x3 = 432x336)",
           fill=RGB['cr'], font=f)
    for i, (sd, im) in enumerate(zip(seeds, starts)):
        x, y = 10 + (i % 3) * (sw + 10), 34 + (i // 3) * (sh + 18)
        sheet.paste(im.convert("RGB").resize((sw, sh), Image.NEAREST), (x, y))
        d.text((x + 4, y + sh + 2), f"ziarno {sd}", fill=RGB['b3'], font=fs)
    sheet.save(OUT + "wyspa_starty.png")

    maps = [minimap(s) for s in seeds[:3]]
    mw = maps[0].width
    sheet = Image.new("RGB", (mw * 3 + 40, mw + 120), RGB['o'])
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "Świat 32x32 z ziarna (pod mgłą): strefy, znaleziska, dziwne kafelki", fill=RGB['cr'], font=f)
    for i, im in enumerate(maps):
        sheet.paste(im, (10 + i * (mw + 10), 34))
        d.text((10 + i * (mw + 10), 38 + mw), f"ziarno {seeds[i]}", fill=RGB['b3'], font=fs)
    legend = [('g2', 'start 2x2'), ('p2', 'cmentarz'), ('g3', 'bagno'), ('b3', 'ruiny'), ('go', 'monety'),
              ('fo', 'nasiono nocne'), ('wl', 'odłamek'), ('co', 'lore'), ('wi', 'źródło'), ('ml', 'pradawny kamień')]
    for i, (k, t) in enumerate(legend):
        x, y = 10 + (i % 5) * 140, mw + 60 + (i // 5) * 20
        d.rectangle([x, y + 2, x + 10, y + 12], fill=RGB[k]); d.text((x + 16, y), t, fill=RGB['cr'], font=fs)
    sheet.save(OUT + "swiat_mapy.png")

    for n in (0, 10, 50, 100, 300, 600, 1020):
        print(n, price(n))
    print("ok")


if __name__ == "__main__":
    main()
