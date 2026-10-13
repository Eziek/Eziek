"""UI: font pikselowy 3x5 (wielkie litery + polskie znaki), ikony 12x12, elementy 9-slice."""
import sprites3            # ustawia paletę mroczną + emisyjną
from sprites import Sprite
from sprites3 import RGB

# ---------------------------------------------------------------- font
# komórka 8 wierszy: 0 akcent, 1 odstęp, 2-6 znak, 7 ogonek; linia bazowa pod wierszem 6
G = {
    'A': [".#.", "#.#", "###", "#.#", "#.#"], 'B': ["##.", "#.#", "##.", "#.#", "##."],
    'C': [".##", "#..", "#..", "#..", ".##"], 'D': ["##.", "#.#", "#.#", "#.#", "##."],
    'E': ["###", "#..", "##.", "#..", "###"], 'F': ["###", "#..", "##.", "#..", "#.."],
    'G': [".##", "#..", "#.#", "#.#", ".##"], 'H': ["#.#", "#.#", "###", "#.#", "#.#"],
    'I': ["###", ".#.", ".#.", ".#.", "###"], 'J': ["..#", "..#", "..#", "#.#", ".#."],
    'K': ["#.#", "#.#", "##.", "#.#", "#.#"], 'L': ["#..", "#..", "#..", "#..", "###"],
    'M': ["#.#", "###", "###", "#.#", "#.#"], 'N': ["##.", "#.#", "#.#", "#.#", "#.#"],
    'O': [".#.", "#.#", "#.#", "#.#", ".#."], 'P': ["##.", "#.#", "##.", "#..", "#.."],
    'Q': [".#.", "#.#", "#.#", "##.", ".##"], 'R': ["##.", "#.#", "##.", "#.#", "#.#"],
    'S': [".##", "#..", ".#.", "..#", "##."], 'T': ["###", ".#.", ".#.", ".#.", ".#."],
    'U': ["#.#", "#.#", "#.#", "#.#", "###"], 'V': ["#.#", "#.#", "#.#", ".#.", ".#."],
    'W': ["#.#", "#.#", "###", "###", "#.#"], 'X': ["#.#", "#.#", ".#.", "#.#", "#.#"],
    'Y': ["#.#", "#.#", ".#.", ".#.", ".#."], 'Z': ["###", "..#", ".#.", "#..", "###"],
    '0': ["###", "#.#", "#.#", "#.#", "###"], '1': [".#.", "##.", ".#.", ".#.", "###"],
    '2': ["##.", "..#", ".#.", "#..", "###"], '3': ["##.", "..#", ".#.", "..#", "##."],
    '4': ["#.#", "#.#", "###", "..#", "..#"], '5': ["###", "#..", "##.", "..#", "##."],
    '6': [".##", "#..", "###", "#.#", "###"], '7': ["###", "..#", ".#.", ".#.", ".#."],
    '8': ["###", "#.#", "###", "#.#", "###"], '9': ["###", "#.#", "###", "..#", "##."],
    '.': [".", ".", ".", ".", "#"], ',': [".", ".", ".", "#", "#"], ':': [".", "#", ".", "#", "."],
    '!': ["#", "#", "#", ".", "#"], '?': ["##.", "..#", ".#.", "...", ".#."],
    '-': ["...", "...", "###", "...", "..."], '+': ["...", ".#.", "###", ".#.", "..."],
    '/': ["..#", "..#", ".#.", "#..", "#.."], '%': ["#.#", "..#", ".#.", "#..", "#.#"],
    '(': [".#", "#.", "#.", "#.", ".#"], ')': ["#.", ".#", ".#", ".#", "#."],
    '>': ["#..", ".#.", "..#", ".#.", "#.."], '<': ["..#", ".#.", "#..", ".#.", "..#"],
    "'": ["#", "#", ".", ".", "."], '"': ["#.#", "#.#", "...", "...", "..."],
    '=': ["...", "###", "...", "###", "..."], '*': ["...", "#.#", ".#.", "#.#", "..."],
    'Ł': ["#..", "##.", "#..", "#..", "###"],
    'ẞ': [".#.", "#.#", "##.", "#.#", "##."],
}
# polskie: (litera bazowa, akcent) - akcent: 'acute' (piksel nad, z prawej), 'dot' (piksel nad, środek), 'ogonek'
PL = {'Ą': ('A', 'ogonek'), 'Ę': ('E', 'ogonek'), 'Ć': ('C', 'acute'), 'Ń': ('N', 'acute'),
      'Ó': ('O', 'acute'), 'Ś': ('S', 'acute'), 'Ź': ('Z', 'acute'), 'Ż': ('Z', 'dot'),
      # niemieckie (lokalizacja DE)
      'Ä': ('A', 'umlaut'), 'Ö': ('O', 'umlaut'), 'Ü': ('U', 'umlaut')}
