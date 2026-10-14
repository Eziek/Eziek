using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Upgrades
{
    /// <summary>
    /// Drzewko ulepszeń (prośba gracza: „na środku ognik-ogrodnik, rozchodzi się w drzewko, które się odkrywa”).
    /// Pozycje w siatce 12x7 (kolumna, wiersz od góry), ikona i zamiana wartości poziomu na liczbę do opisu.
    /// Gałęzie: rój (góra), Żar (lewo), ziemia (prawo), handel i morze (dół). Wymagania (rodzic) są w danych ulepszeń.
    /// </summary>
    public static class UpgradeTree
    {
        public const int Columns = 12, Rows = 7;

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
            { UpgradeIds.WispGardener, new Node { cell = new Vector2Int(6, 3), icon = "wisp", show = Same } },
            // rój
            { UpgradeIds.SoilMemory, new Node { cell = new Vector2Int(6, 2), icon = "sapling", show = Same } },
            { UpgradeIds.WispSpeed, new Node { cell = new Vector2Int(5, 1), icon = "wing", show = Percent } },
            { UpgradeIds.NestSize, new Node { cell = new Vector2Int(6, 1), icon = "plus", show = Same } },
            { UpgradeIds.WispXp, new Node { cell = new Vector2Int(7, 1), icon = "star", show = Percent } },
            { UpgradeIds.BigCrates, new Node { cell = new Vector2Int(6, 0), icon = "crate", show = Same } },
            // Żar
            { UpgradeIds.EssenceJar, new Node { cell = new Vector2Int(4, 3), icon = "essence", show = Same } },
            { UpgradeIds.Kindling, new Node { cell = new Vector2Int(3, 3), icon = "keys", show = Percent } },
            { UpgradeIds.SecondWind, new Node { cell = new Vector2Int(2, 2), icon = "fatigue", show = Whole } },
            { UpgradeIds.FocusStreak, new Node { cell = new Vector2Int(2, 4), icon = "focus", show = Whole } },
            { UpgradeIds.SpiderWicks, new Node { cell = new Vector2Int(4, 5), icon = "lamp", show = Less } },
            // ziemia
            { UpgradeIds.FertileSoil, new Node { cell = new Vector2Int(8, 3), icon = "sun", show = Percent } },
            { UpgradeIds.NightDew, new Node { cell = new Vector2Int(9, 3), icon = "drop", show = Percent } },
            { UpgradeIds.Crossbreed, new Node { cell = new Vector2Int(10, 2), icon = "seed_hybrid", show = Whole } },
            { UpgradeIds.WildRunes, new Node { cell = new Vector2Int(10, 4), icon = "ritual", show = Less } },
            // handel i morze
            { UpgradeIds.MerchantChest, new Node { cell = new Vector2Int(6, 4), icon = "shop", show = Same } },
            { UpgradeIds.FairTrade, new Node { cell = new Vector2Int(5, 5), icon = "coin", show = Whole } },
            { UpgradeIds.SwiftBoat, new Node { cell = new Vector2Int(7, 5), icon = "boat", show = Less } },
        };

        /// <summary>Środek całego drzewka w komórkach (do wyśrodkowania na stronie).</summary>
        public static Vector2 Middle
        {
            get
            {
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                foreach (var n in Nodes.Values)
                {
                    min = Vector2.Min(min, n.cell);
                    max = Vector2.Max(max, n.cell);
                }
                return (min + max) / 2f;
            }
        }

        public static bool InTree(string id) => id != null && Nodes.ContainsKey(id);
        public static Vector2Int Cell(string id) => Nodes.TryGetValue(id, out var n) ? n.cell : new Vector2Int(0, 0);
        public static string Icon(string id) => Nodes.TryGetValue(id, out var n) ? n.icon : "upgrade";

        /// <summary>Wartość poziomu w postaci do opisu (np. procent zamiast mnożnika).</summary>
        public static float Display(string id, float value) => Nodes.TryGetValue(id, out var n) ? n.show(value) : value;
    }
}
