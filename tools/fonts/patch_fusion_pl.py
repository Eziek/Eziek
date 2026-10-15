"""Poprawka znaków diakrytycznych w Fusion Pixel 8px (latin): w oryginale ą, ę, Ą, Ę nie mają ogonka, a wielkie litery
nie mają znaków nad sobą (Ż bez kropki, Ó Ć Ś Ń Ź bez kreski, niemieckie Ä Ö Ü bez kropek; w grze „Zaru”, „wrzucaja”). Skrypt dorysowuje brakujące piksele (1 piksel = 100 jednostek) i nadpisuje plik.
Idempotentny: glif, który już ma ogonek/kropkę, zostaje bez zmian. Licencja SIL OFL 1.1 pozwala na zmiany (bez Reserved
Font Name); opis zmiany jest w GDD §9 p. 111.
    python3 tools/fonts/patch_fusion_pl.py
"""
import os
from fontTools.ttLib import TTFont
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.pens.boundsPen import BoundsPen

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONT = os.path.join(ROOT, "Assets", "_Project", "Art", "Fonts", "FusionPixel", "fusion-pixel-8px-proportional-latin.otf")

OGONEK = [(200, -100, 300, 0), (300, -200, 400, -100)]   # pod prawą nóżką, potem w prawo w dół
PATCH = {
    'ą': ('ogonek', OGONEK), 'ę': ('ogonek', OGONEK), 'Ą': ('ogonek', OGONEK), 'Ę': ('ogonek', OGONEK),
    'Ż': ('dot', [(100, 700, 200, 800)]),
    'Ó': ('dot', [(200, 700, 300, 800)]), 'Ć': ('dot', [(200, 700, 300, 800)]), 'Ś': ('dot', [(200, 700, 300, 800)]),
    'Ń': ('dot', [(200, 700, 300, 800)]), 'Ź': ('dot', [(200, 700, 300, 800)]),
    'Ä': ('dot', [(0, 700, 100, 800), (200, 700, 300, 800)]), 'Ö': ('dot', [(0, 700, 100, 800), (200, 700, 300, 800)]),
    'Ü': ('dot', [(0, 700, 100, 800), (200, 700, 300, 800)]),
}

f = TTFont(FONT)
cff = f['CFF '].cff
top = cff.topDictIndex[0]
cs = top.CharStrings
gs = f.getGlyphSet()
cmap = f.getBestCmap()
hmtx = f['hmtx']
done = []
for ch, (kind, rects) in PATCH.items():
    name = cmap[ord(ch)]
    b = BoundsPen(gs)
    gs[name].draw(b)
    x0, y0, x1, y1 = b.bounds
    if (kind == 'ogonek' and y0 < 0) or (kind == 'dot' and y1 > 600):
        continue   # już poprawiony
    width = hmtx[name][0]
    pen = T2CharStringPen(width, gs)
    gs[name].draw(pen)
    for (ax, ay, bx, by) in rects:
        pen.moveTo((ax, ay)); pen.lineTo((bx, ay)); pen.lineTo((bx, by)); pen.lineTo((ax, by)); pen.closePath()
    private = top.Private if not hasattr(top, 'FDArray') else top.FDArray[top.FDSelect[top.CharStrings.charStrings[name]]].Private
    c = pen.getCharString(private, cff.GlobalSubrs)
    cs[name] = c
    done.append(ch)
f.save(FONT)
print("ok, poprawione:", "".join(done) or "nic (już poprawione)")
