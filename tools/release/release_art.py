"""Grafiki do wydania Misty Packland: ikony gry, logo, ekran startowy i obrazy do sklepów (Steam, itch.io).

Obrazy sklepowe składa z prawdziwych sprite'ów gry (16 px) na wyspie nocą, z lampami i mgłą z Fog.shader
(tools/pixel-proto/fog_clouds_preview.py). Siatka jak w grze: piksel sprite'a = 2 piksele mgły i wody (32 px),
potem całość powiększona bez wygładzania do wymaganego rozmiaru.

Uruchom z katalogu głównego repo:  python3 tools/release/release_art.py
Wyniki:
  Assets/_Project/Art/Release/icon_*.png, splash_logo.png   (ustawienia gracza: Wyspa → Wydanie → Ustawienia)
  Assets/_Project/Art/Sprites/UI/logo.png                    (logo w menu głównym)
  release/icon/misty_packland.ico                            (ikona Windows, wiele rozmiarów)
  release/store/steam/*.png, release/store/itch/*.png        (obrazy sklepowe, opis w docs/RELEASE.md)
"""
import os, sys, json, math, random
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ROOT, "tools", "pixel-proto"))
import icon as ICON
import logo as LOGO
import fog_clouds_preview as FOG

SPR = os.path.join(ROOT, "Assets", "_Project", "Art", "Sprites")
PIV = {i["name"]: i for i in json.load(open(os.path.join(SPR, "pivots.json")))["items"]}
OUT_ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Release")
OUT_REL = os.path.join(ROOT, "release")

HEX = lambda h: np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)
O, G0, G1, G2, G3 = HEX("0f0a10"), HEX("141f1c"), HEX("21342c"), HEX("344a3b"), HEX("566d52")
E0, E1, E2, P0, P1 = HEX("20171c"), HEX("352629"), HEX("523b35"), HEX("1c1520"), HEX("33263a")
B0, B1, B2, B3, FO = HEX("121c27"), HEX("1f3044"), HEX("30485f"), HEX("56697a"), HEX("848e9b")
CLIFF = [G1, E2, E2, E2, E1, E1, P1, P1, P0]
NIGHT = np.array([0.62, 0.68, 0.86])          # noc: chłodniej i ciemniej (jak światło globalne nocą w grze)
EMBER = np.array([0xf0, 0x8a, 0x3a], float)    # światło latarni


def sprite(name, folder):
    im = Image.open(os.path.join(SPR, folder, name + ".png")).convert("RGBA")
    p = PIV.get(name, {"x": 0.5, "y": 0.0})
    glow_path = os.path.join(SPR, folder, name + "_glow.png")
    glow = Image.open(glow_path).convert("RGBA") if os.path.exists(glow_path) else None
    return im, glow, (p["x"], p["y"])


