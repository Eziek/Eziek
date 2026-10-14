using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Upgrades;
using Wyspa.World;

namespace Wyspa.Automation
{
    /// <summary>
    /// Automatyka (GDD §11.3 E, Etap 6b): budowle w wielu egzemplarzach - skrzynie, ogniki-ogrodnicy, duszki-handlarze, runy siewu.
    /// Ulepszenie z drzewka odblokowuje rodzaj budowli i daje pierwszą sztukę za darmo; kolejne kupuje się w Sklep → Budowle
    /// (cena rośnie z każdą sztuką). Ulepszenia dalej skracają odstępy pracy wszystkich ogników i duszków.
    /// </summary>
    public class AutomationSystem : MonoBehaviour
    {
        public const string Crate = "crate", Wisp = "wisp", Merchant = "merchant", Rune = "rune";
        public static readonly string[] Kinds = { Crate, Wisp, Merchant, Rune };

        /// <summary>Rodzaje, których darmowa pierwsza sztuka już była (w zapisie, żeby po przeniesieniu/usunięciu nie wracała).</summary>
        readonly HashSet<string> granted = new HashSet<string>();

        public static string NameKey(string kind)
        {
            switch (kind)
            {
                case Crate: return "bld.harvest_crate";
                case Wisp: return "bld.wisp_gardener";
                case Merchant: return "bld.merchant_spirit";
                case Rune: return "bld.seed_rune";
                default: return kind;
            }
        }

        /// <summary>Ulepszenie, które odblokowuje rodzaj budowli.</summary>
        public static string RequiredUpgrade(string kind)
        {
            switch (kind)
            {
                case Merchant: return UpgradeIds.MerchantChest;
                case Rune: return UpgradeIds.SoilMemory;
                default: return UpgradeIds.WispGardener;   // skrzynie i ogniki
            }
        }

        public static bool IsUnlocked(string kind, UpgradeState upgrades) => upgrades != null && upgrades.Has(RequiredUpgrade(kind));

        /// <summary>Koszt kolejnej sztuki: monety rosną x1.6 z każdą posiadaną, materiał stały (GDD §11.3 E, do balansu).</summary>
        public static List<CurrencyAmount> Cost(string kind, int owned)
        {
            int coins; CurrencyAmount extra;
            switch (kind)
            {
                case Crate: coins = 60; extra = new CurrencyAmount(CurrencyType.Wood, 4); break;
                case Wisp: coins = 250; extra = new CurrencyAmount(CurrencyType.NightHarvest, 5); break;
                case Merchant: coins = 500; extra = new CurrencyAmount(CurrencyType.Stone, 8); break;
                default: coins = 150; extra = new CurrencyAmount(CurrencyType.Stone, 5); break;
            }
            coins = Mathf.RoundToInt(coins * Mathf.Pow(1.6f, Mathf.Max(0, owned)) / 5f) * 5;
            return new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, coins), extra };
        }

        public List<string> SnapshotGranted() => new List<string>(granted);

        public void RestoreGranted(IEnumerable<string> list)
        {
            granted.Clear();
            if (list != null)
                foreach (var k in list)
                    if (!string.IsNullOrEmpty(k)) granted.Add(k);
        }

        /// <summary>Po zakupie ulepszenia (i po wczytaniu): pierwsza sztuka budowli staje sama na wolnym kafelku.</summary>
        public void ApplyUpgrades(UpgradeState upgrades)
        {
            var island = Island.Instance;
            if (island == null || !island.Ready || upgrades == null)
                return;
            bool wisp = upgrades.Has(UpgradeIds.WispGardener), merchant = upgrades.Has(UpgradeIds.MerchantChest);
            Grant(island, Crate, wisp || merchant);
            Grant(island, Wisp, wisp);
            Grant(island, Merchant, merchant);
            Grant(island, Rune, upgrades.Has(UpgradeIds.SoilMemory));
        }

        void Grant(Island island, string kind, bool on)
        {
            if (!on || granted.Contains(kind))
                return;
            if (island.CountBuilding(kind) > 0 || island.SpawnBuildingNear(kind) != null)
                granted.Add(kind);   // bez wolnego kafelka (toast) spróbuje przy następnym zakupie albo wczytaniu
        }

        /// <summary>Tick automatyki: ogniki wylatują na kursy, duszki sprzedają, runy sieją. harvested: zbiory do AfterHarvest (na razie puste - ogniki zgłaszają same).</summary>
        public void Tick(float dt, GameLoop game, List<(FarmPlot plot, CropDefinition crop)> harvested)
        {
            var upgrades = game.Upgrades;
            if (upgrades.Has(UpgradeIds.WispGardener))
            {
                float interval = upgrades.Value(UpgradeIds.WispGardener, 30f);
                // ogniki zbierają w trakcie lotu (kurs do 3 plonów) i same wołają GameLoop.AfterHarvest
                for (int i = WispGardener.All.Count - 1; i >= 0; i--)
                    WispGardener.All[i].Tick(dt, interval);
            }
            // duszki-handlarze sprzedają swoją skrzynię, gdy tylko się zapełni
            for (int i = MerchantSpirit.All.Count - 1; i >= 0; i--)
                MerchantSpirit.All[i].Tick(game.Wallet, game.Crops);
            for (int i = SeedRune.All.Count - 1; i >= 0; i--)
                SeedRune.All[i].Tick(dt, game);
        }
    }
}
