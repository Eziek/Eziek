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

        /// <summary>Kopia do malowania w tle (IslandPainter w osobnym wątku): własne kafelki, ten sam plan świata (tylko do odczytu).</summary>
        public IslandMap Snapshot()
        {
            var copy = new IslandMap { Seed = Seed, Plan = Plan, Purchased = Purchased };
            foreach (var kv in tiles)
                copy.tiles[kv.Key] = kv.Value;
            return copy;
        }
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

        // ---------------------------------------------------------------- wysepki (§13.10)

        /// <summary>Wysepka wyłania się z mgły, gdy własny ląd jest najwyżej tyle kafelków od niej (także po skosie).</summary>
        public const int IsletReveal = 4;

        bool OwnedWithin(TilePos p, int range)
        {
            for (int dc = -range; dc <= range; dc++)
                for (int dr = -range; dr <= range; dr++)
                    if (tiles.ContainsKey(new TilePos(p.c + dc, p.r + dr)))
                        return true;
            return false;
        }

        /// <summary>
        /// Wysepka jeszcze odcięta: żaden jej kafelek nie należy do gracza ani nie dotyka jego lądu (także po skosie).
        /// Stan wynika z samych kafelków, więc zapis go nie trzyma: przyłączona wysepka to zwykłe posiadane kafelki,
        /// a w starszych zapisach wysepka, do której ląd już dorósł, po prostu się nie pojawia.
        /// </summary>
        public bool IsletCut(int i)
        {
            if (Plan == null || i < 0 || i >= Plan.islets.Count)
                return false;
            foreach (var p in Plan.islets[i])
                if (OwnedWithin(p, 1))
                    return false;
            return true;
        }

        /// <summary>Odcięta i widoczna (ląd gracza dość blisko).</summary>
        public bool IsletVisible(int i)
        {
            if (!IsletCut(i))
                return false;
            foreach (var p in Plan.islets[i])
                if (OwnedWithin(p, IsletReveal))
                    return true;
            return false;
        }

        public List<int> VisibleIslets()
        {
            var list = new List<int>();
            if (Plan != null)
                for (int i = 0; i < Plan.islets.Count; i++)
                    if (IsletVisible(i))
                        list.Add(i);
            return list;
        }

        public IReadOnlyList<TilePos> IsletTiles(int i) => Plan.islets[i];

        /// <summary>Ile kafelków wody dzieli wysepkę od lądu (najkrócej, także po skosie); 1 = następny zakup obok ją przyłączy.</summary>
        public int IsletGap(int i)
        {
            int best = int.MaxValue;
            foreach (var p in Plan.islets[i])
                foreach (var q in tiles.Keys)
                    best = Math.Min(best, Math.Max(Math.Abs(p.c - q.c), Math.Abs(p.r - q.r)));
            return best == int.MaxValue ? 0 : Math.Max(1, best - 1);
        }

        /// <summary>Kupno kafelka p przyłączy wysepkę (podpowiedź w trybie rozbudowy).</summary>
        public bool JoinsIslet(TilePos p) => IsletsTouching(p).Count > 0;

        /// <summary>Kafelek widocznej, odciętej wysepki (do podpowiedzi), -1 gdy żadnej.</summary>
        public int IsletAt(TilePos p)
        {
            if (Plan == null)
                return -1;
            for (int i = 0; i < Plan.islets.Count; i++)
                if (Plan.islets[i].Contains(p))
                    return IsletVisible(i) ? i : -1;
            return -1;
        }

        /// <summary>Ląd do namalowania: własne kafelki i widoczne wysepki.</summary>
        public List<TilePos> PaintedTiles()
        {
            var list = new List<TilePos>(tiles.Keys);
            foreach (int i in VisibleIslets())
                list.AddRange(Plan.islets[i]);
            return list;
        }

        /// <summary>Wysepki, które dołączy nowy kafelek p (dotyka ich, także po skosie). Liczone przed dodaniem p.</summary>
        List<int> IsletsTouching(TilePos p)
        {
            var list = new List<int>();
            if (Plan == null)
                return list;
            for (int i = 0; i < Plan.islets.Count; i++)
            {
                if (!IsletCut(i))
                    continue;
                foreach (var q in Plan.islets[i])
                    if (Math.Abs(q.c - p.c) <= 1 && Math.Abs(q.r - p.r) <= 1)
                    {
                        list.Add(i);
                        break;
                    }
            }
            return list;
        }

        /// <summary>Dokłada kafelki dotkniętych wysepek (za darmo, bez liczenia do ceny) i dopisuje je do merged.</summary>
        void JoinIslets(List<int> touching, List<TilePos> merged)
        {
            foreach (int i in touching)
                foreach (var q in Plan.islets[i])
                    if (!tiles.ContainsKey(q))
                    {
                        tiles[q] = TileKind.Grass;
                        merged?.Add(q);
                    }
        }

        bool IsCutIsletTile(TilePos p)
        {
            if (Plan == null)
                return false;
            for (int i = 0; i < Plan.islets.Count; i++)
                if (Plan.islets[i].Contains(p))
                    return IsletCut(i);
            return false;
        }

        /// <summary>Kafelki do kupienia: przylegające do lądu (4 kierunki), w granicach świata.</summary>
        public List<TilePos> Candidates()
        {
            var set = new HashSet<TilePos>();
            foreach (var p in tiles.Keys)
                foreach (var q in Neighbours(p))
                    if (WorldGenerator.InWorld(q) && !tiles.ContainsKey(q) && !IsCutIsletTile(q))
                        set.Add(q);
            var list = new List<TilePos>(set);
            list.Sort((a, b) => a.r != b.r ? a.r.CompareTo(b.r) : a.c.CompareTo(b.c));
            return list;
        }

        public bool CanBuy(TilePos p)
        {
            if (!WorldGenerator.InWorld(p) || tiles.ContainsKey(p) || IsCutIsletTile(p))
                return false;
            foreach (var q in Neighbours(p))
                if (tiles.ContainsKey(q))
                    return true;
            return false;
        }

        /// <summary>
        /// Dodaje kafelek (koszt płaci wywołujący). Zwraca znalezisko spod kafelka. Kafelek dotykający wysepki przyłącza ją
        /// całą: jej kafelki trafiają do merged (znaleziska spod nich wydaje wywołujący).
        /// </summary>
        public FindKind Buy(TilePos p, List<TilePos> merged = null)
        {
            if (!CanBuy(p))
                return FindKind.None;
            var touching = IsletsTouching(p);
            tiles[p] = TileKind.Grass;
            Purchased++;
            JoinIslets(touching, merged);
            Changed?.Invoke();
            return Plan != null ? Plan.FindAt(p) : FindKind.None;
        }

        /// <summary>Darmowy kafelek (miejsce dla darmowej budowli): bez liczenia do ceny następnych kafelków. Też przyłącza wysepki.</summary>
        public bool AddFree(TilePos p, List<TilePos> merged = null)
        {
            if (!CanBuy(p))
                return false;
            var touching = IsletsTouching(p);
            tiles[p] = TileKind.Grass;
            JoinIslets(touching, merged);
            Changed?.Invoke();
            return true;
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
        // Balans (tempo „szybkie”, cykl gry 1 h, wzrost x3; tools/balance/sim.py): 25 kafelków po ok. 3-4 dniach pracy,
        // 100 po ok. 2 tygodniach, 150 po ok. miesiącu (8 h pracy dziennie)
        // pierwsze 20 kafelków taniej: zniżka 60% na starcie, maleje do zera przy 20. kafelku
        public static int TileCoins(int n) =>
            Math.Max(10, (int)Math.Round((10 + 20 * n + 1.5 * n * n) * (n < 20 ? 0.4 + 0.6 * n / 20.0 : 1.0)));
        public static int TileEssence(int n) => 3 + n / 3;

        /// <summary>Przekopanie kafelka na grządkę: 10 monet, potem +5 za każdą kolejną ponad dwie startowe.</summary>
        public static int DigCoins(int soilTiles) => 10 + 5 * Math.Max(0, soilTiles - 2);
    }
}
