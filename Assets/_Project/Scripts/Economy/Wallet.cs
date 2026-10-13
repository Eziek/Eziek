using System;
using UnityEngine;

namespace Wyspa.Economy
{
    public class Wallet : MonoBehaviour
    {
        [SerializeField] int startingCoins = 50;

        int coins;
        bool initialized;

        public event Action Changed;

        public int Coins
        {
            get
            {
                EnsureInit();
                return coins;
            }
        }

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (initialized)
                return;
            initialized = true;
            coins = startingCoins;
        }

        public void Add(int value) => Set(Coins + value);

        public bool TrySpend(int value)
        {
            if (Coins < value)
                return false;
            Set(Coins - value);
            return true;
        }

        public void Set(int value)
        {
            EnsureInit();
            coins = Mathf.Max(0, value);
            Changed?.Invoke();
        }
    }
}
