"""Warstwowa woda: głębokość -> ryby pod wodą -> powierzchnia (grzbiety fal, połyski) -> przybój i piana."""
import math, random

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
DARKER = {'b2': 'b0', 'b1': 'o', 'b0': 'o', 'p0': 'o'}

FISH_A = ["....##....", "#..######.", ".#########", "#..######."]
FISH_B = ["....##....", ".#.######.", "#.########", ".#.######."]


def distance_field(W, H, blocked):
    INF = 10 ** 6
    d = [[0 if (x, y) in blocked else INF for x in range(W)] for y in range(H)]
    for y in range(H):                      # chamfer 3-4, dwa przebiegi
        for x in range(W):
            v = d[y][x]
            if x > 0: v = min(v, d[y][x - 1] + 3)
            if y > 0:
                v = min(v, d[y - 1][x] + 3)
                if x > 0: v = min(v, d[y - 1][x - 1] + 4)
                if x < W - 1: v = min(v, d[y - 1][x + 1] + 4)
            d[y][x] = v
    for y in range(H - 1, -1, -1):
        for x in range(W - 1, -1, -1):
            v = d[y][x]
            if x < W - 1: v = min(v, d[y][x + 1] + 3)
            if y < H - 1:
                v = min(v, d[y + 1][x] + 3)
                if x < W - 1: v = min(v, d[y + 1][x + 1] + 4)
                if x > 0: v = min(v, d[y + 1][x - 1] + 4)
            d[y][x] = v
    return [[v / 3 for v in row] for row in d]


