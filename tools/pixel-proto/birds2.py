"""Więcej ptaków (GDD §9 p. 113): sroka, gil, sikora, mewa, czapla. Pozy jak u wrony i zięby (sprites6.bird):
sit0, sit1 (dziób w dół / u czapli: uderzenie dziobem w wodę), fly0 (skrzydła w górze), fly1 (w dole).
Patrzą w prawo, w lewo odbija kod (flipX). Zaczepienie: środek dołu (łapki). Paleta mroczna, stonowane kolory.
Podgląd: python3 birds2.py -> docs/img/ptaki.png
"""
import os
from sprites import Sprite

POSES = ('sit0', 'sit1', 'fly0', 'fly1')


def _wings(s, x, y, k, pose, span=3, up=None):
    """Skrzydła w locie: dwie linie od barku w górę (fly0) albo w dół (fly1)."""
    if pose == 'fly0':
        s.line(x, y, x - span, y - 4, k); s.line(x + 1, y, x - span + 2, y - 4, k)
    elif pose == 'fly1':
        s.line(x, y + 1, x - span, y + 4, k); s.line(x + 1, y + 1, x - span + 2, y + 4, k)


def small(kind, pose):
    """Gil (szaroniebieski grzbiet, przygaszony czerwony brzuch, czarna czapeczka) albo sikora (oliwkowy grzbiet,
    przygaszony żółty brzuch, czarna głowa z jasnym policzkiem)."""
    s = Sprite(12, 10, 6, 9)
    back, belly, head = ((['p0', 'b1', 'b2'], 'co', ['o', 'o', 'p0']) if kind == 'bullfinch'
                         else (['g0', 'g1', 'g2'], 'go', ['o', 'o', 'p0']))
    fly = pose.startswith('fly')
    by = 4 if fly else 5
    s.blob(5.5, by, 2.8, 2.0, back)
    s.set(5, by + 1, belly); s.set(6, by + 1, belly); s.set(7, by, belly)
    hx, hy = (8.3, by - 1.8) if pose != 'sit1' else (8.8, by + 0.6)
    s.blob(hx, hy, 1.4, 1.4, head)
    if kind == 'tit':
        s.set(int(hx), int(round(hy)) + (0 if pose != 'sit1' else -1), 'cr')     # jasny policzek
    s.set(int(hx + 1.6), int(round(hy)), 'o' if kind == 'bullfinch' else 'p1')    # krótki dziób
    s.line(1, by + 1, 3, by, back[1])                                            # ogon
    _wings(s, 5, by - 1, back[1], pose)
    if not fly:
        s.set(5, 8, 'e1'); s.set(7, 8, 'e1')
    s.outline()
    return s


def magpie(pose):
    """Sroka: czarna z białym brzuchem i białą plamą na skrzydle, długi ogon z granatowym połyskiem."""
    s = Sprite(16, 10, 9, 9)
    fly = pose.startswith('fly')
    by = 4 if fly else 5
    s.blob(8.5, by, 3.0, 2.0, ['o', 'p0', 'p1'])
    s.set(8, by + 1, 'cr'); s.set(9, by + 1, 'cr'); s.set(10, by, 'cr')        # biały brzuch
    s.set(7, by - 1, 'cr'); s.set(8, by - 1, 'fo')                               # biała plama na skrzydle
    hx, hy = (11.5, by - 1.8) if pose != 'sit1' else (12.0, by + 0.6)
    s.blob(hx, hy, 1.5, 1.4, ['o', 'o', 'p0'])
    s.set(int(hx + 1.7), int(round(hy)), 'o')
    s.line(0, by - 1, 5, by, 'b1'); s.line(1, by, 5, by + 1, 'p1')               # długi ogon
    _wings(s, 8, by - 1, 'p0', pose, 4)
    if not fly:
        s.set(8, 8, 'o'); s.set(10, 8, 'o')
    s.outline()
    return s


