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
    }

    /// <summary>Stan pola - czyste dane, bez MonoBehaviour (łatwe do zapisu i testów).</summary>
    [Serializable]
    public class PlotState
    {
        public string cropId;
        public float growth;

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

        public bool CanGrow(in GrowthContext ctx)
        {
            if (crop == null || IsRipe)
                return false;
            return crop.kind == CropKind.Day
                ? ctx.playerActive && ctx.lit
                : ctx.night && !ctx.lit;
        }

        /// <summary>Zwraca true, gdy zmieniła się faza (do efektu "pop").</summary>
        public bool Tick(float dt, in GrowthContext ctx)
        {
            if (!CanGrow(ctx))
                return false;
            int before = Stage;
            float mult = ctx.multiplier > 0f ? ctx.multiplier : 1f;
            growth = Mathf.Min(crop.growSeconds, growth + dt * mult);
            return Stage != before;
        }

        public void Plant(CropDefinition def)
        {
            crop = def;
            cropId = def != null ? def.id : null;
            growth = 0f;
        }

        public void Clear()
        {
            crop = null;
            cropId = null;
            growth = 0f;
        }
    }
}
