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

        public int seedPrice = 5;
        public int sellPrice = 12;

        [Tooltip("Rośliny nocy: ile „plonów nocy” (waluta) daje zbiór.")]
        public int nightHarvestYield = 1;

        public Sprite StageSprite(int stage)
        {
            if (stages == null || stages.Length == 0 || stage < 0)
                return null;
            return stages[Mathf.Min(stage, stages.Length - 1)];
        }
    }
}
