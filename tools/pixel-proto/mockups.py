"""Makiety UI na scenie prototypu (natywnie 272x200, zapis x3)."""
import os, math
from PIL import Image, ImageDraw, ImageFont
import ui_kit as K
import sprites3 as S3
import daynight as DN
import anim
from sprites import Sprite, RGB

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/"
os.makedirs(OUT, exist_ok=True)
W, H = anim.W, anim.H
ICONS = K.all_icons()
SCALE = 3


class Canvas:
    def __init__(self, img):
        self.img = img
        self.px = img.load()

    def put(self, x, y, k):
        if 0 <= x < W and 0 <= y < H and k:
            self.px[x, y] = RGB[k] + (255,)

    def sprite(self, s, x, y, dither=False):
        for (sx, sy), k in s.px.items():
            if dither and (x + sx + y + sy) % 2:
                continue
            self.put(x + sx, y + sy, k)

    def icon(self, name, x, y):
        self.sprite(ICONS[name], x, y)

    def nine(self, name, x, y, w, h):
        fn, L, B, R, T = K.NINE_SLICE[name]
        s = fn()
        for ty in range(h):
            sy = ty if ty < T else (s.h - (h - ty) if ty >= h - B else T + (ty - T) % max(1, s.h - T - B))
            for tx in range(w):
                sx = tx if tx < L else (s.w - (w - tx) if tx >= w - R else L + (tx - L) % max(1, s.w - L - R))
                k = s.px.get((sx, sy))
                if k: self.put(x + tx, y + ty, k)

    def text(self, x, y, t, k='cr', shadow='o', scale=1):
        return K.draw_text(self.put, x, y, t, k, shadow, scale)

    def bar(self, x, y, w, t, kind):
        self.nine('ui_bar_frame', x, y, w, 8)
        f = K.bar_fill(kind)
        for i in range(int((w - 4) * t)):
            for j in range(4):
                self.put(x + 2 + i, y + 2 + j, f.px[(i % 4, j)])

    def dim(self, amount=0.45):
        tint = RGB['p0']
        for yy in range(H):
            for xx in range(W):
                r, g, b, a = self.px[xx, yy]
                self.px[xx, yy] = (int(r * amount + tint[0] * (1 - amount) * 0.6),
                                   int(g * amount + tint[1] * (1 - amount) * 0.6),
                                   int(b * amount + tint[2] * (1 - amount) * 0.6), 255)

    def line(self, x0, y0, x1, y1, k, dotted=False):
        n = max(abs(x1 - x0), abs(y1 - y0))
        for i in range(n + 1):
            if dotted and i % 2: continue
            self.put(round(x0 + (x1 - x0) * i / max(1, n)), round(y0 + (y1 - y0) * i / max(1, n)), k)

    def save(self, name):
        self.img.convert("RGB").resize((W * SCALE, H * SCALE), Image.NEAREST).save(OUT + name)


def scene(f=5, amb=1.0, col=(1, 1, 1), lit=False, extras=()):
    fr = anim.make_frame(f, ui=False)
    emis = []
    for fn, x, y, *_ in sorted(DN.LAMPS, key=lambda t: t[2]):
        emis += DN.stamp(fr, fn(f), x, y, lit)
    for spr, x, y in extras:
        emis += DN.stamp(fr, spr, x, y, lit or amb < 1)
    if amb < 1:
        DN.light(fr, f, amb, col, lit)
    if lit:
        for x, y in anim.WATER.emit:
            emis.append((x, y, RGB['wi']))
    for x, y, c in emis:
        fr.putpixel((x, y), c + (255,))
    return Canvas(fr)


def hud(c, night=False):
    c.nine('ui_tooltip', 3, 3, 170, 16)
    x = 6
    c.icon('coin', x, 5); x = c.text(x + 13, 7, "1250") + 6
    c.icon('essence', x, 5); c.bar(x + 13, 7, 34, 0.6, 'essence'); x += 13 + 34 + 3
    x = c.text(x, 7, "36/60") + 6
    c.icon('nightcrop', x, 5); x = c.text(x + 13, 7, "12") + 6
    c.icon('starshard', x, 5); c.text(x + 13, 7, "3")
    for i, ic in enumerate(('book', 'settings')):
        bx = W - 42 + i * 20
        c.nine('ui_button_normal', bx, 3, 18, 16)
        c.icon(ic, bx + 3, 5)
    c.nine('ui_tooltip', 3, H - 21, 128, 18)
    c.icon('focus', 5, H - 18)
    c.text(19, H - 18, "SERIA 32 MIN  +25%")
    c.bar(19, H - 10, 70, 0.75, 'rhythm')
    c.text(106, H - 18, "23:41" if night else "14:32", 'fo')


