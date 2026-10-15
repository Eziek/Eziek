using System.Collections.Generic;
using Wyspa.Economy;

namespace Wyspa.World
{
    /// <summary>
    /// Historia wysepki (§13.11): szczątki dawnej budowli Łańcucha Latarni. Każdą część (kafelek wysepki) odbudowuje się
    /// za drewno, kamień i monety; po odbudowie wszystkich stoi budowla, która przy pracy gracza daje walutę.
    /// Klucze tekstów: story.&lt;id&gt;.ruin / .building / .desc, zapiski lore.story.&lt;id&gt;.1-4 i .end.
    /// </summary>
    public class StorySite
    {
        public string id;
        public int wood, stone, coins;        // koszt odbudowy jednej części
        public CurrencyType product;
        public int amount;
        public float interval;                // sekundy pracy na jeden dar

        public const int LoreLines = 4;

        public string NameKey => "story." + id + ".ruin";
        public string BuildingKey => "story." + id + ".building";
        public string DescKey => "story." + id + ".desc";
        public string Lore(int line) => "lore.story." + id + "." + line;
        public string EndKey => "lore.story." + id + ".end";

        public List<CurrencyAmount> RepairCost() => new List<CurrencyAmount>
        {
            new CurrencyAmount(CurrencyType.Wood, wood),
            new CurrencyAmount(CurrencyType.Stone, stone),
            new CurrencyAmount(CurrencyType.Coins, coins),
        };

        /// <summary>Wszystkie zapiski historii w kolejności czytania (strona Zapiski w bestiariuszu).</summary>
        public IEnumerable<string> AllLore()
        {
            for (int i = 1; i <= LoreLines; i++)
                yield return Lore(i);
            yield return EndKey;
        }
    }

    public static class StorySites
    {
        // balans wstępny: dużo drewna i kamienia na część (prośba gracza), produkcja tylko w czasie pracy
        public static readonly StorySite[] All =
        {
            new StorySite { id = "chapel", wood = 40, stone = 20, coins = 200, product = CurrencyType.NightHarvest, amount = 2, interval = 480f },
            new StorySite { id = "watch", wood = 30, stone = 40, coins = 250, product = CurrencyType.Coins, amount = 80, interval = 300f },
            new StorySite { id = "sawmill", wood = 20, stone = 30, coins = 300, product = CurrencyType.Wood, amount = 8, interval = 240f },
            new StorySite { id = "observatory", wood = 60, stone = 40, coins = 400, product = CurrencyType.StarShard, amount = 1, interval = 1200f },
        };

        /// <summary>Historia wysepki: stała dla ziarna świata, każda wysepka (najwyżej 4) ma inną.</summary>
        public static StorySite ForIslet(int seed, int islet)
        {
            int n = All.Length;
            return All[(((seed % n) + n) % n + islet) % n];
        }

        public static StorySite Find(string id)
        {
            foreach (var s in All)
                if (s.id == id)
                    return s;
            return null;
        }
    }
}