SPACE_W = 2
LINE_H = 8


def glyph_pixels(ch):
    """Lista pikseli (x, y) w komórce 8-wierszowej i szerokość znaku."""
    ch = 'ẞ' if ch == 'ß' else ch.upper()
    accent = None
    if ch in PL:
        ch, accent = PL[ch]
    rows = G.get(ch)
    if rows is None:
        return None, 0
    w = len(rows[0])
    px = [(x, 2 + y) for y, r in enumerate(rows) for x, c in enumerate(r) if c == '#']
    if accent == 'acute': px.append((min(2, w - 1), 0))
    elif accent == 'dot': px.append((1, 0))
    elif accent == 'ogonek': px.append((w - 1, 7))
    elif accent == 'umlaut': px += [(0, 0), (w - 1, 0)]
    return px, w


def text_width(text, scale=1):
    text = text.replace('ß', 'SS')
    w = 0
    for ch in text:
        if ch == ' ':
            w += SPACE_W + 1
            continue
        _, gw = glyph_pixels(ch)
        w += gw + 1 if gw else 0
    return max(0, w - 1) * scale


def draw_text(put, x, y, text, k='cr', shadow='o', scale=1):
    """put(x, y, key). y = górna krawędź komórki (wiersz akcentu).
    Font jest tylko wielkimi literami, więc niemieckie ß zapisujemy poprawnie jako SS."""
    text = text.replace('ß', 'SS')
    cx = x
    for ch in text:
        if ch == ' ':
            cx += (SPACE_W + 1) * scale
            continue
        px, w = glyph_pixels(ch)
        if px is None:
            continue
        for gx, gy in px:
            for sx in range(scale):
                for sy in range(scale):
                    if shadow:
                        put(cx + gx * scale + sx + scale, y + gy * scale + sy + scale, shadow)
        for gx, gy in px:
            for sx in range(scale):
                for sy in range(scale):
                    put(cx + gx * scale + sx, y + gy * scale + sy, k)
        cx += (w + 1) * scale
    return cx


def font_chars():
    base = [c for c in G] + list(PL)
    return base


