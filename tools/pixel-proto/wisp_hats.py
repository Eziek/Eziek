"""Czapki ogników (sklep z czapkami, GDD §9 p. 88): mapy w kluczach ui_kit.KEY, obrys dokłada from_map.

Pivot: środek dolnej krawędzi. Czapka jest nasunięta na czubek płomyka (dół na wysokości 12 px od dołu sprite'a ognika,\ngłowa ma tam 8 px szerokości): treść do 8 px szerokości i 5 px wysokości, z obrysem 10 x 7.
Klucze: o kontur, 1 2 3 śliwka, c r g w czerwień, pomarańcz, złoto, kość, e f h j ziemia i drewno, k m n q zieleń,
a b d s woda, z mgła, F X płomień, W L ognik, U M fiolet.
"""

HATS = {
    'straw':    ["..gggg..", "..grrg..", "gggggggg"],
    'beanie':   ["...w....", "..cccc..", ".cwcwcw.", ".wwwwww."],
    'nightcap': ["......w.", ".....d..", "...ddd..", "..dddd..", "wwwwwww."],
    'flower':   ["c.M.F.c.", "nqnqnqnq"],
    'mushroom': ["..cccc..", ".cwcccw.", "cccwcccc", "..wwww.."],
    'feather':  [".....c..", "....cc..", ".UUUUU..", "UUUUUUU."],
    'tophat':   ["..1111..", "..1221..", "..1cc1..", "11111111"],
    'witch':    [".....12.", "....11..", "...1U1..", "..1111..", "11111111"],
    'pumpkin':  ["...m....", ".rrmrrr.", "rXrrrXrr", ".rrrrrr."],
    'horns':    ["w......w", "ww....ww", ".wwhhww.", "..hhhh.."],
    'tricorne': [".111111.", "11gggg11", "11111111"],
    'skull':    [".wwwwww.", "ww1ww1ww", ".wwwwww.", "..w1w1.."],
    'candle':   ["..F..", ".FXF.", "..w..", "..w..", "hhhhh"],
    'crown':    ["g..g..g.", "gg.g.gg.", "gggggggg", "gcggWgcg"],
    'halo':     [".LLLLLL.", "L......L", ".LLLLLL.", "........", "........"],   # unosi się nad głową
}

# cena: monety albo perły mgły (kolejność = kolejność w sklepie)
PRICES = [
    ('straw', 3000, 0), ('beanie', 4000, 0), ('nightcap', 5000, 0), ('flower', 6000, 0), ('mushroom', 8000, 0),
    ('feather', 10000, 0), ('tophat', 12000, 0), ('witch', 15000, 0), ('pumpkin', 20000, 0), ('horns', 25000, 0),
    ('tricorne', 0, 2), ('skull', 0, 3), ('candle', 0, 3), ('crown', 0, 4), ('halo', 0, 5),
]
