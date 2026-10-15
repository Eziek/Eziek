namespace Wyspa.Upgrades
{
    /// <summary>
    /// Efekty drzewka ulepszeń jako liczby, które czytają systemy gry (jak mnożniki rytuałów i chowańców).
    /// Przeliczane w GameLoop.ApplyUpgrades po zakupie i po wczytaniu; bez ulepszenia wartości neutralne.
    /// </summary>
    public static class UpgradeEffects
    {
        public static float WispSpeed = 1f;
        public static int NestCapacity = 3;
        public static float WispXp = 1f;
        public static int CrateCapacity = 20;
        public static float EssenceRate = 1f;   // Rozpałka: Żar płynie szybciej
        public static float NightEssence = 0f;   // Nocny żar: w nocy Żar płynie szybciej
        public static float HearthBoost = 1f;   // Żarliwe palenisko: piec i kryształy dają więcej Żaru
        public static float DayGrowth = 1f, NightGrowth = 1f;
        public static float MutationChance = 0.03f;
        public static float WildRuneInterval = 1f;
        public static float MerchantPrice = 0.9f;
        public static float BoatDuration = 1f;
        public static float LampBurn = 1f;

        // handel i dochody (czyta je Economy/Market i systemy obok); wartości neutralne bez ulepszeń
        public static float SaleAll = 0f;   // Stragan
        public static float SaleAll2 = 0f;   // Uczciwa waga
        public static float CritChance = 0f;   // Targowanie
        public static float CritMult = 2f;   // Złoty interes
        public static float BulkBonus = 0f;   // Hurt
        public static float MerchantThreshold = 1f;   // Gorliwy handlarz
        public static float RepCap = 0f;   // Renoma
        public static float DemandMult = 1f;   // Popyt dnia
        public static float MarketDay = 1f;   // Dzień targowy
        public static float ComboCap = 0f;   // Zbiór seryjny
        public static float InterestRate = 0f;   // Odsetki
        public static float CrateBonus = 0f;   // Targ skrzyń
        public static float OrderBonus = 0f;   // Sakiewka
        public static int OrderSlots = 0;   // Pełna tablica
        public static int OrderFaster = 0;   // Częste zlecenia
        public static float OrderVip = 0f;   // Szlachetni klienci
        public static float OrderSeedChance = 0f;   // Zapłata w nasionach
        public static float GhostBuy = 0f;   // Targ z widmem
        public static float GhostStay = 1f;   // Cierpliwy kupiec
        public static int GhostTip = 0;   // Widmowy napiwek
        public static float GhostDiscount = 0f;   // Widmowe rabaty
        public static float ExpLoot = 1f;   // Bogaty ładunek
        public static int ExpCoins = 0;   // Morski handel
        public static int WoodBonus = 0;   // Handel drewnem
        public static int StoneBonus = 0;   // Kamieniarz
        public static float StoreBatch = 1f;   // Wielki skład
        public static float WispSale = 0.9f;   // Ognik-kupiec
        public static int WispTip = 0;   // Napiwki ogników
        public static float SaleDay = 0f;   // Słoneczny zbiór
        public static float HandBonus = 0f;   // Prosto z grządki
        public static float SeasonSale = 0f;   // Sezonowy targ (rośliny jednej pory roku)
        public static float GroupRoots = 0f;   // Korzenny targ
        public static float GroupGrain = 0f;   // Cech młynarzy
        public static float GroupPumpkin = 0f;   // Dyniowe pole
        public static float DawnSale = 0f;   // Poranny targ
        public static float GroupHerb = 0f;   // Zielarz
        public static float SaleNight = 0f;   // Księżycowy targ
        public static float NightTimeSale = 0f;   // Sowi kupiec
        public static float LampCap = 0f;   // Latarnie kupieckie
        public static float GroupGrave = 0f;   // Cmentarne dobra
        public static float GroupFlower = 0f;   // Kwiaciarka
        public static int NightYieldBonus = 0;   // Obfite noce
        public static float FullMoonSale = 1f;   // Jarmark pełni
        public static float QualityBoost = 0f;   // Gwiazdy jakości
        public static float SaleHybrid = 0f;   // Sława hybryd
        public static float QualityUp = 0f;   // Staranne ręce
        public static float SeedReturn = 0f;   // Zbieracz nasion
        public static float CollectorCap = 0f;   // Kolekcjoner
        public static float SeedDiscount = 0f;   // Pamiątkowe odmiany
        public static float ShardChance = 0f;   // Szczęśliwa moneta
        public static float SaleFruit = 0f;   // Owocowy stragan
        public static float FruitSpeed = 1f;   // Dojrzałe owoce
        public static float FruitDouble = 0f;   // Ciężkie gałęzie
        public static float TreeDiscount = 0f;   // Szczepienie
        public static float FullMoonFruit = 1f;   // Owoce pełni
        public static int EmberCoins = 0;   // Żar w złoto
        public static int WorkWage = 0;   // Dniówka
        public static int LandRent = 0;   // Dzierżawa
        public static float RainSale = 0f;   // Deszczowy kupiec
        public static float WinterSale = 0f;   // Zimowy jarmark

        public static void Apply(UpgradeState u)
        {
            WispSpeed = u.Value(UpgradeIds.WispSpeed, 1f);
            NestCapacity = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.NestSize, 3f));
            WispXp = u.Value(UpgradeIds.WispXp, 1f);
            CrateCapacity = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.BigCrates, 20f));
            EssenceRate = u.Value(UpgradeIds.Kindling, 1f);
            NightEssence = u.Value(UpgradeIds.SecondWind, 0f);
            HearthBoost = 1f + u.Value(UpgradeIds.FocusStreak, 0f);
            DayGrowth = u.Value(UpgradeIds.FertileSoil, 1f);
            NightGrowth = u.Value(UpgradeIds.NightDew, 1f);
            MutationChance = u.Value(UpgradeIds.Crossbreed, 0.03f);
            WildRuneInterval = u.Value(UpgradeIds.WildRunes, 1f);
            MerchantPrice = u.Value(UpgradeIds.FairTrade, 0.9f);
            BoatDuration = u.Value(UpgradeIds.SwiftBoat, 1f);
            LampBurn = u.Value(UpgradeIds.SpiderWicks, 1f);
            SaleAll = u.Value(UpgradeIds.Stall, 0f);
            SaleAll2 = u.Value(UpgradeIds.Scales, 0f);
            CritChance = u.Value(UpgradeIds.Haggle, 0f);
            CritMult = u.Value(UpgradeIds.GoldenDeal, 2f);
            BulkBonus = u.Value(UpgradeIds.Bulk, 0f);
            MerchantThreshold = u.Value(UpgradeIds.EagerMerchant, 1f);
            RepCap = u.Value(UpgradeIds.Reputation, 0f);
            DemandMult = u.Value(UpgradeIds.DailyDemand, 1f);
            MarketDay = u.Value(UpgradeIds.MarketDay, 1f);
            ComboCap = u.Value(UpgradeIds.Combo, 0f);
            InterestRate = u.Value(UpgradeIds.Interest, 0f);
            CrateBonus = u.Value(UpgradeIds.CrateMarket, 0f);
            OrderBonus = u.Value(UpgradeIds.CoinPurse, 0f);
            OrderSlots = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.MoreOrders, 0f));
            OrderFaster = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.QuickOrders, 0f));
            OrderVip = u.Value(UpgradeIds.VipOrders, 0f);
            OrderSeedChance = u.Value(UpgradeIds.OrderSeeds, 0f);
            GhostBuy = u.Value(UpgradeIds.GhostHaggle, 0f);
            GhostStay = u.Value(UpgradeIds.LongStay, 1f);
            GhostTip = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.GhostTip, 0f));
            GhostDiscount = u.Value(UpgradeIds.GhostDiscount, 0f);
            ExpLoot = u.Value(UpgradeIds.RichCargo, 1f);
            ExpCoins = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.SeaTrade, 0f));
            WoodBonus = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.LumberTrade, 0f));
            StoneBonus = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.StoneTrade, 0f));
            StoreBatch = u.Value(UpgradeIds.BigStorehouse, 1f);
            WispSale = u.Value(UpgradeIds.WispTrade, 0.9f);
            WispTip = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.WispTips, 0f));
            SaleDay = u.Value(UpgradeIds.SunHarvest, 0f);
            HandBonus = u.Value(UpgradeIds.FreshPicked, 0f);
            SeasonSale = u.Value(UpgradeIds.BusyHands, 0f);
            GroupRoots = u.Value(UpgradeIds.RootsMarket, 0f);
            GroupGrain = u.Value(UpgradeIds.GrainGuild, 0f);
            GroupPumpkin = u.Value(UpgradeIds.PumpkinPatch, 0f);
            DawnSale = u.Value(UpgradeIds.MorningMarket, 0f);
            GroupHerb = u.Value(UpgradeIds.Herbalist, 0f);
            SaleNight = u.Value(UpgradeIds.MoonMarket, 0f);
            NightTimeSale = u.Value(UpgradeIds.OwlTrader, 0f);
            LampCap = u.Value(UpgradeIds.LampMarket, 0f);
            GroupGrave = u.Value(UpgradeIds.GraveGoods, 0f);
            GroupFlower = u.Value(UpgradeIds.FlowerTrade, 0f);
            NightYieldBonus = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.NightYield, 0f));
            FullMoonSale = u.Value(UpgradeIds.FullMoonFair, 1f);
            QualityBoost = u.Value(UpgradeIds.StarQuality, 0f);
            SaleHybrid = u.Value(UpgradeIds.HybridFame, 0f);
            QualityUp = u.Value(UpgradeIds.CarefulHands, 0f);
            SeedReturn = u.Value(UpgradeIds.SeedSaver, 0f);
            CollectorCap = u.Value(UpgradeIds.Collector, 0f);
            SeedDiscount = u.Value(UpgradeIds.Heirloom, 0f);
            ShardChance = u.Value(UpgradeIds.LuckyCoin, 0f);
            SaleFruit = u.Value(UpgradeIds.OrchardStall, 0f);
            FruitSpeed = u.Value(UpgradeIds.RipeFruit, 1f);
            FruitDouble = u.Value(UpgradeIds.HeavyBranches, 0f);
            TreeDiscount = u.Value(UpgradeIds.Grafting, 0f);
            FullMoonFruit = u.Value(UpgradeIds.MoonFruit, 1f);
            EmberCoins = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.EmberCoins, 0f));
            WorkWage = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.WorkWage, 0f));
            LandRent = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.LandRent, 0f));
            RainSale = u.Value(UpgradeIds.FocusSeller, 0f);
            WinterSale = u.Value(UpgradeIds.RestedSeller, 0f);
        }
    }
}
