using System;
using System.Collections.Generic;

namespace Wyspa.World
{
    /// <summary>Ścieżka ryby (ósemka) w pikselach tekstury, y w dół - jak w Water.shader.</summary>
    public struct FishPath
    {
        public float cx, cy, rx, ry, phase;
        public bool glow;
    }

    /// <summary>Gotowe obrazy wyspy (RGBA, wiersz 0 = DÓŁ, jak Texture2D w Unity) i dane dla wody, mgły i kamery.</summary>
    public class IslandPicture
    {
        public int width, height;
        public byte[] island;      // ląd: kontur, klif, trawa
        public byte[] islandHi;    // Etap 7: ląd w 32 px na kafelek (HiScale x rozdzielczość), szczegółowy
        public byte[] waterBase;   // statyczna woda (gdy shader nie działa)
        public byte[] data;        // R = odległość od brzegu x4, G = klasa x32 (opis w Water.shader)
        /// <summary>Maska lądu dla grządek (trawa bez 2 px przy brzegu, bez konturu i klifu), RGBA jak island: alfa 255 = ląd.</summary>
        public byte[] landMask;
        public readonly List<FishPath> fish = new List<FishPath>();
        public bool hasBeast;
        public int beastX, beastY;
        /// <summary>Prostokąt lądu z klifem i konturem (piksele tekstury, y w dół, włącznie).</summary>
        public int minX, minY, maxX, maxY;
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
            G0 = Hex("141f1c"), B3 = Hex("56697a"),
            G3 = Hex("566d52"), E3 = Hex("715444"), CR = Hex("d8ccab"), GO = Hex("c49a55"), ML = Hex("8f6aa8");
        /// <summary>Etap 7: teren rysowany w 32 px na kafelek (dwa razy gęściej niż siatka logiki i danych wody).</summary>
        public const int HiScale = 2;
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
            foreach (var p in map.Tiles.Keys)
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
            foreach (var p in map.Tiles.Keys)
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
            pic.islandHi = PaintHi(map, R, land, cliffCol, outline, W, H, seed);
            pic.waterBase = waterBase;
            pic.data = data;
            pic.minX = minX; pic.minY = minY; pic.maxX = maxX; pic.maxY = maxY;
            PlaceFish(pic, dist, seed);
            PlaceBeast(pic, dist);
            return pic;
        }

        // ------------------------------------------------------------ teren 32 px (Etap 7, styl zaakceptowany na próbce)

