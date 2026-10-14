using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Upgrades;

namespace Wyspa.Economy
{
    [Serializable]
    public class PrestigeSave
    {
        public int pearls;      // niewydane perły mgły
        public int earned;      // perły zdobyte łącznie (każda daje „blask pereł”)
        public int voyages;     // ile razy gracz odpłynął we mgłę
        public List<UpgradeSave> gifts = new List<UpgradeSave>();
    }

    /// <summary>
    /// Prestiż: „Rejs we mgłę”. Gracz zostawia wyspę mgle i zaczyna na nowej, a za zarobek rejsu
    /// dostaje Perły Mgły. Każda zdobyta perła na stałe podnosi ceny (+2%, „blask pereł”), a perły wydaje się na Dary Mgły
    /// (trwałe ulepszenia między rejsami). Zostają też bestiariusz i kronika. Sam rejs (nowy zapis) robi SaveSystem.SailIntoMist.
    /// </summary>
    public static class Prestige
    {
        /// <summary>Perły za rejs = pierwiastek z (zarobek rejsu / 10 000): 10 tys. = 1, 40 tys. = 2, 1 mln = 10.</summary>
        public const double CoinsPerPearlUnit = 10000.0;
        public const float GlowPerPearl = 0.02f;

        public static int Pearls { get; private set; }
        public static int Earned { get; private set; }
        public static int Voyages { get; private set; }
        static readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public class Gift
        {
            public string id, icon;
            public int[] cost;        // perły za kolejne poziomy
            public float[] values;    // efekt na poziomie (opis: {0})
            public bool percent;      // opis w procentach (0.1 → 10)
            public string NameKey => "gift." + id + ".name";
            public string DescKey => "gift." + id + ".desc";
            public int MaxLevel => cost.Length;
        }

        public const string Dowry = "dowry", SeedMemory = "seed_memory", FaithfulWisp = "faithful_wisp", MistMarket = "mist_market",
            KeepersEmber = "keepers_ember", GreenHands = "green_hands", OldFriends = "old_friends", CheapLand = "cheap_land",
            FullCellar = "full_cellar", PearlLuck = "pearl_luck";

        public static readonly Gift[] Gifts =
        {
            new Gift { id = MistMarket, icon = "shop", cost = new[] { 1, 2, 3, 4, 5 }, values = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, percent = true },
            new Gift { id = Dowry, icon = "coin", cost = new[] { 1, 2, 4 }, values = new[] { 500f, 2000f, 8000f } },
            new Gift { id = FaithfulWisp, icon = "wisp", cost = new[] { 2 }, values = new[] { 1f } },
            new Gift { id = SeedMemory, icon = "sapling", cost = new[] { 2 }, values = new[] { 1f } },
            new Gift { id = KeepersEmber, icon = "essence", cost = new[] { 1, 2, 3 }, values = new[] { 0.1f, 0.2f, 0.3f }, percent = true },
            new Gift { id = GreenHands, icon = "sun", cost = new[] { 1, 2, 3 }, values = new[] { 0.1f, 0.2f, 0.3f }, percent = true },
            new Gift { id = OldFriends, icon = "upgrade", cost = new[] { 2, 3, 4 }, values = new[] { 0.1f, 0.2f, 0.3f }, percent = true },
            new Gift { id = CheapLand, icon = "plus", cost = new[] { 1, 2, 3 }, values = new[] { 0.1f, 0.2f, 0.3f }, percent = true },
            new Gift { id = FullCellar, icon = "wood", cost = new[] { 1, 2 }, values = new[] { 30f, 80f } },
            new Gift { id = PearlLuck, icon = "star", cost = new[] { 2, 3, 4 }, values = new[] { 0.03f, 0.06f, 0.09f }, percent = true },
        };

        public static Gift Find(string id)
        {
            foreach (var g in Gifts)
                if (g.id == id) return g;
            return null;
        }

