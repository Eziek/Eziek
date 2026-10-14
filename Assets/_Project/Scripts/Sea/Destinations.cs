using System.Collections.Generic;

namespace Wyspa.Sea
{
    /// <summary>Co może przywieźć łódź (tabela łupów celu wyprawy).</summary>
    public enum LootKind { NightSeeds, Wood, Stone, Coins, Fertilizer, Shard, Hybrid }

    /// <summary>
    /// Cel wyprawy łodzią: czas w godzinach aktywnej pracy, liczba losowań,
    /// tabela łupów z wagami i własna pula zapisków lore (jeden nowy zapisek na wyprawę, po kolei). Kolejne cele odblokowują
    /// się liczbą odbytych wypraw, a Serce mgły - zapiskami ze wszystkich pozostałych celów.
    /// </summary>
    public class Destination
    {
        public string id;
        public float hours;
        public int rollsMin, rollsMax;
        /// <summary>Dodatkowe losowanie z rzadkich łupów (odłamek albo hybryda).</summary>
        public bool rareRoll;
        /// <summary>Ile wypraw trzeba odbyć, żeby cel się pokazał.</summary>
        public int minTrips;
        /// <summary>Cel wymaga przeczytania wszystkich zapisków z pozostałych celów.</summary>
        public bool needsAllLore;
        public (LootKind kind, int weight)[] loot;
        public string[] lore;

        public string NameKey => "exp." + id;
    }

    public static class Destinations
    {
        public static readonly Destination[] All =
        {
            new Destination
            {
                id = "wreck", hours = 2f, rollsMin = 2, rollsMax = 3,
                loot = new[] { (LootKind.Wood, 35), (LootKind.Coins, 30), (LootKind.Fertilizer, 15), (LootKind.Stone, 10), (LootKind.NightSeeds, 10) },
                lore = new[] { "lore.wreck.1", "lore.wreck.2", "lore.wreck.3", "lore.wreck.4" },
            },
            new Destination
            {
                id = "reef", hours = 2f, rollsMin = 2, rollsMax = 3, minTrips = 1,
                loot = new[] { (LootKind.Stone, 35), (LootKind.NightSeeds, 25), (LootKind.Fertilizer, 15), (LootKind.Coins, 15), (LootKind.Shard, 10) },
                lore = new[] { "lore.reef.1", "lore.reef.2", "lore.reef.3", "lore.reef.4" },
            },
            new Destination
            {
                id = "tower", hours = 3f, rollsMin = 3, rollsMax = 4, minTrips = 3,
                loot = new[] { (LootKind.Shard, 25), (LootKind.Coins, 25), (LootKind.Stone, 20), (LootKind.NightSeeds, 15), (LootKind.Fertilizer, 10), (LootKind.Hybrid, 5) },
                lore = new[] { "lore.tower.1", "lore.tower.2", "lore.tower.3", "lore.tower.4" },
            },
            new Destination
            {
                id = "graveyard", hours = 4f, rollsMin = 5, rollsMax = 6, rareRoll = true, minTrips = 5,
                loot = new[] { (LootKind.Wood, 30), (LootKind.Coins, 25), (LootKind.Shard, 15), (LootKind.Hybrid, 10), (LootKind.NightSeeds, 10), (LootKind.Fertilizer, 10) },
                lore = new[] { "lore.graveyard.1", "lore.graveyard.2", "lore.graveyard.3", "lore.graveyard.4" },
            },
            new Destination
            {
                id = "heart", hours = 6f, rollsMin = 4, rollsMax = 5, rareRoll = true, needsAllLore = true,
                loot = new[] { (LootKind.Shard, 30), (LootKind.Hybrid, 25), (LootKind.Coins, 25), (LootKind.NightSeeds, 20) },
                lore = new[] { "lore.heart.1", "lore.heart.2", "lore.heart.3", "lore.heart.4", "lore.heart.5" },
            },
        };

        public static Destination Find(string id)
        {
            foreach (var d in All)
                if (d.id == id) return d;
            return null;
        }

        /// <summary>Zapiski ze wszystkich celów poza Sercem mgły: (odkryte, wszystkie).</summary>
        public static (int found, int total) OtherLore()
        {
            int found = 0, total = 0;
            foreach (var d in All)
            {
                if (d.needsAllLore) continue;
                found += Lore.LoreBook.FoundIn(d.lore);
                total += d.lore.Length;
            }
            return (found, total);
        }

        public static bool Unlocked(Destination d, int trips)
        {
            if (d.needsAllLore)
            {
                var (found, total) = OtherLore();
                return found >= total;
            }
            return trips >= d.minTrips;
        }

        /// <summary>Losowanie z wag tabeli (rare: tylko odłamek albo hybryda).</summary>
        public static LootKind Roll(Destination d, System.Random rnd, bool rare)
        {
            if (rare)
                return rnd.NextDouble() < 0.6 ? LootKind.Shard : LootKind.Hybrid;
            int sum = 0;
            foreach (var (_, w) in d.loot) sum += w;
            int v = rnd.Next(sum);
            foreach (var (k, w) in d.loot)
            {
                if (v < w) return k;
                v -= w;
            }
            return d.loot[0].kind;
        }
    }
}
