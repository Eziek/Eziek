using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Economy
{
    /// <summary>Portfel na wszystkie waluty (monety, plony nocy, odłamki, łuski, dary, drewno, kamień).</summary>
    public class Wallet : MonoBehaviour
    {
        [SerializeField] int startingCoins = 50;

        static readonly int Count = Enum.GetValues(typeof(CurrencyType)).Length;

        int[] amounts;

        public event Action Changed;

        public int Coins => Get(CurrencyType.Coins);

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (amounts != null)
                return;
            amounts = new int[Count];
            amounts[(int)CurrencyType.Coins] = startingCoins;
        }

        public int Get(CurrencyType type)
        {
            EnsureInit();
            return amounts[(int)type];
        }

        public void Set(CurrencyType type, int value)
        {
            EnsureInit();
            amounts[(int)type] = Mathf.Max(0, value);
            Changed?.Invoke();
        }

        public void Add(CurrencyType type, int value)
        {
            if (type == CurrencyType.Coins)
                Market.NoteIncome(value);   // zarobek rejsu (perły mgły przy prestiżu)
            Set(type, Get(type) + value);
        }

        public bool CanAfford(IReadOnlyList<CurrencyAmount> cost)
        {
            if (cost == null)
                return true;
            // ten sam typ może wystąpić kilka razy - sumujemy
            var need = new int[Count];
            foreach (var c in cost)
                need[(int)c.type] += c.amount;
            for (int i = 0; i < Count; i++)
                if (need[i] > Get((CurrencyType)i))
                    return false;
            return true;
        }

        public bool TrySpend(IReadOnlyList<CurrencyAmount> cost)
        {
            if (!CanAfford(cost))
                return false;
            if (cost != null)
                foreach (var c in cost)
                    amounts[(int)c.type] -= c.amount;
            Changed?.Invoke();
            return true;
        }

        // --- skróty dla monet (używane przez grządki i stary zapis) ---
        public void Add(int coins) => Add(CurrencyType.Coins, coins);

        public bool TrySpend(int coins)
        {
            if (Coins < coins)
                return false;
            Add(CurrencyType.Coins, -coins);
            return true;
        }

        public void Set(int coins) => Set(CurrencyType.Coins, coins);

        public List<CurrencyAmount> Snapshot()
        {
            var list = new List<CurrencyAmount>();
            for (int i = 0; i < Count; i++)
                list.Add(new CurrencyAmount((CurrencyType)i, Get((CurrencyType)i)));
            return list;
        }

        public void Restore(IEnumerable<CurrencyAmount> list)
        {
            EnsureInit();
            if (list == null)
                return;
            foreach (var c in list)
                if ((int)c.type >= 0 && (int)c.type < Count)
                    amounts[(int)c.type] = Mathf.Max(0, c.amount);
            Changed?.Invoke();
        }
    }
}