        public static int Level(string id) => id != null && levels.TryGetValue(id, out var l) ? l : 0;

        /// <summary>Wartość daru na obecnym poziomie albo 0.</summary>
        public static float Value(string id)
        {
            var g = Find(id);
            int l = Level(id);
            return g == null || l <= 0 ? 0f : g.values[Mathf.Min(l, g.values.Length) - 1];
        }

        public static int NextCost(Gift g) => g != null && Level(g.id) < g.MaxLevel ? g.cost[Level(g.id)] : -1;

        public static bool TryBuy(Gift g)
        {
            int cost = NextCost(g);
            if (cost < 0 || Pearls < cost)
                return false;
            Pearls -= cost;
            levels[g.id] = Level(g.id) + 1;
            return true;
        }

        // ------------------------------------------------------------ efekty (czytają je Market, GameLoop, UpgradeState, Island)

        /// <summary>Mnożnik cen i dochodów: blask pereł (+2% za każdą zdobytą) i Mgielny targ.</summary>
        public static float SaleMultiplier => 1f + GlowPerPearl * Earned + Value(MistMarket);
        public static float EmberMultiplier => 1f + Value(KeepersEmber);
        public static float GrowthMultiplier => 1f + Value(GreenHands);
        public static float UpgradeCostMultiplier => 1f - Value(OldFriends);
        public static float TileCostMultiplier => 1f - Value(CheapLand);
        public static float CritBonus => Value(PearlLuck);

        /// <summary>Perły za rejs teraz (z zarobku tego rejsu).</summary>
        public static int PendingPearls => (int)Math.Floor(Math.Sqrt(Market.RunCoins / CoinsPerPearlUnit));

        /// <summary>Ile trzeba zarobić w tym rejsie (łącznie) na kolejną perłę.</summary>
        public static long CoinsForNextPearl
        {
            get
            {
                long n = PendingPearls + 1;
                return (long)(n * n * CoinsPerPearlUnit);
            }
        }

        public static bool CanSail => PendingPearls >= 1;

        // ------------------------------------------------------------ zapis

        public static void Restore(PrestigeSave s)
        {
            levels.Clear();
            Pearls = s != null ? Mathf.Max(0, s.pearls) : 0;
            Earned = s != null ? Mathf.Max(0, s.earned) : 0;
            Voyages = s != null ? Mathf.Max(0, s.voyages) : 0;
            if (s != null && s.gifts != null)
                foreach (var g in s.gifts)
                    if (!string.IsNullOrEmpty(g.id) && g.level > 0 && Find(g.id) != null)
                        levels[g.id] = Mathf.Min(g.level, Find(g.id).MaxLevel);
        }

        public static PrestigeSave Snapshot()
        {
            var s = new PrestigeSave { pearls = Pearls, earned = Earned, voyages = Voyages };
            foreach (var kv in levels)
                s.gifts.Add(new UpgradeSave { id = kv.Key, level = kv.Value });
            return s;
        }

        /// <summary>Rejs: perły z zarobku trafiają do sakwy, licznik rejsów rośnie. Zwraca liczbę nowych pereł (0 = za mało).</summary>
        public static int CollectVoyage()
        {
            int gain = PendingPearls;
            if (gain < 1)
                return 0;
            Pearls += gain;
            Earned += gain;
            Voyages++;
            return gain;
        }

        /// <summary>Waluty na start nowej wyspy: 50 monet + Posag, drewno i kamień z Pełnej spiżarni.</summary>
        public static List<Economy.CurrencyAmount> StartCurrencies()
        {
            int stock = Mathf.RoundToInt(Value(FullCellar));
            return new List<CurrencyAmount>
            {
                new CurrencyAmount(CurrencyType.Coins, 50 + Mathf.RoundToInt(Value(Dowry))),
                new CurrencyAmount(CurrencyType.Wood, stock),
                new CurrencyAmount(CurrencyType.Stone, stock),
            };
        }
    }
}
