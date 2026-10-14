using UnityEngine;

namespace Wyspa.Farming
{
    public enum CropKind
    {
        /// <summary>Rośnie, gdy gracz jest aktywny i pole jest oświetlone (dzień albo lampa).</summary>
        Day,
        /// <summary>Rośnie w nocy, w ciemności (poza lampami) - także gdy gracz śpi.</summary>
        Night,
    }

    [CreateAssetMenu(menuName = "Wyspa/Roślina", fileName = "Crop_")]
    public class CropDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public CropKind kind = CropKind.Day;

        [Tooltip("Dzień: sekundy aktywności w świetle. Noc: sekundy ciemności.")]
        public float growSeconds = 600f;

        [Tooltip("4 fazy: 0 nasiono, 1 kiełek, 2 rośnie, 3 dojrzała.")]
        public Sprite[] stages = new Sprite[4];

        [Tooltip("Piksele emisyjne faz (*_glow), puste gdy faza nie świeci. Rysowane na materiale bez oświetlenia.")]
        public Sprite[] glowStages = new Sprite[4];

        public int seedPrice = 5;
        public int sellPrice = 12;

        [Tooltip("Rośliny nocy: ile „plonów nocy” (waluta) daje zbiór.")]
        public int nightHarvestYield = 1;

        [Header("Etap 4a")]
        [Tooltip("Rośnie tylko w pełnię księżyca (krwawa róża, GDD §4.7).")]
        public bool requiresFullMoon;
        [Tooltip("Hybryda: nasiona tylko z mutacji (worek nasion), nie do kupienia.")]
        public bool hybrid;
        [Tooltip("Rodzice hybrydy: sąsiednie grządki z tymi roślinami mogą dać nasiono przy zbiorze (§11.3 D).")]
        public string parentA, parentB;
        [Tooltip("Ikona nasiona w UI (bez przedrostka icon_), puste = seed_<id>.")]
        public string seedIcon;

        public string SeedIcon => string.IsNullOrEmpty(seedIcon) ? "seed_" + id : seedIcon;

        /// <summary>Jakość 1-3 gwiazdki: cena x1 / x1.5 / x2.5 (GDD §11.3 D).</summary>
        public static float QualityMultiplier(int quality) => quality >= 3 ? 2.5f : (quality == 2 ? 1.5f : 1f);

        public int SellPrice(int quality) => Mathf.RoundToInt(sellPrice * QualityMultiplier(quality));

        public bool IsParentPair(string a, string b) =>
            hybrid && ((a == parentA && b == parentB) || (a == parentB && b == parentA));

        public Sprite StageSprite(int stage)
        {
            if (stages == null || stages.Length == 0 || stage < 0)
                return null;
            return stages[Mathf.Min(stage, stages.Length - 1)];
        }

        public Sprite StageGlow(int stage)
        {
            if (glowStages == null || glowStages.Length == 0 || stage < 0)
                return null;
            return glowStages[Mathf.Min(stage, glowStages.Length - 1)];
        }
    }
}
