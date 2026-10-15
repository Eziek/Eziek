using System;
using System.Collections.Generic;

namespace Wyspa.World
{
    /// <summary>Ścieżka ryby (ósemka) w pikselach tekstury, y w dół - jak w Water.shader.</summary>
    public struct FishPath
    {
        public float cx, cy, rx, ry, phase;
        public bool glow;
        /// <summary>Gatunek (FishKinds): kształt i ruch w Water.shader.</summary>
        public int kind;
        /// <summary>Mnożnik czasu okrążenia (wolniejsze gatunki &gt; 1).</summary>
        public float slow;
    }

    /// <summary>
    /// Gatunki ryb pod wodą (GDD §9 p. 112), rysowane w Water.shader (FishPixel): rozmiar ramki w pikselach danych wody,
    /// środek ramki (klik w łowisku) i czy da się ją złowić.
    /// </summary>
    public static class FishKinds
    {
        public const int Common = 0, School = 1, Eel = 2, Ray = 3, Carp = 4, Jelly = 5, Angler = 6;
        public const int Count = 7;
        static readonly int[] W = { 10, 12, 16, 14, 15, 7, 10 };
        static readonly int[] H = { 4, 7, 5, 6, 5, 6, 6 };
        static readonly int[] Top = { 0, 0, 0, 0, 0, 0, -1 };   // żabnica: przynęta wystaje nad ramkę

        public static int Width(int kind) => W[Clamp(kind)];
        public static int Height(int kind) => H[Clamp(kind)];
        public static float CenterX(int kind) => W[Clamp(kind)] / 2f;
        public static float CenterY(int kind) => Top[Clamp(kind)] + H[Clamp(kind)] / 2f;
        public static bool Catchable(int kind) => kind != Jelly;
        static int Clamp(int k) => k < 0 || k >= W.Length ? 0 : k;
    }

    /// <summary>Gotowe obrazy wyspy (RGBA, wiersz 0 = DÓŁ, jak Texture2D w Unity) i dane dla wody, mgły i kamery.</summary>
    public class IslandPicture
    {
        public int width, height;
        public byte[] island;      // ląd: kontur, klif, trawa
        public byte[] waterBase;   // statyczna woda (gdy shader nie działa)
        public byte[] data;        // R = odległość od brzegu x4, G = klasa x32 (opis w Water.shader)
        /// <summary>Maska lądu dla grządek (trawa bez 2 px przy brzegu, bez konturu i klifu), RGBA jak island: alfa 255 = ląd.</summary>
        public byte[] landMask;
        public readonly List<FishPath> fish = new List<FishPath>();
        public bool hasBeast;
        public int beastX, beastY;
        /// <summary>Prostokąt lądu z klifem i konturem (piksele tekstury, y w dół, włącznie).</summary>
        public int minX, minY, maxX, maxY;

        /// <summary>Odległość od brzegu w pikselach (y w dół, jak w malarzu), 0 na lądzie i poza obrazem; z mapy danych wody.</summary>
        public float ShoreDistance(int x, int y)
        {
            if (data == null || x < 0 || y < 0 || x >= width || y >= height)
                return 0f;
            return data[((height - 1 - y) * width + x) * 4] / 4f;
        }

        /// <summary>Piksel lądu na obrazie wyspy (trawa, klif, kontur), y w dół.</summary>
        public bool IsLand(int x, int y)
        {
            if (island == null || x < 0 || y < 0 || x >= width || y >= height)
                return false;
            return island[((height - 1 - y) * width + x) * 4 + 3] > 200;
        }

        /// <summary>Otwarta woda pod pikselem (nie ląd, nie klif), y w dół.</summary>
        public bool IsWater(int x, int y) => ShoreDistance(x, y) > 0f && !IsLand(x, y);
    }

    /// <summary>
    /// Maluje wyspę z kafelków jak prototyp (tools/pixel-proto/tiles_proto.py, build): maska z kafelków, zaokrąglenie
    /// (otwarcie i domknięcie), nieregularny brzeg ze stałego szumu, klif, kontur, cień, piana i mapa odległości dla wody i mgły.
    /// Morfologia przez dokładną transformatę odległości (Felzenszwalb), więc koszt nie zależy od promienia.
    /// </summary>
    public static class IslandPainter
    {
        public const int Tile = 16;
        public const int Margin = 64;
        public const int Size = WorldGenerator.Size * Tile + 2 * Margin;   // 640

