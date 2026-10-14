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
            return plan;
        }
    }
}
