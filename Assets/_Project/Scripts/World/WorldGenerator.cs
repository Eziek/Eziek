using System;
using System.Collections.Generic;

namespace Wyspa.World
{
    /// <summary>Wynik generatora: co leży pod mgłą i jak wygląda start (GDD §13.1, §13.4).</summary>
    public class WorldPlan
    {
        public int seed;
        public readonly Dictionary<TilePos, ZoneKind> zones = new Dictionary<TilePos, ZoneKind>();
        public readonly Dictionary<TilePos, FindKind> finds = new Dictionary<TilePos, FindKind>();
        public readonly List<(ZoneKind kind, TilePos center)> zoneCenters = new List<(ZoneKind, TilePos)>();
        public readonly List<TilePos> start = new List<TilePos>();
        public readonly List<TilePos> startSoil = new List<TilePos>();
        public TilePos startDecoTile;
        public DecoKind startDeco;
        /// <summary>Wysepki (§13.10): odcięte skrawki lądu. Nie należą do gracza, dopóki ich nie przyłączy.</summary>
        public readonly List<List<TilePos>> islets = new List<List<TilePos>>();
        /// <summary>Wysepki fabularne (§13.11): indeks wysepki → lewy górny kafelek monumentu 3x2.</summary>
        public readonly Dictionary<int, TilePos> monuments = new Dictionary<int, TilePos>();

        public ZoneKind ZoneAt(TilePos p) => zones.TryGetValue(p, out var z) ? z : ZoneKind.None;
        public FindKind FindAt(TilePos p) => finds.TryGetValue(p, out var f) ? f : FindKind.None;
    }

    /// <summary>
    /// Świat 32x32 z ziarna zapisu (port tools/pixel-proto/world_proto.py): start 2x2 (2 grządki + losowy obiekt),
    /// 3 strefy w różnych kierunkach, znaleziska, źródło i pradawny kamień. To samo ziarno = ten sam świat.
    /// </summary>
    public static class WorldGenerator
    {
        public const int Size = 32;
        static readonly TilePos[] StartTiles = { new TilePos(15, 15), new TilePos(16, 15), new TilePos(15, 16), new TilePos(16, 16) };
        static readonly ZoneKind[] Zones = { ZoneKind.Cemetery, ZoneKind.Swamp, ZoneKind.Ruins };
        static readonly (int dc, int dr)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public static bool InWorld(TilePos p) => p.c >= 0 && p.r >= 0 && p.c < Size && p.r < Size;

