"""Warianty sezonowe sprite'ów (GDD §9 p. 127): z gotowych PNG w Assets/_Project/Art/Sprites dorabia pliki
<nazwa>__spring.png, <nazwa>__autumn.png i <nazwa>__winter.png (lato = obecne sprite'y) i dopisuje im pivoty do pivots.json
(takie same jak oryginał). Uruchamiać NA KOŃCU, po wszystkich eksportach (także pad_sway.py i export_unity_stages.py),
bo czyta gotowe pliki. Gra podmienia sprite'y według pory roku (Visual/SeasonalSprites, rejestr robi Wyspa → 2).

- liściaste (drzewa owocowe we wszystkich fazach i klatkach ścinania, okrągłe drzewo, krzak): wiosna jaśniejsza zieleń
  i kwiaty (blady róż i biel), jesień rdzawe i miedziane liście, zima przymrozone szaro-brązowe liście i śnieg,
- świerk: tylko zimą śnieg (zielony cały rok),
- budowle, lampy, skrzynie, domki, monumenty, pomost, łódź, kamienie, pnie, nagrobki: zimą czapy śniegu na górnych krawędziach.
- liście-cząsteczki (Fx/fx_leaf*, Removal/fx_leaf): wiosną różowe płatki, jesienią rude, zimą brązowe (bez śniegu).
Pliki *_glow (piksele świecące) bez wariantów. Podgląd: docs/img/pory_roku.png.
"""
import colorsys
import glob
import json
import os
import zlib
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
SEASONS = ("spring", "autumn", "winter")


