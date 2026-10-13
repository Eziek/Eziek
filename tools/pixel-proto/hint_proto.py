"""Zarysy stref we mgle na starcie: sylwetka w kierunku strefy, przyklejona do krawędzi okna, dopóki strefa jest poza kadrem."""
import math
from PIL import Image, ImageDraw, ImageFont
import world_proto as WP
import tiles_proto as TP
import sprites3 as S3
import sprites
from sprites import RGB

ICON = {'cemetery': lambda: S3.gravestone(1), 'swamp': S3.reeds, 'ruins': S3.broken_pillar}
VIEW_W, VIEW_H, CX, CY = 144, 112, 136, 100


def view_with_hints(seed):
    tiles, soil = WP.start_island(seed)
    TP.OX, TP.OY = CX - 16 * 16, CY - 16 * 16
    isl = TP.build(tiles, seed=seed)
    objs = [(sprites.seed(), *WP.mid(p, 12)) for p in soil]
    _, _, zones = WP.gen_world(seed)
    hidden = []
    for ztype, name, (zc, zr) in zones:
        zx, zy = TP.tile_rect(zc, zr)
        zx, zy = zx + 8, zy + 12
        dx, dy = zx - CX, zy - CY
        # przyklej do krawędzi widoku (margines 14 px), zachowując kierunek
        hx, hy = VIEW_W / 2 - 14, VIEW_H / 2 - 10
        k = min(1.0, hx / abs(dx) if dx else 1e9, hy / abs(dy) if dy else 1e9)
        hidden.append((ICON[ztype](), round(CX + dx * k), round(CY + dy * k)))
    cv = TP.render(isl, objs, fog_from=14, fog_span=30, hidden=hidden)
    return cv.img.crop((CX - VIEW_W // 2, CY - VIEW_H // 2, CX + VIEW_W // 2, CY + VIEW_H // 2)), zones


if __name__ == "__main__":
    S = 3
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 13)
    seeds = [11, 23, 42]
    views = [view_with_hints(s) for s in seeds]
    w, h = VIEW_W * S, VIEW_H * S
    sheet = Image.new("RGB", (w * 3 + 40, h + 60), RGB['o'])
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "Start: zarysy stref we mgle przy krawędzi okna, w ich kierunku", fill=RGB['cr'], font=f)
    for i, (im, zones) in enumerate(views):
        sheet.paste(im.convert("RGB").resize((w, h), Image.NEAREST), (10 + i * (w + 10), 30))
        d.text((10 + i * (w + 10), 34 + h), ", ".join(n.lower() for _, n, _ in zones), fill=RGB['b3'], font=f)
    sheet.save(TP.OUT + "start_zarysy.png")
    print("ok")
