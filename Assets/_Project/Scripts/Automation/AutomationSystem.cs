using UnityEngine;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Upgrades;

namespace Wyspa.Automation
{
    /// <summary>
    /// Automatyzacja (GDD §11.3 E): włącza obiekty według kupionych ulepszeń i prowadzi ich ticki.
    /// Ognik-ogrodnik i skrzynia kupca pojawiają się na wyspie dopiero po zakupie.
    /// </summary>
    public class AutomationSystem : MonoBehaviour
    {
        [SerializeField] HarvestCrate crate;
        [SerializeField] WispGardener wisp;
        [SerializeField] MerchantChest merchantChest;
        [SerializeField] GameObject runeStone;

        public HarvestCrate Crate => crate;
        public WispGardener Wisp => wisp;
        public MerchantChest MerchantChest => merchantChest;

        public void Setup(HarvestCrate harvestCrate, WispGardener wispGardener, MerchantChest chest, GameObject rune)
        {
            crate = harvestCrate;
            wisp = wispGardener;
            merchantChest = chest;
            runeStone = rune;
        }

        public void ApplyUpgrades(UpgradeState upgrades)
        {
            SetActive(wisp != null ? wisp.gameObject : null, upgrades.Has(UpgradeIds.WispGardener));
            SetActive(merchantChest != null ? merchantChest.gameObject : null, upgrades.Has(UpgradeIds.MerchantChest));
            SetActive(runeStone, upgrades.Has(UpgradeIds.SoilMemory));
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on)
                go.SetActive(on);
        }

        /// <summary>Tick automatyki. Zwraca grządkę zebraną przez ognika i jej roślinę (do pamięci ziemi).</summary>
        public FarmPlot Tick(float dt, UpgradeState upgrades, Wallet wallet, CropDatabase crops, out CropDefinition harvestedCrop)
        {
            FarmPlot harvested = null;
            harvestedCrop = null;
            if (wisp != null && upgrades.Has(UpgradeIds.WispGardener))
            {
                var plot = wisp.Tick(dt, upgrades.Value(UpgradeIds.WispGardener, 30f), crate);
                if (plot != null && crate.TryAdd(plot.State.crop))
                {
                    harvestedCrop = plot.HarvestSilently();
                    harvested = plot;
                }
            }
            if (merchantChest != null && upgrades.Has(UpgradeIds.MerchantChest))
                merchantChest.Tick(dt, upgrades.Value(UpgradeIds.MerchantChest, 3600f), crate, wallet, crops);
            return harvested;
        }
    }
}
