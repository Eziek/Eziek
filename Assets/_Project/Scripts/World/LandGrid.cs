using System;
using System.Collections.Generic;

namespace Wyspa.World
{
    /// <summary>
    /// Mapa chodzenia po lądzie dla chowańców (GDD §9 p. 125), czysty C# (testowana poza Unity). Piksele obrazu wyspy
    /// (16 na kafelek, y w dół). Chodzić można po trawie z maski grządek (ląd bez 2 px przy brzegu i bez klifu) na kafelkach
    /// gracza. Trasa: prosto, gdy każdy piksel, przez który przechodzi odcinek, jest trawą, inaczej BFS po pikselach trawy
    /// i wygładzenie (z każdego punktu do najdalszego widocznego po trawie).
    /// </summary>
    public class LandGrid
    {
        const int CellPx = 1;

        readonly IslandPicture pic;
        readonly IslandMap map;
        readonly int x0, y0, w, h;
        readonly bool[] cells;

        public IslandPicture Picture => pic;

        public LandGrid(IslandPicture picture, IslandMap islandMap)
        {
            pic = picture;
            map = islandMap;
            x0 = Math.Max(0, pic.minX - 2);
            y0 = Math.Max(0, pic.minY - 2);
            w = (Math.Min(pic.width - 1, pic.maxX + 2) - x0) / CellPx + 1;
            h = (Math.Min(pic.height - 1, pic.maxY + 2) - y0) / CellPx + 1;
            cells = new bool[w * h];
            for (int cy = 0; cy < h; cy++)
                for (int cx = 0; cx < w; cx++)
                {
                    cells[cy * w + cx] = WalkablePixel(x0 + cx * CellPx, y0 + cy * CellPx);
                }
        }

        bool WalkablePixel(int x, int y) => pic.IsWalkable(x, y) && map.Owns(TileOf(x, y));

        static TilePos TileOf(int x, int y) => new TilePos((int)Math.Floor((x - IslandPainter.Margin) / (double)IslandPainter.Tile),
                                                           (int)Math.Floor((y - IslandPainter.Margin) / (double)IslandPainter.Tile));

        /// <summary>Punkt (piksele obrazu, ułamkowo) na trawie.</summary>
        public bool Walkable(double x, double y) => WalkablePixel((int)Math.Floor(x), (int)Math.Floor(y));

        /// <summary>
        /// Cały odcinek na trawie: sprawdza każdy piksel, przez który przechodzi (przejście po siatce Amanatides-Woo;
        /// przy przejściu dokładnie przez narożnik oba sąsiednie piksele).
        /// </summary>
        public bool Line(double ax, double ay, double bx, double by)
        {
            int x = (int)Math.Floor(ax), y = (int)Math.Floor(ay);
            int ex = (int)Math.Floor(bx), ey = (int)Math.Floor(by);
            double dx = bx - ax, dy = by - ay;
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            double tdx = sx != 0 ? Math.Abs(1.0 / dx) : double.MaxValue, tdy = sy != 0 ? Math.Abs(1.0 / dy) : double.MaxValue;
            double tmx = sx > 0 ? (x + 1 - ax) * tdx : sx < 0 ? (ax - x) * tdx : double.MaxValue;
            double tmy = sy > 0 ? (y + 1 - ay) * tdy : sy < 0 ? (ay - y) * tdy : double.MaxValue;
            if (!WalkablePixel(x, y))
                return false;
            for (int guard = 0; guard < 4096 && (x != ex || y != ey); guard++)
            {
                if (Math.Abs(tmx - tmy) < 1e-9)
                {
                    if (!WalkablePixel(x + sx, y) || !WalkablePixel(x, y + sy))
                        return false;
                    x += sx; y += sy; tmx += tdx; tmy += tdy;
                }
                else if (tmx < tmy) { x += sx; tmx += tdx; }
                else { y += sy; tmy += tdy; }
                if (tmx > 1.0 + tdx && tmy > 1.0 + tdy)
                    break;
                if (!WalkablePixel(x, y))
                    return false;
            }
            return true;
        }

