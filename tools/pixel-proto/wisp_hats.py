"""Czapki ogników (sklep z czapkami, GDD §9 p. 88): mapy w kluczach ui_kit.KEY, obrys dokłada from_map.

Pivot: środek dolnej krawędzi (rondo leży na czubku głowy ognika). Szerokość do 12 px, wysokość do 9 px.
Klucze: o kontur, 1 2 3 śliwka, c r g w czerwień, pomarańcz, złoto, kość, e f h j ziemia i drewno, k m n q zieleń,
a b d s woda, z mgła, F X płomień, W L ognik, U M fiolet.
"""

HATS = {
    'straw':    ["...gggg...", "..grggrg..", "gggggggggg"],
    'beanie':   ["....ww....", "...cwc....", "..cccccc..", ".cwcwcwcw.", ".wwwwwwww."],
    'nightcap': [".....ww...", "....dd....", "...ddd....", "..dddd....", ".ddsdddd..", "wwwwwwwww."],
    'flower':   [".c.M.F.c..", "nqnqnqnqn."],
    'mushroom': ["...cccc...", "..cwcccw..", ".ccccwccc.", "cwcccccwcc", "...wwww..."],
    'tophat':   ["..111111..", "..122221..", "..111111..", "..1cccc1..", "1111111111"],
    'witch':    ["......12..", ".....11...", "....111...", "...1U11...", "..11111...", "1111111111"],
    'pumpkin':  ["....m.....", "..rrmrr...", ".rXrrrXr..", ".rrrXrrr..", "..rrrrr..."],
    'horns':    ["w........w", "ww......ww", ".ww....ww.", "..wwhhww..", "...hhhh..."],
    'feather':  [".......c..", "......cc..", ".....cc...", "..UUUUU...", ".UUUUUUU..", "..hhhhh..."],
    'tricorne': ["..111111..", ".11gggg11.", "1111111111", ".1......1."],
    'crown':    ["g..g..g..g", "gg.gFg.gg.", "gggggggggg", "gcgggWggcg", "gggggggggg"],
    'candle':   ["....F.....", "...FXF....", "....w.....", "...www....", "...www....", ".hhhhhhh.."],
    'halo':     [".LLLLLLL..", "L.......L.", ".LLLLLLL.."],
    'skull':    ["..wwwwww..", ".wwwwwwww.", ".w11ww11w.", ".wwwwwwww.", "..w1w1w..."],
}

# cena: monety albo perły mgły (kolejność = kolejność w sklepie)
PRICES = [
    ('straw', 3000, 0), ('beanie', 4000, 0), ('nightcap', 5000, 0), ('flower', 6000, 0), ('mushroom', 8000, 0),
    ('feather', 10000, 0), ('tophat', 12000, 0), ('witch', 15000, 0), ('pumpkin', 20000, 0), ('horns', 25000, 0),
    ('tricorne', 0, 2), ('skull', 0, 3), ('candle', 0, 3), ('crown', 0, 4), ('halo', 0, 5),
]
