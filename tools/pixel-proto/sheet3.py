"""Arkusze podglądowe nowych sprite'ów (sprites3)."""
import os
from PIL import Image, ImageDraw, ImageFont
import sprites3 as S3
from sprites3 import RGB
from sheet import render

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/"
os.makedirs(OUT, exist_ok=True)
F = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"


def sheet(title, rows, path, scale=4, width=1180):
    f = ImageFont.truetype(F, 16); fs = ImageFont.truetype(F, 11)
    blocks = []
    for label, items, bg in rows:
        ims = [(n, render(sp, bg, scale)) for n, sp in items]
        blocks.append((label, ims))
    hgt = 40 + sum(30 + max(im.height for _, im in ims) + 22 for _, ims in blocks)
    img = Image.new("RGBA", (width, hgt), RGB['p0'] + (255,))
    d = ImageDraw.Draw(img)
    d.text((16, 10), title, fill=RGB['cr'], font=f)
    y = 40
    for label, ims in blocks:
        d.text((16, y), label, fill=RGB['fo'], font=fs)
        x = 16; mh = max(im.height for _, im in ims)
        for n, im in ims:
            if x + im.width > width - 10:
                break
            img.alpha_composite(im, (x, y + 18 + mh - im.height))
            d.text((x, y + 22 + mh), n, fill=RGB['b3'], font=ImageFont.truetype(F, 9))
            x += max(im.width, 70) + 10
        y += 30 + mh + 22
    img.save(path)


if __name__ == "__main__":
    rows = []
    for cid, (name, fn) in S3.CROPS_NIGHT.items():
        rows.append((name, [("0", S3.night_seed())] + [(str(i), fn(i)) for i in (1, 2, 3)], 'g2'))
    rows.append(("Hybryda: dynia-upiór", [("3", S3.hybrid_pumpkin())], 'g2'))
    sheet("Rośliny nocy (faza 0 nasiono -> 3 dojrzała)", rows, OUT + "arkusz_rosliny_nocy.png", scale=5)

    for cat, objs in S3.OBJECTS.items():
        items = [(n, fn()) for n, fn in objs.items()]
        bg = 'b1' if cat in ('Sea', 'Events') else 'g2'
        chunks = [items[i:i + 7] for i in range(0, len(items), 7)]
        sheet(f"Obiekty: {cat}", [(f"{cat} {i + 1}", c, bg) for i, c in enumerate(chunks)],
              OUT + f"arkusz_{cat.lower()}.png", scale=4)
    print("ok")
