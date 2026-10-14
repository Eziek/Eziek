using System;
using UnityEngine;

namespace Wyspa.Farming
{
    public struct GrowthContext
    {
        public bool playerActive;
        public bool night;
        public bool lit;
        /// <summary>Np. klatka z ognikiem przyspiesza wzrost w swoim świetle.</summary>
        public float multiplier;
        /// <summary>Pełnia księżyca: rośliny nocy rosną 2x szybciej, krwawa róża rośnie tylko wtedy.</summary>
        public bool fullMoon;
    }

    /// <summary>Stan pola - czyste dane, bez MonoBehaviour (łatwe do zapisu i testów).</summary>
    [Serializable]
    public class PlotState
    {
        public string cropId;
        public float growth;
        /// <summary>Sekundy wzrostu w świetle ognika (lampa z mnożnikiem wzrostu): połowa czasu daje +1 do jakości.</summary>
        public float boostSeconds;
        /// <summary>Nawóz (kompostownik, później): +1 do jakości.</summary>
        public bool fertilized;

        [NonSerialized] public CropDefinition crop;

        public bool IsEmpty => crop == null;
        public bool IsRipe => crop != null && growth >= crop.growSeconds;

        /// <summary>-1 puste, 0 nasiono, 1 kiełek, 2 rośnie, 3 dojrzała.</summary>
        public int Stage
        {
            get
            {
                if (crop == null)
                    return -1;
                if (IsRipe)
                    return 3;
                return Mathf.Clamp((int)(growth / crop.growSeconds * 3f), 0, 2);
            }
        }

        /// <summary>Jakość 1-3: baza 1, +1 za wzrost w świetle ognika, +1 za nawóz (GDD §11.3 D).</summary>
        public int Quality
        {
            get
            {
                if (crop == null)
                    return 1;
                int q = 1;
                if (boostSeconds >= crop.growSeconds * 0.5f) q++;
                if (fertilized) q++;
                return q;
            }
        }

        public bool CanGrow(in GrowthContext ctx)
        {
            if (crop == null || IsRipe)
                return false;
            if (crop.kind == CropKind.Day)
                return ctx.playerActive && ctx.lit;
            return ctx.night && !ctx.lit && (!crop.requiresFullMoon || ctx.fullMoon);
        }

        /// <summary>Zwraca true, gdy zmieniła się faza (do efektu "pop").</summary>
        public bool Tick(float dt, in GrowthContext ctx)
        {
            if (!CanGrow(ctx))
                return false;
            int before = Stage;
            float mult = ctx.multiplier > 0f ? ctx.multiplier : 1f;
            if (crop.kind == CropKind.Night && ctx.fullMoon)
                mult *= 2f;
            if (ctx.multiplier > 1f)
                boostSeconds += dt * mult;
            growth = Mathf.Min(crop.growSeconds, growth + dt * mult);
            return Stage != before;
        }

        public void Plant(CropDefinition def)
        {
            crop = def;
            cropId = def != null ? def.id : null;
            growth = 0f;
            boostSeconds = 0f;
            fertilized = false;
        }

        public void Clear()
        {
            crop = null;
            cropId = null;
            growth = 0f;
            boostSeconds = 0f;
            fertilized = false;
        }
    }
}