def build_ttf(path):
    """TrueType z kwadratów pikseli: 1 piksel = 100 jednostek, em = 800 (8 px)."""
    from fontTools.fontBuilder import FontBuilder
    from fontTools.pens.ttGlyphPen import TTGlyphPen
    U = 100
    order = ['.notdef', 'space']
    cmap = {32: 'space'}
    glyphs, metrics = {}, {}
    pen = TTGlyphPen(None)
    for (x0, y0, x1, y1) in ((0, 0, 300, 500),):
        pen.moveTo((x0, y0)); pen.lineTo((x0, y1)); pen.lineTo((x1, y1)); pen.lineTo((x1, y0)); pen.closePath()
    glyphs['.notdef'] = pen.glyph(); metrics['.notdef'] = (400, 0)
    glyphs['space'] = TTGlyphPen(None).glyph(); metrics['space'] = ((SPACE_W + 1) * U, 0)
    for ch in font_chars():
        px, w = glyph_pixels(ch)
        name = 'uni%04X' % ord(ch)
        pen = TTGlyphPen(None)
        for gx, gy in px:
            xa, xb = gx * U, (gx + 1) * U
            yb, ya = (6 - gy) * U, (7 - gy) * U          # wiersz 6 stoi na linii bazowej
            pen.moveTo((xa, yb)); pen.lineTo((xa, ya)); pen.lineTo((xb, ya)); pen.lineTo((xb, yb)); pen.closePath()
        glyphs[name] = pen.glyph()
        metrics[name] = ((w + 1) * U, min(g[0] for g in px) * U)
        order.append(name)
        cmap[ord(ch)] = name
        low = ch.lower()
        if low != ch:
            cmap[ord(low)] = name
    fb = FontBuilder(800, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=700, descent=-100)
    fb.setupNameTable({"familyName": "Wyspa Pixel", "styleName": "Regular"})
    fb.setupOS2(sTypoAscender=700, sTypoDescender=-100, sTypoLineGap=0, usWinAscent=700, usWinDescent=100)
    fb.setupPost()
    fb.save(path)


# ---------------------------------------------------------------- ikony
KEY = {'.': None, 'o': 'o', '1': 'p0', '2': 'p1', '3': 'p2', 'c': 'co', 'r': 'or', 'g': 'go', 'w': 'cr',
       'e': 'e0', 'f': 'e1', 'h': 'e2', 'j': 'e3', 'k': 'g0', 'm': 'g1', 'n': 'g2', 'q': 'g3',
       'a': 'b0', 'b': 'b1', 'd': 'b2', 's': 'b3', 'z': 'fo', 'F': 'fl', 'X': 'fx', 'W': 'wi', 'L': 'wl',
       'U': 'mu', 'M': 'ml'}


