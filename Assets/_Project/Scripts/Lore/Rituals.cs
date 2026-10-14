using System.Collections.Generic;
using Wyspa.Core;
using Wyspa.Economy;

namespace Wyspa.Lore
{
    /// <summary>Rytuał z Księgi (GDD §11.3 G): stały, pasywny bonus za punkty cyklu księżyca i rzadkie surowce.</summary>
    public class RitualDef
    {
        public string id, nameKey, descKey;
        public int points;
        public List<CurrencyAmount> cost;
    }

    public static class Rituals
    {
        static List<CurrencyAmount> C(CurrencyType t, int n) => new List<CurrencyAmount> { new CurrencyAmount(t, n) };

        public static readonly RitualDef[] All =
        {
            new RitualDef { id = "dusk", nameKey = "ritual.dusk", descKey = "ritual.dusk.desc", points = 2, cost = C(CurrencyType.StarShard, 3) },
            new RitualDef { id = "quiet_lamps", nameKey = "ritual.quiet_lamps", descKey = "ritual.quiet_lamps.desc", points = 2, cost = C(CurrencyType.Scale, 8) },
            new RitualDef { id = "moon_dew", nameKey = "ritual.moon_dew", descKey = "ritual.moon_dew.desc", points = 3, cost = C(CurrencyType.DeepGift, 1) },
            new RitualDef { id = "deep_roots", nameKey = "ritual.deep_roots", descKey = "ritual.deep_roots.desc", points = 2, cost = C(CurrencyType.Wood, 20) },
            new RitualDef { id = "fog_whisper", nameKey = "ritual.fog_whisper", descKey = "ritual.fog_whisper.desc", points = 2, cost = C(CurrencyType.StarShard, 2) },
            new RitualDef { id = "tide_memory", nameKey = "ritual.tide_memory", descKey = "ritual.tide_memory.desc", points = 3, cost = C(CurrencyType.DeepGift, 1) },
        };

        /// <summary>Ustawia efekty odprawionych rytuałów w systemach gry.</summary>
        public static void Apply(Chronicle c)
        {
            bool Has(string id) => c != null && c.Has(id);
            GameLoop.DuskGrowth = Has("dusk");
            Lighting.Lamp.ConsumptionMultiplier = Has("quiet_lamps") ? 0.75f : 1f;
            GameClock.FullMoonWindow = Has("moon_dew") ? 2f / 16f : 1f / 16f;   // jedna albo dwie noce pełni (cykl 8 nocy)
            GameLoop.DayGrowthMultiplier = Has("deep_roots") ? 1.1f : 1f;
            Sea.GhostMerchant.StayMultiplier = Has("fog_whisper") ? 2f : 1f;
            Sea.Expedition.DurationMultiplier = Has("tide_memory") ? 0.75f : 1f;
            Story.Refresh(c);   // zakończenie historii: stałe nagrody
        }
    }
}
