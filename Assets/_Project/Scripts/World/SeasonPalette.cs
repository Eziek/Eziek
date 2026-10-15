using System.Collections.Generic;
using Wyspa.Core;

namespace Wyspa.World
{
    /// <summary>
    /// Kolory lądu w porach roku (GDD §9 p. 127): gotowy obraz wyspy (malarz zawsze maluje lato) przemalowany przy
    /// wgrywaniu tekstury: trawa wiosną świeższa, jesienią oliwkowo-brunatna, zimą śnieg (z cieniami w miejscach ciemniejszej
    /// trawy); błoto, kamienie, ziemia klifu i kałuże bez zmian. Czysty C# (zamiana dokładnych kolorów palety).
    /// </summary>
    public static class SeasonPalette
    {
        static readonly Dictionary<int, int>[] Maps =
        {
            Map(("344a3b", "3a5640"), ("21342c", "263d30"), ("141f1c", "16231d")),   // wiosna
            null,                                                                    // lato: obraz bez zmian
            Map(("344a3b", "4a4630"), ("21342c", "3a3424"), ("141f1c", "241e16")),   // jesień
            Map(("344a3b", "808b96"), ("21342c", "636e7a"), ("141f1c", "48515c")),   // zima: śnieg (przygaszony)
        };

        static Dictionary<int, int> Map(params (string from, string to)[] pairs)
        {
            var d = new Dictionary<int, int>();
            foreach (var (f, t) in pairs)
                d[System.Convert.ToInt32(f, 16)] = System.Convert.ToInt32(t, 16);
            return d;
        }

        /// <summary>Obraz lądu (RGBA) w danej porze; lato: ta sama tablica, inne pory: nowa.</summary>
        public static byte[] Recolor(byte[] rgba, Season season)
        {
            var map = Maps[(int)season];
            if (rgba == null || map == null)
                return rgba;
            var outp = (byte[])rgba.Clone();
            for (int i = 0; i + 3 < outp.Length; i += 4)
            {
                if (outp[i + 3] == 0)
                    continue;
                int c = (outp[i] << 16) | (outp[i + 1] << 8) | outp[i + 2];
                if (!map.TryGetValue(c, out int n))
                    continue;
                outp[i] = (byte)(n >> 16);
                outp[i + 1] = (byte)(n >> 8);
                outp[i + 2] = (byte)n;
            }
            return outp;
        }
    }
}