def from_map(rows, outline=True, pad=1):
    h, w = len(rows), max(len(r) for r in rows)
    s = Sprite(w + pad * 2, h + pad * 2, (w + pad * 2) // 2, h + pad * 2 - 1)
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            k = KEY.get(c)
            if k: s.set(x + pad, y + pad, k)
    return s.outline() if outline else s


def icon_coin():
    s = Sprite(12, 12, 6, 11)
    s.blob(6, 6, 4.6, 4.6, ['e2', 'go', 'fl'])
    for y in (4, 5, 6, 7): s.set(6, y, 'e2')
    return s.outline()


def icon_gear():
    s = Sprite(12, 12, 6, 11)
    s.blob(6, 6, 4, 4, ['p1', 'b3', 'fo'])
    for x, y in ((6, 1), (6, 10), (1, 6), (10, 6), (2, 2), (9, 2), (2, 9), (9, 9)): s.set(x, y, 'b3')
    s.blob(6, 6, 1.6, 1.6, ['o', 'o', 'p0'])
    return s.outline()


ICON_MAPS = {
    'essence': ["....F.....", "...FF.....", "...FXF....", "..FXXF....", "..XXFXX...",
                ".XXFFXXc..", ".XFFFFXc..", ".XXFFXXc..", "..XXXcc...", "...ccc...."],
    'nightcrop': ["...zzz....", "..zLz.....", ".zLz......", ".zz.......", ".zz.......",
                  ".zz....s..", ".zLz..ss..", "..zLzzs...", "...zzz....", ".........."],
    'starshard': ["....L.....", "...LLz....", "...Lwz....", "..LLwzz...", "..Lwwzs...",
                  ".LLwwzzs..", "..wwzzs...", "...wzs....", "...wz.....", "....s....."],
    'scale': ["....dd....", "...dWWd...", "..dWLWsd..", "..dWWWsd..", ".dWWWWWsd.",
              ".dWWWWWsd.", ".dsWWWssd.", "..dssssd..", "...dddd...", ".........."],
    'deepgift': ["..........", "....ww....", "...wLwz...", "...wwzz...", "....zz....",
                 ".33333333.", "3222222223", ".32222223.", "..333333..", ".........."],
    'book': ["2333333333", "2333zz3333", "233z33z333", "233z333333", "233z33z333",
             "2333zz3g33", "233333gg33", "2333333333", "2wwwwwwwww", "2222222222"],
    'shop': ["...ffff...", "....hh....", "..hjjjhh..", ".hjjjjjhf.", ".hjjgjjhf.",
             ".hjgggjhf.", ".hjjgjjhf.", ".hhjjjhhf.", "..fffffff.", ".........."],
    'upgrade': ["....W.....", "...WWW....", "..W.W.W...", "....W.....", "....W.....",
                "..sssss...", ".szzzzzs..", ".szzzzzs..", "..sssss...", ".........."],
    'ritual': ["..zzzzzz..", ".z......z.", "z..LLL...z", "z.L......z", "z.L......z",
               "z.L......z", "z..LLL...z", ".z......z.", "..zzzzzz..", ".........."],
    'bestiary': ["..wwwwww..", ".wwwwwwwz.", "wwwwwwwwzz", "w11ww11wzz", "w11ww11wz.",
                 ".wwwwwwz..", "..w1w1w...", "..wwwww...", "..........", ".........."],
    'stats': [".hhhhhhh..", "..wFFFw...", "..wFFFw...", "...wFw....", "....w.....",
              "...wgw....", "..wgggw...", "..ggggg...", ".hhhhhhh..", ".........."],
    'close': ["cc......cc", ".cc....cc.", "..cc..cc..", "...cccc...", "....cc....",
              "...cccc...", "..cc..cc..", ".cc....cc.", "cc......cc", ".........."],
    'lock': ["...ssss...", "..s....s..", "..s....s..", ".gggggggg.", ".gFgggggh.",
             ".ggg11ggh.", ".ggg1gggh.", ".gggggggh.", "..hhhhhh..", ".........."],
    'star': ["....F.....", "....F.....", "...FFF....", "FFFFFFFFF.", ".FFFFFFF..",
             "..FFFFF...", "..FF.FF...", ".FF...FF..", "..........", ".........."],
    'star_empty': ["....2.....", "....2.....", "...222....", "222222222.", ".2222222..",
                   "..22222...", "..22.22...", ".22...22..", "..........", ".........."],
    'focus': ["....c.....", "...cc.....", "...crc....", "..crrc....", "..rrFrr...",
              ".rrFFrrc..", ".rFFFFrc..", ".rrFFrrc..", "..rrrcc...", "...ccc...."],
    'fatigue': ["...zzz..ww", "..zz.....w", ".zz.....w.", ".zz....www", ".zz.......",
                ".zz.......", "..zz...zz.", "...zzzzz..", "..........", ".........."],
    'break': ["...s.s....", "..s.s.....", "..........", ".wwwwww...", ".wzzzzwww.",
              ".wzzzzw.w.", ".wzzzzwww.", "..wwww....", "..........", ".........."],
    'boat': ["......f...", "......fX..", "......f...", "......f...", "hhhhhhhhhh",
             ".heeeeeeh.", "..hhhhhh..", "..........", ".dd..dd...", ".........."],
    'wisp': ["....L.....", "...WL.....", "..WWWW....", ".WWLLWW...", ".Wo1W1W...",
             ".WWWWWW...", "..WWWW....", "...W.W....", "....W.....", ".........."],
    'lamp': ["...222....", "..2XFX2...", "..2FFF2...", "..2XFX2...", "...222....",
             "....f.....", "....f.....", "....f.....", "...fff....", ".........."],
    'plus': ["..........", "....nn....", "....nn....", "..nnnnnn..", "..nnnnnn..",
             "....nn....", "....nn....", "..........", "..........", ".........."],
}

SEED_SYMBOLS = {
    'carrot': ["...mm", "..rm.", ".rr..", "rc...", "c...."],
    'wheat': ["..g..", ".gFg.", "..g..", ".ggg.", "..h.."],
    'pumpkin': ["..m..", ".rrr.", "rrrrr", "rrrrc", ".ccc."],
    'moonflower': [".zLz.", "zLWLz", ".zLz.", "..b..", ".bb.."],
    'ghostpumpkin': ["..b..", ".zzz.", "zWzWz", "zzzzs", ".sss."],
    'corpseshroom': [".333.", "32223", "..z..", "..z..", ".zz.."],
    'bloodrose': [".c2c.", "c222c", ".c2c.", "..k..", ".kk.."],
    'hybrid': ["..b..", ".rrr.", "rWrWr", "rrrrc", ".ccc."],
}


def seed_packet(sym):
    rows = [list("jjjjjjjjj."), list("hhhhhhhhh."), list("wwwwwwwwz.")]
    for _ in range(6): rows.append(list("wwwwwwwwz."))
    rows.append(list("zzzzzzzzz."))
    for y, r in enumerate(SEED_SYMBOLS[sym]):
        for x, c in enumerate(r):
            if c != '.': rows[3 + y][2 + x] = c
    return from_map([''.join(r) for r in rows])


def all_icons():
    out = {'coin': icon_coin(), 'settings': icon_gear()}
    for n, m in ICON_MAPS.items():
        out[n] = from_map(m)
    for n in SEED_SYMBOLS:
        out['seed_' + n] = seed_packet(n)
    return out


# ---------------------------------------------------------------- 9-slice
def panel(fill='p0', page=False):
    """24x24, ramka 7 px (border L/B/R/T = 7)."""
    s = Sprite(24, 24, 0, 0)
    for y in range(24):
        for x in range(24):
            d = min(x, y, 23 - x, 23 - y)
            if d == 0: k = 'o'
            elif d <= 5:
                k = 'p1'
                if d == 1 and (x < 23 - 1 and y < 23 - 1) and (x == 1 or y == 1): k = 'p2'
                if d == 5: k = 'p0'
                if (x in (1, 22) or y in (1, 22)) and d == 1 and (x == 22 or y == 22): k = 'p0'
            elif d == 6: k = 'o' if not page else 'e2'
            else: k = fill
            s.set(x, y, k)
    for cx, cy in ((2, 2), (20, 2), (2, 20), (20, 20)):           # nity
        s.set(cx, cy, 'fo'); s.set(cx + 1, cy, 'b3'); s.set(cx, cy + 1, 'b3'); s.set(cx + 1, cy + 1, 'b1')
    if page:
        for x in range(7, 17): s.set(x, 7, 'fo')
    return s


def button(state):
    """16x12, border 4. state: normal/hover/pressed/disabled."""
    fill, hi, lo = {'normal': ('p1', 'p2', 'p0'), 'hover': ('p2', 'mu', 'p1'),
                    'pressed': ('p0', 'o', 'p1'), 'disabled': ('p0', 'p0', 'p0')}[state]
    s = Sprite(16, 12, 0, 0)
    for y in range(12):
        for x in range(16):
            edge = x in (0, 15) or y in (0, 11)
            corner = x in (0, 15) and y in (0, 11)
            if corner: continue
            k = 'o' if edge else fill
            if not edge and y == 1: k = hi
            if not edge and y == 10: k = lo
            s.set(x, y, k)
    if state == 'disabled':
        for x in range(2, 14, 2): s.set(x, 6, 'b1')
    return s


def tab(active):
    s = Sprite(20, 12, 0, 0)
    for y in range(12):
        for x in range(20):
            if x in (0, 19) and y == 0: continue
            edge = x in (0, 19) or y == 0
            k = 'o' if edge else ('p0' if active else 'p1')
            if not edge and y == 1: k = 'p2' if active else 'p1'
            if y == 11 and not active: k = 'o'
            s.set(x, y, k)
    return s


def slot(state):
    """18x18, border 5. state: normal/selected/locked."""
    s = Sprite(18, 18, 0, 0)
    ring = {'normal': 'p1', 'selected': 'go', 'locked': 'p0'}[state]
    for y in range(18):
        for x in range(18):
            d = min(x, y, 17 - x, 17 - y)
            if d == 0: k = 'o'
            elif d == 1: k = ring
            elif d == 2: k = 'o' if (x <= 2 or y <= 2) else 'p2'
            else: k = 'o' if state == 'locked' and (x + y) % 2 else 'p0'
            s.set(x, y, k)
    if state == 'selected':
        for cx, cy in ((1, 1), (16, 1), (1, 16), (16, 16)): s.set(cx, cy, 'fl')
    return s


def tooltip():
    s = Sprite(16, 16, 0, 0)
    for y in range(16):
        for x in range(16):
            d = min(x, y, 15 - x, 15 - y)
            s.set(x, y, 'o' if d == 0 else ('b3' if d == 1 else 'p0'))
    return s


def toast():
    s = Sprite(24, 14, 0, 0)
    for y in range(14):
        for x in range(24):
            d = min(x, y, 23 - x, 13 - y)
            k = 'o' if d == 0 else ('p1' if d == 1 else 'p0')
            if 1 <= x <= 3 and 1 <= y <= 12: k = 'wi'
            s.set(x, y, k)
    return s


def bar_frame():
    s = Sprite(32, 8, 0, 0)
    for y in range(8):
        for x in range(32):
            d = min(x, y, 31 - x, 7 - y)
            s.set(x, y, 'o' if d == 0 else ('p1' if d == 1 else 'p0'))
    return s


def bar_fill(kind):
    top, mid, bot = {'essence': ('fl', 'fx', 'co'), 'growth': ('g3', 'g2', 'g1'), 'rhythm': ('wl', 'wi', 'b2')}[kind]
    s = Sprite(4, 4, 0, 0)
    for x in range(4):
        s.set(x, 0, top); s.set(x, 1, mid); s.set(x, 2, mid); s.set(x, 3, bot)
    return s


def wheel():
    import math
    s = Sprite(60, 60, 0, 0)
    for y in range(60):
        for x in range(60):
            d = math.hypot(x - 29.5, y - 29.5)
            if 20 <= d <= 29:
                k = 'o' if d > 28 or d < 21 else ('p1' if d < 27 else 'p2')
                s.set(x, y, k)
    return s


def cursor():
    return from_map(["w.......", "ww......", "wzw.....", "wzzw....", "wzzzw...", "wzzzzw..",
                     "wzzwww..", "ww.zw...", "w...zw..", "....ww.."], pad=1)


NINE_SLICE = {   # nazwa: (sprite, border L, B, R, T)
    'ui_panel': (lambda: panel('p0'), 7, 7, 7, 7),
    'ui_panel_page': (lambda: panel('cr', page=True), 7, 7, 7, 7),
    'ui_button_normal': (lambda: button('normal'), 4, 4, 4, 4),
    'ui_button_hover': (lambda: button('hover'), 4, 4, 4, 4),
    'ui_button_pressed': (lambda: button('pressed'), 4, 4, 4, 4),
    'ui_button_disabled': (lambda: button('disabled'), 4, 4, 4, 4),
    'ui_tab_active': (lambda: tab(True), 4, 2, 4, 4),
    'ui_tab_inactive': (lambda: tab(False), 4, 2, 4, 4),
    'ui_slot': (lambda: slot('normal'), 5, 5, 5, 5),
    'ui_slot_selected': (lambda: slot('selected'), 5, 5, 5, 5),
    'ui_slot_locked': (lambda: slot('locked'), 5, 5, 5, 5),
    'ui_tooltip': (tooltip, 4, 4, 4, 4),
    'ui_toast': (toast, 5, 4, 4, 4),
    'ui_bar_frame': (bar_frame, 3, 3, 3, 3),
}
SIMPLE_UI = {
    'ui_bar_fill_essence': lambda: bar_fill('essence'),
    'ui_bar_fill_growth': lambda: bar_fill('growth'),
    'ui_bar_fill_rhythm': lambda: bar_fill('rhythm'),
    'ui_wheel': wheel,
    'ui_cursor': cursor,
}
