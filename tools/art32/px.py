"""Wspólne narzędzia docelowej grafiki 32 px (Etap 7): paleta dark fantasy, szum, dithering, pola odległości.

Grafiki rysuje kod piksel po pikselu (numpy + Pillow), deterministycznie (stałe ziarna), bez filtrów rozmywających.
"""
import numpy as np
from PIL import Image


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


# ---------------------------------------------------------------- paleta
PAL = {
    # woda (jak na filmiku gracza: granat, ciemne kształty pod wodą, turkusowe kręgi, ostra piana)
    'w_abyss': '#091523', 'w_deep': '#0d2135', 'w_mid': '#122c45', 'w_shelf': '#173a57', 'w_shallow': '#1d4966',
    'w_shadow': '#070f1a', 'w_under': '#0a1828', 'w_ring': '#4fb3c9', 'w_ring_hi': '#9fe8f2', 'w_foam': '#d8f6fa',
    # skały (chłodny ciemny kamień z fioletem)
    'r_out': '#141218', 'r_dark': '#231f27', 'r_mid': '#332d36', 'r_lit': '#4a414b', 'r_hi': '#665a63', 'r_moss': '#2c3a2c',
    # trawa (mroczna, stonowana)
    'g_dark': '#16211a', 'g_0': '#1e2d22', 'g_1': '#273a2a', 'g_2': '#324631', 'g_3': '#42573a', 'g_hi': '#566b45',
    # ziemia
    'e_dark': '#1d1514', 'e_0': '#2b201d', 'e_1': '#3b2b25', 'e_2': '#4f3a2f', 'e_hi': '#6a4e3a',
    # metal, drewno
    'm_dark': '#17141b', 'm_0': '#2a2531', 'm_1': '#3e3747', 'm_hi': '#5d5468',
    'wd_0': '#2a1d19', 'wd_1': '#3c2a22', 'wd_2': '#55392b',
    # światło
    'f_core': '#fff1c2', 'f_0': '#ffd27a', 'f_1': '#f2a24a', 'f_2': '#c0602e',
    'glow_cyan': '#7fe3cf', 'glow_pale': '#cfeee6', 'glow_violet': '#b38ad6',
    # mgła (chibi, ciemniejsza obwódka)
    'fog_out': '#1f2430', 'fog_dark': '#4a5163', 'fog_0': '#646d80', 'fog_1': '#7d879a', 'fog_hi': '#9ea7b8',
    # kości, nagrobki
    's_0': '#3a3642', 's_1': '#514b5a', 's_2': '#6b6475', 's_hi': '#8a8293',
    'bone': '#c9bfa8', 'bone_d': '#8f8574',
}
C = {k: hexc(v) for k, v in PAL.items()}

BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32) / 16.0


