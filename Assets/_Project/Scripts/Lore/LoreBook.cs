using System.Collections.Generic;

namespace Wyspa.Lore
{
    /// <summary>
    /// Zapiski „Ostatniej latarni” (GDD §11.3 H, wybór gracza): wyspa to ostatni oświetlony ląd Łańcucha Latarni, poprzedni
    /// Opiekun popłynął w mgłę. Każde źródło ma własną pulę, a zapiski z puli odkrywa się po kolei, więc historia czyta się
    /// w porządku. Klucze lore.* to wpisy tabeli game.csv, w kronice zapisywane jako „lore:klucz”.
    /// </summary>
    public static class LoreBook
    {
        /// <summary>Zapiski Opiekuna: znaleziska na nowych kafelkach i nagrobki.</summary>
        public static readonly string[] Keeper = { "lore.1", "lore.2", "lore.3", "lore.4", "lore.5", "lore.6", "lore.7", "lore.8" };
        /// <summary>Zabytki i potwór (stałe miejsca na wyspie).</summary>
        public static readonly string[] Island = { "lore.crypt", "lore.statue", "lore.stairs", "lore.monster" };

        /// <summary>Wszystkie zapiski w kolejności czytania (strona Zapiski w bestiariuszu).</summary>
        public static IEnumerable<string> All
        {
            get
            {
                foreach (var k in Keeper) yield return k;
                foreach (var k in Island) yield return k;
                foreach (var d in Sea.Destinations.All)
                    foreach (var k in d.lore) yield return k;
            }
        }

        public static bool Found(string key) => Chronicle.Instance != null && Chronicle.Instance.Found("lore:" + key);

        /// <summary>Pierwszy nieodkryty zapisek z puli (null, gdy wszystkie odkryte).</summary>
        public static string NextOf(string[] pool)
        {
            foreach (var k in pool)
                if (!Found(k)) return k;
            return null;
        }

        public static int FoundIn(string[] pool)
        {
            int n = 0;
            foreach (var k in pool)
                if (Found(k)) n++;
            return n;
        }

        /// <summary>Kolejny zapisek Opiekuna; gdy wszystkie odkryte, losowy z nich (przypomnienie). Notuje w kronice.</summary>
        public static string TakeKeeper(System.Random rnd = null)
        {
            string key = NextOf(Keeper) ?? Keeper[rnd != null ? rnd.Next(Keeper.Length) : UnityEngine.Random.Range(0, Keeper.Length)];
            Chronicle.Note("lore:" + key);
            return key;
        }
    }
}
