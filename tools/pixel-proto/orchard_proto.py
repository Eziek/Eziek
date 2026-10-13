"""Sad na wyspie: dzień i noc (świecące owoce upiornej gruszy i drzewa księżyca)."""
from PIL import Image
import tiles_proto as TP
import sprites5 as S5
import sprites
import daynight as DN
from sprites3 import RGB, EMISSIVE
import ui_kit as K


def light(img, amb, amb_col, sources):
    """Jak daynight.light: 3 pasma światła z ditheringiem Bayera, ale z dowolnych źródeł."""
    px = img.load()
    W, H = img.size
    for y in range(H):
        for x in range(W):
            mr, mg, mb = (amb * c for c in amb_col)
            th = (DN.BAYER[y % 4][x % 4] + 0.5) / 16
            for lx, ly, col, r in sources:
                d = ((x - lx) ** 2 + ((y - ly) * 1.6) ** 2) ** 0.5
                if d >= r: continue
                lvl = min(3, int((1 - d / r) * 3 + th)) / 3
                k = lvl * (1 - amb) * 1.35
                mr += col[0] * k; mg += col[1] * k; mb += col[2] * k
            c = px[x, y]
            px[x, y] = (min(255, int(c[0] * min(mr, 1.25))), min(255, int(c[1] * min(mg, 1.25))),
                        min(255, int(c[2] * min(mb, 1.25))), 255)


def main():
    TP.OX, TP.OY = 8, 4
    tiles = {(c, r): 'grass' for c in range(4, 13) for r in range(4, 9)}
    for p in [(4, 4), (12, 8), (12, 4)]: tiles.pop(p)
    for p in [(6, 7), (7, 7), (8, 7)]: tiles[p] = 'soil'
    def at(c, r, dx=8, dy=13):
        x, y = TP.tile_rect(c, r); return x + dx, y + dy
    trees = [('apple', 3, (5, 5)), ('elder', 3, (7, 5)), ('ghostpear', 3, (9, 5)), ('rowan', 3, (11, 5)),
             ('moon', 3, (10, 7)), ('apple', 1, (5, 7)), ('rowan', 0, (12, 7)), ('ghostpear', 2, (4, 8))]
    objs = [(S5.tree(s, st, seed=i), *at(*p)) for i, (s, st, p) in enumerate(trees)]
    objs += [(sprites.carrot(3), *at(6, 7, 8, 12)), (sprites.wheat(2), *at(7, 7, 8, 12)), (sprites.seed(), *at(8, 7, 8, 12))]
    isl = TP.build(tiles, seed=9)
    day = TP.render(isl, objs)
    night = TP.render(isl, objs)
    sources = []
    for i, (s, st, p) in enumerate(trees):
        if st == 3 and s in S5.TREE_LIGHT:
            col, r, (ox, oy) = S5.TREE_LIGHT[s]
            x, y = at(*p)
            sources.append((x + ox, y + oy, col, r))
    light(night.img, 0.34, (0.48, 0.55, 0.95), sources)
    for spr, x, y in objs:                      # emisja ponad ciemnością
        for (sx, sy), k in spr.px.items():
            if k in EMISSIVE:
                night.put(x - spr.ax + sx, y - spr.ay + sy, k)
    for cv, t in ((day, "SAD: DZIEŃ"), (night, "SAD: NOC - UPIORNA GRUSZA I DRZEWO KSIĘŻYCA ŚWIECĄ")):
        K.draw_text(cv.put, 4, 4, t, 'cr', 'o')
    S = 3
    out = Image.new("RGB", (TP.W * S * 2 + 8, TP.H * S), RGB['o'])
    out.paste(day.img.convert("RGB").resize((TP.W * S, TP.H * S), Image.NEAREST), (0, 0))
    out.paste(night.img.convert("RGB").resize((TP.W * S, TP.H * S), Image.NEAREST), (TP.W * S + 8, 0))
    out.save(TP.OUT + "sad_dzien_noc.png")
    print("ok")


if __name__ == "__main__":
    main()
