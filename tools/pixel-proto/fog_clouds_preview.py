"""Podgląd mgły „kłębiaste chmury” (prośba gracza: jak pixel-artowe cumulusy z referencji, ale w brudnej palecie dark fantasy).

Każdy kłąb to kula: cieniowanie od światła z lewej-góry w 6 tonach, z poszarpanymi granicami tonów (szum w parach pikseli
zamiast gładkiego przejścia). Kłąb niżej na ekranie zasłania wyższe (stoi bliżej), więc jasny wierzch przedniego kłębu
odcina się od ciemnego spodu tylnego. Bez obrysu. Im dalej od wyspy, tym kłęby ciemniejsze (głębia mgły).
Mgła liczona w siatce 32 px (dwa piksele na piksel danych wody), jak woda. Logika = Assets/_Project/Shaders/Fog.shader.

Uruchom: python3 tools/pixel-proto/fog_clouds_preview.py  ->  docs/img/mgla_chmury.png
"""
import os, math
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
S = 2                                   # piksele mgły na piksel danych (siatka 32 px)
W, H = 240, 200                         # kadr w pikselach danych (16 px)

# tony mgły od najciemniejszego (brudne, mroczne; ciepły, przybrudzony wierzch jak stary pergamin)
TONES = np.array([[0x1b, 0x20, 0x29], [0x2a, 0x31, 0x3c], [0x3f, 0x47, 0x51], [0x5c, 0x60, 0x63],
                  [0x83, 0x80, 0x76], [0xa9, 0xa0, 0x88]], float)
CELL = 30.0                             # komórka siatki chmur (piksele mgły)
RC = (14.0, 10.0)                       # promień bryły chmury: min, rozrzut
NB = 6                                  # guzki na obwodzie bryły
RIM = 2.5                               # szerokość rąbka (piksele mgły)
LIGHT = np.array([-0.55, -0.75, 0.45]); LIGHT /= np.linalg.norm(LIGHT)   # y w dół: z lewej-góry, trochę od widza
BAND_SCALE = 0.8                        # przezroczyste okno: trochę mniejsze chmury (mieszczą się w 34 px)
CAP = 2.0                               # łuki guzków w środku bryły (piksele mgły)
TONE_MID, TONE_SPAN, RIM_TONE, CAP_TONE, DEPTH, DEPTH_TONES, JIT, BASE_TONE = 2.9, 2.6, 1.1, 0.6, 30.0, 1.5, 0.35, 1.2


def hash4(x, y):
    """WyspaHash (Hash without Sine) dla tablic."""
    p = np.stack([x * 0.1031, y * 0.1030, x * 0.0973, y * 0.1099], -1)
    p = p - np.floor(p)
    p = p + (p * (p[..., [3, 2, 0, 1]] + 33.33)).sum(-1, keepdims=True)
    r = (p[..., [0, 0, 1, 2]] + p[..., [1, 2, 2, 3]]) * p[..., [2, 1, 3, 0]]
    return r - np.floor(r)


def island_dist(x, y):
    """Odległość od brzegu w pikselach danych (wyspa z dwóch zaokrąglonych prostokątów, jak w fog_chibi_preview)."""
    d1 = np.hypot(np.maximum(abs(x - 120) - 34, 0), np.maximum(abs(y - 105) - 20, 0))
    d2 = np.hypot(np.maximum(abs(x - 100) - 14, 0), np.maximum(abs(y - 77) - 12, 0))
    return np.minimum(d1, d2) - 3.0 + 1.5 * np.sin(x * 0.4) * np.cos(y * 0.33)


def cluster(ci, cj, T, seed=0.0, scale=1.0):
    """Chmura z komórki (ci, cj): środek, promień bryły i guzki (środki, promienie). Wszystko w pikselach mgły."""
    h = hash4(ci + 17.0 + seed, cj + 5.0)
    cx = (ci + 0.25 + 0.5 * h[..., 0]) * CELL * scale + np.round(np.sin(T + h[..., 2] * 6.283) * 2.0)
    cy = (cj + 0.25 + 0.5 * h[..., 1]) * CELL * scale
    rc = (RC[0] + RC[1] * h[..., 2]) * scale
    bumps = []
    for b in range(NB):
        g = hash4(ci * 7.0 + b * 13.0 + 1.0 + seed, cj * 3.0 + b * 5.0 + 2.0)
        ang = (b + 0.6 * g[..., 0]) / NB * 6.283
        rr = rc * (0.45 + 0.35 * g[..., 1])
        bx = cx + np.cos(ang) * rr
        by = cy + np.sin(ang) * rr * 0.7                       # chmury szersze niż wyższe
        br = rc * (0.42 + 0.25 * g[..., 2]) + 0.6 * np.sin(T * 2.0 + g[..., 3] * 6.283)
        bumps.append((bx, by, br))
    bumps.append((cx, cy, rc * 0.72))                          # rdzeń bryły
    return cx, cy, rc, bumps


def in_cluster(px, py, bumps):
    m = np.zeros(px.shape, bool)
    for bx, by, br in bumps:
        m |= (px - bx) ** 2 + (py - by) ** 2 < br * br
    return m


def inner_caps(px, py, bumps, lx, ly):
    """Jasne łuki guzków w środku bryły: piksel w guzku, a punkt przesunięty ku światłu już poza nim."""
    m = np.zeros(px.shape, bool)
    for bx, by, br in bumps[:-1]:
        inb = (px - bx) ** 2 + (py - by) ** 2 < br * br
        out = (px + lx * CAP - bx) ** 2 + (py + ly * CAP - by) ** 2 >= br * br
        m |= inb & out
    return m


