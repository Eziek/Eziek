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
    /// Automatyka (GDD §11.3 E): budowle w wielu egzemplarzach - skrzynie, gniazda ogników, duszki-handlarze,
    /// runy dziczy, kotły.
    /// Ulepszenie z drzewka odblokowuje rodzaj budowli i daje pierwszą sztukę za darmo; kolejne kupuje się w Sklep → Budowle
    /// (cena rośnie z każdą sztuką). Ulepszenia dalej skracają odstępy pracy wszystkich ogników i duszków.
    /// </summary>
    public class AutomationSystem : MonoBehaviour
    {
        public const string Crate = "crate", Nest = "nest", Merchant = "merchant", Rune = "rune", Cauldron = "cauldron", Store = "store";
        /// <summary>Zapisy sprzed roju: ognik-ogrodnik na własnym kafelku (wczytanie zamienia go na gniazdo z ognikiem).</summary>
        public const string Wisp = "wisp";
        public static readonly string[] Kinds = { Crate, Nest, Merchant, Rune, Cauldron, Store };

        /// <summary>Rodzaje, których darmowa pierwsza sztuka już była (w zapisie, żeby po przeniesieniu/usunięciu nie wracała).</summary>
        readonly HashSet<string> granted = new HashSet<string>();

        public static string NameKey(string kind)
        {
            switch (kind)
            {
                case Crate: return "bld.harvest_crate";
                case Nest: return "bld.wisp_nest";
                case Merchant: return "bld.merchant_spirit";
                case Rune: return "bld.seed_rune";
                case Cauldron: return "bld.cauldron";
                case Store: return "bld.storehouse";
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
                case Nest: coins = 150; extra = new CurrencyAmount(CurrencyType.Wood, 6); break;
                case Merchant: coins = 500; extra = new CurrencyAmount(CurrencyType.Stone, 8); break;
                case Cauldron: coins = 300; extra = new CurrencyAmount(CurrencyType.Stone, 6); break;
                case Store: coins = 120; extra = new CurrencyAmount(CurrencyType.Wood, 10); break;
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
            GrantNest(island, wisp);
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

        /// <summary>Darmowe gniazdo z ognikiem-ogrodnikiem (ulepszenie „Ognik-ogrodnik”; zapisy sprzed roju miały „wisp”).</summary>
        void GrantNest(Island island, bool on)
        {
            if (!on || granted.Contains(Nest))
                return;
            if (granted.Contains(Wisp) || island.CountBuilding(Nest) > 0)
            {
                granted.Add(Nest);
                return;
            }
            var b = island.SpawnBuildingNear(Nest);
            if (b == null)
                return;   // bez wolnego kafelka (toast) spróbuje przy następnym zakupie albo wczytaniu
            granted.Add(Nest);
            if (b.TryGetComponent(out WispNest nest))
                nest.AddWisp(WispKind.Gardener);
        }

        /// <summary>Tick automatyki: duszki sprzedają, runy dziczy rosną. Ogniki pracują same (Wisp), harvested zostaje puste.</summary>
        public void Tick(float dt, GameLoop game, List<(FarmPlot plot, CropDefinition crop)> harvested)
        {
            // duszki-handlarze sprzedają swoją skrzynię, gdy tylko się zapełni
            for (int i = MerchantSpirit.All.Count - 1; i >= 0; i--)
                MerchantSpirit.All[i].Tick(game.Wallet, game.Crops);
            for (int i = WildRune.All.Count - 1; i >= 0; i--)
                WildRune.All[i].Tick(dt, game);
            for (int i = Storehouse.All.Count - 1; i >= 0; i--)
                Storehouse.All[i].Tick(dt, game);
        }
    }
}
