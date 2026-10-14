using System;

namespace Wyspa.Economy
{
    /// <summary>Waluty z GDD §11.2. Kolejność jest zapisywana (indeksy) - nowe dopisuj tylko na końcu.</summary>
    public enum CurrencyType
    {
        Coins = 0,
        NightHarvest = 1,
        StarShard = 2,
        Scale = 3,
        DeepGift = 4,
        Wood = 5,
        Stone = 6,
    }

    [Serializable]
    public struct CurrencyAmount
    {
        public CurrencyType type;
        public int amount;

        public CurrencyAmount(CurrencyType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }
}
