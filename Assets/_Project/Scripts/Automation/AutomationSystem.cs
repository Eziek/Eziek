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
        /// <summary>Budowle ogników (GDD §9 p. 103): ognisko (spotkania, nastrój) i sennik (odpoczynek).</summary>
        public const string Bonfire = "bonfire", DreamHouse = "dreamhouse";
        public const string Kiln = "ember_kiln", Crystal = "ember_crystal";   // generatory Żaru
        /// <summary>Domek chowańca: "home_" + id chowańca (jeden na chowańca, gdy już przyszedł).</summary>
        public const string HomePrefix = "home_";
        /// <summary>Zapisy sprzed roju: ognik-ogrodnik na własnym kafelku (wczytanie zamienia go na gniazdo z ognikiem).</summary>
        public const string Wisp = "wisp";
        public static readonly string[] Kinds = BuildKinds();

        static string[] BuildKinds()
        {
            var list = new List<string> { Crate, Nest, Merchant, Rune, Cauldron, Store, Kiln, Crystal, Bonfire, DreamHouse };
            foreach (var d in Familiars.FamiliarSystem.Defs)
                list.Add(HomePrefix + d.id);
            return list.ToArray();
        }

        /// <summary>Id chowańca dla domku ("home_fox" → "fox"), null dla innych budowli.</summary>
        public static string HomeOf(string kind) =>
            kind != null && kind.StartsWith(HomePrefix) ? kind.Substring(HomePrefix.Length) : null;

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
                case Bonfire: return "bld.bonfire";
                case DreamHouse: return "bld.dreamhouse";
                case Kiln: return "bld.ember_kiln";
                case Crystal: return "bld.ember_crystal";
                default: return HomeOf(kind) != null ? "bld." + kind : kind;
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

        public static bool IsUnlocked(string kind, UpgradeState upgrades)
        {
            if (kind == Kiln || kind == Crystal)
                return true;   // generatory Żaru od początku gry
            var familiar = HomeOf(kind);
            if (familiar != null)
                return Familiars.FamiliarSystem.Has(familiar);   // domek dopiero, gdy chowaniec przyszedł
            return upgrades != null && upgrades.Has(RequiredUpgrade(kind));
        }

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
                case Bonfire: coins = 200; extra = new CurrencyAmount(CurrencyType.Stone, 8); break;
                case DreamHouse: coins = 250; extra = new CurrencyAmount(CurrencyType.Wood, 10); break;
                case Kiln: coins = 180; extra = new CurrencyAmount(CurrencyType.Stone, 12); break;
                case Crystal: coins = 250; extra = new CurrencyAmount(CurrencyType.Stone, 15); break;
                default: coins = 150; extra = new CurrencyAmount(CurrencyType.Stone, 5); break;
            }
            if (HomeOf(kind) != null)
                return new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, 120), new CurrencyAmount(CurrencyType.Wood, 6) };   // jeden na chowańca
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
            GrantSower(upgrades.Has(UpgradeIds.SoilMemory));   // „Ognik-siewca”: darmowy siewca (runę dziczy kupuje się w sklepie)
        }

        /// <summary>Klucz w granted: darmowy siewca już był.</summary>
        public const string Sower = "sower";

        /// <summary>
        /// „Ognik-siewca” daje darmowego ognika-siewcę w pierwszym gnieździe (zgłoszenie gracza: wcześniej dawał runę dziczy).
        /// Gdy pierwsze gniazdo jest pełne, siewca trafia do innego z miejscem; gdy wszystkie pełne, czeka (toast) do
        /// następnego zakupu ulepszenia albo wczytania.
        /// </summary>
        void GrantSower(bool on)
        {
            if (!on || granted.Contains(Sower) || WispNest.All.Count == 0)
                return;
            WispNest target = null;
            foreach (var n in WispNest.All)
                if (n != null && n.HasRoom)
                {
                    target = n;
                    break;
                }
            if (target == null)
            {
                GameEvents.RaiseToast("toast.sower_waits");
                return;
            }
            if (target.AddWisp(WispKind.Sower) != null)
            {
                granted.Add(Sower);
                Visual.IslandFx.FindPopped(target.transform.position + new Vector3(0f, 1f, 0f));
                GameEvents.RaiseToast("toast.sower_joined");
            }
        }

        void Grant(Island island, string kind, bool on)
        {
            if (!on || granted.Contains(kind))
                return;
            // pierwsza sztuka jest darmowa i zawsze dostaje miejsce (pełna wyspa: darmowy kafelek obok)
            if (island.CountBuilding(kind) > 0 || island.SpawnBuildingNear(kind, true) != null)
                granted.Add(kind);   // gdyby nawet to się nie udało (toast): spróbuje przy następnym zakupie albo wczytaniu
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
            var b = island.SpawnBuildingNear(Nest, true);
            if (b == null)
                return;   // nie było nawet darmowego kafelka (toast): spróbuje przy następnym zakupie albo wczytaniu
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
            for (int i = EmberKiln.All.Count - 1; i >= 0; i--)
                EmberKiln.All[i].Tick(dt, game);   // generatory Żaru
            for (int i = EmberCrystal.All.Count - 1; i >= 0; i--)
                EmberCrystal.All[i].Tick(dt, game);
            for (int i = World.Monument.All.Count - 1; i >= 0; i--)
                World.Monument.All[i].Tick(dt, game);   // monumenty wysp fabularnych: budowa i dary
        }
    }
}