        // paleta mroczna (GDD §5.3)
        static readonly byte[] O = Hex("0f0a10"), G1 = Hex("21342c"), G2 = Hex("344a3b"), E0 = Hex("20171c"),
            E1 = Hex("352629"), E2 = Hex("523b35"), P0 = Hex("1c1520"), P1 = Hex("33263a"),
            B0 = Hex("121c27"), B1 = Hex("1f3044"), B2 = Hex("30485f"), FO = Hex("848e9b"),
            G0 = Hex("141f1c"), B3 = Hex("56697a");
        static readonly byte[][] Cliff = { G1, E2, E2, E2, E1, E1, P1, P1, P0 };
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        static byte[] Hex(string h) => new[] { Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16) };

        /// <summary>Lewy górny róg kafelka w pikselach tekstury (y w dół).</summary>
        public static (int x, int y) TileOrigin(TilePos p) => (Margin + p.c * Tile, Margin + p.r * Tile);

        /// <summary>
        /// Maluje wyspę. rising + grow: świeżo kupiony kafelek wysuwa się z lądu - wypełnione jest tylko grow pikseli
        /// od strony sąsiedniego kafelka (animacja wynurzania: kilka klatek z rosnącym grow, GDD §13.5).
        /// </summary>
        public static IslandPicture Paint(IslandMap map, TilePos? rising = null, int grow = Tile)
        {
            int W = Size, H = Size, seed = map.Seed;
            var pic = new IslandPicture { width = W, height = H };

            // --- region pracy: kafelki + zapas na zaokrąglenie, klif, kontur i pianę ---
            int rx0 = int.MaxValue, ry0 = int.MaxValue, rx1 = int.MinValue, ry1 = int.MinValue;
            var painted = map.PaintedTiles();   // własny ląd i widoczne wysepki (§13.10)
            foreach (var p in painted)
            {
                var (x, y) = TileOrigin(p);
                rx0 = Math.Min(rx0, x); ry0 = Math.Min(ry0, y);
                rx1 = Math.Max(rx1, x + Tile - 1); ry1 = Math.Max(ry1, y + Tile - 1);
            }
            if (rx0 == int.MaxValue) { rx0 = ry0 = W / 2; rx1 = ry1 = W / 2; }
            rx0 = Math.Max(0, rx0 - 24); ry0 = Math.Max(0, ry0 - 24);
            rx1 = Math.Min(W - 1, rx1 + 24); ry1 = Math.Min(H - 1, ry1 + 24);
            var R = new Region(rx0, ry0, rx1 - rx0 + 1, ry1 - ry0 + 1);

            // --- maska z kafelków ---
            var m = new bool[R.w * R.h];
            foreach (var p in painted)
            {
                var (x0, y0) = TileOrigin(p);
                int ax = x0, ay = y0, bx = x0 + Tile, by = y0 + Tile;
                if (rising.HasValue && rising.Value == p && grow < Tile)
                {
                    int g = Math.Max(0, grow);
                    // rośnie od pierwszego posiadanego sąsiada (lewo, prawo, góra, dół)
                    if (map.Owns(new TilePos(p.c - 1, p.r))) bx = x0 + g;
                    else if (map.Owns(new TilePos(p.c + 1, p.r))) ax = x0 + Tile - g;
                    else if (map.Owns(new TilePos(p.c, p.r - 1))) by = y0 + g;
                    else ay = y0 + Tile - g;
                }
                for (int y = ay; y < by; y++)
                    for (int x = ax; x < bx; x++)
                        m[R.I(x, y)] = true;
            }
            m = Dilate(Erode(m, R, 6), R, 6);   // zaokrąglenie wypukłych narożników
            m = Erode(Dilate(m, R, 5), R, 5);   // wypełnienie wklęsłych

            // --- nieregularny brzeg: gładki szum przypięty do miejsca w świecie ---
            var outer = Dilate(m, R, 3);
            var inner = Erode(m, R, 3);
            var m2 = new bool[m.Length];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                {
                    int i = R.I(x, y);
                    double n = Noise(x, y, seed);
                    bool edgeOut = outer[i] && !m[i];
                    bool edgeIn = m[i] && !inner[i];
                    m2[i] = (m[i] || (edgeOut && n > 0.62)) && !(edgeIn && n < 0.3);
                }
            var land = Erode(Dilate(m2, R, 1), R, 1);
            bool Land(int x, int y) => R.Contains(x, y) && land[R.I(x, y)];
            // grządki kończą się 2 px przed brzegiem: od strony wody zostaje pasek trawy
            var plotLand = Erode(land, R, 2);
            pic.landMask = new byte[W * H * 4];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                    if (plotLand[R.I(x, y)])
                    {
                        int i = ((H - 1 - y) * W + x) * 4;
                        pic.landMask[i] = pic.landMask[i + 1] = pic.landMask[i + 2] = pic.landMask[i + 3] = 255;
                    }

            var island = new byte[W * H * 4];
            void Put(byte[] img, int x, int y, byte[] c)
            {
                int i = ((H - 1 - y) * W + x) * 4;
                img[i] = c[0]; img[i + 1] = c[1]; img[i + 2] = c[2]; img[i + 3] = 255;
            }

            // --- klif pod dolnymi krawędziami lądu ---
            var solid = new bool[R.w * R.h];
            var cliffCol = new byte[R.w * R.h][];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                {
                    if (!Land(x, y))
                        continue;
                    solid[R.I(x, y)] = true;
                    if (Land(x, y + 1))
                        continue;
                    for (int d = 1; d <= Cliff.Length; d++)
                    {
                        if (!R.Contains(x, y + d) || Land(x, y + d))
                            break;
                        var k = Cliff[d - 1];
                        if (d == 2 && WHash.Value(x, y + d, seed * 13 + 5) < 0.3) k = G1;
                        if (d >= 3 && d <= 7 && WHash.Value(x, y + d, seed * 13 + 6) < 0.08) k = E0;
                        int ci = R.I(x, y + d);
                        if (cliffCol[ci] == null)
                            cliffCol[ci] = k;
                        solid[ci] = true;
                    }
                }

            // --- kontur, cień klifu, dwa pierścienie piany ---
            bool Solid(int x, int y) => R.Contains(x, y) && solid[R.I(x, y)];
            var outline = new bool[R.w * R.h];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                    if (!Solid(x, y) && (Solid(x + 1, y) || Solid(x - 1, y) || Solid(x, y + 1) || Solid(x, y - 1)))
                        outline[R.I(x, y)] = true;
            bool Outline(int x, int y) => R.Contains(x, y) && outline[R.I(x, y)];
            bool Blocked(int x, int y) => Solid(x, y) || Outline(x, y);
            var cls = new byte[W * H];          // klasa wody, 0 = ląd / zablokowane
            var blocked = new bool[W * H];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                    if (Blocked(x, y))
                        blocked[y * W + x] = true;
            var ring1 = new bool[R.w * R.h];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                    if (!Blocked(x, y) && (Outline(x + 1, y) || Outline(x - 1, y) || Outline(x, y + 1) || Outline(x, y - 1)))
                        ring1[R.I(x, y)] = true;
            bool Ring1(int x, int y) => R.Contains(x, y) && ring1[R.I(x, y)];

            // --- odległość od brzegu (chamfer 3-4, jak water.py) ---
            var dist = Chamfer(blocked, W, H);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            var waterBase = new byte[W * H * 4];
            var data = new byte[W * H * 4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float d = dist[i];
                    int di = ((H - 1 - y) * W + x) * 4;
                    data[di + 3] = 255;
                    if (blocked[i])
                    {
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                        continue;
                    }
                    // głębia z ditheringiem Bayera
                    double th = Bayer[(y & 3) * 4 + (x & 3)] / 16.0;
                    byte c;
                    if (d < 7) c = 1;
                    else if (d < 10) c = th < (10 - d) / 3.0 ? (byte)1 : (byte)2;
                    else if (d < 22) c = 2;
                    else if (d < 32) c = th < (32 - d) / 10.0 ? (byte)2 : (byte)3;
                    else if (d < 42) c = 3;
                    else c = th < Math.Max(0.35, (52 - d) / 10.0) ? (byte)3 : (byte)4;
                    bool inR = R.Contains(x, y);
                    bool isRing1 = inR && ring1[R.I(x, y)];
                    bool isRing2 = inR && !isRing1 && (Ring1(x + 1, y) || Ring1(x - 1, y) || Ring1(x, y + 1) || Ring1(x, y - 1));
                    bool isShadow = inR && (Outline(x, y - 1) || Outline(x, y - 2) || Outline(x, y - 3));
                    byte k = c;
                    if (isShadow) k = 5;
                    if (isRing1) k = 6;
                    if (isRing2) k = 7;
                    cls[i] = k;
                    data[di] = (byte)Math.Min(255, (int)Math.Round(d * 4));
                    data[di + 1] = (byte)(k * 32);
                    var col = k == 1 ? B2 : k == 2 ? B1 : k == 3 ? B0 : k == 4 ? P0 : k == 5 ? B0 : k == 6 ? FO : B2;
                    Put(waterBase, x, y, col);
                }

            // --- ląd: kontur, klif, trawa z kępkami ---
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                {
                    int i = R.I(x, y);
                    if (outline[i]) Put(island, x, y, O);
                    else if (cliffCol[i] != null && !land[i]) Put(island, x, y, cliffCol[i]);
                    else if (land[i]) Put(island, x, y, Land(x, y + 1) ? Surface(map, x, y) : G1);
                }
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                {
                    if (!Land(x, y) || WHash.Value(x, y, seed * 17 + 3) >= 1.0 / 60.0 || ZoneOfPixel(map, x, y) != ZoneKind.None)
                        continue;   // kępki trawy tylko poza strefami
                    var pts = new[] { (x, y), (x + 1, y - 1), (x + 2, y), (x + 3, y - 1), (x + 4, y) };
                    bool ok = true;
                    foreach (var (px, py) in pts)
                        if (!Land(px, py + 3) || !Land(px, py)) ok = false;
                    if (ok)
                        foreach (var (px, py) in pts)
                            Put(island, px, py, G1);
                }

            pic.island = island;
            pic.waterBase = waterBase;
            pic.data = data;
            pic.minX = minX; pic.minY = minY; pic.maxX = maxX; pic.maxY = maxY;
            PlaceFish(pic, dist, seed);
            PlaceBeast(pic, dist);
            return pic;
        }

