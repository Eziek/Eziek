using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using E = Wyspa.Upgrades.UpgradeEffects;

namespace Wyspa.Economy
{
    /// <summary>Którędy idzie sprzedaż (różne mnożniki i premie).</summary>
    public enum SaleChannel
    {
        Hand,       // klik w dojrzałą roślinę / zbiór owoców ręcznie
        Crate,      // „Sprzedaj” przy skrzyni
        Merchant,   // duszek-handlarz (pełna skrzynia)
        Wisp,       // ognik bez miejsca w skrzyni
        Ghost,      // kupiec-widmo skupuje skrzynie
    }

    [Serializable]
    public class MarketSave
    {
        public long runCoins;   // monety zarobione w tym rejsie (perły przy prestiżu)
        public int sold;        // sprzedane plony i owoce w tym rejsie (renoma)
        public int harvests;    // zebrane plony i owoce w tym rejsie (podsumowanie rejsu)
        public long startTicks; // początek rejsu, UTC (podsumowanie: dni na wyspie)
    }

    /// <summary>
    /// Wszystkie ceny sprzedaży w jednym miejscu.
    /// Cena = podstawa x jakość x (1 + suma premii) x kanał x perły x mnożniki chwili (popyt dnia, dzień targowy, pełnia),
    /// potem targowanie (szansa na x2 i więcej). Premie z UpgradeEffects (drzewko) i Prestige (dary mgły).
    /// Zlicza też zarobek rejsu (prestiż) i sprzedane sztuki (renoma).
    /// </summary>
    public static class Market
    {
        public const float BaseCritMultiplier = 2f;
        const float ComboWindow = 15f;        // sekundy między sprzedażami, które liczą się do serii
        const float ComboStep = 0.02f;        // +2% za każdą sprzedaż w serii
        const float LampStep = 0.02f;         // +2% za zapaloną lampę
        const float SpeciesStep = 0.02f;      // +2% za gatunek w bestiariuszu
        const int ReputationStep = 200;       // +1% za każde 200 sprzedanych sztuk
        const float DawnWindow = 600f;        // poranny targ: 10 minut po świcie (sekundy doby gry)
        const int MarketDayEvery = 4;         // co czwarta doba gry

        public static long RunCoins { get; private set; }
        public static int Sold { get; private set; }
        /// <summary>Zebrane plony i owoce w tym rejsie (ręcznie i przez ogniki).</summary>
        public static int RunHarvests { get; private set; }
        /// <summary>Początek rejsu (UTC ticks); starsze zapisy: chwila wczytania.</summary>
        public static long RunStartTicks { get; private set; }

        public static void NoteHarvest(int count = 1) => RunHarvests += Math.Max(0, count);
        static float lastSale = -100f;
        static int combo;
        static float pendingIncome;           // ułamki monet z dniówki i Żaru

        // grupy roślin dla ulepszeń z gałęzi plonów (Korzenny targ, Cech młynarzy...)
        enum Group { None, Roots, Grain, Pumpkin, Herb, Grave, Flower }
        static readonly Dictionary<string, Group> Groups = new Dictionary<string, Group>
        {
            { "carrot", Group.Roots }, { "garlic", Group.Roots }, { "wolfcarrot", Group.Roots },
            { "wheat", Group.Grain }, { "ashwheat", Group.Grain },
            { "pumpkin", Group.Pumpkin }, { "ghostpumpkin", Group.Pumpkin }, { "hybridpumpkin", Group.Pumpkin },
            { "poppy", Group.Herb }, { "wormwood", Group.Herb }, { "moonpoppy", Group.Herb }, { "bloodwormwood", Group.Herb },
            { "corpseshroom", Group.Grave }, { "graveivy", Group.Grave }, { "mandrake", Group.Grave }, { "screamgarlic", Group.Grave },
            { "moonflower", Group.Flower }, { "bloodrose", Group.Flower }, { "belladonna", Group.Flower },
        };

