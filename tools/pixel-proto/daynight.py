"""Dark fantasy: lampy + oświetlenie zależne od pory dnia (pikselowe pasma światła z ditheringiem)."""
import os, sys, math, random
os.environ["DARK"] = "1"
os.environ["NO_FF"] = "1"
import sprites
from run_dark_palette import DARK
EXTRA = {
    'fl': "#ffe08a", 'fx': "#f08a3a",          # płomień: rdzeń / zewnętrzny
    'wi': "#5fcfb8", 'wl': "#d2fff0",          # błędny ognik (zimne, upiorne światło)
    'mu': "#7a4f9a", 'ml': "#d49af0",          # świecące grzyby
}
for d in (DARK, EXTRA):
    sprites.PAL.update(d)
    sprites.RGB.update({k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for k, v in d.items()})

from sprites import Sprite, RGB
import anim
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/"
os.makedirs(os.path.dirname(OUT), exist_ok=True)
W, H = anim.W, anim.H
EMISSIVE = {'fl', 'fx', 'wi', 'wl', 'ml'}
OFF = {'fl': 'p1', 'fx': 'p2', 'wi': 'b2', 'wl': 'b3', 'ml': 'mu'}


# ---------------- lampy ----------------
def lantern_post(f):
    s = Sprite(18, 34, 6, 32)
    for y in range(8, 33):
        x = 6 + (1 if 14 < y < 24 else 0)
        s.set(x, y, 'e2'); s.set(x + 1, y, 'e1')
    s.line(6, 7, 12, 6, 'e2'); s.set(5, 8, 'e2'); s.set(4, 9, 'e1')       # ramię z zawijasem
    s.set(12, 7, 'p1'); s.set(12, 8, 'p1')                                # haczyk
    s.rect(10, 9, 14, 10, 'p1'); s.set(12, 8, 'p1')                       # daszek
    for y in range(11, 17):
        for x in range(10, 15):
            edge = x in (10, 14) or y == 16
            s.set(x, y, 'p1' if edge else ('fl' if (x == 12 and 12 <= y <= 14) else 'fx'))
    if f % 3 == 1: s.set(11, 12, 'fl')
    if f % 3 == 2: s.set(13, 13, 'fl')
    s.rect(11, 17, 13, 17, 'p1')
    for x in (4, 5, 8, 9): s.set(x, 32, 'e1')
    return s.outline()


def skull_candle(f):
    s = Sprite(14, 22, 7, 20)
    s.blob(7, 15, 5, 4.4, ['b3', 'cr', 'cr'])
    s.rect(4, 18, 10, 20, 'cr'); s.rect(4, 20, 10, 20, 'b3')
    for x in (5, 7, 9): s.set(x, 19, 'o')                                 # zęby
    s.rect(4, 14, 5, 16, 'o'); s.rect(8, 14, 9, 16, 'o')                  # oczodoły
    s.set(5, 15, 'fx'); s.set(8, 15, 'fx')                                # żarzące się oczy
    s.set(7, 17, 'o')
    s.rect(6, 7, 7, 11, 'cr'); s.set(8, 10, 'cr'); s.set(8, 11, 'cr'); s.set(5, 11, 'cr')  # świeca z woskiem
    s.set(7, 7, 'b3')
    s.outline()
    s.set(6 + (f % 2), 5, 'fx'); s.set(6, 6, 'fl'); s.set(7, 6, 'fx')    # płomień (bez konturu)
    if f % 3 == 0: s.set(6, 4, 'fx')
    return s


def wisp_cage(f):
    s = Sprite(16, 30, 8, 28)
    s.line(8, 28, 8, 17, 'e1'); s.line(7, 28, 7, 18, 'e2')
    s.rect(3, 5, 12, 5, 'p1'); s.rect(3, 16, 12, 16, 'p1'); s.rect(6, 3, 9, 4, 'p1')
    wy = 10.5 + (0.5 if f % 4 < 2 else -0.5)
    s.blob(7.5, wy, 3, 3.4, ['wi', 'wi', 'wl'])
    s.set(7, int(wy) - 4, 'wi')
    for x in (3, 6, 9, 12):
        for y in range(6, 16): s.set(x, y, 'p1')
    return s.outline()


def mushrooms(f):
    s = Sprite(20, 14, 10, 12)
    for cx, cy, r, sh in ((6, 7, 3.6, 4), (13, 5, 4.6, 6), (16.5, 9, 2.4, 3)):
        s.rect(int(cx) - 1, int(cy), int(cx), int(cy) + sh, 'cr')
        s.blob(cx, cy, r, r * 0.65, ['mu', 'mu', 'ml'], clip_y=int(cy))
    for x, y in ((12, 3), (14, 4), (5, 6)): s.set(x, y, 'ml')
    return s.outline()


