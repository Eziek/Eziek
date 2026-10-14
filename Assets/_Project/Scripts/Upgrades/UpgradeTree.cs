using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Upgrades
{
    /// <summary>
    /// Drzewko ulepszeń (prośba gracza: „na środku ognik-ogrodnik, rozchodzi się w drzewko, które się odkrywa”).
    /// Pozycje w siatce (kolumna, wiersz od góry; środek = ognik-ogrodnik w 13, 9), ikona i zamiana wartości poziomu na liczbę.
    /// Gałęzie: rój (góra), Żar (lewo), ziemia (prawo), handel i morze (dół). Wymagania (rodzic) są w danych ulepszeń.
    /// </summary>
    public static class UpgradeTree
    {

        struct Node
        {
            public Vector2Int cell;
            public string icon;
            public System.Func<float, float> show;
        }

        static float Percent(float v) => Mathf.Round((v - 1f) * 100f);    // 1.15 → 15
        static float Less(float v) => Mathf.Round((1f - v) * 100f);        // 0.85 → 15
        static float Whole(float v) => Mathf.Round(v * 100f);              // 0.65 → 65
        static float Same(float v) => v;

        static readonly Dictionary<string, Node> Nodes = new Dictionary<string, Node>
        {
            { UpgradeIds.WispGardener, new Node { cell = new Vector2Int(13, 9), icon = "wisp", show = Same } },
            // rój
            { UpgradeIds.SoilMemory, new Node { cell = new Vector2Int(13, 8), icon = "sapling", show = Same } },
            { UpgradeIds.WispSpeed, new Node { cell = new Vector2Int(12, 7), icon = "wing", show = Percent } },
            { UpgradeIds.NestSize, new Node { cell = new Vector2Int(13, 7), icon = "plus", show = Same } },
            { UpgradeIds.WispXp, new Node { cell = new Vector2Int(14, 7), icon = "star", show = Percent } },
            { UpgradeIds.BigCrates, new Node { cell = new Vector2Int(13, 6), icon = "crate", show = Same } },
            // Żar
            { UpgradeIds.EssenceJar, new Node { cell = new Vector2Int(11, 9), icon = "essence", show = Same } },
            { UpgradeIds.Kindling, new Node { cell = new Vector2Int(10, 9), icon = "keys", show = Percent } },
            { UpgradeIds.SecondWind, new Node { cell = new Vector2Int(9, 8), icon = "fatigue", show = Whole } },
            { UpgradeIds.FocusStreak, new Node { cell = new Vector2Int(9, 10), icon = "focus", show = Whole } },
            { UpgradeIds.SpiderWicks, new Node { cell = new Vector2Int(11, 11), icon = "lamp", show = Less } },
            // ziemia
            { UpgradeIds.FertileSoil, new Node { cell = new Vector2Int(15, 9), icon = "sun", show = Percent } },
            { UpgradeIds.NightDew, new Node { cell = new Vector2Int(16, 9), icon = "drop", show = Percent } },
            { UpgradeIds.Crossbreed, new Node { cell = new Vector2Int(17, 8), icon = "seed_hybrid", show = Whole } },
            { UpgradeIds.WildRunes, new Node { cell = new Vector2Int(17, 10), icon = "ritual", show = Less } },
            // handel i morze
            { UpgradeIds.MerchantChest, new Node { cell = new Vector2Int(13, 10), icon = "shop", show = Same } },
            { UpgradeIds.FairTrade, new Node { cell = new Vector2Int(12, 11), icon = "coin", show = Whole } },
            { UpgradeIds.SwiftBoat, new Node { cell = new Vector2Int(14, 11), icon = "boat", show = Less } },
            // Etap 8: handel i dochody
            { UpgradeIds.Stall, new Node { cell = new Vector2Int(12, 12), icon = "shop", show = Whole } },
            { UpgradeIds.Scales, new Node { cell = new Vector2Int(12, 13), icon = "scale", show = Whole } },
            { UpgradeIds.Haggle, new Node { cell = new Vector2Int(11, 13), icon = "coin", show = Whole } },
            { UpgradeIds.GoldenDeal, new Node { cell = new Vector2Int(10, 13), icon = "star", show = Same } },
            { UpgradeIds.Bulk, new Node { cell = new Vector2Int(13, 13), icon = "crate", show = Whole } },
            { UpgradeIds.EagerMerchant, new Node { cell = new Vector2Int(13, 14), icon = "wisp", show = Same } },
            { UpgradeIds.Reputation, new Node { cell = new Vector2Int(12, 14), icon = "bestiary", show = Whole } },
            { UpgradeIds.DailyDemand, new Node { cell = new Vector2Int(11, 14), icon = "sun", show = Same } },
            { UpgradeIds.MarketDay, new Node { cell = new Vector2Int(11, 15), icon = "shop", show = Percent } },
            { UpgradeIds.Combo, new Node { cell = new Vector2Int(10, 14), icon = "focus", show = Whole } },
            { UpgradeIds.Interest, new Node { cell = new Vector2Int(12, 15), icon = "coin", show = Whole } },
            { UpgradeIds.CrateMarket, new Node { cell = new Vector2Int(13, 15), icon = "crate", show = Whole } },
            { UpgradeIds.CoinPurse, new Node { cell = new Vector2Int(10, 15), icon = "book", show = Whole } },
            { UpgradeIds.MoreOrders, new Node { cell = new Vector2Int(9, 15), icon = "plus", show = Same } },
            { UpgradeIds.QuickOrders, new Node { cell = new Vector2Int(10, 16), icon = "wing", show = Same } },
            { UpgradeIds.VipOrders, new Node { cell = new Vector2Int(9, 16), icon = "star", show = Whole } },
            { UpgradeIds.OrderSeeds, new Node { cell = new Vector2Int(11, 16), icon = "seed_hybrid", show = Whole } },
            { UpgradeIds.GhostHaggle, new Node { cell = new Vector2Int(14, 12), icon = "deepgift", show = Whole } },
            { UpgradeIds.LongStay, new Node { cell = new Vector2Int(15, 11), icon = "moon", show = Percent } },
            { UpgradeIds.GhostTip, new Node { cell = new Vector2Int(16, 11), icon = "coin", show = Same } },
            { UpgradeIds.GhostDiscount, new Node { cell = new Vector2Int(15, 12), icon = "shop", show = Whole } },
            { UpgradeIds.RichCargo, new Node { cell = new Vector2Int(14, 13), icon = "boat", show = Percent } },
            { UpgradeIds.SeaTrade, new Node { cell = new Vector2Int(15, 13), icon = "coin", show = Same } },
            { UpgradeIds.LumberTrade, new Node { cell = new Vector2Int(12, 5), icon = "wood", show = Same } },
            { UpgradeIds.StoneTrade, new Node { cell = new Vector2Int(14, 5), icon = "stone", show = Same } },
            { UpgradeIds.BigStorehouse, new Node { cell = new Vector2Int(13, 5), icon = "crate", show = Same } },
            { UpgradeIds.WispTrade, new Node { cell = new Vector2Int(13, 4), icon = "wisp", show = Whole } },
            { UpgradeIds.WispTips, new Node { cell = new Vector2Int(12, 4), icon = "coin", show = Same } },
            { UpgradeIds.SunHarvest, new Node { cell = new Vector2Int(15, 8), icon = "sun", show = Whole } },
            { UpgradeIds.FreshPicked, new Node { cell = new Vector2Int(15, 7), icon = "sapling", show = Whole } },
            { UpgradeIds.BusyHands, new Node { cell = new Vector2Int(16, 8), icon = "keys", show = Whole } },
            { UpgradeIds.RootsMarket, new Node { cell = new Vector2Int(16, 7), icon = "seed_carrot", show = Whole } },
            { UpgradeIds.GrainGuild, new Node { cell = new Vector2Int(16, 6), icon = "seed_wheat", show = Whole } },
            { UpgradeIds.PumpkinPatch, new Node { cell = new Vector2Int(15, 6), icon = "seed_pumpkin", show = Whole } },
            { UpgradeIds.MorningMarket, new Node { cell = new Vector2Int(15, 5), icon = "sun", show = Whole } },
            { UpgradeIds.Herbalist, new Node { cell = new Vector2Int(16, 5), icon = "seed_poppy", show = Whole } },
            { UpgradeIds.MoonMarket, new Node { cell = new Vector2Int(18, 9), icon = "moon", show = Whole } },
            { UpgradeIds.OwlTrader, new Node { cell = new Vector2Int(19, 9), icon = "bestiary", show = Whole } },
            { UpgradeIds.LampMarket, new Node { cell = new Vector2Int(20, 9), icon = "lamp", show = Whole } },
            { UpgradeIds.GraveGoods, new Node { cell = new Vector2Int(18, 10), icon = "seed_corpseshroom", show = Whole } },
            { UpgradeIds.FlowerTrade, new Node { cell = new Vector2Int(19, 10), icon = "seed_moonflower", show = Whole } },
            { UpgradeIds.NightYield, new Node { cell = new Vector2Int(18, 11), icon = "nightcrop", show = Same } },
            { UpgradeIds.FullMoonFair, new Node { cell = new Vector2Int(19, 11), icon = "moon", show = Same } },
            { UpgradeIds.StarQuality, new Node { cell = new Vector2Int(17, 7), icon = "star", show = Whole } },
            { UpgradeIds.HybridFame, new Node { cell = new Vector2Int(18, 8), icon = "seed_hybrid", show = Whole } },
            { UpgradeIds.CarefulHands, new Node { cell = new Vector2Int(18, 7), icon = "focus", show = Whole } },
            { UpgradeIds.SeedSaver, new Node { cell = new Vector2Int(19, 8), icon = "seed_wheat", show = Whole } },
            { UpgradeIds.Collector, new Node { cell = new Vector2Int(19, 7), icon = "bestiary", show = Whole } },
            { UpgradeIds.Heirloom, new Node { cell = new Vector2Int(17, 6), icon = "seed_pumpkin", show = Whole } },
            { UpgradeIds.LuckyCoin, new Node { cell = new Vector2Int(20, 8), icon = "starshard", show = Whole } },
            { UpgradeIds.OrchardStall, new Node { cell = new Vector2Int(17, 11), icon = "fruit_apple", show = Whole } },
            { UpgradeIds.RipeFruit, new Node { cell = new Vector2Int(17, 12), icon = "fruit_elder", show = Percent } },
            { UpgradeIds.HeavyBranches, new Node { cell = new Vector2Int(18, 12), icon = "fruit_rowan", show = Whole } },
            { UpgradeIds.Grafting, new Node { cell = new Vector2Int(18, 13), icon = "sapling", show = Whole } },
            { UpgradeIds.MoonFruit, new Node { cell = new Vector2Int(17, 13), icon = "fruit_moon", show = Same } },
            { UpgradeIds.EmberCoins, new Node { cell = new Vector2Int(11, 8), icon = "essence", show = Same } },
            { UpgradeIds.WorkWage, new Node { cell = new Vector2Int(10, 8), icon = "mouse", show = Same } },
            { UpgradeIds.LandRent, new Node { cell = new Vector2Int(8, 8), icon = "plus", show = Same } },
            { UpgradeIds.FocusSeller, new Node { cell = new Vector2Int(9, 11), icon = "focus", show = Whole } },
            { UpgradeIds.RestedSeller, new Node { cell = new Vector2Int(9, 7), icon = "break", show = Whole } },
        };

        /// <summary>Lewy górny róg drzewka w komórkach (drzewko rysowane od tego miejsca, strona przewija się w obie strony).</summary>
        public static Vector2Int Min
        {
            get
            {
                var min = new Vector2Int(int.MaxValue, int.MaxValue);
                foreach (var n in Nodes.Values)
                    min = Vector2Int.Min(min, n.cell);
                return min;
            }
        }

        public static bool InTree(string id) => id != null && Nodes.ContainsKey(id);
        public static Vector2Int Cell(string id) => Nodes.TryGetValue(id, out var n) ? n.cell : new Vector2Int(0, 0);
        public static string Icon(string id) => Nodes.TryGetValue(id, out var n) ? n.icon : "upgrade";

        /// <summary>Wartość poziomu w postaci do opisu (np. procent zamiast mnożnika).</summary>
        public static float Display(string id, float value) => Nodes.TryGetValue(id, out var n) ? n.show(value) : value;
    }
}
