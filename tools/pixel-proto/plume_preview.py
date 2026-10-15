"""Podgląd dymu z pieca Żaru i pary znad kotła (GDD §9 p. 124): symulacja jak Visual/Plume.cs (kłęby 3-6 px, rosną,
rzedną ditheringiem, znosi je lekki wiatr, a podmuch odgina smugę). Trzy klatki: spokojnie, deszcz (silniejszy wiatr),
w trakcie podmuchu. Uruchom: python3 plume_preview.py -> docs/img/dym_para.png
"""
import math
import os
import random
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
BAYER = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5]


def puff(size, thin, rgb, alpha):
    d = 3 + size
    im = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    r = d / 2
    for y in range(d):
        for x in range(d):
            dx, dy = x + 0.5 - r, (d - 1 - y) + 0.5 - r
            if dx * dx + dy * dy > r * r + 0.3 or (thin and BAYER[(y & 3) * 4 + (x & 3)] < 7):
                continue
            k = 1.0 if dy > 0.3 else 0.8
            im.putpixel((x, y), tuple(int(c * k) for c in rgb) + (int(255 * alpha),))
    return im


def simulate(kind, breeze, gust_fn, t_end=8.0, dt=1 / 30):
    rnd = random.Random(3)
    puffs, timer, t = [], 0.0, 0.0
    while t < t_end:
        timer -= dt
        if timer <= 0:
            timer = rnd.uniform(0.18, 0.3) if kind == "smoke" else rnd.uniform(0.25, 0.4)
            puffs.append({"x": rnd.uniform(-1, 1) if kind == "smoke" else rnd.uniform(-3, 3), "y": 0.0, "vx": rnd.uniform(-1.5, 1.5),
                          "vy": rnd.uniform(8, 11) if kind == "smoke" else rnd.uniform(6, 9), "age": 0.0,
                          "life": rnd.uniform(3.2, 4.4) if kind == "smoke" else rnd.uniform(1.6, 2.4), "seed": rnd.random() * 6.28})
        for p in puffs:
            if p["age"] >= p["life"]:
                continue
            p["age"] += dt
            u = p["age"] / p["life"]
            side = breeze * (0.4 + u) + abs(gust_fn(t, p["x"])) * 26 + math.sin(p["age"] * 2.3 + p["seed"]) * 1.2
            p["x"] += (p["vx"] + side) * dt
            p["y"] += p["vy"] * (1 - 0.5 * u) * dt
        t += dt
    return [p for p in puffs if p["age"] < p["life"]]


def gust(t, x):
    front = (t - 6.0) * 55.0
    u = (front - x) / 40.0
    return 0.0 if u <= 0 else 0.9 * 2 * math.exp(-1.6 * u) * math.sin(3 * u)


if __name__ == "__main__":
    W, H = 70, 50
    panels = []
    for breeze, g in ((2.5, lambda t, x: 0.0), (4.0, lambda t, x: 0.0), (2.5, gust)):
        im = Image.new("RGBA", (W * 2, H), (32, 52, 44, 255))
        for col, (kind, rgb, a) in enumerate((("smoke", (92, 92, 102), 0.7), ("steam", (209, 219, 224), 0.5))):
            ox, oy = col * W + 18, H - 8
            for p in simulate(kind, breeze, g):
                u = p["age"] / p["life"]
                alpha = a * min(1, (1 - u) * 2.2) * min(1, p["age"] * 6)
                s = puff(min(3, int(u * 4)), u > 0.55, rgb, alpha)
                im.alpha_composite(s, (int(round(ox + p["x"])) - s.width // 2, int(round(oy - p["y"])) - s.height // 2))
        panels.append(im)
    sheet = Image.new("RGBA", (W * 2, H * len(panels)))
    for i, p in enumerate(panels):
        sheet.paste(p, (0, i * H))
    out = os.path.join(ROOT, "docs", "img", "dym_para.png")
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(out)
    print("ok:", out)