LAMPS = [  # (sprite fn, x, y, (światło x, y), kolor światła, promień)
    (lantern_post, 62, 96, (68, 82), (1.25, 0.85, 0.45), 38),
    (skull_candle, 122, 108, (122, 96), (1.2, 0.8, 0.45), 24),
    (wisp_cage, 154, 54, (153, 42), (0.45, 1.15, 1.05), 34),
    (mushrooms, 128, 126, (128, 120), (1.0, 0.55, 1.2), 20),
]
LAMPS = [(fn, x + anim.OX, y + anim.OY, (lx + anim.OX, ly + anim.OY), c, r) for fn, x, y, (lx, ly), c, r in LAMPS]
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def stamp(fr, spr, X, Y, lit):
    emis = []
    for (x, y), k in spr.px.items():
        tx, ty = X - spr.ax + x, Y - spr.ay + y
        if not (0 <= tx < W and 0 <= ty < H): continue
        if k in EMISSIVE:
            if lit: emis.append((tx, ty, RGB[k])); k = OFF[k] if k != 'ml' else 'mu'
            else: k = OFF[k]
        fr.putpixel((tx, ty), RGB[k] + (255,))
    return emis


def light(fr, f, amb, amb_col, lit):
    px = fr.load()
    lamps = []
    for i, (_, _, _, (lx, ly), col, r) in enumerate(LAMPS):
        flick = 1 + 0.06 * math.sin(f * 1.7 + i * 2) + 0.04 * math.sin(f * 3.1 + i)
        lamps.append((lx, ly, col, r * flick))
    for y in range(H):
        for x in range(W):
            mr, mg, mb = (amb * c for c in amb_col)
            if lit:
                th = (BAYER[y % 4][x % 4] + 0.5) / 16
                for lx, ly, col, r in lamps:
                    d = math.hypot(x - lx, (y - ly) * 1.6)
                    if d >= r: continue
                    v = 1 - d / r
                    lvl = min(3, int(v * 3 + th)) / 3          # 3 pasma + dithering na granicach
                    k = lvl * (1 - amb) * 1.05
                    mr += col[0] * k; mg += col[1] * k; mb += col[2] * k
            c = px[x, y]
            px[x, y] = (min(255, int(c[0] * min(mr, 1.25))), min(255, int(c[1] * min(mg, 1.25))),
                        min(255, int(c[2] * min(mb, 1.25))), 255)


def frame(f, amb, amb_col, lit):
    fr = anim.make_frame(f)
    bare = anim.make_frame(f, ui=False)
    ui = [(x, y, fr.getpixel((x, y))) for y in range(20) for x in range(W)
          if fr.getpixel((x, y)) != bare.getpixel((x, y))]
    emis = []
    for fn, x, y, *_ in sorted(LAMPS, key=lambda t: t[2]):
        emis += stamp(fr, fn(f), x, y, lit)
    if amb < 1:
        light(fr, f, amb, amb_col, lit)
    if lit:   # świecące ryby i oko potwora
        for x, y in anim.WATER.emit:
            emis.append((x, y, RGB['wi']))
    for x, y, c in emis:
        fr.putpixel((x, y), c + (255,))
    for x, y, c in ui:   # UI nie podlega oświetleniu
        fr.putpixel((x, y), c)
    if lit:   # świetliki jako emisja
        fl = random.Random(5)
        for i in range(10):
            x0, y0 = fl.randrange(30, 190) + anim.OX, fl.randrange(30, 120) + anim.OY; ph = fl.randrange(48)
            x = x0 + round(3 * math.sin((f + ph) * 0.25)); y = y0 + round(2 * math.cos((f + ph) * 0.18))
            if (f + ph) % 16 < 11 and 0 <= x < W and 0 <= y < H:
                fr.putpixel((x, y), RGB['fl'] + (255,))
    return fr


TIMES = [("12:00  dzień", 1.0, (1, 1, 1), False),
         ("19:30  zmierzch", 0.62, (1.0, 0.72, 0.8), True),
         ("23:30  noc", 0.34, (0.48, 0.55, 0.95), True)]

if __name__ == "__main__":
    # panel: 3 pory dnia
    S = 3
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 20)
    panel = Image.new("RGB", (W * S, (H * S + 34) * 3), (15, 10, 16))
    d = ImageDraw.Draw(panel)
    for i, (name, amb, col, lit) in enumerate(TIMES):
        fr = frame(5, amb, col, lit).convert("RGB").resize((W * S, H * S), Image.NEAREST)
        y = i * (H * S + 34)
        d.text((10, y + 6), name, fill=(216, 204, 171), font=font)
        panel.paste(fr, (0, y + 34))
    panel.save(OUT + "pory_dnia.png")

    # GIF: noc, migotanie płomieni
    name, amb, col, lit = TIMES[2]
    frames = [frame(f, amb, col, lit).convert("RGB").resize((W * 3, H * 3), Image.NEAREST) for f in range(anim.FRAMES)]
    pal = frames[5].quantize(colors=255, method=Image.Quantize.MEDIANCUT)
    q = [fr.quantize(palette=pal, dither=Image.Dither.NONE) for fr in frames]
    q[0].save(OUT + "noc.gif", save_all=True, append_images=q[1:], duration=100, loop=0)

    # arkusz lamp
    from sheet import render
    sh = Image.new("RGB", (560, 230), (15, 10, 16))
    x = 20
    for fn in (lantern_post, skull_candle, wisp_cage, mushrooms):
        im = render(fn(0), 'p0', 6)
        sh.paste(im, (x, 210 - im.height)); x += im.width + 24
    sh.save(OUT + "lampy.png")
    print("ok")