        // ------------------------------------------------------------ podłoża stref (port sprites4.py: ground)

        /// <summary>Strefa pod pikselem; granice rozmyte szumem, żeby nie było widać kwadratów kafelków (§13.8).</summary>
        static ZoneKind ZoneOfPixel(IslandMap map, int x, int y)
        {
            if (map.Plan == null || map.Plan.zones.Count == 0)
                return ZoneKind.None;
            double jx = (VNoise(x, y, 4, 21) - 0.5) * 9, jy = (VNoise(x, y, 4, 22) - 0.5) * 9;
            var t = PixelTile(x + jx, y + jy);
            if (!map.Owns(t))
                t = PixelTile(x, y);
            return map.Owns(t) ? map.Plan.ZoneAt(t) : ZoneKind.None;
        }

        static TilePos PixelTile(double x, double y) =>
            new TilePos((int)Math.Floor((x - Margin) / Tile), (int)Math.Floor((y - Margin) / Tile));

        static byte[] Surface(IslandMap map, int x, int y)
        {
            switch (ZoneOfPixel(map, x, y))
            {
                case ZoneKind.Cemetery:
                {
                    double n = VNoise(x, y, 7, 1);
                    var k = n > 0.68 ? E1 : (n < 0.25 ? G0 : G1);
                    double r = H(x, y, 2);
                    if (r > 0.985) k = P1;                        // kamyki
                    else if (r > 0.978 && k == E1) k = B3;        // kosteczki
                    return k;
                }
                case ZoneKind.Swamp:
                {
                    double n = VNoise(x, y, 6, 3);
                    if (n > 0.66)
                    {
                        if (n < 0.69) return E0;                  // brzeg błota
                        return VNoise(x, y, 3, 4) < 0.7 ? B1 : B2; // kałuże
                    }
                    if (H(x, y, 5) > 0.97) return E1;
                    return n > 0.35 ? G1 : G0;
                }
                case ZoneKind.Ruins:
                {
                    int row = y / 6;
                    int off = row % 2 == 1 ? 3 : 0;
                    int bx = (x + off) / 7, ex = (x + off) % 7, ey = y % 6;
                    if (VNoise(x, y, 8, 7) > 0.72)
                        return H(x, y, 8) > 0.3 ? G1 : G2;         // mech na płytach
                    if (ex == 0 || ey == 0)
                        return P0;                                 // fugi
                    var k = H(bx, row, 9) < 0.5 ? B1 : P1;
                    if (ex == 1 && ey == 1) k = B3;
                    if (H(x, y, 10) > 0.97) k = P0;                // pęknięcia
                    return k;
                }
                default:
                    return G2;
            }
        }