def hexc(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


# zielenie liści z palety (od najciemniejszej) i ich odpowiedniki w porach roku
GREENS = ["#141f1c", "#21342c", "#344a3b", "#566d52", "#7a9564"]
RAMPS = {
    "spring": ["#16231d", "#263f2f", "#3d5c40", "#638353", "#93ad6c"],
    "autumn": ["#1f1512", "#3b2420", "#5e3424", "#8a4a2a", "#b4703a"],
    "winter": ["#1a1514", "#2b2321", "#3d322d", "#52453c", "#665749"],   # przemarznięte, zbrązowiałe liście
}
BLOSSOM = [hexc("#c49aac"), hexc("#ddd0d4")]
SNOW_TOP, SNOW_SHADE = hexc("#b8c4cc"), hexc("#8896a3")


def leaf_index(rgb):
    """Indeks w rampie zieleni (0 ciemny .. 4 jasny) albo None, gdy piksel nie jest liściem."""
    h = "#%02x%02x%02x" % rgb
    if h in GREENS:
        return GREENS.index(h)
    hh, s, v = colorsys.rgb_to_hsv(*(c / 255 for c in rgb))
    if 70 / 360 <= hh <= 175 / 360 and s >= 0.15 and v >= 0.08:
        return min(4, max(0, int(v * 6.5) - 1))
    return None


def recolor(im, season, blossom):
    out = im.copy()
    px = out.load()
    w, h = out.size
    ramp = [hexc(c) for c in (PETALS if season == "petals" else RAMPS[season])]
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 128:
                continue
            i = leaf_index((r, g, b))
            if i is None:
                continue
            c = ramp[i]
            # wiosną kwiaty: rzadkie jasne plamki na jaśniejszych liściach (stałe miejsca z hasha pozycji)
            if blossom and season == "spring" and i >= 2 and (zlib.crc32(bytes([x, y, w, h])) % 100) < 9:
                c = BLOSSOM[(x + y) % 2]
            px[x, y] = c + (a,)
    return out


def snow(im, depth=2):
    """
    Czapy śniegu: na każdej powierzchni zwróconej w górę (nad nią przezroczysto) śnieg tuż pod ciemnym konturem
    (kontur zostaje, sprite nadal czytelny): jasny wiersz, potem cień śniegu; depth = ile wierszy (korony drzew grubiej,
    ostatni wiersz w szachownicę, żeby czapa miękko przechodziła w liście albo dach).
    """
    out = im.copy()
    src = im.load()
    px = out.load()
    w, h = out.size

    def dark(c):
        return (c[0] * 299 + c[1] * 587 + c[2] * 114) / 1000 < 30

    for x in range(w):
        for y in range(h):
            if src[x, y][3] < 128 or (y > 0 and src[x, y - 1][3] >= 128):
                continue
            start = y + 1 if dark(src[x, y]) else y          # kontur zostaje, śnieg pod nim
            for k in range(depth):
                yy = start + k
                if yy >= h or src[x, yy][3] < 128 or (k > 0 and dark(src[x, yy])):
                    break
                if k == depth - 1 and depth > 1 and (x + yy) % 2 == 1:
                    continue                                   # ostatni wiersz: szachownica
                px[x, yy] = (SNOW_TOP if k == 0 else SNOW_SHADE) + (src[x, yy][3],)
    return out


def files(patterns):
    out = []
    for p in patterns:
        for f in sorted(glob.glob(os.path.join(SPR, p))):
            name = os.path.basename(f)
            if "_glow" in name or "__" in name:
                continue
            out.append(f)
    return out


# liście-cząsteczki (spadające z drzew, przy ścinaniu): wiosną płatki kwiatów, jesienią rude, zimą brązowe, bez śniegu
LEAF_FX = files(["Fx/fx_leaf_*.png", "Fx/fx_leaf_fall.png", "Removal/fx_leaf.png"])
PETALS = ["#5c3a48", "#8a5a6c", "#b07c90", "#c49aac", "#ddd0d4"]
LEAFY = files(["Trees/tree_*.png", "Objects/bush.png", "Objects/tree_round.png", "Removal/bush_cut.png", "Removal/tree_round_fall_*.png"])
BLOSSOMING = ("tree_apple", "tree_rowan", "tree_elder", "tree_round", "bush")
SNOW_ONLY = files([
    "Objects/tree_spruce.png", "Objects/bonfire_*.png", "Objects/cauldron_*.png", "Objects/crate.png", "Objects/dreamhouse.png",
    "Objects/ember_*.png", "Objects/home_*.png", "Objects/notice_board*.png", "Objects/rock.png", "Objects/storehouse.png",
    "Lamps/*.png", "Sea/altar.png", "Sea/boat.png", "Sea/lighthouse.png", "Sea/pier.png", "Sea/net_buoy.png",
    "Story/monument_*.png", "Automation/harvest_crate_*.png", "Automation/rune_stone.png", "Automation/wisp_nest_*.png",
    "Automation/compost.png", "Automation/essence_jar_*.png", "Automation/merchant_chest*.png",
    "Zones/gravestone_*.png", "Zones/iron_fence.png", "Zones/broken_pillar.png", "Zones/ruined_arch.png", "Zones/dead_tree.png",
    "Zones/reeds.png", "Removal/stump_*.png", "Removal/rock_cracked.png", "Removal/rubble.png", "Removal/gravestone_broken.png",
])


def variants(path):
    im = Image.open(path).convert("RGBA")
    name = os.path.splitext(os.path.basename(path))[0]
    out = {}
    if path in LEAF_FX:
        out["spring"] = recolor(im, "petals", False)
        out["autumn"] = recolor(im, "autumn", False)
        out["winter"] = recolor(im, "winter", False)
    elif path in LEAFY:
        bl = name.startswith(BLOSSOMING)
        out["spring"] = recolor(im, "spring", bl)
        out["autumn"] = recolor(im, "autumn", bl)
        out["winter"] = snow(recolor(im, "winter", bl), 3)   # korony: grubsza czapa
    else:
        out["winter"] = snow(im)
    return im, out


def main():
    pivots_path = os.path.join(SPR, "pivots.json")
    pivots = json.load(open(pivots_path, encoding="utf-8"))
    items = pivots["items"]
    by_name = {it["name"]: it for it in items}
    # stare warianty od nowa (gdy zmienił się oryginał albo lista)
    for f in glob.glob(os.path.join(SPR, "**", "*__*.png"), recursive=True):
        os.remove(f)
        meta = f + ".meta"
        if os.path.exists(meta):
            os.remove(meta)
    items[:] = [it for it in items if "__" not in it["name"]]
    count = 0
    for path in LEAF_FX + LEAFY + SNOW_ONLY:
        base, var = variants(path)
        name = os.path.splitext(os.path.basename(path))[0]
        for season, im in var.items():
            if list(im.getdata()) == list(base.getdata()):
                continue   # nic się nie zmieniło (np. brak zieleni): bez pliku, gra zostawi oryginał
            out = os.path.join(os.path.dirname(path), f"{name}__{season}.png")
            im.save(out)
            count += 1
            if name in by_name:
                p = by_name[name]
                items.append({"name": f"{name}__{season}", "x": p["x"], "y": p["y"]})
    items.sort(key=lambda it: it["name"])
    json.dump(pivots, open(pivots_path, "w", encoding="utf-8"), indent=2)
    print(f"ok: {count} wariantów sezonowych")
    preview()


def preview():
    names = ["Objects/tree_round.png", "Trees/tree_apple_3.png", "Trees/tree_rowan_2.png", "Objects/bush.png",
             "Objects/tree_spruce.png", "Objects/crate.png", "Lamps/lamp_lantern.png", "Objects/home_cat.png"]
    cells = []
    for n in names:
        p = os.path.join(SPR, n)
        if not os.path.exists(p):
            continue
        base = Image.open(p).convert("RGBA")
        stem = os.path.splitext(p)[0]
        row = [base]
        for s in SEASONS:
            v = stem + "__" + s + ".png"
            row.append(Image.open(v).convert("RGBA") if os.path.exists(v) else base)
        # kolejność kolumn: wiosna, lato, jesień, zima
        cells.append([row[1], row[0], row[2], row[3]])
    cw = max(i.width for r in cells for i in r) + 4
    ch = max(i.height for r in cells for i in r) + 4
    bg = [(52, 74, 59, 255), (52, 74, 59, 255), (82, 59, 53, 255), (120, 132, 142, 255)]
    sheet = Image.new("RGBA", (cw * 4, ch * len(cells)))
    for j, r in enumerate(cells):
        for i, im in enumerate(r):
            tile = Image.new("RGBA", (cw, ch), bg[i])
            tile.alpha_composite(im, ((cw - im.width) // 2, ch - 2 - im.height))
            sheet.paste(tile, (i * cw, j * ch))
    out = os.path.join(ROOT, "docs", "img", "pory_roku.png")
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(out)
    print("podgląd:", out)


if __name__ == "__main__":
    main()
