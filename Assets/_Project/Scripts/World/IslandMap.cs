using System;
using System.Collections.Generic;

namespace Wyspa.World
{
    /// <summary>
    /// Wyspa jako zbiór kafelków (GDD §13.7): posiadane kafelki z typem (trawa / grządka), ziarno świata,
    /// liczba kupionych od startu. Czyste dane, bez Unity.
    /// </summary>
    public class IslandMap
    {
        readonly Dictionary<TilePos, TileKind> tiles = new Dictionary<TilePos, TileKind>();

        public int Seed { get; private set; }
        public WorldPlan Plan { get; private set; }
        /// <summary>Kafelki kupione od startu (n we wzorze na cenę, §13.2).</summary>
        public int Purchased { get; private set; }

        public event Action Changed;

        public IReadOnlyDictionary<TilePos, TileKind> Tiles => tiles;
        public int Count => tiles.Count;

        public bool Owns(TilePos p) => tiles.ContainsKey(p);
        public TileKind KindAt(TilePos p) => tiles.TryGetValue(p, out var k) ? k : TileKind.Grass;

        /// <summary>Nowy świat: start 2x2 z dwiema grządkami.</summary>
        public void NewWorld(int seed)
        {
            Seed = seed;
            Plan = WorldGenerator.Generate(seed);
            tiles.Clear();
            foreach (var p in Plan.start)
                tiles[p] = TileKind.Grass;
            foreach (var p in Plan.startSoil)
                tiles[p] = TileKind.Soil;
            Purchased = 0;
            Changed?.Invoke();
        }

        /// <summary>Wczytanie zapisu: świat z ziarna, kafelki z listy.</summary>
        public void Restore(int seed, IEnumerable<(TilePos pos, TileKind kind)> saved, int purchased)
        {
            Seed = seed;
            Plan = WorldGenerator.Generate(seed);
            tiles.Clear();
            foreach (var (pos, kind) in saved)
                if (WorldGenerator.InWorld(pos))
                    tiles[pos] = kind;
            if (tiles.Count == 0)
                foreach (var p in Plan.start)
                    tiles[p] = TileKind.Grass;
            Purchased = Math.Max(0, purchased);
            Changed?.Invoke();
        }

        /// <summary>Kafelki do kupienia: przylegające do lądu (4 kierunki), w granicach świata.</summary>
        public List<TilePos> Candidates()
        {
            var set = new HashSet<TilePos>();
            foreach (var p in tiles.Keys)
                foreach (var q in Neighbours(p))
                    if (WorldGenerator.InWorld(q) && !tiles.ContainsKey(q))
                        set.Add(q);
            var list = new List<TilePos>(set);
            list.Sort((a, b) => a.r != b.r ? a.r.CompareTo(b.r) : a.c.CompareTo(b.c));
            return list;
        }

        public bool CanBuy(TilePos p)
        {
            if (!WorldGenerator.InWorld(p) || tiles.ContainsKey(p))
                return false;
            foreach (var q in Neighbours(p))
                if (tiles.ContainsKey(q))
                    return true;
            return false;
        }

        /// <summary>Dodaje kafelek (koszt płaci wywołujący). Zwraca znalezisko spod kafelka.</summary>
        public FindKind Buy(TilePos p)
        {
            if (!CanBuy(p))
                return FindKind.None;
            tiles[p] = TileKind.Grass;
            Purchased++;
            Changed?.Invoke();
            return Plan != null ? Plan.FindAt(p) : FindKind.None;
        }

        public void SetKind(TilePos p, TileKind kind)
        {
            if (!tiles.ContainsKey(p) || tiles[p] == kind)
                return;
            tiles[p] = kind;
            Changed?.Invoke();
        }

        public static IEnumerable<TilePos> Neighbours(TilePos p)
        {
            yield return new TilePos(p.c + 1, p.r);
            yield return new TilePos(p.c - 1, p.r);
            yield return new TilePos(p.c, p.r + 1);
            yield return new TilePos(p.c, p.r - 1);
        }
    }

    /// <summary>Ceny (GDD §13.2): kafelek = monety + Esencja, krzywa kwadratowa; przekopanie na grządkę tanio.</summary>
    public static class IslandEconomy
    {
        // Etap 6d (balans, tempo „szybkie”, tools/balance/sim.py): 25 kafelków po ok. 3 dniach pracy, 100 po ok. 2 tygodniach,
        // 150 po ok. miesiącu (8 h pracy dziennie)
        public static int TileCoins(int n) => (int)Math.Round(10 + 8 * n + 0.5 * n * n);
        public static int TileEssence(int n) => 3 + n / 4;

        /// <summary>Przekopanie kafelka na grządkę: 10 monet, potem +5 za każdą kolejną ponad dwie startowe.</summary>
        public static int DigCoins(int soilTiles) => 10 + 5 * Math.Max(0, soilTiles - 2);
    }
}