def vnoise(w, h, cell, seed):
    rng = np.random.default_rng(seed)
    g = rng.random((h // cell + 3, w // cell + 3))
    ys, xs = np.mgrid[0:h, 0:w] / float(cell)
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = ys - y0, xs - x0
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    return (g[y0, x0] * (1 - fx) * (1 - fy) + g[y0, x0 + 1] * fx * (1 - fy)
            + g[y0 + 1, x0] * (1 - fx) * fy + g[y0 + 1, x0 + 1] * fx * fy)


def chamfer(land):
    """Odległość od lądu w pikselach (przybliżenie euklidesowej, maska 3-4)."""
    h, w = land.shape
    INF = 10 ** 6
    d = np.where(land, 0, INF).astype(np.int64)
    for y in range(h):
        for x in range(w):
            v = d[y, x]
            if x > 0: v = min(v, d[y, x - 1] + 3)
            if y > 0:
                v = min(v, d[y - 1, x] + 3)
                if x > 0: v = min(v, d[y - 1, x - 1] + 4)
                if x < w - 1: v = min(v, d[y - 1, x + 1] + 4)
            d[y, x] = v
    for y in range(h - 1, -1, -1):
        for x in range(w - 1, -1, -1):
            v = d[y, x]
            if x < w - 1: v = min(v, d[y, x + 1] + 3)
            if y < h - 1:
                v = min(v, d[y + 1, x] + 3)
                if x < w - 1: v = min(v, d[y + 1, x + 1] + 4)
                if x > 0: v = min(v, d[y + 1, x - 1] + 4)
            d[y, x] = v
    return d / 3.0


class Scene:
    """Wyspa nocą w siatce danych (piksel sprite'a). Rysunek w siatce x2 (jak woda i mgła w grze)."""

    def __init__(self, dw, dh, cx, cy, rx, ry, seed=5):
        self.dw, self.dh = dw, dh
        n = vnoise(dw, dh, 9, seed) - 0.5
        ys, xs = np.mgrid[0:dh, 0:dw]
        e = ((xs - cx) / rx) ** 2 + ((ys - cy) / ry) ** 2 + n * 0.55
        self.land = e < 1.0
        self.cx, self.cy, self.rx, self.ry = cx, cy, rx, ry
        self.rng = random.Random(seed)
        self.objects = []     # (nazwa, folder, x, y) w pikselach danych, punkt = pivot sprite'a
        self.lamps = []       # (x, y) źródła światła (głowa lampy)

    def add(self, name, folder, x, y, light=None):
        self.objects.append((name, folder, x, y))
        if light is not None:
            self.lamps.append((x + light[0], y + light[1]))

    def render(self, fog_edge=20.0, fog_t=1.0, night=True):
        dw, dh, S = self.dw, self.dh, 2
        land = self.land
        # klif: pod każdym brzegiem od dołu kilka pikseli ziemi i skały
        cliff = {}
        for x in range(dw):
            for y in range(dh - 1):
                if land[y, x] and not land[y + 1, x]:
                    for d in range(1, len(CLIFF) + 1):
                        if y + d >= dh or land[y + d, x]:
                            break
                        k = CLIFF[d - 1]
                        if 3 <= d <= 7 and self.rng.random() < 0.08:
                            k = E0
                        cliff[(x, y + d)] = k
        solid = land.copy()
        for (x, y) in cliff:
            solid[y, x] = True
        outline = np.zeros_like(land)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            outline |= np.roll(np.roll(solid, dy, 0), dx, 1)
        outline &= ~solid
        dist = chamfer(solid | outline)

        # woda w siatce x2: pasy głębi od brzegu, piana, rzadkie błyski
        gh, gw = dh * S, dw * S
        yy, xx = np.mgrid[0:gh, 0:gw]
        dd = dist[yy // S, xx // S]
        img = np.zeros((gh, gw, 3))
        img[:] = B1
        img[dd > 9] = B0 * 0.5 + B1 * 0.5
        img[(dd > 2) & (dd <= 5)] = B2
        foam = (dd > 0) & (dd <= 1.5) & ((xx // 2 + yy // 3) % 3 != 0)
        img[foam] = FO
        spark = (np.random.default_rng(3).random((gh, gw)) < 0.004) & (dd > 3)
        img[spark] = B3
        # ląd: trawa w płatach, kępki, klif, obrys
        nz = vnoise(dw, dh, 5, 11)
        top = np.zeros((dh, dw, 3))
        top[:] = G2
        top[nz < 0.38] = G1
        top[(nz > 0.66)] = G2 * 0.6 + G3 * 0.4
        for _ in range(int(land.sum()) // 40):
            x, y = self.rng.randrange(dw), self.rng.randrange(dh)
            if land[y, x]:
                top[y, x] = G3
        # krawędź od dołu (nad klifem) ciemniejsza
        edge_dn = land & ~np.roll(land, -1, 0)
        top[edge_dn] = G1
        data = np.zeros((dh, dw, 3)); dmask = np.zeros((dh, dw), bool)
        data[land] = top[land]; dmask |= land
        for (x, y), k in cliff.items():
            data[y, x] = k; dmask[y, x] = True
        data[outline] = O; dmask |= outline
        up = lambda a: np.repeat(np.repeat(a, S, 0), S, 1)
        img = np.where(up(dmask)[..., None], up(data), img)

        # obiekty (posortowane po y), poświaty osobno (świecą też nocą)
        glow_layer = np.zeros((gh, gw, 4))
        canvas = Image.fromarray(img.astype(np.uint8)).convert("RGBA")
        for name, folder, x, y in sorted(self.objects, key=lambda o: o[3]):
            im, glow, (px, py) = sprite(name, folder)
            w, h = im.size
            X = int(round((x - px * w) * S)); Y = int(round((y - (1 - py) * h) * S))
            canvas.alpha_composite(im.resize((w * S, h * S), Image.NEAREST), (max(X, 0), max(Y, 0)) if X >= 0 and Y >= 0 else (0, 0)) \
                if X >= 0 and Y >= 0 else self._paste_clip(canvas, im.resize((w * S, h * S), Image.NEAREST), X, Y)
            if glow is not None:
                gl = Image.new("RGBA", (gw, gh), (0, 0, 0, 0))
                self._paste_clip(gl, glow.resize((w * S, h * S), Image.NEAREST), X, Y)
                ga = np.asarray(gl).astype(float)
                glow_layer = np.where(ga[..., 3:4] > 0, ga, glow_layer)
        img = np.asarray(canvas)[..., :3].astype(float)

        if night:
            img = img * NIGHT
            # światło lamp: ciepłe kręgi w trzech stopniach z ditheringiem na granicach
            bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
            th = bayer[yy % 4, xx % 4]
            light = np.zeros((gh, gw))
            for (lx, ly) in self.lamps:
                r = np.hypot(xx / S - lx, (yy / S - ly) * 1.3)
                light = np.maximum(light, np.clip(1.0 - r / 30.0, 0, 1))
            lvl = np.floor(light * 3.0 + th * 0.9) / 3.0
            img = img + (img * 1.0 + EMBER * 0.42) * lvl[..., None]
            img = np.where(glow_layer[..., 3:4] > 0, glow_layer[..., :3], img)

        # mgła (Fog.shader) w siatce x2; nocą przygaszona i chłodniejsza
        def dist_fn(x, y):
            xi = np.clip(x - 0.5, 0, dw - 1.001); yi = np.clip(y - 0.5, 0, dh - 1.001)
            x0, y0 = xi.astype(int), yi.astype(int); fx, fy = xi - x0, yi - y0
            a, b = dist[y0, x0], dist[y0, np.minimum(x0 + 1, dw - 1)]
            c, d = dist[np.minimum(y0 + 1, dh - 1), x0], dist[np.minimum(y0 + 1, dh - 1), np.minimum(x0 + 1, dw - 1)]
            return np.minimum((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy, 63.75)
        fcol, fa, _ = FOG.fog('normal', fog_edge, fog_t, dist=dist_fn, w=dw, h=dh, s=S)
        if night:
            fcol = fcol * np.array([0.82, 0.85, 0.95])
        img = img * (1 - fa[..., None]) + fcol * fa[..., None]
        return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).convert("RGBA")

    @staticmethod
    def _paste_clip(canvas, im, X, Y):
        x0, y0 = max(0, -X), max(0, -Y)
        if x0 >= im.width or y0 >= im.height:
            return
        canvas.alpha_composite(im.crop((x0, y0, im.width, im.height)), (X + x0, Y + y0))


def default_island(dw, dh, cx, cy, rx, ry, seed=5):
    """Wyspa z ogródkiem, drzewami, lampami i ognikiem. Pozycje względem środka i promieni."""
    s = Scene(dw, dh, cx, cy, rx, ry, seed)
    P = lambda fx, fy: (cx + fx * rx, cy + fy * ry)
    # ogródek: rzędy grządek co 16 px (kafelek), nocne rośliny świecą; tyle, ile zmieści się na wyspie
    crops = ("crop_moonflower_3", "crop_ghostpumpkin_3", "crop_moonpoppy_3", "crop_mandrake_3", "crop_belladonna_3")
    cols = max(2, min(4, int(rx * 1.1) // 16))
    nrows = 2 if ry > 32 else 1
    gx, gy = cx - (cols - 1) * 8, cy + 0.12 * ry
    for r in range(nrows):
        for i in range(cols - r):
            x, y = gx + i * 16 + r * 8, gy + r * 16
            s.add("plot", "Ground", x, y - 6)
            s.add(crops[(i + 2 * r) % len(crops)], "Crops", x, y)
    s.add("tree_spruce", "Objects", *P(-0.62, -0.25))
    s.add("tree_round", "Objects", *P(0.6, -0.3))
    s.add("tree_apple_3", "Trees", *P(-0.35, -0.45))
    s.add("bush", "Objects", *P(0.78, 0.25))
    s.add("rock", "Objects", *P(-0.75, 0.3))
    x, y = P(-0.45, 0.05); s.add("lamp_lantern", "Lamps", x, y, light=(0, -26))
    x, y = P(0.38, 0.1); s.add("lamp_lantern", "Lamps", x, y, light=(0, -26))
    x, y = P(0.2, -0.5); s.add("lamp_mushrooms", "Lamps", x, y, light=(0, -6))
    s.add("wisp_gardener_0", "Automation", *P(0.05, -0.05))
    return s


def logo_img(scale):
    im = LOGO.logo()
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


def capsule(size, data_size, island, logo_scale, logo_pos, fog_edge=20.0, seed=5):
    """Obraz sklepowy: scena w siatce danych data_size (x2 mgła), logo w pikselach siatki x2, powiększenie do size."""
    dw, dh = data_size
    cx, cy, rx, ry = island
    sc = default_island(dw, dh, cx * dw, cy * dh, rx * dw, ry * dh, seed) if rx > 0 else Scene(dw, dh, -999, -999, 1, 1, seed)
    im = sc.render(fog_edge=fog_edge)
    if logo_scale:
        lg = logo_img(logo_scale)
        x = int(logo_pos[0] * im.width - lg.width / 2)
        y = int(logo_pos[1] * im.height - lg.height / 2)
        im.alpha_composite(lg, (x, y))
    k = size[0] // im.width
    out = im.resize((im.width * k, im.height * k), Image.NEAREST)
    if out.size != tuple(size):   # wyrównanie do dokładnego rozmiaru (ucięcie albo dopełnienie krawędzi)
        full = Image.new("RGBA", size, (0x12, 0x1c, 0x27, 255))
        full.paste(out, ((size[0] - out.width) // 2, (size[1] - out.height) // 2))
        out = full
    return out.convert("RGB")


def save(im, *parts):
    path = os.path.join(*parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    print("  ", os.path.relpath(path, ROOT), im.size)


def main():
    print("ikony")
    icons = ICON.icon_set()
    for n in (16, 32, 48, 64, 128, 256, 512, 1024):
        save(icons[n], OUT_ART, f"icon_{n}.png")
    ico_sizes = [16, 24, 32, 48, 64, 128, 256]
    os.makedirs(os.path.join(OUT_REL, "icon"), exist_ok=True)
    icons[256].save(os.path.join(OUT_REL, "icon", "misty_packland.ico"), sizes=[(n, n) for n in ico_sizes],
                    append_images=[icons[n] for n in ico_sizes if n != 256])
    print("   release/icon/misty_packland.ico", ico_sizes)

    print("logo")
    save(LOGO.logo(), SPR, "UI", "logo.png")
    save(logo_img(8), OUT_ART, "splash_logo.png")

    print("Steam")
    steam = os.path.join(OUT_REL, "store", "steam")
    # (plik, rozmiar docelowy, siatka danych, wyspa (środek x, y, promień x, y jako ułamki), skala logo, środek logo)
    jobs = [
        ("header_capsule.png", (920, 430), (230, 107), (0.68, 0.56, 0.27, 0.34), 3, (0.25, 0.5)),
        ("small_capsule.png", (462, 174), (115, 43), (0.0, 0.0, 0.0, 0.0), 3, (0.5, 0.5)),
        ("main_capsule.png", (1232, 706), (308, 176), (0.62, 0.58, 0.2, 0.22), 4, (0.3, 0.3)),
        ("vertical_capsule.png", (748, 896), (187, 224), (0.5, 0.62, 0.4, 0.2), 4, (0.5, 0.17)),
        ("library_capsule.png", (600, 900), (150, 225), (0.5, 0.64, 0.42, 0.17), 3, (0.5, 0.17)),
        ("library_hero.png", (3840, 1240), (480, 155), (0.5, 0.55, 0.13, 0.3), 0, None),
    ]
    for name, size, ds, isl, ls, lp in jobs:
        save(capsule(size, ds, isl, ls, lp), steam, name)
    lib_logo = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
    lg = logo_img(20)
    lib_logo.alpha_composite(lg, ((1280 - lg.width) // 2, (720 - lg.height) // 2))
    save(lib_logo, steam, "library_logo.png")
    comm = Image.new("RGB", (184, 184), (0x12, 0x1c, 0x27))
    big = icons[32].resize((160, 160), Image.NEAREST)
    comm.paste(big, (12, 12), big)
    save(comm, steam, "community_icon.png")

    print("itch.io")
    save(capsule((630, 500), (157, 125), (0.5, 0.62, 0.4, 0.26), 2, (0.5, 0.15)), OUT_REL, "store", "itch", "cover.png")


if __name__ == "__main__":
    main()