def bayer(h, w):
    return np.tile(BAYER4, (h // 4 + 1, w // 4 + 1))[:h, :w]


def value_noise(h, w, scale, seed, octaves=3):
    """Gładki szum wartości (0..1), deterministyczny."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        s = max(1.0, scale / (2 ** o))
        gh, gw = int(h / s) + 3, int(w / s) + 3
        grid = rng.random((gh, gw)).astype(np.float32)
        ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
        fy, fx = ys / s, xs / s
        y0, x0 = fy.astype(int), fx.astype(int)
        ty, tx = fy - y0, fx - x0
        ty, tx = ty * ty * (3 - 2 * ty), tx * tx * (3 - 2 * tx)
        a = grid[y0, x0] * (1 - tx) + grid[y0, x0 + 1] * tx
        b = grid[y0 + 1, x0] * (1 - tx) + grid[y0 + 1, x0 + 1] * tx
        out += amp * (a * (1 - ty) + b * ty)
        tot += amp
        amp *= 0.5
    return out / tot


def dist_out(mask, maxd=80):
    """Odległość (w pikselach, 8-sąsiedztwo) od maski dla pikseli poza nią; w masce 0."""
    d = np.full(mask.shape, maxd + 1, np.int32)
    cur = mask.copy()
    d[cur] = 0
    for i in range(1, maxd + 1):
        grown = cur.copy()
        grown[1:, :] |= cur[:-1, :]; grown[:-1, :] |= cur[1:, :]
        grown[:, 1:] |= cur[:, :-1]; grown[:, :-1] |= cur[:, 1:]
        if i % 2 == 0:   # naprzemiennie 4- i 8-sąsiedztwo: kształt bliższy okręgowi
            grown[1:, 1:] |= cur[:-1, :-1]; grown[:-1, :-1] |= cur[1:, 1:]
            grown[1:, :-1] |= cur[:-1, 1:]; grown[:-1, 1:] |= cur[1:, :-1]
        new = grown & ~cur
        d[new] = i
        cur = grown
    return d


def outline(mask):
    """Piksele maski, które sąsiadują (4) z pikselem spoza niej."""
    m = mask
    edge = np.zeros_like(m)
    edge[1:, :] |= m[1:, :] & ~m[:-1, :]; edge[:-1, :] |= m[:-1, :] & ~m[1:, :]
    edge[:, 1:] |= m[:, 1:] & ~m[:, :-1]; edge[:, :-1] |= m[:, :-1] & ~m[:, 1:]
    return edge


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3), np.float32)
        self.a = np.zeros((h, w), np.float32)

    def fill(self, mask, col, alpha=1.0):
        c = C[col] if isinstance(col, str) else col
        if np.isscalar(alpha):
            self.rgb[mask] = c
            self.a[mask] = np.maximum(self.a[mask], alpha)
        else:
            al = alpha[mask][:, None]
            self.rgb[mask] = self.rgb[mask] * (1 - al) + c * al
            self.a[mask] = np.maximum(self.a[mask], alpha[mask])

    def px(self, x, y, col):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.rgb[y, x] = C[col]
            self.a[y, x] = 1.0

    def image(self, scale=1, bg=None):
        rgb = np.clip(self.rgb, 0, 255)
        if bg is not None:
            a = self.a[..., None]
            rgb = rgb * a + bg * (1 - a)
            im = Image.fromarray(rgb.astype(np.uint8), 'RGB')
        else:
            rgba = np.dstack([rgb, self.a * 255]).astype(np.uint8)
            im = Image.fromarray(rgba, 'RGBA')
        return im.resize((self.w * scale, self.h * scale), Image.NEAREST) if scale > 1 else im


def ellipse(h, w, cx, cy, rx, ry):
    ys, xs = np.mgrid[0:h, 0:w]
    return ((xs - cx) / max(rx, 0.5)) ** 2 + ((ys - cy) / max(ry, 0.5)) ** 2 <= 1.0


def shade_blob(cv, cx, cy, rx, ry, ramp, out=None, light=(-0.55, -0.85), clip=None, dither=True):
    """Kształt (elipsa) z cieniowaniem od światła z lewej-góry na kolory ramp (ciemny → jasny) i obwódką out.
    Malowane po kolei od tyłu do przodu daje kępki trawy, głazy, kłęby mgły."""
    x0, x1 = int(cx - rx - 2), int(cx + rx + 3)
    y0, y1 = int(cy - ry - 2), int(cy + ry + 3)
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(cv.w, x1), min(cv.h, y1)
    if x1 <= x0 or y1 <= y0:
        return None
    ys, xs = np.mgrid[y0:y1, x0:x1]
    nx, ny = (xs - cx) / max(rx, 0.5), (ys - cy) / max(ry, 0.5)
    m = nx * nx + ny * ny <= 1.0
    if clip is not None:
        m &= clip[y0:y1, x0:x1]
    if not m.any():
        return None
    v = -(nx * light[0] + ny * light[1])                  # -1 (cień) .. 1 (światło)
    v = (v + 1) / 2
    if dither:
        v = v + (BAYER4[ys % 4, xs % 4] - 0.5) * 0.18
    k = np.clip((v * len(ramp)).astype(int), 0, len(ramp) - 1)
    sub = cv.rgb[y0:y1, x0:x1]
    suba = cv.a[y0:y1, x0:x1]
    for i, name in enumerate(ramp):
        sel = m & (k == i)
        sub[sel] = C[name]
        suba[sel] = 1.0
    if out is not None:
        e = np.zeros_like(m)
        e[1:, :] |= m[1:, :] & ~m[:-1, :]; e[:-1, :] |= m[:-1, :] & ~m[1:, :]
        e[:, 1:] |= m[:, 1:] & ~m[:, :-1]; e[:, :-1] |= m[:, :-1] & ~m[:, 1:]
        sub[e] = C[out]
    full = np.zeros((cv.h, cv.w), bool)
    full[y0:y1, x0:x1] = m
    return full
