"""Arkusz drzew owocowych + scena sadu (dzień / noc)."""
from PIL import Image
import sprites5 as S5
import sprites4 as S4
from sheet3 import sheet, OUT

if __name__ == "__main__":
    rows = []
    for sid, sp in S5.SPECIES.items():
        rows.append((sp['name'], [(n, S5.tree(sid, st)) for st, n in enumerate(("sadzonka", "młode", "dorosłe", "owocujące"))]
                     + [("owoc", S5.fruit_icon(sid))], 'g2'))
    rows.append(("Sklep: sadzonka", [("ikona", S5.sapling_icon())], 'p1'))
    sheet("Drzewa owocowe: 4 fazy + owoc", rows, OUT + "arkusz_drzewa.png", scale=4, width=1100)
    print("ok")