def mock_hud():
    c = scene()
    hud(c)
    # koło nasion wokół pustej grządki
    px, py = anim.plot_pos(3, 1)
    cx, cy = px + 9, py + 7
    wh = K.wheel()
    c.sprite(wh, cx - 30, cy - 30)
    seeds = ['carrot', 'wheat', 'pumpkin', 'moonflower', 'ghostpumpkin', 'bloodrose']
    for i, sd in enumerate(seeds):
        a = -math.pi / 2 + i * 2 * math.pi / len(seeds)
        sx, sy = round(cx + 24 * math.cos(a)) - 6, round(cy + 24 * math.sin(a)) - 6
        if i == 0:
            c.nine('ui_slot_selected', sx - 3, sy - 3, 18, 18)
        c.icon('seed_' + sd, sx, sy)
    c.icon('close', cx - 6, cy - 6)
    # podpowiedź do zaznaczonego
    tx, ty = cx + 24, cy - 46
    c.nine('ui_tooltip', tx, ty, 92, 34)
    c.text(tx + 4, ty + 3, "MARCHEW", 'fl')
    c.icon('coin', tx + 4, ty + 12); c.text(tx + 17, ty + 14, "5")
    c.text(tx + 28, ty + 14, "SPRZEDAŻ 12", 'fo')
    c.text(tx + 4, ty + 23, "10 MIN PRACY", 'b3')
    # toast
    c.nine('ui_toast', W - 120, 22, 116, 24)
    c.icon('wisp', W - 114, 28)
    c.text(W - 99, 26, "OGNIK ZEBRAŁ 6 PLONÓW")
    c.text(W - 99, 35, "+72", 'fl'); c.icon('coin', W - 84, 33)
    c.save("ui_hud.png")


def book(c, active):
    c.dim()
    c.nine('ui_panel', 8, 8, W - 16, H - 16)
    tabs = ['shop', 'upgrade', 'ritual', 'bestiary', 'stats', 'settings']
    for i, ic in enumerate(tabs):
        y = 18 + i * 24
        c.nine('ui_slot_selected' if ic == active else 'ui_slot', 16, y, 20, 20)
        c.icon(ic, 20, y + 4)
    c.nine('ui_panel_page', 42, 14, W - 56, H - 28)
    c.nine('ui_button_normal', W - 30, 12, 16, 16)
    c.icon('close', W - 28, 14)


