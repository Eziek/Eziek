using System.Collections.Generic;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.Farming
{
    /// <summary>
    /// Rośliny sezonowe (GDD §9 p. 127): w których porach roku można daną roślinę zasiać. W każdej porze są co najmniej dwie
    /// startowe rośliny dnia (marchew, zboże, dynia) i dwie nocy (trupi grzyb, księżycowy kwiat, upiorna dynia). Rośliny
    /// posadzone przed zmianą pory rosną dalej normalnie; poza porą nie da się ich tylko zasiać (koło nasion, losowe
    /// sadzenie, ognik-siewca). Hybrydy, krwawa róża i cmentarny bluszcz: cały rok. Nieznane id: cały rok.
    /// Do tego 16 roślin jednej pory (p. 129): na każdą porę 2 dnia i 2 nocy, do zasiania tylko w niej.
    /// </summary>
    public static class CropSeasons
    {
        const int Sp = 1 << (int)Season.Spring, Su = 1 << (int)Season.Summer, Au = 1 << (int)Season.Autumn, Wi = 1 << (int)Season.Winter;
        const int All = Sp | Su | Au | Wi;

        static readonly Dictionary<string, int> Masks = new Dictionary<string, int>
        {
            // rośliny dnia
            { "carrot", Sp | Au | Wi }, { "wheat", Sp | Su | Au }, { "pumpkin", Su | Au | Wi },
            { "garlic", Au | Wi | Sp }, { "poppy", Sp | Su }, { "wormwood", Su | Au | Wi },
            // rośliny nocy
            { "corpseshroom", Su | Au | Wi }, { "moonflower", Sp | Su | Wi }, { "ghostpumpkin", Au | Wi | Sp },
            { "belladonna", Su | Au }, { "mandrake", Wi | Sp }, { "graveivy", All }, { "bloodrose", All },
            // rośliny sezonowe (§9 p. 129): tylko jedna pora
            { "radish", Sp }, { "rhubarb", Sp }, { "wolfsbane", Sp }, { "fernflower", Sp },
            { "lavender", Su }, { "sunflower", Su }, { "nightjasmine", Su }, { "lanternfruit", Su },
            { "turnip", Au }, { "heather", Au }, { "puffball", Au }, { "catseye", Au },
            { "kale", Wi }, { "hellebore", Wi }, { "frostbloom", Wi }, { "mistletoe", Wi },
        };

        static int Mask(CropDefinition c) => c == null || c.hybrid || !Masks.TryGetValue(c.id, out int m) ? All : m;

        public static bool Grows(CropDefinition c, Season s) => (Mask(c) & (1 << (int)s)) != 0;

        /// <summary>Roślina jednej pory roku (p. 129): poza nią koło nasion jej nie pokazuje (chyba że są nasiona w worku).</summary>
        public static bool SingleSeason(CropDefinition c)
        {
            int m = Mask(c);
            return m != 0 && (m & (m - 1)) == 0;
        }

        /// <summary>Czy można ją zasiać teraz (bieżąca pora roku).</summary>
        public static bool Now(CropDefinition c) => Grows(c, Seasons.Current);

        /// <summary>„wiosna, jesień, zima” albo „cały rok” (do opisów nasion).</summary>
        public static string Text(CropDefinition c)
        {
            int m = Mask(c);
            if (m == All)
                return Loc.Get("ui.seed.all_year");
            var names = new List<string>();
            for (int i = 0; i < 4; i++)
                if ((m & (1 << i)) != 0)
                    names.Add(Loc.Get(Seasons.NameKey((Season)i) + ".lower"));
            return string.Join(", ", names);
        }
    }
}