        /// <summary>
        /// Ląd w podwójnej rozdzielczości: brzeg wygładzony Scale2x (bez schodków 2x2), trawa w płatach z kępkami i kwiatkami,
        /// klif z warstwami ziemi, kamieniami i korzeniami, cienki kontur. Siatka logiki (maska, klif, kontur) zostaje 16 px,
        /// więc woda, grządki i obiekty pasują do niego bez zmian.
        /// </summary>
        static byte[] PaintHi(IslandMap map, Region R, bool[] land, byte[][] cliffCol, bool[] outline, int width, int height, int seed)
        {
            int S = HiScale, W2 = width * S, H2 = height * S;
            var img = new byte[W2 * H2 * 4];
            int X0 = R.x * S, Y0 = R.y * S, X1 = (R.x + R.w) * S, Y1 = (R.y + R.h) * S;
            bool L(int x, int y) => R.Contains(x, y) && land[R.I(x, y)];
            bool CliffCell(int x, int y) => R.Contains(x, y) && cliffCol[R.I(x, y)] != null && !land[R.I(x, y)];
            bool OutlineCell(int x, int y) => R.Contains(x, y) && outline[R.I(x, y)];

            // ląd 2x z wygładzeniem skosów (EPX na masce)
            var land2 = new bool[(X1 - X0) * (Y1 - Y0)];
            int I2(int x, int y) => (y - Y0) * (X1 - X0) + (x - X0);
            bool In2(int x, int y) => x >= X0 && y >= Y0 && x < X1 && y < Y1;
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                {
                    bool p = L(x, y), a = L(x, y - 1), b = L(x + 1, y), c = L(x - 1, y), d = L(x, y + 1);
                    bool e0 = c == a && c != d && a != b ? a : p;
                    bool e1 = a == b && a != c && b != d ? b : p;
                    bool e2 = d == c && d != b && c != a ? c : p;
                    bool e3 = b == d && b != a && d != c ? d : p;
                    // EPX nie dokłada lądu na komórkach klifu (klif ma zostać pod lądem)
                    int X = x * S, Y = y * S;
                    land2[I2(X, Y)] = e0 && !CliffCell(x, y);
                    land2[I2(X + 1, Y)] = e1 && !CliffCell(x, y);
                    land2[I2(X, Y + 1)] = e2 && !CliffCell(x, y);
                    land2[I2(X + 1, Y + 1)] = e3 && !CliffCell(x, y);
                }
            bool L2(int x, int y) => In2(x, y) && land2[I2(x, y)];
            // strefa i jej podłoże raz na komórkę logiki (zamiast na każdy piksel 32 px - malowanie jest wtedy kilka razy szybsze)
            var zoneOf = new ZoneKind[R.w * R.h];
            var zoneCol = new byte[R.w * R.h][];
            for (int y = R.y; y < R.y + R.h; y++)
                for (int x = R.x; x < R.x + R.w; x++)
                    if (land[R.I(x, y)])
                    {
                        var z = ZoneOfPixel(map, x, y);
                        zoneOf[R.I(x, y)] = z;
                        if (z != ZoneKind.None)
                            zoneCol[R.I(x, y)] = Surface(map, x, y);
                    }
            ZoneKind Zone(int x, int y) => R.Contains(x, y) ? zoneOf[R.I(x, y)] : ZoneKind.None;
            bool C2(int x, int y) => In2(x, y) && !L2(x, y) && CliffCell(x / S, y / S);
            void Put(int x, int y, byte[] c)
            {
                int i = ((H2 - 1 - y) * W2 + x) * 4;
                img[i] = c[0]; img[i + 1] = c[1]; img[i + 2] = c[2]; img[i + 3] = 255;
            }

            // trawa (podłoża stref z siatki logiki, z drobnym ditheringiem)
            for (int y = Y0; y < Y1; y++)
                for (int x = X0; x < X1; x++)
                {
                    if (!L2(x, y))
                        continue;
                    int lx = x / S, ly = y / S;
                    byte[] k;
                    if (R.Contains(lx, ly) && zoneCol[R.I(lx, ly)] != null)
                        k = zoneCol[R.I(lx, ly)];
                    else
                    {
                        double n = VNoise(x, y, 10, seed * 3 + 51) + (Bayer[(y & 3) * 4 + (x & 3)] / 16.0 - 0.5) * 0.12;
                        k = n < 0.38 ? G1 : n > 0.66 ? G3 : G2;
                    }
                    bool edge = !L2(x + 1, y) || !L2(x - 1, y) || !L2(x, y - 1) || !L2(x, y + 1);
                    if (edge && !C2(x, y + 1))
                        k = G1;   // ciemniejszy rąbek trawy przy wodzie
                    Put(x, y, k);
                }
            // kępki trawy i kwiatki (poza strefami)
            for (int y = Y0 + 2; y < Y1 - 3; y++)
                for (int x = X0 + 2; x < X1 - 2; x++)
                {
                    if (!L2(x, y) || !L2(x, y + 3) || !L2(x - 2, y) || !L2(x + 2, y) || Zone(x / S, y / S) != ZoneKind.None)
                        continue;
                    double h = H(x, y, seed * 5 + 53);
                    if (h < 1.0 / 70.0)
                    {
                        Put(x, y, G3); Put(x - 1, y + 1, G3); Put(x + 1, y + 1, G3);
                        Put(x, y + 1, G1); Put(x - 1, y + 2, G1); Put(x + 1, y + 2, G1);
                        if (h < 1.0 / 180.0) { Put(x, y - 1, G3); Put(x + 2, y, G2); }
                    }
                    else if (h > 1.0 - 1.0 / 600.0)
                    {
                        double c = H(x, y, seed * 5 + 57);
                        Put(x, y, c < 0.4 ? CR : c < 0.7 ? GO : ML);
                        Put(x, y + 1, G1);
                    }
                }

            // klif: kolumna po kolumnie od górnej krawędzi w dół
            for (int x = X0; x < X1; x++)
            {
                int y = Y0;
                while (y < Y1)
                {
                    if (!C2(x, y) || C2(x, y - 1))   // początek pasa klifu w kolumnie (nad nim ląd albo róg ścięty przez EPX)
                    {
                        y++;
                        continue;
                    }
                    int top = y, h = 0;
                    while (C2(x, top + h)) h++;
                    bool root = H(x, top, seed * 7 + 41) < 0.04;
                    int rootLen = 4 + (int)(H(x, top, seed * 7 + 42) * 8);
                    bool drip = H(x, top, seed * 7 + 43) < 0.2;
                    int dripLen = 1 + (int)(H(x, top, seed * 7 + 44) * 3);
                    for (int k = 0; k < h; k++)
                    {
                        int yy = top + k;
                        int fromBottom = h - 1 - k;
                        byte[] c;
                        if (k < 2) c = k == 0 ? G1 : G0;
                        else if (k == 2) c = E3;
                        else if (fromBottom <= 2) c = fromBottom == 0 ? O : P0;
                        else if (fromBottom <= 4) c = P1;
                        else
                        {
                            // warstwy ziemi poziomo w świecie (od stałej wysokości, nie od góry kolumny): bez pionowych pasów
                            double st = VNoise(x, yy, 9, seed * 7 + 45);
                            int row = yy + (int)(st * 3);
                            int band = (row / 5) % 2;
                            c = band == 0 ? E2 : E1;
                            if (row % 5 == 0) c = band == 0 ? E1 : E0;
                            double sn = VNoise(x, yy, 3, seed * 7 + 46);   // kamienie w ścianie
                            if (k > 4)
                            {
                                if (sn > 0.86) c = B2;
                                else if (sn > 0.8) c = B1;
                                else if (sn > 0.77) c = P0;
                            }
                        }
                        if (root && k >= 3 && k < 3 + rootLen && fromBottom > 3) c = (k % 3 == 0) ? P1 : E0;
                        if (drip && k < 2 + dripLen) c = k < 2 ? G2 : G1;   // zwisająca trawa
                        Put(x, yy, c);
                    }
                    y = top + h;
                }
            }

            // kontur 1 px: komórki konturu z siatki logiki (i kąty ścięte przez EPX) - przy lądzie ciemny, dalej woda przy brzegu
            for (int y = Y0; y < Y1; y++)
                for (int x = X0; x < X1; x++)
                {
                    if (L2(x, y) || C2(x, y))
                        continue;
                    int lx = x / S, ly = y / S;
                    if (!OutlineCell(lx, ly) && !L(lx, ly) && !CliffCell(lx, ly))
                        continue;
                    bool touch = L2(x + 1, y) || L2(x - 1, y) || L2(x, y + 1) || L2(x, y - 1)
                              || C2(x + 1, y) || C2(x - 1, y) || C2(x, y + 1) || C2(x, y - 1);
                    Put(x, y, touch ? O : FO);   // dalej od lądu: linia piany
                }
            return img;
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

        static void PlaceFish(IslandPicture pic, float[] dist, int seed)
        {
            int W = pic.width, H = pic.height;
            int x0 = Math.Max(10, pic.minX - 80), x1 = Math.Min(W - 10, pic.maxX + 80);
            int y0 = Math.Max(10, pic.minY - 60), y1 = Math.Min(H - 6, pic.maxY + 60);
            var rnd = new WRandom(seed * 101L + 3);
            float D(double x, double y) => dist[Math.Min(H - 1, Math.Max(0, (int)y)) * W + Math.Min(W - 1, Math.Max(0, (int)x))];
            for (int tries = 0; tries < 2000 && pic.fish.Count < 12; tries++)
            {
                int cx = rnd.Range(x0, x1), cy = rnd.Range(y0, y1);
                double frx = rnd.Range(8.0, 16.0), fry = rnd.Range(2.0, 5.0), ph = rnd.Range(0.0, 2 * Math.PI);
                if (D(cx, cy) < 7)
                    continue;
                bool ok = true;
                for (int k = 0; k < 16 && ok; k++)
                {
                    double a = k * Math.PI / 8;
                    if (D(cx + frx * Math.Sin(a), cy + fry * Math.Sin(2 * a)) < 5) ok = false;
                }
                if (!ok)
                    continue;
                int n = pic.fish.Count;
                pic.fish.Add(new FishPath { cx = cx, cy = cy, rx = (float)frx, ry = (float)fry, phase = (float)ph, glow = n == 1 || n == 4 || n == 7 });
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