        public static void Restore(MarketSave s)
        {
            RunCoins = s != null ? Math.Max(0, s.runCoins) : 0;
            Sold = s != null ? Math.Max(0, s.sold) : 0;
            RunHarvests = s != null ? Math.Max(0, s.harvests) : 0;
            RunStartTicks = s != null && s.startTicks > 0 ? s.startTicks : DateTime.UtcNow.Ticks;
            lastSale = -100f;
            combo = 0;
            pendingIncome = 0f;
        }

        public static MarketSave Snapshot() =>
            new MarketSave { runCoins = RunCoins, sold = Sold, harvests = RunHarvests, startTicks = RunStartTicks };

        /// <summary>Każda moneta, która wpada do portfela (Wallet.Add), liczy się do zarobku rejsu.</summary>
        public static void NoteIncome(int coins)
        {
            if (coins > 0)
                RunCoins += coins;
        }

        // ------------------------------------------------------------ ceny

        /// <summary>Cena nasiona (Pamiątkowe odmiany, dary mgły). Hybryd się nie kupuje (0).</summary>
        public static int SeedPrice(CropDefinition c)
        {
            if (c == null || c.seedPrice <= 0)
                return 0;
            return Mathf.Max(1, Mathf.RoundToInt(c.seedPrice * (1f - E.SeedDiscount)));
        }

        /// <summary>Mnożnik jakości (1-3 gwiazdki) z premią „Gwiazdy jakości”.</summary>
        public static float QualityMultiplier(int quality) => 1f + (CropDefinition.QualityMultiplier(quality) - 1f) * (1f + E.QualityBoost);

        /// <summary>Sekundy wzrostu rośliny nocy na 1 plon nocy (waluta): krótkie rośliny dają go czasem, długie częściej.</summary>
        public const float NightYieldSeconds = 600f;

        /// <summary>
        /// Plony nocy (waluta) za count roślin nocy („Obfite noce” więcej). Średnio plon × wzrost / 10 min + premia,
        /// reszta ułamkowa losowana (trupi grzyb: 1 co piąty zbiór, upiorna dynia: 3 na 5, cmentarny bluszcz: 1).
        /// </summary>
        public static int NightYield(CropDefinition c, int count = 1)
        {
            if (c == null || c.kind != CropKind.Night || count <= 0)
                return 0;
            float expected = count * (c.nightHarvestYield * c.growSeconds / NightYieldSeconds + E.NightYieldBonus);   // „Obfite noce”: +N za każdą
            int whole = Mathf.FloorToInt(expected);
            return whole + (UnityEngine.Random.value < expected - whole ? 1 : 0);
        }

        /// <summary>Monety za count sztuk rośliny o jakości 1-3. Liczy premie, targowanie, serię i renomę.</summary>
        public static int SellCrop(CropDefinition crop, int quality, int count, SaleChannel channel)
        {
            if (crop == null || count <= 0)
                return 0;
            if (E.QualityUp > 0f && quality < 3 && UnityEngine.Random.value < E.QualityUp)
                quality++;   // Staranne ręce
            float bonus = SituationBonus(channel, count) + (crop.kind == CropKind.Day ? E.SaleDay : E.SaleNight)
                        + GroupBonus(crop.id) + (crop.hybrid ? E.SaleHybrid : 0f)
                        + (CropSeasons.SingleSeason(crop) ? E.SeasonSale : 0f);   // Sezonowy targ
            float value = crop.sellPrice * QualityMultiplier(quality) * count * (1f + bonus) * ChannelMultiplier(channel)
                        * Prestige.SaleMultiplier * MomentMultiplier();
            if (crop.id == DemandCropId())
                value *= Mathf.Max(1f, E.DemandMult);
            if (crop.kind == CropKind.Night)
                value *= Familiars.FamiliarSystem.NightSale;   // ćma księżycowa
            if (IsFullMoon())
                value *= Mathf.Max(1f, E.FullMoonSale);
            return Finish(value, count);
        }