class Water:
    def __init__(self, W, H, blocked, shadow, ring1, ring2, frames, seed=11):
        self.W, self.H, self.F = W, H, frames
        self.blocked, self.shadow, self.ring1, self.ring2 = blocked, shadow, ring1, ring2
        self.dist = distance_field(W, H, blocked)
        rnd = random.Random(seed)
        # warstwa 1: głębokość (statyczna)
        self.base = {}
        for y in range(H):
            for x in range(W):
                if (x, y) in blocked: continue
                d, th = self.dist[y][x], BAYER[y % 4][x % 4] / 16
                if d < 7: k = 'b2'
                elif d < 10: k = 'b2' if th < (10 - d) / 3 else 'b1'
                elif d < 22: k = 'b1'
                elif d < 32: k = 'b1' if th < (32 - d) / 10 else 'b0'
                elif d < 42: k = 'b0'
                else: k = 'b0' if th < max(0.35, (52 - d) / 10) else 'p0'
                self.base[(x, y)] = k
        # warstwa 2: ryby (ścieżki-ósemki zapętlone w 'frames' klatkach)
        self.fish = []
        tries = 0
        while len(self.fish) < 12 and tries < 2000:
            tries += 1
            cx, cy = rnd.randrange(10, W - 10), rnd.randrange(10, H - 6)
            if self.dist[cy][cx] < 7: continue
            rx, ry = rnd.uniform(8, 16), rnd.uniform(2, 5)
            ok = all(self.dist[int(min(H - 1, max(0, cy + ry * math.sin(2 * a))))][
                         int(min(W - 1, max(0, cx + rx * math.sin(a))))] >= 5
                     for a in [i * math.pi / 8 for i in range(16)])
            if ok:
                self.fish.append((cx, cy, rx, ry, rnd.uniform(0, 2 * math.pi), len(self.fish) in (1, 4, 7)))
        # coś dużego w głębinie
        far = max(((x, y) for y in range(int(H * 0.6), H - 8) for x in range(24, W - 24)), key=lambda p: self.dist[p[1]][p[0]])
        self.beast = far
        # warstwa 3: grzbiety fal
        self.crests = []
        while len(self.crests) < 24:
            x, y = rnd.randrange(W), rnd.randrange(H)
            if self.dist[y][x] > 12:
                self.crests.append((x, y, rnd.randrange(frames)))
        self.glints = [(rnd.randrange(W), rnd.randrange(H), rnd.randrange(frames)) for _ in range(40)]
        self.emit = []

    def _water(self, x, y):
        return 0 <= x < self.W and 0 <= y < self.H and (x, y) in self.base

    def draw(self, put, f):
        W, H, F = self.W, self.H, self.F
        cur = dict(self.base)
        self.emit = []
        # --- ryby (pod powierzchnią: ciemniejszy odcień wody pod spodem) ---
        def shade(x, y):
            if (x, y) in cur and (x, y) not in self.shadow:
                cur[(x, y)] = DARKER.get(self.base[(x, y)], cur[(x, y)])
        for i, (cx, cy, rx, ry, ph, glow) in enumerate(self.fish):
            a = 2 * math.pi * f / F + ph
            x = round(cx + rx * math.sin(a)); y = round(cy + ry * math.sin(2 * a))
            right = math.cos(a) > 0
            shape = FISH_A if (f + i) % 4 < 2 else FISH_B
            for j, row in enumerate(shape):
                for k, c in enumerate(row):
                    if c == '#':
                        shade(x + (k if right else len(row) - 1 - k), y + j)
            if glow:
                hx = x + (8 if right else 1)
                if self._water(hx, y + 2): self.emit.append((hx, y + 2))
        # potwór: długie, falujące cielsko głęboko pod wodą
        bx, by = self.beast
        a = 2 * math.pi * f / F
        ox, oy = round(bx + 7 * math.cos(a)), round(by + 2 * math.sin(a))
        for s in range(40):
            t = s / 39
            th = 4.2 * math.sin(math.pi * min(1, t * 1.6)) * (1 - t * 0.6)
            yy = oy + 2.5 * math.sin(s * 0.35 - f * 2 * math.pi / F * 2)
            for dy in range(-int(th), int(th) + 1):
                p = (ox - 20 + s, round(yy) + dy)
                if p in cur and self.base[p] in ('b0', 'p0', 'b1'):
                    cur[p] = 'o'
        if (f // 3) % 8 == 0:   # błysk oka
            eye = (ox - 17, round(oy + 2.5 * math.sin(-f * 2 * math.pi / F * 2)) - 1)
            if eye in cur: self.emit.append(eye)
        # --- powierzchnia: połyski ---
        for x, y, ph in self.glints:
            t = (f + ph) % F
            if t < 10:
                xx = (x - t // 3) % W
                for i in range(2 if t < 6 else 1):
                    if (xx + i, y) in cur and self.dist[y][min(W - 1, xx + i)] > 9:
                        cur[(xx + i, y)] = 'b2' if self.base[(xx + i, y)] != 'b2' else 'b3'
        # --- grzbiety fal (cykl: narasta -> szczyt -> łamie się w pianę) ---
        for x0, y0, ph in self.crests:
            t = (f + ph) % F
            if not 6 <= t < 42: continue
            x = x0 - t // 3
            if t < 16: rows = [("", ''), ("..####..", 'b2')]
            elif t < 30: rows = [("..####..", 'b3'), (".#....#.", 'b2')]
            else: rows = [(".#.#..#.", 'fo'), ("..####..", 'b3')]
            for j, (row, k) in enumerate(rows):
                for i, c in enumerate(row):
                    p = ((x + i) % W, y0 + j)
                    if c == '#' and p in cur and self.dist[p[1]][p[0]] > 9:
                        cur[p] = k
        # --- przybój: fale napływają do brzegu ---
        for off in (0, F // 2):
            k = 9 - ((f + off) % (F // 2)) / (F // 2) * 7
            for p in cur:
                d = self.dist[p[1]][p[0]]
                if k <= d < k + 1 and (p[0] + p[1]) % 2 == 0:
                    cur[p] = 'b3'
        # cień klifu i piana
        for p in self.shadow:
            if p in cur: cur[p] = 'b0'
        a2, b2 = ('fo', 'b2') if (f // 5) % 2 == 0 else ('b3', 'fo')
        for p in self.ring1:
            if p in cur: cur[p] = a2
        for p in self.ring2:
            if p in cur: cur[p] = b2
        for p, k in cur.items():
            put(*p, k)
