import os
from PIL import Image, ImageDraw, ImageFont
from sprites import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/"
os.makedirs(os.path.dirname(OUT), exist_ok=True)


def render(s, bg=None, scale=1):
    img = Image.new("RGBA", (s.w, s.h), (RGB[bg] + (255,)) if bg else (0, 0, 0, 0))
    for (x, y), k in s.px.items():
        img.putpixel((x, y), RGB[k] + (255,))
    return img.resize((s.w * scale, s.h * scale), Image.NEAREST)


if __name__ == "__main__":
    S = 5
    W = 1000
    sheet = Image.new("RGBA", (W, 900), RGB['fo'] + (255,))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 16)
    fs = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 11)

    d.text((20, 14), "Paleta: rampy z hue-shiftem (cień → śliwka, światło → złoto)", fill=RGB['o'], font=f)
    ramps = [("kontur/śliwka", ['o', 'p0', 'p1', 'p2']), ("ciepłe", ['co', 'or', 'go', 'cr']),
             ("ziemia", EARTH), ("zieleń", GREEN), ("woda", ['b0', 'b1', 'b2', 'b3', 'fo'])]
    x = 20
    for name, r in ramps:
        d.text((x, 42), name, fill=RGB['o'], font=fs)
        for i, k in enumerate(r):
            d.rectangle([x + i * 34, 60, x + i * 34 + 30, 90], fill=RGB[k], outline=RGB['o'])
            d.text((x + i * 34, 94), PAL[k][1:], fill=RGB['p1'], font=ImageFont.truetype(
                "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 8))
        x += len(r) * 34 + 22

    def row(label, sprites, y, bg='g2', scale=S):
        d.text((20, y), label, fill=RGB['o'], font=f)
        x = 20
        for sp in sprites:
            im = render(sp, bg, scale)
            sheet.alpha_composite(im, (x, y + 24))
            x += im.width + 14

    row("Marchew", [seed(), carrot(1), carrot(2), carrot(3)], 125)
    row("Zboże", [seed(), wheat(1), wheat(2), wheat(3)], 280)
    row("Dynia", [seed(), pumpkin(1), pumpkin(2), pumpkin(3)], 435)
    row("Obiekty", [round_tree(), spruce(), bush(), rock(), crate()], 590, scale=4)
    row("UI / cząsteczki", [coin(), carrot_item(), text("128"), sparkle(1), sparkle(2), plot()], 790, bg='p1', scale=4)
    sheet.save(OUT + "arkusz_styl.png")
    print("ok")