        /// <summary>Monety za count owoców gatunku (Owocowy stragan, Ciężkie gałęzie, Owoce pełni).</summary>
        public static int SellFruit(TreeSpecies tree, int count, SaleChannel channel)
        {
            if (tree == null || count <= 0)
                return 0;
            float value = tree.fruitPrice * count * (1f + SituationBonus(channel, count) + E.SaleFruit) * ChannelMultiplier(channel)
                        * Prestige.SaleMultiplier * MomentMultiplier();
            if (E.FruitDouble > 0f && UnityEngine.Random.value < E.FruitDouble)
                value *= 2f;
            if (IsFullMoon())
                value *= Mathf.Max(1f, E.FullMoonFruit);
            return Finish(value, count);
        }

        static float ChannelMultiplier(SaleChannel channel)
        {
            switch (channel)
            {
                case SaleChannel.Merchant: return E.MerchantPrice;
                case SaleChannel.Wisp: return Mathf.Max(E.MerchantPrice, E.WispSale);
                case SaleChannel.Ghost: return Sea.GhostMerchant.SellMultiplier * (1f + E.GhostBuy);
                default: return 1f;
            }
        }

        /// <summary>Premie sumowane (procenty): ogólne, chwila (noc, świt, deszcz, zima), lampy, renoma, kolekcja, seria sprzedaży.</summary>
        static float SituationBonus(SaleChannel channel, int count)
        {
            float s = E.SaleAll + E.SaleAll2;
            var game = GameLoop.Instance;
            if (game != null)
            {
                if (game.Clock != null)
                {
                    if (game.Clock.IsNight) s += E.NightTimeSale;
                    else if (game.Clock.Second < DawnWindow) s += E.DawnSale;
                }
                if (Weather.WeatherSystem.Raining) s += E.RainSale;   // Deszczowy kupiec
                if (Seasons.Current == Season.Winter) s += E.WinterSale;   // Zimowy jarmark
            }
            if (E.LampCap > 0f) s += Mathf.Min(E.LampCap, LampStep * LampsOn());
            if (E.RepCap > 0f) s += Mathf.Min(E.RepCap, 0.01f * (Sold / ReputationStep));
            if (E.CollectorCap > 0f) s += Mathf.Min(E.CollectorCap, SpeciesStep * SpeciesSeen());
            if (E.ComboCap > 0f && Time.unscaledTime - lastSale <= ComboWindow) s += Mathf.Min(E.ComboCap, ComboStep * combo);
            if (count >= 10 && channel != SaleChannel.Hand) s += E.BulkBonus;
            if (channel == SaleChannel.Hand) s += E.HandBonus;
            if (channel == SaleChannel.Crate) s += E.CrateBonus;
            return s;
        }

        /// <summary>Mnożnik chwili: dzień targowy (co czwarta doba).</summary>
        static float MomentMultiplier()
        {
            var game = GameLoop.Instance;
            if (E.MarketDay > 1f && game != null && game.Clock != null && game.Clock.CycleNumber % MarketDayEvery == 0)
                return E.MarketDay;
            return 1f;
        }

        /// <summary>Targowanie (szansa na x2 / x3 / x4), szczęśliwa moneta, liczniki serii i renomy.</summary>
        static int Finish(float value, int count)
        {
            var game = GameLoop.Instance;
            float critChance = E.CritChance + Prestige.CritBonus;
            if (critChance > 0f && UnityEngine.Random.value < critChance)
            {
                float m = Mathf.Max(BaseCritMultiplier, E.CritMult);
                value *= m;
                GameEvents.RaiseToast("toast.golden_deal", Mathf.Round(m * 10f) / 10f);
            }
            if (E.ShardChance > 0f && game != null && UnityEngine.Random.value < E.ShardChance)
            {
                game.Wallet.Add(CurrencyType.StarShard, 1);
                GameEvents.RaiseToast("toast.lucky_coin");
            }
            combo = Time.unscaledTime - lastSale <= ComboWindow ? combo + 1 : 0;
            lastSale = Time.unscaledTime;
            Sold += count;
            return Mathf.Max(1, Mathf.RoundToInt(value));
        }