        public static WorldPlan Generate(int seed)
        {
            var plan = new WorldPlan { seed = seed };
            var rnd = new WRandom(seed);
            var start = new HashSet<TilePos>(StartTiles);
            plan.start.AddRange(StartTiles);

            // strefy: kierunki co najmniej 90° od siebie, 6-12 kafelków od startu, kształt = losowa plama 8-14 kafelków
            var angles = new List<double>();
            while (angles.Count < Zones.Length)
            {
                double a = rnd.Range(0.0, 2 * Math.PI);
                bool ok = true;
                foreach (var b in angles)
                {
                    double diff = Math.Abs(((a - b + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI);
                    if (diff <= Math.PI / 2) ok = false;
                }
                if (ok) angles.Add(a);
            }
            for (int i = 0; i < Zones.Length; i++)
            {
                double d = rnd.Range(6.0, 12.0);
                var center = new TilePos((int)(15.5 + d * Math.Cos(angles[i])), (int)(15.5 + d * Math.Sin(angles[i])));
                int size = rnd.Range(8, 15);
                var cells = new List<TilePos> { center };
                var set = new HashSet<TilePos> { center };
                int guard = 0;
                while (set.Count < size && guard++ < 1000)
                {
                    var p = cells[rnd.Range(0, cells.Count)];
                    var (dc, dr) = Dirs[rnd.Range(0, 4)];
                    var q = new TilePos(Math.Min(Size - 2, Math.Max(1, p.c + dc)), Math.Min(Size - 2, Math.Max(1, p.r + dr)));
                    if (start.Contains(q) || !set.Add(q))
                        continue;
                    cells.Add(q);
                }
                foreach (var p in set)
                    if (!start.Contains(p))
                        plan.zones[p] = Zones[i];
                plan.zoneCenters.Add((Zones[i], center));
            }

            // znaleziska: ok. 5%, 12% w pobliżu stref
            FindKind[] loot = { FindKind.Coins, FindKind.Coins, FindKind.Coins, FindKind.Coins, FindKind.Coins,
                                FindKind.NightSeed, FindKind.NightSeed, FindKind.NightSeed,
                                FindKind.Shard, FindKind.Shard, FindKind.Lore, FindKind.Lore };
            for (int c = 0; c < Size; c++)
                for (int r = 0; r < Size; r++)
                {
                    var p = new TilePos(c, r);
                    if (start.Contains(p))
                        continue;
                    double near = double.MaxValue;
                    foreach (var z in plan.zoneCenters)
                        near = Math.Min(near, Math.Sqrt((c - z.center.c) * (c - z.center.c) + (r - z.center.r) * (r - z.center.r)));
                    if (rnd.Value() < (near < 4 ? 0.12 : 0.05))
                        plan.finds[p] = loot[rnd.Range(0, loot.Length)];
                }
            // dziwne kafelki: po jednym, co najmniej 5 kafelków od startu, poza strefami
            foreach (var special in new[] { FindKind.Spring, FindKind.Monolith })
                for (int guard = 0; guard < 1000; guard++)
                {
                    var p = new TilePos(rnd.Range(3, Size - 3), rnd.Range(3, Size - 3));
                    double dist = Math.Sqrt((p.c - 15.5) * (p.c - 15.5) + (p.r - 15.5) * (p.r - 15.5));
                    if (plan.zones.ContainsKey(p) || start.Contains(p) || dist <= 5)
                        continue;
                    if (plan.finds.TryGetValue(p, out var f) && (f == FindKind.Spring || f == FindKind.Monolith))
                        continue;
                    plan.finds[p] = special;
                    break;
                }

            // start: 2 losowe grządki, na jednym z pozostałych kafelków losowa dekoracja
            var startRnd = new WRandom(seed * 31L + 7);
            var pool = new List<TilePos>(StartTiles);
            for (int i = 0; i < 2; i++)
            {
                int k = startRnd.Range(0, pool.Count);
                plan.startSoil.Add(pool[k]);
                pool.RemoveAt(k);
            }
            plan.startDecoTile = pool[startRnd.Range(0, pool.Count)];
            plan.startDeco = (DecoKind)startRnd.Range(0, 4);
            PlaceIslets(plan, seed);
            return plan;
        }

        public const int MonumentW = 3, MonumentH = 2;   // monument na wysepce fabularnej: prostokąt 3x2 kafelki

        /// <summary>
        /// Wysepki (§13.10, §13.11): najpierw 2 wysepki fabularne z monumentem (prostokąt 3x2, czasem z jednym kafelkiem
        /// obok: 6-7 kafelków), 7-11 kafelków od środka startu, potem 1-2 zwykłe po 3-4 kafelki, 6-10 kafelków od środka.
        /// Poza strefami, co najmniej 2 kafelki wody między sobą, na każdej co najmniej jedno znalezisko.
        /// Osobny generator liczb, więc strefy i znaleziska świata są te same co przed dodaniem wysepek.
        /// </summary>
        static void PlaceIslets(WorldPlan plan, int seed)
        {
            var rnd = new WRandom(seed * 53L + 19);
            int small = rnd.Range(1, 3);
            var taken = new List<TilePos>();
            for (int guard = 0; plan.islets.Count < 2 + small && guard < 600; guard++)
            {
                bool monument = plan.monuments.Count < 2;
                double a = rnd.Range(0.0, 2 * Math.PI), d = monument ? rnd.Range(7.5, 11.0) : rnd.Range(6.5, 10.0);
                var first = new TilePos((int)Math.Round(15.5 + d * Math.Cos(a)), (int)Math.Round(15.5 + d * Math.Sin(a)));
                var cells = new List<TilePos>();
                if (monument)
                {
                    for (int r = 0; r < MonumentH; r++)
                        for (int c = 0; c < MonumentW; c++)
                            cells.Add(new TilePos(first.c + c, first.r + r));
                    if (rnd.Value() < 0.5)
                    {
                        // siódmy kafelek: wolne miejsce obok monumentu
                        int side = rnd.Range(0, 4);
                        var extra = side == 0 ? new TilePos(first.c - 1, first.r + rnd.Range(0, MonumentH))
                                  : side == 1 ? new TilePos(first.c + MonumentW, first.r + rnd.Range(0, MonumentH))
                                  : side == 2 ? new TilePos(first.c + rnd.Range(0, MonumentW), first.r - 1)
                                  : new TilePos(first.c + rnd.Range(0, MonumentW), first.r + MonumentH);
                        cells.Add(extra);
                    }
                }
                else
                {
                    cells.Add(first);
                    int size = rnd.Range(3, 5);
                    for (int g = 0; cells.Count < size && g < 60; g++)
                    {
                        var p = cells[rnd.Range(0, cells.Count)];
                        var (dc, dr) = Dirs[rnd.Range(0, 4)];
                        var q = new TilePos(p.c + dc, p.r + dr);
                        if (!cells.Contains(q))
                            cells.Add(q);
                    }
                    if (cells.Count != size)
                        continue;
                }
                bool ok = true;
                foreach (var p in cells)
                {
                    if (p.c < 2 || p.r < 2 || p.c > Size - 3 || p.r > Size - 3 || plan.zones.ContainsKey(p))
                        ok = false;
                    foreach (var t in taken)
                        if (Math.Max(Math.Abs(t.c - p.c), Math.Abs(t.r - p.r)) <= 2)
                            ok = false;
                    foreach (var t in plan.start)   // na starcie schowana we mgle (IslandMap.IsletReveal)
                        if (Math.Max(Math.Abs(t.c - p.c), Math.Abs(t.r - p.r)) <= IslandMap.IsletReveal)
                            ok = false;
                }
                if (!ok)
                    continue;
                taken.AddRange(cells);
                if (monument)
                    plan.monuments[plan.islets.Count] = first;
                plan.islets.Add(cells);
                bool hasFind = false;
                foreach (var p in cells)
                    if (plan.FindAt(p) != FindKind.None)
                        hasFind = true;
                if (!hasFind)
                {
                    double v = rnd.Value();
                    plan.finds[cells[rnd.Range(0, cells.Count)]] = v < 0.35 ? FindKind.Shard : v < 0.65 ? FindKind.NightSeed : FindKind.Coins;
                }
            }
        }
    }
}
