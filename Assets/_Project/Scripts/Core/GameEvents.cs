using System;
using UnityEngine;
using Wyspa.Economy;

namespace Wyspa.Core
{
    /// <summary>Zdarzenia gry dla UI (np. toasty). Argumentem jest klucz lokalizacji i jego parametry, nie gotowy tekst.</summary>
    public static class GameEvents
    {
        public static event Action<string, object[]> Toast;

        public static void RaiseToast(string key, params object[] args) => Toast?.Invoke(key, args);

        /// <summary>Klik w obiekt na wyspie (dekoracja, lampa, skrzynia): UI pokazuje panel kontekstowy z akcjami.</summary>
        public static event Action<IContextActions> ContextRequested;

        public static void RequestContext(IContextActions target) => ContextRequested?.Invoke(target);

        /// <summary>Zdobyta waluta w miejscu na świecie (np. zbiór): UI pokazuje lecące monety do licznika.</summary>
        public static event Action<Vector3, CurrencyType, int> CurrencyGained;

        public static void RaiseCurrencyGained(Vector3 world, CurrencyType type, int amount)
        {
            if (amount > 0)
                CurrencyGained?.Invoke(world, type, amount);
        }
    }
}