        static float GroupBonus(string id)
        {
            if (id == null || !Groups.TryGetValue(id, out var g))
                return 0f;
            switch (g)
            {
                case Group.Roots: return E.GroupRoots;
                case Group.Grain: return E.GroupGrain;
                case Group.Pumpkin: return E.GroupPumpkin;
                case Group.Herb: return E.GroupHerb;
                case Group.Grave: return E.GroupGrave;
                case Group.Flower: return E.GroupFlower;
                default: return 0f;
            }
        }

        static int LampsOn()
        {
            int n = 0;
            foreach (var l in Lighting.Lamp.All)
                if (l != null && l.IsOn) n++;
            return n;
        }

        static int SpeciesSeen()
        {
            var chronicle = Lore.Chronicle.Instance;
            if (chronicle == null)
                return 0;
            int n = 0;
            foreach (var _ in chronicle.FoundWithPrefix("crop:"))
                n++;
            return n;
        }

        static bool IsFullMoon()
        {
            var game = GameLoop.Instance;
            return game != null && game.Clock != null && game.Clock.IsFullMoon;
        }

        // ------------------------------------------------------------ popyt dnia

        /// <summary>Roślina z popytem w tej dobie gry (tylko z ulepszeniem „Popyt dnia”), null bez niego.</summary>
        public static string DemandCropId()
        {
            if (E.DemandMult <= 1f)
                return null;
            var game = GameLoop.Instance;
            if (game == null || game.Crops == null || game.Clock == null)
                return null;
            var pool = new List<CropDefinition>();
            foreach (var c in game.Crops.crops)
                if (c != null && !c.hybrid && Farming.CropSeasons.Now(c))   // tylko to, co da się teraz zasiać (pory roku)
                    pool.Add(c);
            if (pool.Count == 0)
                return null;
            long h = game.Clock.CycleNumber * 2654435761L;
            return pool[(int)((h % pool.Count + pool.Count) % pool.Count)].id;
        }

        // ------------------------------------------------------------ dochody poza sprzedażą

        /// <summary>Ułamkowe monety (dniówka, Żar w złoto): do portfela trafiają całe.</summary>
        public static void AddIncome(Wallet wallet, float coins)
        {
            if (wallet == null || coins <= 0f)
                return;
            pendingIncome += coins * Prestige.SaleMultiplier;
            int whole = Mathf.FloorToInt(pendingIncome);
            if (whole <= 0)
                return;
            pendingIncome -= whole;
            wallet.Add(whole);
        }

        /// <summary>Świt nowej doby gry: odsetki, dzierżawa, ogłoszenie popytu dnia i dnia targowego.</summary>
        public static void OnNewDay(GameLoop game)
        {
            if (game == null)
                return;
            int coins = 0;
            if (E.InterestRate > 0f)
                coins += Mathf.Min(Mathf.RoundToInt(game.Wallet.Coins * E.InterestRate), Mathf.RoundToInt(E.InterestRate * 100000f));   // limit 1000 monet za każdy 1%
            if (E.LandRent > 0 && game.Island != null)
                coins += E.LandRent * game.Island.PurchasedTiles;
            if (coins > 0)
            {
                game.Wallet.Add(coins);
                GameEvents.RaiseToast("toast.dawn_income", coins);
            }
            string demand = DemandCropId();
            if (demand != null)
                GameEvents.RaiseToast("toast.demand", Localization.Loc.Get("crop." + demand), E.DemandMult);
            if (MomentMultiplier() > 1f)
                GameEvents.RaiseToast("toast.market_day", Mathf.RoundToInt((E.MarketDay - 1f) * 100f));
        }
    }
}