def gull(pose):
    """Mewa: jasnoszara (przygaszona biel), czarne końce skrzydeł, żółtawy dziób; w locie szerokie skrzydła w kształcie M."""
    s = Sprite(18, 11, 9, 10)
    fly = pose.startswith('fly')
    by = 5 if fly else 6
    s.blob(9, by, 3.4, 2.0, ['b3', 'fo', 'cr'])
    hx, hy = (12.3, by - 1.8) if pose != 'sit1' else (12.6, by + 0.5)
    s.blob(hx, hy, 1.5, 1.4, ['fo', 'cr', 'cr'])
    s.set(int(hx + 1.7), int(round(hy)), 'go'); s.set(int(hx + 2.6), int(round(hy)), 'go')
    s.line(4, by, 6, by - 1, 'b3')                                               # ogon
    if pose == 'fly0':
        s.line(8, by - 1, 4, by - 4, 'fo'); s.line(4, by - 4, 1, by - 3, 'o')
        s.line(10, by - 1, 14, by - 4, 'fo'); s.line(14, by - 4, 16, by - 3, 'o')
    elif pose == 'fly1':
        s.line(8, by + 1, 4, by + 3, 'fo'); s.line(4, by + 3, 2, by + 4, 'o')
        s.line(10, by + 1, 14, by + 3, 'fo'); s.line(14, by + 3, 16, by + 4, 'o')
    else:
        s.set(6, by - 1, 'o'); s.set(5, by, 'o')                                 # czarne końce złożonych skrzydeł
        s.set(8, 9, 'or'); s.set(10, 9, 'or'); s.set(8, 10, 'or'); s.set(10, 10, 'or')
    s.outline()
    return s


def heron(pose):
    """Czapla: wysoka, szaroniebieska, czarny czubek, długi dziób i nogi. sit0 = stoi w wodzie z szyją w S,
    sit1 = uderzenie dziobem w wodę, fly0/fly1 = szyja podkulona, nogi wyciągnięte do tyłu."""
    fly = pose.startswith('fly')
    if fly:
        s = Sprite(22, 12, 11, 11)
        s.blob(11, 6, 4.2, 2.0, ['b1', 'b2', 'b3'])
        s.blob(15.5, 5, 1.6, 1.4, ['b2', 'b3', 'fo']); s.set(15, 4, 'o')
        s.line(17, 5, 20, 5, 'go')                                               # dziób
        if pose == 'fly0':
            s.line(10, 5, 6, 0, 'b2'); s.line(12, 5, 9, 0, 'b2'); s.set(6, 0, 'o')
        else:
            s.line(10, 7, 6, 11, 'b2'); s.line(12, 7, 9, 11, 'b2'); s.set(6, 11, 'o')
        s.outline()
        s.line(6, 6, 2, 7, 'co'); s.set(1, 7, 'p2')                              # cienkie czerwone nogi do tyłu
        return s
    s = Sprite(14, 22, 7, 21)
    s.blob(6, 10, 3.6, 2.6, ['b1', 'b2', 'b3'])                                   # tułów
    s.line(2, 11, 0, 13, 'b1')                                                   # ogon
    if pose == 'sit0':
        neck = [(8, 8), (9, 7), (9, 6), (8, 5), (8, 4), (9, 3)]
        hx, hy = 10, 2
    else:
        neck = [(8, 9), (9, 10), (10, 11), (11, 12), (11, 13)]
        hx, hy = 12, 14
    for x, y in neck:
        s.set(x, y, 'fo')
    s.blob(hx, hy, 1.3, 1.1, ['b2', 'fo', 'cr'])
    s.set(hx - 1, hy - 1, 'o'); s.set(hx - 2, hy - 1, 'o')                       # czarny czubek
    if pose == 'sit0':
        s.line(hx + 1, hy, hx + 4, hy, 'go')
    else:
        s.line(hx + 1, hy + 1, hx + 2, hy + 4, 'go')                             # dziób w wodzie
    s.outline()
    # nogi po konturze, więc zostają cienkie (1 px): bliższa czerwona ze stawem skokowym, dalsza ciemniejsza, palce
    s.line(5, 14, 5, 20, 'p2'); s.set(4, 21, 'p2'); s.set(5, 21, 'p2')
    s.line(7, 14, 7, 17, 'co'); s.set(7, 18, 'p2'); s.line(7, 19, 7, 20, 'co')
    s.set(6, 21, 'co'); s.set(7, 21, 'co'); s.set(8, 21, 'co')
    return s


BIRDS = {
    'magpie': magpie, 'bullfinch': lambda p: small('bullfinch', p), 'tit': lambda p: small('tit', p),
    'gull': gull, 'heron': heron,
}


if __name__ == "__main__":
    from familiars import to_img
    from PIL import Image
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    rows = [[to_img(fn(p)) for p in POSES] for fn in BIRDS.values()]
    cw = max(i.width for r in rows for i in r) + 4
    ch = max(i.height for r in rows for i in r) + 4
    sheet = Image.new("RGBA", (cw * len(POSES), ch * len(rows)), (60, 80, 70, 255))
    for j, r in enumerate(rows):
        for i, im in enumerate(r):
            sheet.alpha_composite(im, (i * cw + (cw - im.width) // 2, j * ch + ch - 2 - im.height))
    out = os.path.join(ROOT, "docs", "img", "ptaki.png")
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(out)
    print("ok:", out)