        /// <summary>Najbliższy środek piksela trawy (obwody kwadratów do 96 px). false = brak trawy w pobliżu.</summary>
        public bool Nearest(double x, double y, out double nx, out double ny)
        {
            nx = x;
            ny = y;
            if (Walkable(x, y))
                return true;
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            for (int r = 1; r <= 96; r++)
            {
                double best = double.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                            continue;
                        if (!WalkablePixel(cx + dx, cy + dy))
                            continue;
                        double d = dx * dx + dy * dy;
                        if (d < best)
                        {
                            best = d;
                            nx = cx + dx + 0.5;
                            ny = cy + dy + 0.5;
                        }
                    }
                if (best < double.MaxValue)
                    return true;
            }
            return false;
        }

        bool Cell(int cx, int cy) => cx >= 0 && cy >= 0 && cx < w && cy < h && cells[cy * w + cx];

        /// <summary>Piksel trawy najbliższy punktowi (do 6 px dalej) albo (-1, -1).</summary>
        (int, int) CellNear(double x, double y)
        {
            int cx = (int)Math.Floor((x - x0) / CellPx), cy = (int)Math.Floor((y - y0) / CellPx);
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if ((Math.Abs(dx) == r || Math.Abs(dy) == r) && Cell(cx + dx, cy + dy))
                            return (cx + dx, cy + dy);
            return (-1, -1);
        }

        (double, double) CellCenter(int cx, int cy) => (x0 + cx * CellPx + 0.5, y0 + cy * CellPx + 0.5);

        /// <summary>
        /// Punkty trasy (piksele obrazu): pierwszy może być zejściem z wody na brzeg (gdy start nie jest na trawie), ostatni
        /// to cel przesunięty na trawę. Pusta lista: nie ma dokąd iść (brak trawy albo cel na innym kawałku lądu).
        /// </summary>
        public List<(double x, double y)> Route(double fx, double fy, double tx, double ty)
        {
            var route = new List<(double, double)>();
            if (!Nearest(tx, ty, out tx, out ty))
                return route;
            if (!Walkable(fx, fy))
            {
                if (!Nearest(fx, fy, out fx, out fy))
                    return route;
                route.Add((fx, fy));
            }
            if (Line(fx, fy, tx, ty))
            {
                route.Add((tx, ty));
                return route;
            }
            var (sx, sy) = CellNear(fx, fy);
            var (gx, gy) = CellNear(tx, ty);
            if (sx < 0 || gx < 0)
                return route;
            int start = sy * w + sx, goal = gy * w + gx;
            var prev = new int[w * h];
            for (int i = 0; i < prev.Length; i++) prev[i] = -1;
            prev[start] = start;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0 && prev[goal] < 0)
            {
                int c = queue.Dequeue();
                int cx = c % w, cy = c / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (!Cell(nx, ny) || prev[ny * w + nx] >= 0)
                            continue;
                        if (dx != 0 && dy != 0 && (!Cell(cx + dx, cy) || !Cell(cx, cy + dy)))
                            continue;   // po skosie tylko, gdy oba boki są lądem (bez ścinania narożników nad wodą)
                        prev[ny * w + nx] = c;
                        queue.Enqueue(ny * w + nx);
                    }
            }
            if (prev[goal] < 0)
                return route;   // cel na innym kawałku lądu
            var cellsPath = new List<int>();
            for (int c = goal; c != start; c = prev[c])
                cellsPath.Add(c);
            cellsPath.Reverse();
            var points = new List<(double x, double y)>();
            foreach (var c in cellsPath)
                points.Add(CellCenter(c % w, c / w));
            points.Add((tx, ty));
            // wygładzenie: z bieżącego punktu do najdalszego widocznego prosto po trawie
            double ux = fx, uy = fy;
            int k = 0;
            while (k < points.Count)
            {
                int far = k;
                for (int j = points.Count - 1; j > k; j--)
                    if (Line(ux, uy, points[j].x, points[j].y)) { far = j; break; }
                route.Add(points[far]);
                ux = points[far].x;
                uy = points[far].y;
                k = far + 1;
            }
            return route;
        }
    }
}
