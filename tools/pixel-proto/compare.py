from PIL import Image, ImageDraw, ImageFont
from sprites import *
from sprites2 import *
from sheet import render

import os
OUT = os.environ.get("OUT", os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
F = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"


def on_plot(spr, plot_spr, scale, px_off):
    pim = render(plot_spr)
    im = render(spr)
    bg = Image.new("RGBA", (im.width, im.height), RGB['g2'] + (255,))
    bg.alpha_composite(pim, px_off)
    bg.alpha_composite(im)
    return bg.resize((bg.width * scale, bg.height * scale), Image.NEAREST)


sheet = Image.new("RGBA", (1240, 1140), RGB['fo'] + (255,))
d = ImageDraw.Draw(sheet)
f = ImageFont.truetype(F, 18)
fs = ImageFont.truetype(F, 13)

d.text((20, 12), "Obecnie (16 px, ×8)", fill=RGB['o'], font=f)
d.text((640, 12), "Nowa wersja (32 px, ×4) - ten sam rozmiar na ekranie", fill=RGB['o'], font=f)

y = 50
for name, old, new in (("Marchew", [seed(), carrot(1), carrot(2), carrot(3)],
                        [None, carrot2(1), carrot2(2), carrot2(3)]),
                       ("Zboże", [seed(), wheat(1), wheat(2), wheat(3)],
                        [None, wheat2(1), wheat2(2), wheat2(3)])):
    d.text((20, y), name, fill=RGB['o'], font=fs)
    x = 20
    for sp in old:
        im = on_plot(sp, plot(), 6, (2, 14))
        sheet.alpha_composite(im, (x, y + 20)); x += im.width + 8
    x = 640
    for sp in new:
        if sp is None:
            sp = plant2(); mound2(sp); sp.set(20, 40, 'go'); sp.set(20, 39, 'cr')
        im = on_plot(sp, plot2(), 3, (2, 26))
        sheet.alpha_composite(im, (x, y + 20)); x += im.width + 8
    y += 20 + 48 * 3 + 30

d.text((20, y), "Drzewo", fill=RGB['o'], font=fs)
old_t = render(round_tree(), 'g2', 6)
sheet.alpha_composite(old_t, (20, y + 20))
new_t = render(tree2(), 'g2', 3)
sheet.alpha_composite(new_t, (640, y + 20))
d.text((20, y + 20 + 252), "Sylwetki: marchew = szeroki wachlarz, zboże = pionowe źdźbła + kłosy z ościami",
       fill=RGB['p1'], font=fs)
sheet = sheet.crop((0, 0, 1240, y + 20 + 260))
sheet.save(OUT + "porownanie_detal.png")
print("ok")
