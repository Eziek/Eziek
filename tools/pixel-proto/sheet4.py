"""Arkusze: dekoracje stref, podłoża, usuwanie dekoracji."""
from PIL import Image
import sprites4 as S4
from sheet3 import sheet, OUT
from sprites import Sprite

if __name__ == "__main__":
    names = {'cemetery': "Cmentarz", 'swamp': "Bagno", 'ruins': "Ruiny"}
    bg = {'cemetery': 'g1', 'swamp': 'g0', 'ruins': 'b1'}
    rows = []
    for z, decor in S4.ZONE_DECOR.items():
        tiles = [(f"podłoże {i}", S4.ground_tile(z, i * 16, 0)) for i in range(3)]
        rows.append((names[z] + ": podłoże (3 kafelki)", tiles, 'p0'))
        items = [(n, fn()) for n, fn in decor.items()]
        for i in range(0, len(items), 7):
            rows.append((names[z] + ": dekoracje", items[i:i + 7], bg[z]))
    sheet("Strefy: podłoża i dekoracje", rows, OUT + "arkusz_strefy.png", scale=4, width=1260)
    rs = S4.removal_sets()
    rows = [("Drzewo: przewracanie (4 klatki)", [(f"klatka {i}", s) for i, s in enumerate(rs['tree_round_fall'])], 'g2'),
            ("Pnie (drzewo, świerk, martwe drzewo)", [(n, s) for n, s in zip(("pień", "pień świerka", "pień martwego"), rs['stumps'])], 'g2'),
            ("Pozostałości (kamień pęknięty, gruz, rozbity nagrobek, ścięty krzak)",
             [(n, s) for n, s in zip(("kamień", "gruz", "nagrobek", "krzak"), rs['remains'])], 'g2'),
            ("Cząsteczki + materiały", [(n, s) for n, s in zip(("wióry", "liść", "odprysk", "kurz 0", "kurz 1", "kurz 2"),
                                                                rs['particles'])] +
             [("drewno", rs['icons'][0]), ("kamień", rs['icons'][1])], 'p1')]
    sheet("Usuwanie dekoracji", rows, OUT + "arkusz_usuwanie.png", scale=4, width=1260)
    print("ok")
