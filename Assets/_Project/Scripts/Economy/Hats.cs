using System.Collections.Generic;
using Wyspa.Core;

namespace Wyspa.Economy
{
    /// <summary>
    /// Czapki ogników (GDD §9 p. 88): kosmetyka bez wpływu na grę. Kupione za dużo monet albo za Perły Mgły zostają na zawsze,
    /// także po rejsie (zapis w PrestigeSave). Zakłada się je w panelu gniazda, osobno każdemu ognikowi (WispSave.hat).
    /// Grafika: tools/pixel-proto/wisp_hats.py (ta sama kolejność i ceny).
    /// </summary>
    public static class Hats
    {
        public class Hat
        {
            public string id;
            public int coins, pearls;   // cena: monety albo perły
            public string NameKey => "hat." + id;
        }

        static Hat H(string id, int coins, int pearls) => new Hat { id = id, coins = coins, pearls = pearls };

        public static readonly Hat[] All =
        {
            H("straw", 3000, 0), H("beanie", 4000, 0), H("nightcap", 5000, 0), H("flower", 6000, 0), H("mushroom", 8000, 0),
            H("feather", 10000, 0), H("tophat", 12000, 0), H("witch", 15000, 0), H("pumpkin", 20000, 0), H("horns", 25000, 0),
            H("tricorne", 0, 2), H("skull", 0, 3), H("candle", 0, 3), H("crown", 0, 4), H("halo", 0, 5),
        };

        static readonly HashSet<string> owned = new HashSet<string>();

        public static Hat Find(string id)
        {
            foreach (var h in All)
                if (h.id == id) return h;
            return null;
        }

        public static bool Owns(string id) => id != null && owned.Contains(id);
        public static bool AnyOwned => owned.Count > 0;

        public static bool CanBuy(Hat h, Wallet wallet) =>
            h != null && !Owns(h.id) && (h.pearls > 0 ? Prestige.Pearls >= h.pearls : wallet != null && wallet.Coins >= h.coins);

        public static bool TryBuy(Hat h, Wallet wallet)
        {
            if (!CanBuy(h, wallet))
                return false;
            if (h.pearls > 0 ? !Prestige.TrySpendPearls(h.pearls) : !wallet.TrySpend(h.coins))
                return false;
            owned.Add(h.id);
            return true;
        }

        /// <summary>Następna posiadana czapka po current (null = bez czapki): przełączanie w panelu gniazda.</summary>
        public static string Next(string current)
        {
            var list = new List<string> { null };
            foreach (var h in All)
                if (owned.Contains(h.id)) list.Add(h.id);
            int i = list.IndexOf(current);
            return list[(i + 1) % list.Count];
        }

        public static List<string> Snapshot() => new List<string>(owned);

        public static void Restore(List<string> list)
        {
            owned.Clear();
            if (list != null)
                foreach (var id in list)
                    if (Find(id) != null) owned.Add(id);
        }
    }
}