def fog(mode, edge, T, dist=None, w=W, h=H, s=S):
    """Zwraca (kolor RGB, alfa, odległość) mgły na siatce w*s x h*s (dist: funkcja odległości, domyślnie wyspa z podglądu). mode: 'normal' (całe morze) albo 'band' (przezroczyste okno).

    Chmura = bryła z guzków (sylwetka kalafiora). Chmura niżej na ekranie stoi bliżej i zasłania wyższe.
    Cieniowanie na dwóch poziomach: bryła jak kula (jasna góra-lewo, ciemny dół-prawo), a na krawędzi sylwetki od światła
    jasny rąbek, od cienia ciemny (każdy guzek na brzegu dostaje swoją czapę). W środku bryły guzków nie widać.
    """
    dist = dist or island_dist        # odległość od brzegu (piksele danych) w punkcie danych
    yy, xx = np.mgrid[0:h * s, 0:w * s].astype(float)
    px, py = xx + 0.5, yy + 0.5
    d = dist(px / s, py / s)
    band = mode == 'band'
    lo, hi = (edge + 4.0, edge + 20.0) if band else (edge + 2.0, 1e9)
    best_y = np.full(xx.shape, -1e9)
    big = np.zeros(xx.shape); rim = np.zeros(xx.shape); depth = np.zeros(xx.shape)
    cell = CELL * (BAND_SCALE if band else 1.0)
    gx, gy = np.floor(xx / cell), np.floor(yy / cell)
    lx, ly = LIGHT[0] / np.hypot(LIGHT[0], LIGHT[1]), LIGHT[1] / np.hypot(LIGHT[0], LIGHT[1])
    for i in (-1, 0, 1):
        for j in (-1, 0, 1):
          for k in (0, 1):               # dwie chmury na komórkę (gęsto, bez dziur)
            ci, cj = gx + i + 0.5 * k, gy + j + 0.5 * k
            cx, cy, rc, bumps = cluster(ci, cj, T, 41.0 * k, BAND_SCALE if band else 1.0)
            dc = dist(cx / s, cy / s)
            ok = (dc >= lo) & (dc <= hi)
            inside = ok & (cy > best_y) & in_cluster(px, py, bumps)
            lit_edge = ~in_cluster(px + lx * RIM, py + ly * RIM, bumps)      # tuż przy krawędzi od strony światła
            dark_edge = ~in_cluster(px - lx * RIM, py - ly * RIM, bumps)     # i od strony cienia
            qx, qy = (px - cx) / (rc * 1.25), (py - cy) / (rc * 0.95)
            q2 = np.minimum(qx * qx + qy * qy, 1.0)
            nz = np.sqrt(1.0 - q2)
            lit = qx * LIGHT[0] + qy * LIGHT[1] + nz * LIGHT[2]
            best_y = np.where(inside, cy, best_y)
            big = np.where(inside, lit, big)
            caps = inner_caps(px, py, bumps, lx, ly)
            rim = np.where(inside, np.where(lit_edge, 1.0, np.where(dark_edge, -1.0, np.where(caps, CAP_TONE, 0.0))), rim)
            depth = np.where(inside, np.clip((dc - edge) / DEPTH, 0, 1), depth)
    has = best_y > -1e8
    base = (d > edge + 10.0) if not band else ((d > edge - 3.0) & (d < edge + 9.0))
    jit = hash4(np.floor(xx / 2.0) + 3.0, yy + 11.0)[..., 0] - 0.5
    t = TONE_MID + big * TONE_SPAN + rim * RIM_TONE - depth * DEPTH_TONES + jit * JIT
    t = np.where(has, t, BASE_TONE - np.clip((d - edge) / 30.0, 0, 1) + jit * JIT)
    idx = np.clip(np.floor(t), 0, 5).astype(int)
    a = np.where(has | base, 0.95, 0.0)
    if band:
        a = np.where(d < edge + 34.0, a, 0.0)   # twardy limit zasięgu w przezroczystym oknie
    a = np.where(d > 0, a, 0.0)
    return TONES[idx], a, d


def scene(mode, edge, T, night=False):
    col, a, d = fog(mode, edge, T)
    img = np.zeros(col.shape)
    sea = np.array([0x1f, 0x30, 0x44], float)
    img[:] = [0x3a, 0x3b, 0x40] if mode == 'band' else sea
    if mode == 'band':
        img[(d > 0) & (d < edge)] = sea
    img[d <= 0] = [0x34, 0x4a, 0x3b]
    img[(d <= 0) & (d > -1.0)] = [0x0f, 0x0a, 0x10]
    if night:
        col = np.maximum(col * 0.42 + np.array([0, 4, 14]), col * 0.45)
        img = img * 0.45
    out = img * (1 - a[..., None]) + col * a[..., None]
    return Image.fromarray(out.astype(np.uint8))


if __name__ == "__main__":
    frames = [scene('normal', 22, 1.0), scene('band', 18, 1.0), scene('normal', 22, 1.0, night=True)]
    Z = 2
    sheet = Image.new("RGB", (sum(f.width for f in frames) * Z + 8 * (len(frames) - 1), frames[0].height * Z), (12, 12, 14))
    x = 0
    for f in frames:
        sheet.paste(f.resize((f.width * Z, f.height * Z), Image.NEAREST), (x, 0))
        x += f.width * Z + 8
    out = os.path.join(ROOT, "docs", "img", "mgla_chmury.png")
    sheet.save(out)
    print("ok:", out)
