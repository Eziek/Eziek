using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Input;
using Wyspa.Lighting;
using Wyspa.Save;
using Wyspa.UI;
using Wyspa.Upgrades;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.EditorTools
{
    /// <summary>
    /// Menu "Wyspa": ustawienia projektu, dane (rośliny, lampy) i scena gry MainLevel zbudowana z prototype_layout.json.
    /// Wszystko można uruchamiać wielokrotnie - istniejące dane są aktualizowane, scena budowana od nowa.
    /// </summary>
    public static class WyspaSetup
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string ScenePath = Root + "/Scenes/MainLevel.unity";
        const string OldScenePath = Root + "/Scenes/Prototype.unity";   // dawna nazwa sceny: przenoszona przy budowie
        const string LayoutPath = Root + "/Art/prototype_layout.json";
        const string Sprites = PixelArtImporter.SpritesRoot;
        const int Ppu = PixelArtImporter.PixelsPerUnit;
        const int WindowScale = 3;

        static readonly Color OutlineColor = new Color32(0x0f, 0x0a, 0x10, 0xff);

        // ---------- układ z prototypu (współrzędne w pikselach kadru, y w dół) ----------
        [Serializable] class Layout { public int width; public int height; public int ppu; public PlotL[] plots; public ObjL[] objects; public LampL[] lamps; }
        [Serializable] class PlotL { public int row; public int col; public int x; public int y; public int cropX; public int cropY; }
        [Serializable] class ObjL { public string sprite; public int x; public int y; }
        [Serializable] class LampL { public string id; public string sprite; public int x; public int y; public int lightX; public int lightY; public float radius; public float r; public float g; public float b; }

        struct CropSpec
        {
            public string id, name, sprite;
            public CropKind kind;
            public float seconds;
            public int seed, sell;
            public bool fullMoon, hybrid;
            public string parentA, parentB, seedIcon;
            public int unlock;   // cena odblokowania w Sklep → Nasiona (0 = od początku)
        }
        struct LampSpec { public string name; public int price; public float perMinute, growth; public bool flicker; }

        // wzrost roślin dnia x3 szybciej (marchew 10 min → 3:20)
        // fazy roślin (pliki crop_<id>_<faza>.png) i zapasowa faza, gdy dodatkowej grafiki jeszcze nie ma
        static readonly string[] StageFiles = { "0", "shoot", "1", "2", "unripe", "3" };
        static readonly string[] StageFallback = { "0", "0", "1", "2", "3", "3" };

        // balans §9 p. 98: rośliny dnia 20 s - 3 min pracy, nocy 1,5-10 min ciemności; mniejsze ceny za sztukę
        static readonly CropSpec[] CropSpecs =
        {
            new CropSpec { id = "carrot", name = "Marchew", sprite = "crop_carrot", kind = CropKind.Day, seconds = 20, seed = 1, sell = 3 },
            new CropSpec { id = "wheat", name = "Zboże", sprite = "crop_wheat", kind = CropKind.Day, seconds = 45, seed = 2, sell = 6 },
            new CropSpec { id = "pumpkin", name = "Dynia", sprite = "crop_pumpkin", kind = CropKind.Day, seconds = 120, seed = 5, sell = 18 },
            // do odblokowania w Sklep → Nasiona (unlock = monety, jednorazowo)
            new CropSpec { id = "garlic", name = "Czosnek", sprite = "crop_garlic", kind = CropKind.Day, seconds = 30, seed = 2, sell = 5, unlock = 300 },
            new CropSpec { id = "poppy", name = "Mak", sprite = "crop_poppy", kind = CropKind.Day, seconds = 75, seed = 4, sell = 12, unlock = 780 },
            new CropSpec { id = "wormwood", name = "Piołun", sprite = "crop_wormwood", kind = CropKind.Day, seconds = 180, seed = 8, sell = 30, unlock = 1800 },
            // rośliny nocy: czas = sekundy ciemności (rosną też offline). Noc cyklu gry trwa 1200 s (20 min), każda roślina nocy
            // dojrzewa w jedną noc (najdłużej cmentarny bluszcz: 10 min), krótsze da się w nocy zebrać kilka razy
            new CropSpec { id = "corpseshroom", name = "Trupi grzyb", sprite = "crop_corpseshroom", kind = CropKind.Night, seconds = 120, seed = 4, sell = 14 },
            new CropSpec { id = "moonflower", name = "Księżycowy kwiat", sprite = "crop_moonflower", kind = CropKind.Night, seconds = 180, seed = 5, sell = 19 },
            new CropSpec { id = "ghostpumpkin", name = "Upiorna dynia", sprite = "crop_ghostpumpkin", kind = CropKind.Night, seconds = 360, seed = 8, sell = 34 },
            new CropSpec { id = "bloodrose", name = "Krwawa róża", sprite = "crop_bloodrose", kind = CropKind.Night, seconds = 300, seed = 12, sell = 60, fullMoon = true },
            new CropSpec { id = "belladonna", name = "Wilcza jagoda", sprite = "crop_belladonna", kind = CropKind.Night, seconds = 90, seed = 3, sell = 10, unlock = 960 },
            new CropSpec { id = "mandrake", name = "Mandragora", sprite = "crop_mandrake", kind = CropKind.Night, seconds = 300, seed = 9, sell = 40, unlock = 2400 },
            new CropSpec { id = "graveivy", name = "Cmentarny bluszcz", sprite = "crop_graveivy", kind = CropKind.Night, seconds = 600, seed = 15, sell = 80, unlock = 4800 },
            // hybryda: nasiona tylko z mutacji dyni i upiornej dyni na sąsiednich grządkach
            new CropSpec { id = "hybridpumpkin", name = "Dynia-upiór", sprite = "crop_hybridpumpkin", kind = CropKind.Night, seconds = 360, seed = 0, sell = 50,
                           hybrid = true, parentA = "pumpkin", parentB = "ghostpumpkin", seedIcon = "seed_hybrid" },
            // hybrydy z mutacji (treść: 5 nowych par, część dnia, krwawy piołun tylko w pełnię); ikony nasion icon_seed_<id>
            new CropSpec { id = "wolfcarrot", name = "Wilcza marchew", sprite = "crop_wolfcarrot", kind = CropKind.Day, seconds = 90, seed = 0, sell = 15,
                           hybrid = true, parentA = "carrot", parentB = "belladonna" },
            new CropSpec { id = "ashwheat", name = "Popielne zboże", sprite = "crop_ashwheat", kind = CropKind.Day, seconds = 150, seed = 0, sell = 25,
                           hybrid = true, parentA = "wheat", parentB = "corpseshroom" },
            new CropSpec { id = "moonpoppy", name = "Księżycowy mak", sprite = "crop_moonpoppy", kind = CropKind.Night, seconds = 300, seed = 0, sell = 50,
                           hybrid = true, parentA = "poppy", parentB = "moonflower" },
            new CropSpec { id = "screamgarlic", name = "Wrzeszczący czosnek", sprite = "crop_screamgarlic", kind = CropKind.Night, seconds = 420, seed = 0, sell = 70,
                           hybrid = true, parentA = "garlic", parentB = "mandrake" },
            new CropSpec { id = "bloodwormwood", name = "Krwawy piołun", sprite = "crop_bloodwormwood", kind = CropKind.Night, seconds = 300, seed = 0, sell = 140,
                           fullMoon = true, hybrid = true, parentA = "wormwood", parentB = "bloodrose" },
        };

        struct UpgradeSpec
        {
            public string id, name;
            public UpgradeCategory category;
            public (float value, CurrencyAmount[] cost)[] levels;
            public string[] requires;
        }

        static CurrencyAmount Coins(int n) => new CurrencyAmount(CurrencyType.Coins, n);
        static CurrencyAmount Night(int n) => new CurrencyAmount(CurrencyType.NightHarvest, n);

        // GDD §11.3 (wartości wstępne do balansu)
        static CurrencyAmount Wood(int n) => new CurrencyAmount(CurrencyType.Wood, n);
        static CurrencyAmount Stone(int n) => new CurrencyAmount(CurrencyType.Stone, n);
        static CurrencyAmount Shard(int n) => new CurrencyAmount(CurrencyType.StarShard, n);

        static (float, CurrencyAmount[]) L(float value, params CurrencyAmount[] cost) => (value, cost);

        // GDD §11.3 (wartości wstępne do balansu). Drzewko: środek = ognik-ogrodnik, wymagania = rodzic w drzewku,
        // pozycje i ikony w Upgrades/UpgradeTree.cs. Wartość poziomu: mnożnik, liczba albo szansa (patrz UpgradeEffects).
        static readonly UpgradeSpec[] UpgradeSpecs =
        {
            new UpgradeSpec { id = UpgradeIds.WispGardener, name = "Ognik-ogrodnik", category = UpgradeCategory.Automation,
                levels = new[] { L(30f, Coins(100)) } },   // 6d: pierwszy ognik w ciągu ok. 1-3 h pracy
            // rój (góra)
            new UpgradeSpec { id = UpgradeIds.SoilMemory, name = "Ognik-siewca", category = UpgradeCategory.Automation,
                levels = new[] { L(1f, Coins(500)) }, requires = new[] { UpgradeIds.WispGardener } },
            new UpgradeSpec { id = UpgradeIds.WispSpeed, name = "Lekkie skrzydła", category = UpgradeCategory.Automation,
                levels = new[] { L(1.15f, Coins(400)), L(1.3f, Coins(1200), Night(3)) }, requires = new[] { UpgradeIds.SoilMemory } },
            new UpgradeSpec { id = UpgradeIds.NestSize, name = "Przestronne gniazdo", category = UpgradeCategory.Automation,
                levels = new[] { L(4f, Coins(600), Wood(10)), L(5f, Coins(1500), Wood(20)) }, requires = new[] { UpgradeIds.SoilMemory } },
            new UpgradeSpec { id = UpgradeIds.WispXp, name = "Bystre ogniki", category = UpgradeCategory.Automation,
                levels = new[] { L(1.5f, Coins(800), Night(5)) }, requires = new[] { UpgradeIds.SoilMemory } },
            new UpgradeSpec { id = UpgradeIds.BigCrates, name = "Pojemne skrzynie", category = UpgradeCategory.Automation,
                levels = new[] { L(30f, Coins(300), Wood(10)), L(40f, Coins(900), Wood(20)) }, requires = new[] { UpgradeIds.NestSize } },
            // Żar (lewo)
            new UpgradeSpec { id = UpgradeIds.EssenceJar, name = "Słój Żaru", category = UpgradeCategory.Essence,
                levels = new[] { L(120f, Coins(200)), L(240f, Coins(800)), L(480f, Coins(3000)) }, requires = new[] { UpgradeIds.WispGardener } },
            new UpgradeSpec { id = UpgradeIds.Kindling, name = "Rozpałka", category = UpgradeCategory.Essence,
                levels = new[] { L(1.1f, Coins(300)), L(1.2f, Coins(900)), L(1.3f, Coins(2500)) }, requires = new[] { UpgradeIds.EssenceJar } },
            new UpgradeSpec { id = UpgradeIds.SecondWind, name = "Drugi oddech", category = UpgradeCategory.Rhythm,
                levels = new[] { L(0.65f, Coins(600)), L(0.8f, Coins(1800)) }, requires = new[] { UpgradeIds.Kindling } },
            new UpgradeSpec { id = UpgradeIds.FocusStreak, name = "Głębokie skupienie", category = UpgradeCategory.Rhythm,
                levels = new[] { L(0.35f, Coins(500)), L(0.5f, Coins(1500)) }, requires = new[] { UpgradeIds.Kindling } },
            new UpgradeSpec { id = UpgradeIds.SpiderWicks, name = "Pajęcze knoty", category = UpgradeCategory.Lamps,
                levels = new[] { L(0.85f, Coins(300), Wood(5)), L(0.7f, Coins(1000), Wood(10)) }, requires = new[] { UpgradeIds.EssenceJar } },
            // ziemia (prawo)
            new UpgradeSpec { id = UpgradeIds.FertileSoil, name = "Żyzna gleba", category = UpgradeCategory.Island,
                levels = new[] { L(1.1f, Coins(250)), L(1.2f, Coins(800)), L(1.3f, Coins(2400)) }, requires = new[] { UpgradeIds.WispGardener } },
            new UpgradeSpec { id = UpgradeIds.NightDew, name = "Nocna rosa", category = UpgradeCategory.Island,
                levels = new[] { L(1.1f, Coins(400), Night(3)), L(1.2f, Coins(1200), Night(6)), L(1.3f, Coins(3000), Night(10)) },
                requires = new[] { UpgradeIds.FertileSoil } },
            new UpgradeSpec { id = UpgradeIds.Crossbreed, name = "Krzyżowanie", category = UpgradeCategory.Island,
                levels = new[] { L(0.05f, Coins(700), Night(5)), L(0.08f, Coins(2000), Night(10)) }, requires = new[] { UpgradeIds.NightDew } },
            new UpgradeSpec { id = UpgradeIds.WildRunes, name = "Dzikie runy", category = UpgradeCategory.Island,
                levels = new[] { L(0.7f, Coins(400), Stone(5)), L(0.5f, Coins(1200), Stone(10)) }, requires = new[] { UpgradeIds.NightDew } },
            // handel i morze (dół)
            new UpgradeSpec { id = UpgradeIds.MerchantChest, name = "Duszek-handlarz", category = UpgradeCategory.Automation,
                levels = new[] { L(3600f, Coins(1600)) }, requires = new[] { UpgradeIds.WispGardener } },
            new UpgradeSpec { id = UpgradeIds.FairTrade, name = "Uczciwy handel", category = UpgradeCategory.Automation,
                levels = new[] { L(0.95f, Coins(800)), L(1f, Coins(2400)) }, requires = new[] { UpgradeIds.MerchantChest } },
            new UpgradeSpec { id = UpgradeIds.SwiftBoat, name = "Szybka łódź", category = UpgradeCategory.Sea,
                levels = new[] { L(0.85f, Coins(600), Wood(10)), L(0.7f, Coins(1800), Wood(20)) }, requires = new[] { UpgradeIds.MerchantChest } },
            // 60 ulepszeń handlu i dochodów, wartości wstępne do balansu (GDD §11.3, docs/UPGRADES_TRADE.md)
            new UpgradeSpec { id = UpgradeIds.Stall, name = "Stragan", category = UpgradeCategory.Trade,
                levels = new[] { L(0.05f, Coins(1000)), L(0.1f, Coins(3000)), L(0.15f, Coins(9000)) }, requires = new[] { UpgradeIds.FairTrade } },
            new UpgradeSpec { id = UpgradeIds.Scales, name = "Uczciwa waga", category = UpgradeCategory.Trade,
                levels = new[] { L(0.05f, Coins(4000)), L(0.1f, Coins(12000)) }, requires = new[] { UpgradeIds.Stall } },
            new UpgradeSpec { id = UpgradeIds.Haggle, name = "Targowanie", category = UpgradeCategory.Trade,
                levels = new[] { L(0.05f, Coins(2500)), L(0.1f, Coins(7500)), L(0.15f, Coins(20000)) }, requires = new[] { UpgradeIds.Stall } },
            new UpgradeSpec { id = UpgradeIds.GoldenDeal, name = "Złoty interes", category = UpgradeCategory.Trade,
                levels = new[] { L(3f, Coins(10000), Shard(1)), L(4f, Coins(30000), Shard(3)) }, requires = new[] { UpgradeIds.Haggle } },
            new UpgradeSpec { id = UpgradeIds.Bulk, name = "Hurt", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(3000), Wood(20)), L(0.2f, Coins(9000), Wood(40)) }, requires = new[] { UpgradeIds.Stall } },
            new UpgradeSpec { id = UpgradeIds.EagerMerchant, name = "Gorliwy handlarz", category = UpgradeCategory.Trade,
                levels = new[] { L(0.5f, Coins(5000)) }, requires = new[] { UpgradeIds.Bulk } },
            new UpgradeSpec { id = UpgradeIds.Reputation, name = "Renoma", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(6000)), L(0.2f, Coins(18000)), L(0.3f, Coins(50000)) }, requires = new[] { UpgradeIds.Scales } },
            new UpgradeSpec { id = UpgradeIds.DailyDemand, name = "Popyt dnia", category = UpgradeCategory.Trade,
                levels = new[] { L(1.5f, Coins(4000)), L(2f, Coins(12000)), L(2.5f, Coins(36000)) }, requires = new[] { UpgradeIds.Haggle } },
            new UpgradeSpec { id = UpgradeIds.MarketDay, name = "Dzień targowy", category = UpgradeCategory.Trade,
                levels = new[] { L(1.25f, Coins(15000)), L(1.5f, Coins(45000)) }, requires = new[] { UpgradeIds.DailyDemand } },
            new UpgradeSpec { id = UpgradeIds.Combo, name = "Zbiór seryjny", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(5000)), L(0.2f, Coins(15000)), L(0.3f, Coins(40000)) }, requires = new[] { UpgradeIds.GoldenDeal } },
            new UpgradeSpec { id = UpgradeIds.Interest, name = "Odsetki", category = UpgradeCategory.Trade,
                levels = new[] { L(0.01f, Coins(20000)), L(0.02f, Coins(60000)), L(0.03f, Coins(150000)) }, requires = new[] { UpgradeIds.Reputation } },
            new UpgradeSpec { id = UpgradeIds.CrateMarket, name = "Targ skrzyń", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(6000)), L(0.2f, Coins(18000)) }, requires = new[] { UpgradeIds.EagerMerchant } },
            new UpgradeSpec { id = UpgradeIds.CoinPurse, name = "Sakiewka", category = UpgradeCategory.Trade,
                levels = new[] { L(0.15f, Coins(3000)), L(0.3f, Coins(9000)), L(0.5f, Coins(27000)) }, requires = new[] { UpgradeIds.MarketDay } },
            new UpgradeSpec { id = UpgradeIds.MoreOrders, name = "Pełna tablica", category = UpgradeCategory.Trade,
                levels = new[] { L(1f, Coins(5000), Wood(30)), L(2f, Coins(15000), Wood(60)) }, requires = new[] { UpgradeIds.CoinPurse } },
            new UpgradeSpec { id = UpgradeIds.QuickOrders, name = "Częste zlecenia", category = UpgradeCategory.Trade,
                levels = new[] { L(1f, Coins(8000)) }, requires = new[] { UpgradeIds.CoinPurse } },
            new UpgradeSpec { id = UpgradeIds.VipOrders, name = "Szlachetni klienci", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(10000)), L(0.2f, Coins(30000)) }, requires = new[] { UpgradeIds.MoreOrders } },
            new UpgradeSpec { id = UpgradeIds.OrderSeeds, name = "Zapłata w nasionach", category = UpgradeCategory.Trade,
                levels = new[] { L(0.15f, Coins(8000), Night(10)), L(0.3f, Coins(24000), Night(25)) }, requires = new[] { UpgradeIds.QuickOrders } },
            new UpgradeSpec { id = UpgradeIds.GhostHaggle, name = "Targ z widmem", category = UpgradeCategory.Trade,
                levels = new[] { L(0.2f, Coins(2500), Night(5)), L(0.4f, Coins(8000), Night(15)) }, requires = new[] { UpgradeIds.SwiftBoat } },
            new UpgradeSpec { id = UpgradeIds.LongStay, name = "Cierpliwy kupiec", category = UpgradeCategory.Trade,
                levels = new[] { L(1.5f, Coins(2000)), L(2f, Coins(6000)) }, requires = new[] { UpgradeIds.SwiftBoat } },
            new UpgradeSpec { id = UpgradeIds.GhostTip, name = "Widmowy napiwek", category = UpgradeCategory.Trade,
                levels = new[] { L(100f, Coins(3000)), L(300f, Coins(9000)), L(800f, Coins(27000)) }, requires = new[] { UpgradeIds.LongStay } },
            new UpgradeSpec { id = UpgradeIds.GhostDiscount, name = "Widmowe rabaty", category = UpgradeCategory.Trade,
                levels = new[] { L(0.15f, Coins(4000)), L(0.3f, Coins(12000)) }, requires = new[] { UpgradeIds.GhostHaggle } },
            new UpgradeSpec { id = UpgradeIds.RichCargo, name = "Bogaty ładunek", category = UpgradeCategory.Trade,
                levels = new[] { L(1.25f, Coins(5000), Wood(30)), L(1.5f, Coins(15000), Wood(60)) }, requires = new[] { UpgradeIds.GhostHaggle } },
            new UpgradeSpec { id = UpgradeIds.SeaTrade, name = "Morski handel", category = UpgradeCategory.Trade,
                levels = new[] { L(150f, Coins(6000)), L(400f, Coins(18000)) }, requires = new[] { UpgradeIds.RichCargo } },
            new UpgradeSpec { id = UpgradeIds.LumberTrade, name = "Handel drewnem", category = UpgradeCategory.Trade,
                levels = new[] { L(2f, Coins(1500), Wood(20)), L(4f, Coins(4500), Wood(50)) }, requires = new[] { UpgradeIds.BigCrates } },
            new UpgradeSpec { id = UpgradeIds.StoneTrade, name = "Kamieniarz", category = UpgradeCategory.Trade,
                levels = new[] { L(2f, Coins(1500), Stone(20)), L(4f, Coins(4500), Stone(50)) }, requires = new[] { UpgradeIds.BigCrates } },
            new UpgradeSpec { id = UpgradeIds.BigStorehouse, name = "Wielki skład", category = UpgradeCategory.Trade,
                levels = new[] { L(2f, Coins(4000), Wood(30), Stone(30)), L(3f, Coins(12000), Wood(60), Stone(60)) }, requires = new[] { UpgradeIds.BigCrates } },
            new UpgradeSpec { id = UpgradeIds.WispTrade, name = "Ognik-kupiec", category = UpgradeCategory.Trade,
                levels = new[] { L(1f, Coins(5000)), L(1.1f, Coins(15000)) }, requires = new[] { UpgradeIds.BigStorehouse } },
            new UpgradeSpec { id = UpgradeIds.WispTips, name = "Napiwki ogników", category = UpgradeCategory.Trade,
                levels = new[] { L(1f, Coins(3000)), L(2f, Coins(9000)), L(3f, Coins(27000)) }, requires = new[] { UpgradeIds.WispTrade } },
            new UpgradeSpec { id = UpgradeIds.SunHarvest, name = "Słoneczny zbiór", category = UpgradeCategory.Trade,
                levels = new[] { L(0.05f, Coins(1500)), L(0.1f, Coins(4500)), L(0.15f, Coins(13500)) }, requires = new[] { UpgradeIds.FertileSoil } },
            new UpgradeSpec { id = UpgradeIds.FreshPicked, name = "Prosto z grządki", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(2000)), L(0.2f, Coins(6000)), L(0.3f, Coins(18000)) }, requires = new[] { UpgradeIds.SunHarvest } },
            new UpgradeSpec { id = UpgradeIds.BusyHands, name = "Pracowite ręce", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(3000)), L(0.2f, Coins(9000)) }, requires = new[] { UpgradeIds.SunHarvest } },
            new UpgradeSpec { id = UpgradeIds.RootsMarket, name = "Korzenny targ", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(2500)), L(0.5f, Coins(7500)) }, requires = new[] { UpgradeIds.FreshPicked } },
            new UpgradeSpec { id = UpgradeIds.GrainGuild, name = "Cech młynarzy", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(3000)), L(0.5f, Coins(9000)) }, requires = new[] { UpgradeIds.RootsMarket } },
            new UpgradeSpec { id = UpgradeIds.PumpkinPatch, name = "Dyniowe pole", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(4000)), L(0.5f, Coins(12000)) }, requires = new[] { UpgradeIds.FreshPicked } },
            new UpgradeSpec { id = UpgradeIds.MorningMarket, name = "Poranny targ", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(4000)), L(0.5f, Coins(12000)) }, requires = new[] { UpgradeIds.PumpkinPatch } },
            new UpgradeSpec { id = UpgradeIds.Herbalist, name = "Zielarz", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(5000)), L(0.5f, Coins(15000)) }, requires = new[] { UpgradeIds.MorningMarket } },
            new UpgradeSpec { id = UpgradeIds.MoonMarket, name = "Księżycowy targ", category = UpgradeCategory.Trade,
                levels = new[] { L(0.05f, Coins(2000), Night(5)), L(0.1f, Coins(6000), Night(10)), L(0.15f, Coins(18000), Night(20)) }, requires = new[] { UpgradeIds.NightDew } },
            new UpgradeSpec { id = UpgradeIds.OwlTrader, name = "Sowi kupiec", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(4000), Night(10)), L(0.2f, Coins(12000), Night(20)) }, requires = new[] { UpgradeIds.MoonMarket } },
            new UpgradeSpec { id = UpgradeIds.LampMarket, name = "Latarnie kupieckie", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(6000)), L(0.2f, Coins(18000)) }, requires = new[] { UpgradeIds.OwlTrader } },
            new UpgradeSpec { id = UpgradeIds.GraveGoods, name = "Cmentarne dobra", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(4000), Night(10)), L(0.5f, Coins(12000), Night(20)) }, requires = new[] { UpgradeIds.MoonMarket } },
            new UpgradeSpec { id = UpgradeIds.FlowerTrade, name = "Kwiaciarka", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(4000), Night(10)), L(0.5f, Coins(12000), Night(20)) }, requires = new[] { UpgradeIds.OwlTrader } },
            new UpgradeSpec { id = UpgradeIds.NightYield, name = "Obfite noce", category = UpgradeCategory.Trade,
                levels = new[] { L(1f, Coins(5000), Night(15)), L(2f, Coins(15000), Night(30)) }, requires = new[] { UpgradeIds.GraveGoods } },
            new UpgradeSpec { id = UpgradeIds.FullMoonFair, name = "Jarmark pełni", category = UpgradeCategory.Trade,
                levels = new[] { L(1.5f, Coins(8000), Shard(1)), L(2f, Coins(24000), Shard(3)) }, requires = new[] { UpgradeIds.FlowerTrade } },
            new UpgradeSpec { id = UpgradeIds.StarQuality, name = "Gwiazdy jakości", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(5000), Night(10)), L(0.5f, Coins(15000), Night(25)) }, requires = new[] { UpgradeIds.Crossbreed } },
            new UpgradeSpec { id = UpgradeIds.HybridFame, name = "Sława hybryd", category = UpgradeCategory.Trade,
                levels = new[] { L(0.25f, Coins(4000), Night(10)), L(0.5f, Coins(12000), Night(20)), L(0.75f, Coins(36000), Night(40)) }, requires = new[] { UpgradeIds.Crossbreed } },
            new UpgradeSpec { id = UpgradeIds.CarefulHands, name = "Staranne ręce", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(6000)), L(0.2f, Coins(18000)) }, requires = new[] { UpgradeIds.StarQuality } },
            new UpgradeSpec { id = UpgradeIds.SeedSaver, name = "Zbieracz nasion", category = UpgradeCategory.Trade,
                levels = new[] { L(0.05f, Coins(5000)), L(0.1f, Coins(15000)) }, requires = new[] { UpgradeIds.HybridFame } },
            new UpgradeSpec { id = UpgradeIds.Collector, name = "Kolekcjoner", category = UpgradeCategory.Trade,
                levels = new[] { L(0.2f, Coins(8000)), L(0.4f, Coins(24000)) }, requires = new[] { UpgradeIds.CarefulHands } },
            new UpgradeSpec { id = UpgradeIds.Heirloom, name = "Pamiątkowe odmiany", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(2000)), L(0.2f, Coins(6000)), L(0.3f, Coins(18000)) }, requires = new[] { UpgradeIds.StarQuality } },
            new UpgradeSpec { id = UpgradeIds.LuckyCoin, name = "Szczęśliwa moneta", category = UpgradeCategory.Trade,
                levels = new[] { L(0.01f, Coins(10000), Shard(2)), L(0.02f, Coins(30000), Shard(5)) }, requires = new[] { UpgradeIds.SeedSaver } },
            new UpgradeSpec { id = UpgradeIds.OrchardStall, name = "Owocowy stragan", category = UpgradeCategory.Trade,
                levels = new[] { L(0.2f, Coins(2000)), L(0.4f, Coins(6000)), L(0.6f, Coins(18000)) }, requires = new[] { UpgradeIds.WildRunes } },
            new UpgradeSpec { id = UpgradeIds.RipeFruit, name = "Dojrzałe owoce", category = UpgradeCategory.Trade,
                levels = new[] { L(1.15f, Coins(3000)), L(1.3f, Coins(9000)) }, requires = new[] { UpgradeIds.OrchardStall } },
            new UpgradeSpec { id = UpgradeIds.HeavyBranches, name = "Ciężkie gałęzie", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(5000)), L(0.2f, Coins(15000)) }, requires = new[] { UpgradeIds.RipeFruit } },
            new UpgradeSpec { id = UpgradeIds.Grafting, name = "Szczepienie", category = UpgradeCategory.Trade,
                levels = new[] { L(0.2f, Coins(3000)), L(0.4f, Coins(9000)) }, requires = new[] { UpgradeIds.HeavyBranches } },
            new UpgradeSpec { id = UpgradeIds.MoonFruit, name = "Owoce pełni", category = UpgradeCategory.Trade,
                levels = new[] { L(1.5f, Coins(6000), Shard(1)), L(2f, Coins(18000), Shard(2)) }, requires = new[] { UpgradeIds.RipeFruit } },
            new UpgradeSpec { id = UpgradeIds.EmberCoins, name = "Żar w złoto", category = UpgradeCategory.Trade,
                levels = new[] { L(2f, Coins(3000)), L(4f, Coins(9000)), L(6f, Coins(27000)) }, requires = new[] { UpgradeIds.EssenceJar } },
            new UpgradeSpec { id = UpgradeIds.WorkWage, name = "Dniówka", category = UpgradeCategory.Trade,
                levels = new[] { L(1f, Coins(4000)), L(2f, Coins(12000)), L(3f, Coins(36000)) }, requires = new[] { UpgradeIds.EmberCoins } },
            new UpgradeSpec { id = UpgradeIds.LandRent, name = "Dzierżawa", category = UpgradeCategory.Trade,
                levels = new[] { L(5f, Coins(5000)), L(10f, Coins(15000)) }, requires = new[] { UpgradeIds.SecondWind } },
            new UpgradeSpec { id = UpgradeIds.FocusSeller, name = "Skupiony sprzedawca", category = UpgradeCategory.Trade,
                levels = new[] { L(0.1f, Coins(4000)), L(0.2f, Coins(12000)) }, requires = new[] { UpgradeIds.FocusStreak } },
            new UpgradeSpec { id = UpgradeIds.RestedSeller, name = "Wypoczęty kupiec", category = UpgradeCategory.Trade,
                levels = new[] { L(0.15f, Coins(4000)), L(0.3f, Coins(12000)) }, requires = new[] { UpgradeIds.SecondWind } },
        };

        static readonly Dictionary<string, LampSpec> LampSpecs = new Dictionary<string, LampSpec>
        {
            { "lantern", new LampSpec { name = "Krzywa latarnia", price = 30, perMinute = 0.1f, growth = 1f, flicker = true } },
            { "skull", new LampSpec { name = "Czaszka ze świecą", price = 15, perMinute = 0.05f, growth = 1f, flicker = true } },
            { "wisp", new LampSpec { name = "Klatka z ognikiem", price = 80, perMinute = 0.2f, growth = 1.5f, flicker = false } },
            { "mushrooms", new LampSpec { name = "Świecące grzyby", price = 10, perMinute = 0f, growth = 1f, flicker = false } },
        };

        // =====================================================================
        [MenuItem("Wyspa/1. Ustawienia projektu", priority = 1)]
        public static void ConfigureProject()
        {
            var layout = LoadLayout();
            int w = layout != null ? layout.width : 272;
            int h = layout != null ? layout.height : 200;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            // okno startowe = kadr wyspy 2x2 (144x112) x3; potem rośnie razem z wyspą (ViewController)
            w = StartView.x;
            h = StartView.y;
            PlayerSettings.defaultScreenWidth = w * WindowScale;
            PlayerSettings.defaultScreenHeight = h * WindowScale;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            // przezroczyste okno (DWM, opcja gracza) wymaga łańcucha wymiany bez „flip model” (GDD §12.7)
            PlayerSettings.useFlipModelSwapchain = false;
            // font UI powstaje w locie (UiRoot), a TMP szuka wtedy shadera przez Shader.Find - w buildzie musi być dołączony
            AlwaysIncludeShader("TextMeshPro/Mobile/Bitmap");

            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null)
                Debug.LogWarning("[Wyspa] Brak URP! Utwórz projekt z szablonu 'Universal 2D' albo przypisz URP Asset z 2D Rendererem w Project Settings > Graphics.");
            else if (!Uses2DRenderer(rp))
                Debug.LogWarning("[Wyspa] URP używa renderera 3D, więc światła 2D (dzień/noc, lampy) nie zadziałają. " +
                                 "Project: Create > Rendering > URP Asset (with 2D Renderer), potem ustaw go w " +
                                 "Project Settings > Graphics (Default Render Pipeline) i w Quality (Render Pipeline Asset) dla każdego poziomu. " +
                                 "Szczegóły: docs/UNITY_SETUP.md.");
            else
                Debug.Log("[Wyspa] URP z 2D Rendererem: OK.");

            Debug.Log($"[Wyspa] Player Settings: okno {w * WindowScale}x{h * WindowScale}, praca w tle włączona.");
        }

        /// <summary>Czy domyślny renderer URP to 2D Renderer (czyta pola assetu - klasa Renderer2D jest wewnętrzna).</summary>
        /// <summary>Dopisuje shader do Graphics Settings → Always Included Shaders (Unity nie wytnie go z buildu).</summary>
        static void AlwaysIncludeShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Wyspa] Brak shadera '{shaderName}': Window > TextMeshPro > Import TMP Essential Resources, potem Wyspa > 1 jeszcze raz.");
                return;
            }
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null)
                return;
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
                return;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Wyspa] Shader '{shaderName}' dodany do Always Included Shaders.");
        }

        static bool Uses2DRenderer(UniversalRenderPipelineAsset rp)
        {
            var so = new SerializedObject(rp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray || list.arraySize == 0)
                return false;
            var index = so.FindProperty("m_DefaultRendererIndex");
            int i = index != null ? Mathf.Clamp(index.intValue, 0, list.arraySize - 1) : 0;
            var data = list.GetArrayElementAtIndex(i).objectReferenceValue;
            return data != null && data.GetType().Name == "Renderer2DData";
        }

        // =====================================================================
        [MenuItem("Wyspa/2. Utwórz dane (rośliny, lampy)", priority = 2)]
        public static void CreateData()
        {
            AssetDatabase.Refresh();
            EnsureFolder(DataDir + "/Crops");
            EnsureFolder(DataDir + "/Lamps");

            var db = LoadOrCreate<CropDatabase>(DataDir + "/CropDatabase.asset");
            db.crops.Clear();
            foreach (var spec in CropSpecs)
            {
                var crop = LoadOrCreate<CropDefinition>($"{DataDir}/Crops/Crop_{spec.id}.asset");
                crop.id = spec.id;
                crop.displayName = spec.name;
                crop.kind = spec.kind;
                crop.growSeconds = spec.seconds;
                crop.seedPrice = spec.seed;
                crop.sellPrice = spec.sell;
                crop.requiresFullMoon = spec.fullMoon;
                crop.hybrid = spec.hybrid;
                crop.parentA = spec.parentA;
                crop.parentB = spec.parentB;
                crop.seedIcon = spec.seedIcon;
                crop.unlockPrice = spec.unlock;
                // 6 faz: dodatkowe kiełek i niedojrzała z tools/pixel-proto/export_unity_stages.py (brak pliku = faza sąsiednia)
                crop.stages = new Sprite[StageFiles.Length];
                crop.glowStages = new Sprite[StageFiles.Length];
                for (int i = 0; i < StageFiles.Length; i++)
                {
                    string f = StageFiles[i];
                    var stage = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Crops/{spec.sprite}_{f}.png");
                    crop.stages[i] = stage != null ? stage : LoadSprite($"Crops/{spec.sprite}_{StageFallback[i]}");
                    crop.glowStages[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Crops/{spec.sprite}_{f}_glow.png");
                }
                EditorUtility.SetDirty(crop);
                db.crops.Add(crop);
            }
            EditorUtility.SetDirty(db);

            var layout = LoadLayout();
            if (layout != null)
            {
                foreach (var l in layout.lamps)
                {
                    if (!LampSpecs.TryGetValue(l.id, out var spec))
                        continue;
                    var lamp = LoadOrCreate<LampDefinition>($"{DataDir}/Lamps/Lamp_{l.id}.asset");
                    lamp.id = l.id;
                    lamp.displayName = spec.name;
                    lamp.price = spec.price;
                    lamp.essencePerMinute = spec.perMinute;
                    lamp.growthMultiplier = spec.growth;
                    lamp.flicker = spec.flicker;
                    lamp.radius = l.radius / Ppu;
                    lamp.color = new Color(l.r, l.g, l.b);
                    lamp.intensity = 1f;
                    // wygląd do stawiania w trakcie gry: sprite'y i światło względem podstawy (jak w układzie prototypu)
                    lamp.sprite = LoadSprite("Lamps/" + l.sprite);
                    lamp.glowSprite = LoadSprite("Lamps/" + l.sprite + "_glow");
                    lamp.lightOffset = new Vector2((l.lightX - l.x - 0.5f) / Ppu, (l.y + 1 - l.lightY) / Ppu);
                    EditorUtility.SetDirty(lamp);
                }
            }

            // latarnia morska: lampa spoza układu prototypu, stawiana z Morza
            var lh = LoadOrCreate<LampDefinition>($"{DataDir}/Lamps/Lamp_lighthouse.asset");
            lh.id = "lighthouse";
            lh.displayName = "Latarnia morska";
            lh.price = 2000;
            lh.essencePerMinute = 0.3f;
            lh.growthMultiplier = 1f;
            lh.flicker = false;
            lh.radius = 5f;
            lh.color = new Color(0.95f, 0.88f, 0.68f);
            lh.intensity = 1.1f;
            lh.sprite = LoadSprite("Sea/lighthouse");
            lh.glowSprite = LoadSprite("Sea/lighthouse_glow");
            lh.lightOffset = new Vector2(0f, 45.5f / Ppu);   // środek okna latarni (glow) nad podstawą
            EditorUtility.SetDirty(lh);

            CreateUpgrades();
            CreateUiTheme();
            GlowMaterial();
            CreateFxMaterials();
            CreateIslandArt();
            AssetDatabase.SaveAssets();
            Debug.Log("[Wyspa] Dane utworzone w " + DataDir);
        }

        static UpgradeDatabase CreateUpgrades()
        {
            EnsureFolder(DataDir + "/Upgrades");
            var db = LoadOrCreate<UpgradeDatabase>(DataDir + "/UpgradeDatabase.asset");
            db.upgrades.Clear();
            var byId = new Dictionary<string, UpgradeDefinition>();
            foreach (var spec in UpgradeSpecs)
            {
                var u = LoadOrCreate<UpgradeDefinition>($"{DataDir}/Upgrades/Upgrade_{spec.id}.asset");
                u.id = spec.id;
                u.nameKey = $"upgrade.{spec.id}.name";
                u.descriptionKey = $"upgrade.{spec.id}.desc";
                u.debugName = spec.name;
                u.category = spec.category;
                u.levels = new List<UpgradeLevel>();
                foreach (var (value, cost) in spec.levels)
                    u.levels.Add(new UpgradeLevel { value = value, cost = new List<CurrencyAmount>(cost) });
                byId[spec.id] = u;
                db.upgrades.Add(u);
            }
            foreach (var spec in UpgradeSpecs)
            {
                var u = byId[spec.id];
                u.requires = new List<UpgradeRequirement>();
                if (spec.requires != null)
                    foreach (var r in spec.requires)
                        u.requires.Add(new UpgradeRequirement { upgrade = byId[r], minLevel = 1 });
                EditorUtility.SetDirty(u);
            }
            EditorUtility.SetDirty(db);
            return db;
        }

        // =====================================================================
        const string ThemePath = DataDir + "/UiTheme.asset";
        const string IslandArtPath = DataDir + "/IslandArt.asset";
        // startowy kadr wyspy 2x2 (GDD §13.6): 144x112, okno x3 = 432x336
        static readonly Vector2Int StartView = new Vector2Int(144, 112);

        /// <summary>Grafika wyspy z kafelków: grządka, dekoracje, ramki trybu rozbudowy, materiały efektów.</summary>
        static IslandArt CreateIslandArt()
        {
            var art = LoadOrCreate<IslandArt>(IslandArtPath);
            art.plotTile = LoadSprite("Ground/plot_tile");
            Sprite R(string n) => LoadSprite("Removal/" + n);
            Sprite[] wood = { R("fx_woodchip"), R("fx_leaf") }, stone = { R("fx_stonechip") };
            // koszty i nagrody z GDD §14.3 (wstępne, do balansu)
            art.decorations = new List<DecoArt>
            {
                new DecoArt { kind = DecoKind.TreeRound, sprite = LoadSprite("Objects/tree_round"), sway = true, nameKey = "deco.tree_round",
                    essenceCost = 5, reward = CurrencyType.Wood, rewardAmount = 3,
                    bonusChance = 0.1f, bonus = CurrencyType.NightHarvest, bonusAmount = 1, bonusToast = "toast.deco.nest",
                    anim = RemoveAnim.Fall, frames = new[] { R("tree_round_fall_0"), R("tree_round_fall_1"), R("tree_round_fall_2"), R("tree_round_fall_3") },
                    chips = wood, hasRemains = true, remains = DecoKind.Stump },
                new DecoArt { kind = DecoKind.Spruce, sprite = LoadSprite("Objects/tree_spruce"), sway = true, nameKey = "deco.spruce",
                    essenceCost = 4, reward = CurrencyType.Wood, rewardAmount = 2, anim = RemoveAnim.Poof,
                    chips = wood, hasRemains = true, remains = DecoKind.StumpSpruce },
                new DecoArt { kind = DecoKind.DeadTree, sprite = LoadSprite("Zones/dead_tree"), sway = false, nameKey = "deco.dead_tree",
                    essenceCost = 3, reward = CurrencyType.Wood, rewardAmount = 2, anim = RemoveAnim.Poof,
                    chips = new[] { R("fx_woodchip") }, hasRemains = true, remains = DecoKind.StumpDead },
                new DecoArt { kind = DecoKind.Bush, sprite = LoadSprite("Objects/bush"), sway = true, nameKey = "deco.bush",
                    essenceCost = 2, reward = CurrencyType.Wood, rewardAmount = 1,
                    bonusChance = 1f, bonus = CurrencyType.Coins, bonusAmount = 5, bonusToast = "toast.deco.berries",
                    anim = RemoveAnim.Cut, frames = new[] { R("bush_cut") }, chips = new[] { R("fx_leaf") } },
                new DecoArt { kind = DecoKind.Rock, sprite = LoadSprite("Objects/rock"), sway = false, nameKey = "deco.rock",
                    essenceCost = 6, reward = CurrencyType.Stone, rewardAmount = 3,
                    bonusChance = 0.15f, bonus = CurrencyType.Coins, bonusAmount = 15, bonusToast = "toast.deco.coins",
                    anim = RemoveAnim.Crack, frames = new[] { R("rock_cracked") }, chips = stone, hasRemains = true, remains = DecoKind.Rubble },
                new DecoArt { kind = DecoKind.Stump, sprite = R("stump_round"), nameKey = "deco.stump",
                    essenceCost = 1, reward = CurrencyType.Wood, rewardAmount = 1, anim = RemoveAnim.Poof, chips = new[] { R("fx_woodchip") } },
                new DecoArt { kind = DecoKind.StumpSpruce, sprite = R("stump_spruce"), nameKey = "deco.stump",
                    essenceCost = 1, reward = CurrencyType.Wood, rewardAmount = 1, anim = RemoveAnim.Poof, chips = new[] { R("fx_woodchip") } },
                new DecoArt { kind = DecoKind.StumpDead, sprite = R("stump_dead"), nameKey = "deco.stump",
                    essenceCost = 1, reward = CurrencyType.Wood, rewardAmount = 1, anim = RemoveAnim.Poof, chips = new[] { R("fx_woodchip") } },
                new DecoArt { kind = DecoKind.Rubble, sprite = R("rubble"), nameKey = "deco.rubble",
                    essenceCost = 2, reward = CurrencyType.Stone, rewardAmount = 1, anim = RemoveAnim.Poof, chips = stone },
            };
            // --- dekoracje stref (§13.8, §14.3). Zabytki (krypta, posąg, schody) są nieusuwalne i opowiadają lore.
            Sprite Z(string n) => LoadSprite("Zones/" + n);
            Sprite Zg(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Zones/{n}_glow.png");
            DecoArt Zone(DecoKind kind, string sprite, string nameKey, int essence, CurrencyType reward, int amount, bool stoneChips,
                         bool sway = false, float loreChance = 0f, DecoKind? remains = null, bool glow = false)
            {
                return new DecoArt
                {
                    kind = kind, sprite = Z(sprite), glowSprite = glow ? Zg(sprite) : null, sway = sway, nameKey = nameKey,
                    essenceCost = essence, reward = reward, rewardAmount = amount,
                    bonusLore = loreChance > 0f, bonusChance = loreChance,
                    anim = remains.HasValue && kind <= DecoKind.Gravestone2 ? RemoveAnim.Crack : RemoveAnim.Poof,
                    frames = remains.HasValue && kind <= DecoKind.Gravestone2 ? new[] { R("gravestone_broken") } : null,
                    chips = stoneChips ? stone : wood, hasRemains = remains.HasValue, remains = remains ?? DecoKind.Rubble,
                };
            }
            DecoArt Landmark(DecoKind kind, string sprite, string nameKey, string lore) =>
                new DecoArt { kind = kind, sprite = Z(sprite), nameKey = nameKey, removable = false, landmark = true, loreKey = lore };
            var S = CurrencyType.Stone; var Wd = CurrencyType.Wood; var C = CurrencyType.Coins;
            art.decorations.AddRange(new[]
            {
                Zone(DecoKind.Gravestone0, "gravestone_0", "deco.gravestone", 3, S, 2, true, false, 0.1f, DecoKind.GravestoneBroken),
                Zone(DecoKind.Gravestone1, "gravestone_1", "deco.gravestone", 3, S, 2, true, false, 0.1f, DecoKind.GravestoneBroken),
                Zone(DecoKind.Gravestone2, "gravestone_2", "deco.gravestone", 3, S, 2, true, false, 0.1f, DecoKind.GravestoneBroken),
                new DecoArt { kind = DecoKind.GravestoneBroken, sprite = R("gravestone_broken"), nameKey = "deco.gravestone_broken",
                              essenceCost = 1, reward = S, rewardAmount = 1, anim = RemoveAnim.Poof, chips = stone },
                Zone(DecoKind.WoodenCross, "Cemetery/cemetery_wooden_cross", "deco.wooden_cross", 1, Wd, 1, false),
                Zone(DecoKind.Obelisk, "Cemetery/cemetery_obelisk", "deco.obelisk", 4, S, 3, true),
                Zone(DecoKind.OpenGrave, "Cemetery/cemetery_open_grave", "deco.open_grave", 2, S, 1, true, false, 0.15f),
                Landmark(DecoKind.Crypt, "Cemetery/cemetery_crypt", "deco.crypt", "lore.crypt"),
                Zone(DecoKind.SkullPile, "Cemetery/cemetery_skull_pile", "deco.skull_pile", 2, S, 1, true, false, 0.2f),
                Zone(DecoKind.Candles, "Cemetery/cemetery_candles", "deco.candles", 1, C, 3, false, glow: true),
                Zone(DecoKind.IronGate, "Cemetery/cemetery_iron_gate", "deco.iron_gate", 3, S, 2, true),
                Zone(DecoKind.Thorns, "Cemetery/cemetery_thorns", "deco.thorns", 1, Wd, 1, false, true),
                Zone(DecoKind.Mangrove, "Swamp/swamp_mangrove", "deco.mangrove", 5, Wd, 3, false, true, 0f, DecoKind.StumpDead),
                Zone(DecoKind.LilyPads, "Swamp/swamp_lily_pads", "deco.lily_pads", 1, C, 4, false, glow: true),
                Zone(DecoKind.RottenLog, "Swamp/swamp_rotten_log", "deco.rotten_log", 2, Wd, 2, false, glow: true),
                Zone(DecoKind.Cattails, "Swamp/swamp_cattails", "deco.cattails", 1, Wd, 1, false, true),
                Zone(DecoKind.Reeds, "reeds", "deco.reeds", 1, Wd, 1, false, true),
                Zone(DecoKind.SwampPool, "swamp_pool", "deco.swamp_pool", 3, S, 1, true),
                Zone(DecoKind.BrokenWall, "Ruins/ruins_broken_wall", "deco.broken_wall", 4, S, 3, true),
                Zone(DecoKind.FallenColumn, "Ruins/ruins_fallen_column", "deco.fallen_column", 4, S, 3, true),
                Landmark(DecoKind.Statue, "Ruins/ruins_statue", "deco.statue", "lore.statue"),
                Landmark(DecoKind.StoneStairs, "Ruins/ruins_stone_stairs", "deco.stone_stairs", "lore.stairs"),
                Zone(DecoKind.RuinsRubble, "Ruins/ruins_rubble", "deco.rubble", 2, S, 1, true),
                Zone(DecoKind.Brazier, "Ruins/ruins_brazier", "deco.brazier", 2, S, 2, true, glow: true),
                Zone(DecoKind.RuneTablet, "Ruins/ruins_rune_tablet", "deco.rune_tablet", 3, S, 2, true, false, 0.3f, null, true),
                Zone(DecoKind.VinePillar, "Ruins/ruins_vine_pillar", "deco.vine_pillar", 3, S, 2, true),
                Zone(DecoKind.BrokenPillar, "broken_pillar", "deco.broken_pillar", 3, S, 2, true),
                Zone(DecoKind.RuinedArch, "ruined_arch", "deco.ruined_arch", 4, S, 3, true),
            });
            art.crow = new[] { Z("Cemetery/cemetery_crow_0"), Z("Cemetery/cemetery_crow_1") };
            art.crowGlow = new[] { Zg("Cemetery/cemetery_crow_0"), Zg("Cemetery/cemetery_crow_1") };
            art.frog = new[] { Z("Swamp/swamp_frog_0"), Z("Swamp/swamp_frog_1") };
            art.frogGlow = new[] { Zg("Swamp/swamp_frog_0"), Zg("Swamp/swamp_frog_1") };
            art.bubbles = new[] { Z("Swamp/swamp_bog_bubbles_0"), Z("Swamp/swamp_bog_bubbles_1"), Z("Swamp/swamp_bog_bubbles_2") };
            art.dust = new[] { R("fx_dust_0"), R("fx_dust_1"), R("fx_dust_2") };
            art.pier = LoadSprite("Sea/pier");
            art.netBuoy = LoadSprite("Sea/net_buoy");
            art.boat = LoadSprite("Sea/boat");
            art.boatGlow = LoadSprite("Sea/boat_glow");
            art.altar = LoadSprite("Sea/altar");
            art.altarGlow = LoadSprite("Sea/altar_glow");
            art.lighthouse = AssetDatabase.LoadAssetAtPath<LampDefinition>($"{DataDir}/Lamps/Lamp_lighthouse.asset");
            // automatyka
            Sprite[] Frames(string name, string suffix = "")
            {
                var frames = new Sprite[4];
                for (int i = 0; i < 4; i++)
                    frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Automation/{name}_{i}{suffix}.png");
                return frames;
            }
            art.crateFill = new[] { LoadSprite("Automation/harvest_crate_0"), LoadSprite("Automation/harvest_crate_1"),
                                    LoadSprite("Automation/harvest_crate_2") };
            art.wispFrames = Frames("wisp_gardener");
            art.wispGlow = Frames("wisp_gardener", "_glow");
            art.merchantFrames = Frames("merchant_spirit");
            art.merchantGlow = Frames("merchant_spirit", "_glow");
            // drzewa owocowe (GDD §15.1): fazy 0-3, świecenie owoców, przewracanie
            art.trees = new List<TreeArt>();
            foreach (var sp in TreeSpecies.All)
            {
                var ta = new TreeArt { id = sp.id, stages = new Sprite[4], fall = new Sprite[4] };
                for (int i = 0; i < 4; i++)
                {
                    ta.stages[i] = LoadSprite($"Trees/tree_{sp.id}_{i}");
                    ta.fall[i] = LoadSprite($"Trees/tree_{sp.id}_fall_{i}");
                }
                ta.fruitingGlow = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Trees/tree_{sp.id}_3_glow.png");
                ta.fruit = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}UI/Icons/icon_fruit_{sp.id}.png");
                art.trees.Add(ta);
            }
            // ptaki (sit0, sit1, fly0, fly1)
            Sprite[] Bird(string kind) => new[] { LoadSprite($"Critters/bird_{kind}_sit0"), LoadSprite($"Critters/bird_{kind}_sit1"),
                                                   LoadSprite($"Critters/bird_{kind}_fly0"), LoadSprite($"Critters/bird_{kind}_fly1") };
            art.birdCrow = Bird("crow");
            art.birdFinch = Bird("finch");
            art.birds = new List<BirdArt>();
            foreach (var id in new[] { "magpie", "bullfinch", "tit", "gull", "heron" })   // tools/pixel-proto/birds2.py
                art.birds.Add(new BirdArt { id = id, frames = Bird(id) });
            art.noticeBoard = LoadSprite("Objects/notice_board");
            art.noticeBoardEmpty = LoadSprite("Objects/notice_board_empty");
            art.introArrow = LoadSprite("Fx/intro_arrow");
            // kocioł alchemiczny: zimny, warzenie (3 klatki), gotowy (2 klatki) + piksele emisyjne
            Sprite Obj(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Objects/{n}.png");
            art.storehouse = LoadSprite("Objects/storehouse");
            art.moth = new[] { LoadSprite("Events/moth_0"), LoadSprite("Events/moth_1") };
            art.fishJump = new[] { LoadSprite("Events/fish_jump_up"), LoadSprite("Events/fish_jump_down") };
            art.cauldronIdle = LoadSprite("Objects/cauldron_idle");
            // rój ogników: gniazdo i ogniki w kolorach zawodów (klatki i piksele emisyjne)
            art.wispNest = new[] { LoadSprite("Automation/wisp_nest_0"), LoadSprite("Automation/wisp_nest_1") };
            art.wispKinds = new List<WispKindArt>();
            foreach (Wyspa.Automation.WispKind k in System.Enum.GetValues(typeof(Wyspa.Automation.WispKind)))
            {
                string n = k.ToString().ToLowerInvariant();
                string tool = Wyspa.Automation.Wisp.ToolName(k);
                Sprite T(int f, string suffix = "") => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Automation/tool_{tool}_{f}{suffix}.png");
                art.wispKinds.Add(new WispKindArt
                {
                    kind = k, frames = Frames("wisp_" + n), glow = Frames("wisp_" + n, "_glow"),
                    tool = new[] { T(0), T(1) }, toolGlow = new[] { T(0, "_glow"), T(1, "_glow") },
                });
            }
            // czapki ogników (sklep z czapkami)
            art.hats = new List<NamedSprite>();
            art.hatGlows = new List<NamedSprite>();
            foreach (var h in Wyspa.Economy.Hats.All)
            {
                art.hats.Add(new NamedSprite { name = h.id, sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Automation/hat_{h.id}.png") });
                var g = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Automation/hat_{h.id}_glow.png");
                if (g != null)
                    art.hatGlows.Add(new NamedSprite { name = h.id, sprite = g });
            }
            // domki chowańców, ognisko ogników i sennik (tools/pixel-proto/homes.py)
            art.homes = new List<NamedSprite>();
            art.homeGlows = new List<NamedSprite>();
            foreach (var d in Wyspa.Familiars.FamiliarSystem.Defs)
            {
                art.homes.Add(new NamedSprite { name = d.id, sprite = Obj("home_" + d.id) });
                var hg = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Objects/home_{d.id}_glow.png");
                if (hg != null)
                    art.homeGlows.Add(new NamedSprite { name = d.id, sprite = hg });
            }
            art.bonfire = new[] { Obj("bonfire_0"), Obj("bonfire_1"), Obj("bonfire_2") };
            art.bonfireGlow = new[] { Obj("bonfire_0_glow"), Obj("bonfire_1_glow"), Obj("bonfire_2_glow") };
            art.dreamHouse = Obj("dreamhouse");
            art.dreamHouseGlow = Obj("dreamhouse_glow");
            art.emberKiln = new[] { Obj("ember_kiln_0"), Obj("ember_kiln_1"), Obj("ember_kiln_2") };   // generatory Żaru
            art.emberKilnGlow = new[] { Obj("ember_kiln_0_glow"), Obj("ember_kiln_1_glow"), Obj("ember_kiln_2_glow") };
            art.emberCrystal = Obj("ember_crystal");
            art.emberCrystalGlow = Obj("ember_crystal_glow");
            // monumenty wysp fabularnych (tools/pixel-proto/monuments.py): fazy 0-4, świecące okna gotowego
            art.story = new List<StoryArt>();
            foreach (var site in Wyspa.World.StorySites.All)
            {
                var phases = new Sprite[Wyspa.World.Monument.Phases + 1];
                for (int k = 0; k < phases.Length; k++)
                    phases[k] = LoadSprite($"Story/monument_{site.id}_{k}");
                art.story.Add(new StoryArt
                {
                    id = site.id,
                    phases = phases,
                    doneGlow = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Story/monument_{site.id}_{Wyspa.World.Monument.Phases}_glow.png"),
                });
            }
            // ozdobniki na wodzie (tools/pixel-proto/water_flora.py, kolejność jak FLORA i wagi w Visual/WaterFlora)
            string[] flora = { "lily_pads_0", "lily_pads_1", "lily_pads_2", "lily_pale", "lily_mist", "leaf_0", "leaf_1",
                               "duckweed_0", "duckweed_1", "twig" };
            art.waterFlora = new Sprite[flora.Length];
            art.waterFloraGlow = new Sprite[flora.Length];
            for (int i = 0; i < flora.Length; i++)
            {
                art.waterFlora[i] = LoadSprite("Water/flora_" + flora[i]);
                art.waterFloraGlow[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Water/flora_{flora[i]}_glow.png");
            }
            // chowańce: klatki i piksele emisyjne w kolejności póz z FamiliarSystem.Defs (tools/pixel-proto/familiars.py)
            art.familiars = new List<FamiliarArt>();
            foreach (var d in Wyspa.Familiars.FamiliarSystem.Defs)
            {
                var fa = new FamiliarArt { id = d.id, frames = new Sprite[d.poses.Length], glows = new Sprite[d.poses.Length] };
                for (int i = 0; i < d.poses.Length; i++)
                {
                    fa.frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Critters/familiar_{d.id}_{d.poses[i]}.png");
                    fa.glows[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Critters/familiar_{d.id}_{d.poses[i]}_glow.png");
                }
                art.familiars.Add(fa);
            }
            art.cauldronBrew = new[] { Obj("cauldron_brew_0"), Obj("cauldron_brew_1"), Obj("cauldron_brew_2") };
            art.cauldronBrewGlow = new[] { Obj("cauldron_brew_0_glow"), Obj("cauldron_brew_1_glow"), Obj("cauldron_brew_2_glow") };
            art.cauldronReady = new[] { Obj("cauldron_ready_0"), Obj("cauldron_ready_1") };
            art.cauldronReadyGlow = new[] { Obj("cauldron_ready_0_glow"), Obj("cauldron_ready_1_glow") };
            art.rune = LoadSprite("Automation/rune_stone");
            art.runeGlow = LoadSprite("Automation/rune_stone_glow");
            art.lamps = new List<LampDefinition>();
            foreach (var id in new[] { "lantern", "skull", "wisp", "mushrooms" })
            {
                var def = AssetDatabase.LoadAssetAtPath<LampDefinition>($"{DataDir}/Lamps/Lamp_{id}.asset");
                if (def != null)
                    art.lamps.Add(def);
            }
            art.frameBuy = LoadSprite("UI/Frames/frame_buy");
            art.frameBuyHover = LoadSprite("UI/Frames/frame_buy_hover");
            art.frameDig = LoadSprite("UI/Frames/frame_dig");
            art.frameDigHover = LoadSprite("UI/Frames/frame_dig_hover");
            art.foam = new[] { LoadSprite("Fx/fx_foam_0"), LoadSprite("Fx/fx_foam_1") };
            art.swayCrop = LoadMat("SwayCrop");
            art.swayCropGlow = LoadMat("SwayCropGlow");
            art.swayTree = LoadMat("SwayTree");
            art.swayTreeGlow = LoadMat("SwayTreeGlow");
            art.glow = GlowMaterial();
            art.floatMat = FxMaterialAsset("Float", "SpriteFloat", null);   // łodzie i boja w wodzie (Visual/Floating)
            art.reflection = FxMaterialAsset("WaterReflection", "WaterReflection", null);   // odbicia w wodzie (Visual/WaterReflections)
            art.atmosphere = FxMaterialAsset("Atmosphere", "Atmosphere", null);   // cienie chmur, mgiełka o świcie, rosa (Visual/Atmosphere)
            EditorUtility.SetDirty(art);
            return art;
        }
        const string LocDir = Root + "/Localization";
        const string FontPath = Root + "/Art/Fonts/WyspaPixel.ttf";
        static readonly string[] LocTables = { "glossary", "game", "ui" };

        /// <summary>Motyw UI: elementy 9-slice z Art/Sprites/UI/Kit, ikony z UI/Icons, font i tabele lokalizacji.</summary>
        static UiTheme CreateUiTheme()
        {
            var theme = LoadOrCreate<UiTheme>(ThemePath);
            Sprite Kit(string n) => LoadSprite("UI/Kit/ui_" + n);
            theme.panel = Kit("panel");
            theme.panelPage = Kit("panel_page");
            theme.logo = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "UI/logo.png");   // logo w menu głównym (release_art.py)
            theme.buttonNormal = Kit("button_normal");
            theme.buttonHover = Kit("button_hover");
            theme.buttonPressed = Kit("button_pressed");
            theme.buttonDisabled = Kit("button_disabled");
            theme.tabActive = Kit("tab_active");
            theme.tabInactive = Kit("tab_inactive");
            theme.slot = Kit("slot");
            theme.slotSelected = Kit("slot_selected");
            theme.slotLocked = Kit("slot_locked");
            theme.tooltip = Kit("tooltip");
            theme.toast = Kit("toast");
            theme.barFrame = Kit("bar_frame");
            theme.barFillEssence = Kit("bar_fill_essence");
            theme.barFillGrowth = Kit("bar_fill_growth");
            theme.barFillRhythm = Kit("bar_fill_rhythm");
            theme.wheel = Kit("wheel");

            theme.icons = new List<NamedSprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { Sprites + "UI/Icons" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("icon_", StringComparison.Ordinal))
                    continue;
                theme.icons.Add(new NamedSprite { name = file.Substring(5), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
            }

            theme.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (theme.font == null)
                Debug.LogWarning("[Wyspa] Brak fontu: " + FontPath);
            // CJK: Fusion Pixel 8px (TakWolf, SIL OFL 1.1, licencja w Art/Fonts/FusionPixel/OFL.txt)
            theme.fontReadable = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/Fonts/FusionPixel/fusion-pixel-8px-proportional-latin.otf");
            theme.cjkJa = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/Fonts/FusionPixel/fusion-pixel-8px-proportional-ja.otf");
            theme.cjkZhHans = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/Fonts/FusionPixel/fusion-pixel-8px-proportional-zh_hans.otf");
            if (theme.cjkJa == null || theme.cjkZhHans == null)
                Debug.LogWarning("[Wyspa] Brak fontów Fusion Pixel w Art/Fonts/FusionPixel (japoński i chiński pokażą kwadraciki).");
            var tables = new List<TextAsset>();
            foreach (var t in LocTables)
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{LocDir}/{t}.csv");
                if (ta != null) tables.Add(ta);
                else Debug.LogWarning($"[Wyspa] Brak tabeli lokalizacji: {LocDir}/{t}.csv");
            }
            theme.tables = tables.ToArray();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        // ----------------------------------------------------------------- materiały efektów
        const string MatDir = DataDir + "/Materials";
        const string ShaderDir = Root + "/Shaders";
        // prototyp (anim.py): rośliny 2 cykle na 48 klatek (10 kl./s), drzewa 1 cykl
        const float CropSwaySpeed = 2f / 4.8f, TreeSwaySpeed = 1f / 4.8f;

        static Material FxMaterialAsset(string name, string shaderFile, Action<Material> configure)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/{shaderFile}.shader");
            if (shader == null)
            {
                Debug.LogWarning($"[Wyspa] Brak shadera {ShaderDir}/{shaderFile}.shader - efekt wyłączony.");
                return null;
            }
            EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void CreateFxMaterials()
        {
            FxMaterialAsset("SwayCrop", "SpriteSway", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", CropSwaySpeed); });
            FxMaterialAsset("SwayCropGlow", "SpriteSwayUnlit", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", CropSwaySpeed); });
            FxMaterialAsset("SwayTree", "SpriteSway", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", TreeSwaySpeed); });
            FxMaterialAsset("SwayTreeGlow", "SpriteSwayUnlit", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", TreeSwaySpeed); });
            var data = AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + "Ground/water_data.png");
            if (data == null)
                Debug.LogWarning("[Wyspa] Brak Ground/water_data.png - uruchom tools/pixel-proto/export_unity_fx.py.");
            FxMaterialAsset("Water", "Water", m => m.SetTexture("_DataTex", data));
            FxMaterialAsset("Fog", "Fog", null);   // kolory mgły (6 tonów) z domyślnych wartości shadera
        }

        /// <summary>Klikalny obszar = kafelek pod obiektem (podstawa 6 px pod środkiem kafelka), jak w Island.</summary>
        static void TileCollider(GameObject go)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null)
                col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.offset = new Vector2(-0.5f / Ppu, 6f / Ppu);
        }

        static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        /// <summary>Materiał efektu z przełącznikiem (F1): zapasowy = materiał, który renderer miał wcześniej.</summary>
        static void UseFx(SpriteRenderer sr, Material fx, bool hideWithoutFx = false)
        {
            if (sr == null)
                return;
            var fallback = hideWithoutFx ? null : sr.sharedMaterial;
            if (fx != null)
                sr.sharedMaterial = fx;
            sr.gameObject.AddComponent<FxMaterial>().Setup(sr, fx, fallback);
        }

        [MenuItem("Wyspa/3. Zbuduj scenę MainLevel", priority = 3)]
        public static void BuildScene()
        {
            var layout = LoadLayout();
            if (layout == null)
            {
                Debug.LogError("[Wyspa] Brak " + LayoutPath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            CreateData();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Assety ładujemy dopiero PO NewScene - tworzenie sceny zwalnia nieużywane obiekty
            // i wcześniejsze referencje stawały się puste (pole Crops w GameLoop było null).
            var db = AssetDatabase.LoadAssetAtPath<CropDatabase>(DataDir + "/CropDatabase.asset");
            var upgradeDb = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(DataDir + "/UpgradeDatabase.asset");
            var glowMat = GlowMaterial();
            var uiTheme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            Material swayCrop = LoadMat("SwayCrop"), swayCropGlow = LoadMat("SwayCropGlow"), swayTree = LoadMat("SwayTree");
            Material waterMat = LoadMat("Water"), fogMat = LoadMat("Fog");
            if (db == null)
                Debug.LogError("[Wyspa] Nie znaleziono " + DataDir + "/CropDatabase.asset - uruchom Wyspa > 2.");
            float W = layout.width, H = layout.height;

            Vector3 Center(float x, float y) => new Vector3((x - W / 2f) / Ppu, (H / 2f - y) / Ppu, 0f);
            Vector3 Anchor(float x, float y) => new Vector3((x + 0.5f - W / 2f) / Ppu, (H / 2f - (y + 1f)) / Ppu, 0f);

            // --- kamera pixel perfect ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = StartView.y / 2f / Ppu;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = OutlineColor;
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = Ppu;
            ppc.refResolutionX = StartView.x;   // kadr rośnie z wyspą (ViewController)
            ppc.refResolutionY = StartView.y;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;

            // --- światło globalne (pora dnia) ---
            var globalGo = new GameObject("Global Light 2D");
            var global = globalGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
            global.color = Color.white;

            // --- wyspa z kafelków: ląd, woda i mgła malowane w locie przez Island ---
            var world = new GameObject("World").transform;
            // woda liczona w shaderze (ryby, fale, przybój); bez shadera widać statyczną wodę z Island
            var water = MakeSprite("Water", world, LoadSprite("Ground/water_base"), Vector3.zero, -2000);
            UseFx(water, waterMat);
            var islandSr = MakeSprite("Land", world, null, Vector3.zero, -1900);
            // mgła nad morzem, gęstnieje z odległością od brzegu (sprite = mapa danych wody, rysuje tylko shader)
            var fog = MakeSprite("Fog", world, LoadSprite("Ground/water_data"), Vector3.zero, 5000);
            UseFx(fog, fogMat, true);
            // snopy słońca / księżyca: Sprite Light 2D z wolumetrią (LightShafts tworzy światło sam)
            var shafts = new GameObject("LightShafts");
            shafts.transform.SetParent(world, false);

            // --- obiekty stawiane na kafelkach ---
            var objectsRoot = new GameObject("TileObjects").transform;
            var tileObjects = new List<TileObject>();
            TileObject AsTileObject(Component c, string id, string nameKey, TileObject anchor = null, Vector2Int offset = default)
            {
                var t = c.gameObject.AddComponent<TileObject>();
                t.Setup(id, nameKey, anchor, offset);
                c.gameObject.SetActive(false);
                tileObjects.Add(t);
                return t;
            }

            // automatyka: skrzynie, ogniki, duszki-handlarze i runy stawia Island w trakcie gry (IslandArt, Sklep → Budowle)

            // lampy (także startową latarnię) stawia Island w trakcie gry: IslandArt.lamps, sklep, tryb stawiania

            // nocne zdarzenia: ogniki, spadające gwiazdy, potwór
            Sprite E(string n) => LoadSprite("Events/" + n);
            Sprite Eg(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Events/{n}_glow.png");
            var eventsGo = new GameObject("NightEvents");
            eventsGo.AddComponent<Wyspa.Sea.NightEvents>().Setup(
                new[] { LoadSprite("Automation/wisp_gardener_0"), LoadSprite("Automation/wisp_gardener_1"),
                        LoadSprite("Automation/wisp_gardener_2"), LoadSprite("Automation/wisp_gardener_3") },
                new[] { LoadSprite("Automation/wisp_gardener_0_glow"), LoadSprite("Automation/wisp_gardener_1_glow"),
                        LoadSprite("Automation/wisp_gardener_2_glow"), LoadSprite("Automation/wisp_gardener_3_glow") },
                new[] { E("falling_star_0"), E("falling_star_1"), E("falling_star_2") },
                new[] { Eg("falling_star_0"), Eg("falling_star_1"), Eg("falling_star_2") },
                E("star_shard"), Eg("star_shard"),
                new[] { E("tentacle_0"), E("tentacle_1"), E("tentacle_2") },
                new[] { E("monster_eye_0"), E("monster_eye_1"), E("monster_eye_2") }, Eg("monster_eye_2"), glowMat);

            // kupiec-widmo: łódź pod wyspą raz na noc; obiekt zawsze aktywny (harmonogram), sprite'y chowa sam
            var merchantGo = new GameObject("GhostMerchant");
            merchantGo.transform.SetParent(world, false);
            var merchantBody = MakeSprite("Body", merchantGo.transform, LoadSprite("Sea/ghost_merchant"), Vector3.zero, 700);
            merchantBody.transform.localPosition = Vector3.zero;
            var merchantGlow = MakeGlow(merchantBody, LoadSprite("Sea/ghost_merchant_glow"), 701, glowMat);
            var merchantArea = merchantGo.AddComponent<BoxCollider2D>();
            merchantArea.size = new Vector2(2f, 2f);
            merchantArea.offset = new Vector2(0f, 1f);
            merchantGo.AddComponent<Wyspa.Sea.GhostMerchant>().Setup(merchantBody, merchantGlow, merchantArea);

            var islandArt = AssetDatabase.LoadAssetAtPath<IslandArt>(IslandArtPath);
            var waterAnim = water.gameObject.AddComponent<WaterAnimator>();
            var island = world.gameObject.AddComponent<Island>();
            island.Setup(islandArt, islandSr, water, fog, waterAnim, tileObjects);

            // --- logika gry ---
            var game = new GameObject("Game");
            var activity = game.AddComponent<ActivityTracker>();
            var clock = game.AddComponent<GameClock>();
            var essence = game.AddComponent<EssenceStore>();
            var wallet = game.AddComponent<Wallet>();
            var save = game.AddComponent<SaveSystem>();
            var rhythm = game.AddComponent<WorkRhythm>();
            var upgradeState = game.AddComponent<UpgradeState>();
            var loop = game.AddComponent<GameLoop>();
            game.AddComponent<AppPerformance>();
            var automation = game.AddComponent<AutomationSystem>();
            camGo.AddComponent<ViewController>().Setup(ppc);
            var dayNight = game.AddComponent<DayNightLighting>();
            var click = game.AddComponent<ClickRouter>();
            game.AddComponent<Wyspa.Lore.Chronicle>();
            game.AddComponent<DebugHud>();

            SetRefs(loop, ("activity", activity), ("clock", clock), ("essence", essence), ("wallet", wallet),
                ("crops", db), ("saveSystem", save), ("rhythm", rhythm), ("upgrades", upgradeState), ("automation", automation), ("island", island));
            SetRefs(upgradeState, ("database", upgradeDb));
            SetRefs(dayNight, ("clock", clock), ("globalLight", global));
            SetRefs(click, ("targetCamera", cam));

            // --- efekty: woda, mgła, cząsteczki ---
            waterAnim.Setup(water, clock);
            fog.gameObject.AddComponent<FogController>().Setup(fog, clock);
            shafts.AddComponent<LightShafts>().Setup(null, null, clock);
            var fxGo = new GameObject("Fx");
            var particles = fxGo.AddComponent<PixelParticles>();
            particles.Setup(glowMat);
            Sprite Fx(string n) => LoadSprite("Fx/" + n);
            // świetliki i spadające liście biorą obszar lądu i drzewa z Island
            fxGo.AddComponent<IslandFx>().Setup(particles, clock, glowMat,
                new[] { Fx("fx_soil_0"), Fx("fx_soil_1") }, new[] { Fx("fx_leaf_0"), Fx("fx_leaf_1") }, Fx("fx_leaf_fall"),
                new[] { Fx("fx_sparkle_0"), Fx("fx_sparkle_1"), Fx("fx_sparkle_2") }, new[] { Fx("fx_firefly_0"), Fx("fx_firefly_1") },
                new Transform[0], new Rect(-2f, -2f, 4f, 4f));

            // --- UI (płótno budowane w kodzie przy starcie, GDD §12) ---
            var ui = new GameObject("UI");
            var uiRoot = ui.AddComponent<UiRoot>();
            SetRefs(uiRoot, ("theme", uiTheme));

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OldScenePath) != null && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                AssetDatabase.MoveAsset(OldScenePath, ScenePath);   // stara scena Prototype.unity: ta sama scena pod nową nazwą
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Wyspa] Scena zapisana: " + ScenePath + ". Naciśnij Play.");
        }

        // =====================================================================
        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer MakeGlow(SpriteRenderer parent, Sprite sprite, int order, Material glowMat)
        {
            var glow = MakeSprite("Glow", parent.transform, sprite, Vector3.zero, order);
            glow.transform.localPosition = Vector3.zero;
            if (glowMat != null)
                glow.sharedMaterial = glowMat;
            return glow;
        }

        static Sprite LoadSprite(string relative)
        {
            string path = Sprites + relative + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning("[Wyspa] Brak sprite'a: " + path);
            return sprite;
        }

        static Layout LoadLayout()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            return text != null ? JsonUtility.FromJson<Layout>(text.text) : null;
        }

        static Material GlowMaterial()
        {
            const string path = DataDir + "/GlowUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                Debug.LogWarning("[Wyspa] Nie znaleziono shadera Sprite-Unlit-Default (URP). Glow lamp będzie przyciemniany w nocy.");
                return null;
            }
            EnsureFolder(DataDir);
            mat = new Material(shader) { name = "GlowUnlit" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void SetRefs(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[Wyspa] {target.GetType().Name}: brak pola '{field}'");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