        /// <summary>Hash 0..1 jak h() w sprites4.py (stały dla miejsca w świecie).</summary>
        static double H(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177u;
                return (n ^ (n >> 16)) / 4294967295.0;
            }
        }

        static double VNoise(double x, double y, int cell, int s)
        {
            double gx = x / cell, gy = y / cell;
            int x0 = (int)Math.Floor(gx), y0 = (int)Math.Floor(gy);
            double fx = gx - x0, fy = gy - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            double a = H(x0, y0, s), b = H(x0 + 1, y0, s), c = H(x0, y0 + 1, s), d = H(x0 + 1, y0 + 1, s);
            return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
        }

        // ------------------------------------------------------------ ryby i potwór (jak water.py, w pobliżu wyspy)

        // kolejność gatunków przy rozmieszczaniu: zwykłe najczęściej, rzadsze dalej (nie zmieszczą się, gdy wody mało)
        static readonly int[] FishOrder =
        {
            FishKinds.Common, FishKinds.School, FishKinds.Common, FishKinds.Eel, FishKinds.Carp, FishKinds.Common,
            FishKinds.Ray, FishKinds.School, FishKinds.Jelly, FishKinds.Common, FishKinds.Angler, FishKinds.Eel,
            FishKinds.Common, FishKinds.Jelly, FishKinds.Carp, FishKinds.School,
        };

        /// <summary>
        /// Ryby w pobliżu wyspy (do 16, różne gatunki): ścieżka ósemka, cała w wodzie z zapasem zależnym od wielkości ryby;
        /// duże (płaszczka, karp, żabnica, meduza) tylko w głębszej wodzie. Świecą: co trzecia zwykła i żabnica (przynęta).
        /// </summary>
        static void PlaceFish(IslandPicture pic, float[] dist, int seed)
        {
            int W = pic.width, H = pic.height;
            int x0 = Math.Max(10, pic.minX - 80), x1 = Math.Min(W - 10, pic.maxX + 80);
            int y0 = Math.Max(10, pic.minY - 60), y1 = Math.Min(H - 6, pic.maxY + 60);
            var rnd = new WRandom(seed * 101L + 3);
            float D(double x, double y) => dist[Math.Min(H - 1, Math.Max(0, (int)y)) * W + Math.Min(W - 1, Math.Max(0, (int)x))];
            int commons = 0;
            foreach (int kind in FishOrder)
            {
                double rxMin = 8, rxMax = 16, ryMin = 2, ryMax = 5, slow = 1, minD = 7;
                switch (kind)
                {
                    case FishKinds.School: rxMin = 10; rxMax = 18; break;
                    case FishKinds.Eel: rxMin = 12; rxMax = 20; ryMax = 4; slow = 1.2; break;
                    case FishKinds.Ray: rxMin = 14; rxMax = 22; ryMin = 3; ryMax = 6; slow = 1.7; minD = 12; break;
                    case FishKinds.Carp: rxMin = 10; rxMax = 16; ryMax = 4; slow = 1.4; minD = 11; break;
                    case FishKinds.Jelly: rxMin = 2; rxMax = 4; ryMin = 3; ryMax = 6; slow = 2.6; minD = 14; break;
                    case FishKinds.Angler: rxMin = 8; rxMax = 12; ryMax = 3; slow = 1.3; minD = 16; break;
                }
                float hw = FishKinds.CenterX(kind), hh = FishKinds.CenterY(kind);
                double margin = 4 + Math.Max(FishKinds.Width(kind), FishKinds.Height(kind)) / 3.0;
                for (int tries = 0; tries < 160; tries++)
                {
                    int cx = rnd.Range(x0, x1), cy = rnd.Range(y0, y1);
                    double frx = rnd.Range(rxMin, rxMax), fry = rnd.Range(ryMin, ryMax), ph = rnd.Range(0.0, 2 * Math.PI);
                    if (D(cx + hw, cy + hh) < minD)
                        continue;
                    bool ok = true;
                    for (int k = 0; k < 16 && ok; k++)
                    {
                        double a = k * Math.PI / 8;
                        if (D(cx + hw + frx * Math.Sin(a), cy + hh + fry * Math.Sin(2 * a)) < margin) ok = false;
                    }
                    if (!ok)
                        continue;
                    bool glow = kind == FishKinds.Angler || (kind == FishKinds.Common && commons++ % 3 == 1);
                    pic.fish.Add(new FishPath { cx = cx, cy = cy, rx = (float)frx, ry = (float)fry, phase = (float)ph, glow = glow,
                                                kind = kind, slow = (float)slow });
                    break;
                }
            }
        }

        static void PlaceBeast(IslandPicture pic, float[] dist)
        {
            int W = pic.width, H = pic.height;
            int x0 = Math.Max(24, pic.minX - 20), x1 = Math.Min(W - 24, pic.maxX + 20);
            int y0 = Math.Min(H - 9, pic.maxY + 12), y1 = Math.Min(H - 8, pic.maxY + 80);
            float best = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (dist[y * W + x] > best)
                    {
                        best = dist[y * W + x];
                        pic.beastX = x;
                        pic.beastY = y;
                    }
            pic.hasBeast = best >= 24;
        }

        // ------------------------------------------------------------ narzędzia

        static double Noise(int x, int y, int seed)
        {
            double gx = x / 8.0, gy = y / 8.0;
            int ix = (int)Math.Floor(gx), iy = (int)Math.Floor(gy);
            double fx = gx - ix, fy = gy - iy;
            int s = seed * 7 + 1;
            return WHash.Value(ix, iy, s) * (1 - fx) * (1 - fy) + WHash.Value(ix + 1, iy, s) * fx * (1 - fy)
                 + WHash.Value(ix, iy + 1, s) * (1 - fx) * fy + WHash.Value(ix + 1, iy + 1, s) * fx * fy;
        }

        readonly struct Region
        {
            public readonly int x, y, w, h;
            public Region(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
            public bool Contains(int px, int py) => px >= x && py >= y && px < x + w && py < y + h;
            public int I(int px, int py) => (py - y) * w + (px - x);
        }

        // dysk jak w prototypie: dx² + dy² <= r² + r
        static bool[] Dilate(bool[] m, Region R, int r)
        {
            var d2 = Edt(m, R.w, R.h, true);
            var o = new bool[m.Length];
            float lim = r * r + r;
            for (int i = 0; i < m.Length; i++) o[i] = d2[i] <= lim;
            return o;
        }

        static bool[] Erode(bool[] m, Region R, int r)
        {
            var d2 = Edt(m, R.w, R.h, false);
            var o = new bool[m.Length];
            float lim = r * r + r;
            for (int i = 0; i < m.Length; i++) o[i] = d2[i] > lim;
            return o;
        }

        /// <summary>Kwadrat odległości euklidesowej do najbliższego piksela o wartości target (Felzenszwalb i Huttenlocher).</summary>
        static float[] Edt(bool[] m, int w, int h, bool target)
        {
            const float Inf = 1e20f;
            var g = new float[w * h];
            for (int i = 0; i < g.Length; i++) g[i] = m[i] == target ? 0 : Inf;
            int n = Math.Max(w, h);
            var f = new float[n];
            var d = new float[n];
            var v = new int[n];
            var z = new float[n + 1];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) f[y] = g[y * w + x];
                Edt1D(f, h, d, v, z);
                for (int y = 0; y < h; y++) g[y * w + x] = d[y];
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) f[x] = g[y * w + x];
                Edt1D(f, w, d, v, z);
                for (int x = 0; x < w; x++) g[y * w + x] = d[x];
            }
            return g;
        }

        static void Edt1D(float[] f, int n, float[] d, int[] v, float[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
            }
        }

        /// <summary>Odległość chamfer 3-4 od zablokowanych pikseli, podzielona przez 3 (jak distance_field w water.py).</summary>
        static float[] Chamfer(bool[] blocked, int W, int H)
        {
            const int Inf = 1 << 28;
            var d = new int[W * H];
            for (int i = 0; i < d.Length; i++) d[i] = blocked[i] ? 0 : Inf;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x, v = d[i];
                    if (x > 0) v = Math.Min(v, d[i - 1] + 3);
                    if (y > 0)
                    {
                        v = Math.Min(v, d[i - W] + 3);
                        if (x > 0) v = Math.Min(v, d[i - W - 1] + 4);
                        if (x < W - 1) v = Math.Min(v, d[i - W + 1] + 4);
                    }
                    d[i] = v;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x, v = d[i];
                    if (x < W - 1) v = Math.Min(v, d[i + 1] + 3);
                    if (y < H - 1)
                    {
                        v = Math.Min(v, d[i + W] + 3);
                        if (x < W - 1) v = Math.Min(v, d[i + W + 1] + 4);
                        if (x > 0) v = Math.Min(v, d[i + W - 1] + 4);
                    }
                    d[i] = v;
                }
            var o = new float[d.Length];
            for (int i = 0; i < d.Length; i++) o[i] = d[i] / 3f;
            return o;
        }
    }
}
