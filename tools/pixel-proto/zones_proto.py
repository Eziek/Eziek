"""Wyspa z odkrytymi strefami (nowe podłoża + dekoracje) i wycinka drzewa w trakcie."""
from PIL import Image
import tiles_proto as TP
import sprites4 as S4
import sprites3 as S3
import sprites
import ui_kit as K

T = 16


def main():
    TP.OX, TP.OY = 8, 4
    tiles = {}
    for c in range(5, 12):
        for r in range(4, 9): tiles[(c, r)] = 'grass'
    for p in [(12, 3), (13, 3), (12, 4), (13, 4), (14, 4), (12, 5), (13, 5), (11, 3)]: tiles[p] = 'cemetery'
    for p in [(3, 6), (4, 6), (3, 7), (4, 7), (4, 8), (5, 9), (4, 5)]: tiles[p] = 'swamp'
    for p in [(8, 9), (9, 9), (10, 9), (9, 10), (8, 10), (7, 9), (10, 10)]: tiles[p] = 'ruins'
    for p in [(7, 5), (8, 5), (9, 5), (7, 6), (8, 6)]: tiles[p] = 'soil'
    tiles[(13, 5)] = 'soil'; tiles[(12, 5)] = 'soil'          # grządki na cmentarnej ziemi (bonus nocny)
    def at(c, r, dx=8, dy=13):
        x, y = TP.tile_rect(c, r); return x + dx, y + dy
    objs = [(sprites.carrot(3), *at(7, 5, 8, 12)), (sprites.wheat(3), *at(8, 5, 8, 12)), (sprites.pumpkin(2), *at(9, 5, 8, 12)),
            (sprites.carrot(1), *at(7, 6, 8, 12)), (sprites.seed(), *at(8, 6, 8, 12)),
            (S3.ghost_pumpkin(3), *at(13, 5, 8, 12)), (S3.corpse_mushroom(3), *at(12, 5, 8, 12)),
            (S4.crypt(), *at(13, 3, 0, 15)), (S4.obelisk(), *at(12, 4, 6, 12)), (S4.wooden_cross(), *at(14, 4, 6, 13)),
            (S4.candles(), *at(11, 3, 8, 12)), (S4.crow(0), *at(14, 4, 2, 3)),
            (S4.mangrove(), *at(3, 6, 10, 14)), (S4.cattails(), *at(4, 8, 6, 12)), (S4.rotten_log(), *at(4, 7, 4, 13)),
            (S4.lily_pads(), *at(3, 7, 4, 10)), (S4.frog(1), *at(5, 9, 6, 8)),
            (S4.broken_wall(), *at(9, 9, 6, 12)), (S4.statue(), *at(10, 10, 4, 13)), (S4.brazier(), *at(8, 10, 6, 12)),
            (S4.rubble(), *at(7, 9, 8, 12)), (S4.fallen_column(), *at(9, 10, -2, 13)),
            (S4.stump('round'), *at(10, 7, 8, 12)), (sprites.rock(), *at(6, 8, 8, 12)),
            ]
    fall = S4.removal_sets()['tree_round_fall'][2]
    objs.append((fall, *at(5, 5, 8, 13)))
    cv = TP.render(TP.build(tiles, seed=5), objs)
    # cząsteczki wycinki
    bx, by = at(5, 5, 8, 13)
    for i, (dx, dy, kind) in enumerate([(-4, -6, 'woodchip'), (3, -9, 'leaf'), (6, -4, 'leaf'), (-7, -2, 'dust'), (1, -2, 'woodchip')]):
        p = S4.particle(kind, i % 3)
        cv.sprite(p, bx + dx - 3, by + dy - 5)
    # tooltip wycinki
    tx, ty = bx - 44, by - 74
    cv.nine('ui_tooltip', tx, ty, 100, 34)
    cv.text(tx + 4, ty + 3, "WYCINKA DRZEWA", 'fl')
    cv.icon('essence', tx + 4, ty + 11); cv.text(tx + 17, ty + 13, "-5")
    cv.sprite(S4.icon_material('wood'), tx + 34, ty + 11); cv.text(tx + 47, ty + 13, "+3", 'cr')
    cv.text(tx + 4, ty + 24, "ZOSTAJE PIEŃ", 'b3')
    cv.save("wyspa_strefy.png")
    print("ok")


if __name__ == "__main__":
    main()
