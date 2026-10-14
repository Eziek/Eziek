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
        public static float WorkEssence = 1f;
        public static float FatigueMultiplier = 0.5f, HeavyFatigueMultiplier = 0.25f;
        public static float StreakBonus = 0.25f;
        public static float DayGrowth = 1f, NightGrowth = 1f;
        public static float MutationChance = 0.03f;
        public static float WildRuneInterval = 1f;
        public static float MerchantPrice = 0.9f;
        public static float BoatDuration = 1f;
        public static float LampBurn = 1f;

        public static void Apply(UpgradeState u)
        {
            WispSpeed = u.Value(UpgradeIds.WispSpeed, 1f);
            NestCapacity = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.NestSize, 3f));
            WispXp = u.Value(UpgradeIds.WispXp, 1f);
            CrateCapacity = UnityEngine.Mathf.RoundToInt(u.Value(UpgradeIds.BigCrates, 20f));
            WorkEssence = u.Value(UpgradeIds.Kindling, 1f);
            FatigueMultiplier = u.Value(UpgradeIds.SecondWind, 0.5f);
            HeavyFatigueMultiplier = FatigueMultiplier * 0.5f;
            StreakBonus = u.Value(UpgradeIds.FocusStreak, 0.25f);
            DayGrowth = u.Value(UpgradeIds.FertileSoil, 1f);
            NightGrowth = u.Value(UpgradeIds.NightDew, 1f);
            MutationChance = u.Value(UpgradeIds.Crossbreed, 0.03f);
            WildRuneInterval = u.Value(UpgradeIds.WildRunes, 1f);
            MerchantPrice = u.Value(UpgradeIds.FairTrade, 0.9f);
            BoatDuration = u.Value(UpgradeIds.SwiftBoat, 1f);
            LampBurn = u.Value(UpgradeIds.SpiderWicks, 1f);
        }
    }
}