def mock_shop():
    c = scene()
    book(c, 'shop')
    x0, y0 = 52, 22
    c.text(x0, y0, "SKLEP", 'p1', None, 2)
    for i, (t, act) in enumerate((("NASIONA", False), ("LAMPY", True), ("BUDOWLE", False), ("MORZE", False))):
        tx = x0 + i * 48
        c.nine('ui_tab_active' if act else 'ui_tab_inactive', tx, y0 + 18, 46, 13)
        c.text(tx + (46 - K.text_width(t)) // 2, y0 + 21, t, 'cr' if act else 'b3')
    for x in range(x0, W - 22): c.put(x, y0 + 31, 'o')
    items = [('lamp', "30"), ('lamp', "15"), ('wisp', "80"), ('lamp', "10"), ('essence', "100"),
             ('lock', "?"), ('lock', "?"), ('lock', "?"), ('lock', "?"), ('lock', "?")]
    names = ["LATARNIA", "CZASZKA", "OGNIK", "GRZYBY", "SŁÓJ"]
    for i, (ic, price) in enumerate(items):
        gx, gy = x0 + (i % 5) * 36, y0 + 38 + (i // 5) * 40
        state = 'ui_slot_selected' if i == 0 else ('ui_slot_locked' if ic == 'lock' else 'ui_slot')
        c.nine(state, gx, gy, 24, 24)
        c.icon(ic, gx + 6, gy + 6)
        if ic != 'lock':
            c.icon('coin', gx, gy + 25); c.text(gx + 13, gy + 27, price, 'p1', None)
    # opis wybranego
    dx, dy = x0, y0 + 122
    c.nine('ui_tooltip', dx - 2, dy - 2, W - 72, 36)
    c.text(dx + 2, dy + 1, "KRZYWA LATARNIA", 'fl')
    c.text(dx + 2, dy + 10, "ZASIĘG: DUŻY   ESENCJA: 0.1/MIN", 'cr')
    c.text(dx + 2, dy + 19, "CIEPŁE ŚWIATŁO. ROŚLINY DNIA ROSNĄ NOCĄ.", 'b3')
    c.nine('ui_button_hover', W - 68, dy + 13, 44, 16)
    c.text(W - 62, dy + 17, "KUP"); c.icon('coin', W - 46, dy + 15)
    c.save("ui_ksiega_sklep.png")


def mock_upgrades():
    c = scene()
    book(c, 'upgrade')
    x0, y0 = 52, 22
    c.text(x0, y0, "ULEPSZENIA", 'p1', None, 2)
    cols = [("AUTOMATYKA", ['wisp', 'upgrade', 'shop', 'boat'], 2),
            ("RYTM PRACY", ['focus', 'break', 'fatigue', 'stats'], 3),
            ("WYSPA", ['plus', 'plus', 'lock', 'lock'], 1),
            ("LAMPY", ['lamp', 'essence', 'starshard', 'lock'], 1)]
    for ci, (title, nodes, owned) in enumerate(cols):
        cx = x0 + ci * 52
        c.text(cx, y0 + 18, title, 'p1', None)
        for ni, ic in enumerate(nodes):
            ny = y0 + 28 + ni * 26
            if ni > 0:
                k = 'go' if ni < owned else ('p2' if ni == owned else 'b1')
                c.line(cx + 10, ny - 6, cx + 10, ny, k, dotted=ni > owned)
                c.line(cx + 11, ny - 6, cx + 11, ny, k, dotted=ni > owned)
            locked = ni > owned or ic == 'lock'
            state = 'ui_slot_selected' if ni < owned else ('ui_slot_locked' if locked else 'ui_slot')
            if ci == 0 and ni == owned: state = 'ui_slot'
            c.nine(state, cx, ny, 22, 20)
            c.icon('lock' if locked else ic, cx + 5, ny + 4)
    # szczegóły dostępnego węzła
    tx, ty = x0 - 2, y0 + 130
    c.nine('ui_tooltip', tx, ty, W - 72, 34)
    c.text(tx + 4, ty + 3, "SKRZYNIA KUPCA", 'fl')
    c.text(tx + 4, ty + 12, "SPRZEDAJE PLONY CO GODZINĘ (90% CENY)", 'cr')
    c.text(tx + 4, ty + 21, "WYMAGA: OGNIK-OGRODNIK", 'b3')
    c.nine('ui_button_hover', W - 72, ty + 15, 48, 16)
    c.text(W - 66, ty + 19, "KUP"); c.icon('coin', W - 52, ty + 17); c.text(W - 39, ty + 19, "800")
    c.save("ui_ksiega_ulepszenia.png")


def mock_place_night():
    merchant = S3.ghost_merchant()
    c = scene(f=12, amb=0.34, col=(0.48, 0.55, 0.95), lit=True, extras=[(merchant, 22, 170)])
    hud(c, night=True)
    # duch lampy w trybie stawiania
    lamp = DN.lantern_post(0)
    lx, ly = 184, 104
    ghost = Sprite(lamp.w, lamp.h, lamp.ax, lamp.ay)
    for p, k in lamp.px.items():
        ghost.px[p] = 'wl' if k in S3.EMISSIVE else ('wi' if k == 'o' else k)
    c.sprite(ghost, lx - lamp.ax, ly - lamp.ay, dither=True)
    r = 38
    for a in range(0, 360, 6):
        t = math.radians(a)
        c.put(round(lx + 6 + r * math.cos(t)), round(ly - 14 + r * math.sin(t) / 1.6), 'wi')
    lit_count = 0
    for row in range(2):
        for col in range(4):
            px, py = anim.plot_pos(col, row)
            if math.hypot(px + 9 - (lx + 6), (py + 10 - (ly - 14)) * 1.6) < r:
                lit_count += 1
                for (dx, dy) in ((0, 0), (17, 0), (0, 13), (17, 13), (1, 0), (0, 1), (16, 0), (17, 1),
                                 (0, 12), (1, 13), (17, 12), (16, 13)):
                    c.put(px + dx, py + dy, 'wl')
    word = "GRZĄDKA" if lit_count == 1 else ("GRZĄDKI" if 2 <= lit_count <= 4 else "GRZĄDEK")
    c.nine('ui_tooltip', 136, H - 46, 132, 26)
    c.text(140, H - 42, "LPM: POSTAW  PPM: ANULUJ", 'cr')
    c.text(140, H - 33, f"OŚWIETLI: {lit_count} {word}", 'wl')
    c.nine('ui_toast', 4, 24, 120, 24)
    c.icon('wisp', 10, 30)
    c.text(25, 28, "KUPIEC-WIDMO CZEKA")
    c.text(25, 37, "JESZCZE 9 MIN", 'wi')
    c.save("ui_stawianie_noc.png")


def kit_sheet():
    S = 4
    sheet = Image.new("RGB", (1160, 760), RGB['p0'])
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 14)
    small = Image.new("RGBA", (280, 40), RGB['p0'] + (255,))
    cv = Canvas.__new__(Canvas); cv.img = small; cv.px = small.load()
    global W, H
    oldW, oldH = W, H
    W, H = 280, 40
    K.draw_text(cv.put, 2, 2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789", 'cr', 'o')
    K.draw_text(cv.put, 2, 12, "ĄĆĘŁŃÓŚŹŻ .,:!?-+/%()<>=*", 'fl', 'o')
    K.draw_text(cv.put, 2, 22, "ZAŻÓŁĆ GĘŚLĄ JAŹŃ - 25 MIN PRACY", 'wi', 'o')
    W, H = oldW, oldH
    d.text((16, 10), "Font 3x5 (wielkie litery + polskie znaki), x4", fill=RGB['cr'], font=f)
    sheet.paste(small.resize((280 * S, 40 * S), Image.NEAREST), (16, 30))
    d.text((16, 200), "Ikony 12x12", fill=RGB['cr'], font=f)
    x, y = 16, 222
    for n, ic in ICONS.items():
        im = Image.new("RGBA", (ic.w, ic.h), RGB['p1'] + (255,))
        for (px, py), k in ic.px.items(): im.putpixel((px, py), RGB[k] + (255,))
        sheet.paste(im.resize((ic.w * S, ic.h * S), Image.NEAREST), (x, y))
        x += ic.w * S + 10
        if x > 1100: x, y = 16, y + 60
    y += 80
    d.text((16, y), "Elementy 9-slice i paski", fill=RGB['cr'], font=f)
    x, y = 16, y + 22
    for n, (fn, *_b) in K.NINE_SLICE.items():
        s = fn(); im = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        for (px, py), k in s.px.items(): im.putpixel((px, py), RGB[k] + (255,))
        sheet.paste(im.resize((s.w * S, s.h * S), Image.NEAREST), (x, y), im.resize((s.w * S, s.h * S), Image.NEAREST))
        x += s.w * S + 14
        if x > 1060: x, y = 16, y + 110
    for n, fn in K.SIMPLE_UI.items():
        s = fn(); im = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        for (px, py), k in s.px.items(): im.putpixel((px, py), RGB[k] + (255,))
        sc = 2 if s.w > 40 else S
        sheet.paste(im.resize((s.w * sc, s.h * sc), Image.NEAREST), (x, y), im.resize((s.w * sc, s.h * sc), Image.NEAREST))
        x += s.w * sc + 14
    sheet.save(OUT + "ui_zestaw.png")


if __name__ == "__main__":
    mock_hud()
    mock_shop()
    mock_upgrades()
    mock_place_night()
    kit_sheet()
    K.build_ttf(OUT + "WyspaPixel.ttf")
    print("ok")
