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
        public List<string> hats = new List<string>();   // czapki ogników (kosmetyka na zawsze, Economy/Hats)
    }

    public enum GiftRarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>
    /// Prestiż: „Rejs we mgłę”. Gracz zostawia wyspę mgle, a za zarobek rejsu dostaje Perły Mgły. Każda zdobyta perła na stałe
    /// podnosi ceny (+2%, „blask pereł”). Perły wydaje się w Przystani we mgle (między rejsami, UI/MistHarborView): jedna perła =
    /// jedno losowanie daru. Dary mają rzadkość (zwykły, niezwykły, rzadki, epicki, legendarny); wylosowany drugi raz dar
    /// wchodzi na wyższy poziom, a dar na najwyższym poziomie wypada z losowania. Zostają też bestiariusz i kronika.
    /// Sam rejs i nową wyspę zapisuje SaveSystem (SailIntoMist, LeaveMist).
    /// </summary>
    public static class Prestige
    {
        /// <summary>Perły za rejs = pierwiastek z (zarobek rejsu / 10 000): 10 tys. = 1, 40 tys. = 2, 1 mln = 10.</summary>
        public const double CoinsPerPearlUnit = 10000.0;
        public const float GlowPerPearl = 0.02f;
        public const int RollCost = 1;

        public static int Pearls { get; private set; }
        public static int Earned { get; private set; }
        public static int Voyages { get; private set; }
        static readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public class Gift
        {
            public string id, icon;   // icon: zapasowa ikona UI, gdy motyw nie ma jeszcze własnej (icon_gift_<id>, Wyspa → 2)
            public GiftRarity rarity;
            public float[] values;    // efekt na kolejnych poziomach (opis: {0})
            public bool percent;      // opis w procentach (0.1 → 10)
            public string NameKey => "gift." + id + ".name";
            public string DescKey => "gift." + id + ".desc";
            public string IconKey => "gift_" + id;
            public int MaxLevel => values.Length;
        }

        /// <summary>Szansa na rzadkość przy losowaniu (gdy wszystkie dary są jeszcze do zdobycia): 50 / 28 / 14 / 6 / 2%.</summary>
        static readonly float[] RarityWeight = { 50f, 28f, 14f, 6f, 2f };

        public static string RarityKey(GiftRarity r) => "gift.rarity." + r.ToString().ToLowerInvariant();

        /// <summary>Kolor rzadkości (ramka, nazwa, rozbłysk): kamień, mech, zimny błękit, fiolet, stare złoto.</summary>
        public static Color RarityColor(GiftRarity r)
        {
            switch (r)
            {
                case GiftRarity.Uncommon: return new Color32(0x6f, 0xa8, 0x6a, 0xff);
                case GiftRarity.Rare: return new Color32(0x5b, 0x8f, 0xd0, 0xff);
                case GiftRarity.Epic: return new Color32(0xa4, 0x6f, 0xd6, 0xff);
                case GiftRarity.Legendary: return new Color32(0xe0, 0xa0, 0x40, 0xff);
                default: return new Color32(0x9a, 0x92, 0x84, 0xff);
            }
        }

        public const string Dowry = "dowry", SeedMemory = "seed_memory", FaithfulWisp = "faithful_wisp", MistMarket = "mist_market",
            KeepersEmber = "keepers_ember", GreenHands = "green_hands", OldFriends = "old_friends", CheapLand = "cheap_land",
            FullCellar = "full_cellar", PearlLuck = "pearl_luck",
            MistWings = "mist_wings", DeepCrates = "deep_crates", SunSeeds = "sun_seeds", MoonDew = "moon_dew", ThickOil = "thick_oil",
            AxeTrade = "axe_trade", MasonTrade = "mason_trade", WiseWisps = "wise_wisps", MistCompost = "mist_compost",
            GenerousSpirit = "generous_spirit", GhostFriend = "ghost_friend", AncestralOrchard = "ancestral_orchard",
            Regulars = "regulars", FickleSeeds = "fickle_seeds", MistWind = "mist_wind", NightFair = "night_fair",
            EmberReserve = "ember_reserve", CozyNest = "cozy_nest", PiggyBank = "piggy_bank", NoticeBoard = "notice_board",
            Tireless = "tireless", DayWage = "day_wage", GoldenFleece = "golden_fleece", LuckyShard = "lucky_shard",
            MistCrown = "mist_crown", PearlGrace = "pearl_grace", IslandHeart = "island_heart";

        static Gift G(string id, string icon, GiftRarity r, bool percent, params float[] values) =>
            new Gift { id = id, icon = icon, rarity = r, percent = percent, values = values };

        const GiftRarity C = GiftRarity.Common, U = GiftRarity.Uncommon, R = GiftRarity.Rare, E = GiftRarity.Epic, L = GiftRarity.Legendary;

        /// <summary>Wszystkie dary w kolejności kolekcji (od zwykłych do legendarnych).</summary>
        public static readonly Gift[] Gifts =
        {
            G(Dowry, "coin", C, false, 500f, 2000f, 8000f),
            G(CheapLand, "plus", C, true, 0.1f, 0.2f, 0.3f),
            G(FullCellar, "wood", C, false, 30f, 80f, 150f),
            G(MistWings, "wing", C, true, 0.1f, 0.2f, 0.3f),
            G(DeepCrates, "crate", C, false, 5f, 10f, 15f),
            G(SunSeeds, "sun", C, true, 0.08f, 0.16f, 0.24f),
            G(MoonDew, "moon", C, true, 0.08f, 0.16f, 0.24f),
            G(ThickOil, "lamp", C, true, 0.08f, 0.16f, 0.24f),
            G(AxeTrade, "break", C, false, 1f, 2f, 3f),
            G(MasonTrade, "stone", C, false, 1f, 2f, 3f),
            G(WiseWisps, "book", C, true, 0.15f, 0.3f, 0.45f),
            G(MistCompost, "drop", C, false, 3f, 6f, 10f),

            G(FaithfulWisp, "wisp", U, false, 1f),
            G(KeepersEmber, "essence", U, true, 0.1f, 0.2f, 0.3f),
            G(GreenHands, "sapling", U, true, 0.1f, 0.2f, 0.3f),
            G(GenerousSpirit, "seed_ghostpumpkin", U, true, 0.03f, 0.06f, 0.09f),
            G(GhostFriend, "keys", U, true, 0.05f, 0.1f, 0.15f),
            G(AncestralOrchard, "fruit_apple", U, true, 0.1f, 0.2f, 0.3f),
            G(Regulars, "scale", U, true, 0.1f, 0.2f, 0.3f),
            G(FickleSeeds, "seed_hybrid", U, true, 0.01f, 0.02f, 0.03f),
            G(MistWind, "boat", U, true, 0.08f, 0.16f, 0.24f),
            G(NightFair, "nightcrop", U, true, 0.08f, 0.16f, 0.24f),
            G(EmberReserve, "fill", U, false, 1f),

            G(MistMarket, "shop", R, true, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f),
            G(SeedMemory, "seed_wheat", R, false, 1f),
            G(OldFriends, "upgrade", R, true, 0.1f, 0.2f, 0.3f),
            G(CozyNest, "bestiary", R, false, 1f),
            G(PiggyBank, "stats", R, true, 0.005f, 0.01f),
            G(NoticeBoard, "menu", R, false, 1f),
            G(Tireless, "fatigue", R, true, 0.1f, 0.2f),
            G(DayWage, "focus", R, false, 1f, 2f),

            G(PearlLuck, "star", E, true, 0.03f, 0.06f, 0.09f),
            G(GoldenFleece, "deepgift", E, false, 0.5f, 1f),
            G(LuckyShard, "starshard", E, true, 0.02f, 0.04f),

            G(MistCrown, "pearl", L, true, 0.25f),
            G(PearlGrace, "ritual", L, true, 0.25f),
            G(IslandHeart, "seed_bloodrose", L, true, 0.25f),
        };

        public static Gift Find(string id)
        {
            foreach (var g in Gifts)
                if (g.id == id) return g;
            return null;
        }

        public static int Level(string id) => id != null && levels.TryGetValue(id, out var l) ? l : 0;
        public static bool Owned(Gift g) => g != null && Level(g.id) > 0;
        public static bool Maxed(Gift g) => g != null && Level(g.id) >= g.MaxLevel;

        /// <summary>Wartość daru na obecnym poziomie albo 0.</summary>
        public static float Value(string id)
        {
            var g = Find(id);
            int l = Level(id);
            return g == null || l <= 0 ? 0f : g.values[Mathf.Min(l, g.values.Length) - 1];
        }

        /// <summary>Ile darów (z poziomami) jeszcze da się wylosować.</summary>
        public static int RollsLeft
        {
            get
            {
                int n = 0;
                foreach (var g in Gifts)
                    n += Mathf.Max(0, g.MaxLevel - Level(g.id));
                return n;
            }
        }

        public static bool CanRoll => Pearls >= RollCost && RollsLeft > 0;

        /// <summary>Dary, które mogą jeszcze wypaść (nie na najwyższym poziomie).</summary>
        public static List<Gift> Pool()
        {
            var list = new List<Gift>();
            foreach (var g in Gifts)
                if (!Maxed(g)) list.Add(g);
            return list;
        }

        /// <summary>
        /// Losowanie za perłę: najpierw rzadkość (wagi RarityWeight, tylko spośród tych, które mają jeszcze dary), potem dar
        /// tej rzadkości po równo. Wylosowany dar dostaje poziom wyżej. Zwraca dar albo null (brak pereł albo wszystko zdobyte).
        /// </summary>
        public static Gift Roll(System.Random rnd = null)
        {
            if (!CanRoll)
                return null;
            rnd = rnd ?? new System.Random();
            var pool = Pool();
            float total = 0f;
            var weights = new float[RarityWeight.Length];
            foreach (var g in pool)
                weights[(int)g.rarity] = RarityWeight[(int)g.rarity];
            foreach (var w in weights)
                total += w;
            double pick = rnd.NextDouble() * total;
            var rarity = GiftRarity.Common;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f)
                    continue;
                rarity = (GiftRarity)i;
                if (pick < weights[i])
                    break;
                pick -= weights[i];
            }
            var same = pool.FindAll(g => g.rarity == rarity);
            var won = same[rnd.Next(same.Count)];
            Pearls -= RollCost;
            levels[won.id] = Level(won.id) + 1;
            return won;
        }

        // ------------------------------------------------------------ efekty (czytają je Market, GameLoop, UpgradeState, Island)

        /// <summary>Mnożnik cen i dochodów: blask pereł (+2% za każdą zdobytą), Mgielny targ i Korona mgły.</summary>
        public static float SaleMultiplier => 1f + GlowPerPearl * Earned + Value(MistMarket) + Value(MistCrown);
        public static float EmberMultiplier => 1f + Value(KeepersEmber);
        public static float GrowthMultiplier => 1f + Value(GreenHands) + Value(IslandHeart);
        public static float UpgradeCostMultiplier => 1f - Value(OldFriends);
        public static float TileCostMultiplier => 1f - Value(CheapLand);
        public static float CritBonus => Value(PearlLuck);

        /// <summary>
        /// Dary działające przez efekty ulepszeń: wołane po UpgradeEffects.Apply (GameLoop.ApplyUpgrades), więc dokładają się
        /// do tego, co dało drzewko.
        /// </summary>
        public static void ApplyToEffects()
        {
            UpgradeEffects.WispSpeed *= 1f + Value(MistWings);
            UpgradeEffects.CrateCapacity += Mathf.RoundToInt(Value(DeepCrates));
            UpgradeEffects.DayGrowth *= 1f + Value(SunSeeds);
            UpgradeEffects.NightGrowth *= 1f + Value(MoonDew);
            UpgradeEffects.LampBurn *= 1f - Value(ThickOil);
            UpgradeEffects.WoodBonus += Mathf.RoundToInt(Value(AxeTrade));
            UpgradeEffects.StoneBonus += Mathf.RoundToInt(Value(MasonTrade));
            UpgradeEffects.WispXp *= 1f + Value(WiseWisps);
            UpgradeEffects.MerchantPrice += Value(GenerousSpirit);
            UpgradeEffects.GhostDiscount = Mathf.Min(0.8f, UpgradeEffects.GhostDiscount + Value(GhostFriend));
            UpgradeEffects.SaleFruit += Value(AncestralOrchard);
            UpgradeEffects.OrderBonus += Value(Regulars);
            UpgradeEffects.MutationChance += Value(FickleSeeds);
            UpgradeEffects.BoatDuration *= 1f - Value(MistWind);
            UpgradeEffects.SaleNight += Value(NightFair);
            UpgradeEffects.NestCapacity += Mathf.RoundToInt(Value(CozyNest));
            UpgradeEffects.InterestRate += Value(PiggyBank);
            UpgradeEffects.OrderSlots += Mathf.RoundToInt(Value(NoticeBoard));
            UpgradeEffects.FatigueMultiplier = Mathf.Min(1f, UpgradeEffects.FatigueMultiplier + Value(Tireless));
            UpgradeEffects.HeavyFatigueMultiplier = UpgradeEffects.FatigueMultiplier * 0.5f;
            UpgradeEffects.WorkWage += Mathf.RoundToInt(Value(DayWage));
            UpgradeEffects.CritMult += Value(GoldenFleece);
            UpgradeEffects.ShardChance += Value(LuckyShard);
        }

        /// <summary>Perły za rejs teraz (z zarobku tego rejsu; Perłowa łaska dokłada 25%).</summary>
        public static int PendingPearls =>
            (int)Math.Floor(Math.Sqrt(Market.RunCoins / CoinsPerPearlUnit) * (1.0 + Value(PearlGrace)));

        /// <summary>Ile trzeba zarobić w tym rejsie (łącznie) na kolejną perłę.</summary>
        public static long CoinsForNextPearl
        {
            get
            {
                double n = (PendingPearls + 1) / (1.0 + Value(PearlGrace));
                return (long)Math.Ceiling(n * n * CoinsPerPearlUnit);
            }
        }

        public static bool CanSail => PendingPearls >= 1;

        /// <summary>Perły na coś innego niż losowanie (czapki ogników).</summary>
        public static bool TrySpendPearls(int n)
        {
            if (n <= 0 || Pearls < n)
                return false;
            Pearls -= n;
            return true;
        }

        /// <summary>Do testów (HUD debugowy): perły do wydania w Przystani (liczą się też do blasku).</summary>
        public static void DebugAddPearls(int n)
        {
            Pearls += n;
            Earned += n;
        }

        // ------------------------------------------------------------ zapis

        public static void Restore(PrestigeSave s)
        {
            levels.Clear();
            Pearls = s != null ? Mathf.Max(0, s.pearls) : 0;
            Earned = s != null ? Mathf.Max(0, s.earned) : 0;
            Voyages = s != null ? Mathf.Max(0, s.voyages) : 0;
            Hats.Restore(s != null ? s.hats : null);
            if (s != null && s.gifts != null)
                foreach (var g in s.gifts)
                    if (!string.IsNullOrEmpty(g.id) && g.level > 0 && Find(g.id) != null)
                        levels[g.id] = Mathf.Min(g.level, Find(g.id).MaxLevel);
        }

        public static PrestigeSave Snapshot()
        {
            var s = new PrestigeSave { pearls = Pearls, earned = Earned, voyages = Voyages, hats = Hats.Snapshot() };
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
