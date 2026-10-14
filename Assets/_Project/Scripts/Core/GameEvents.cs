using System;
using UnityEngine;
using Wyspa.Economy;

namespace Wyspa.Core
{
    /// <summary>Zdarzenia gry dla UI (np. toasty). Argumentem jest klucz lokalizacji i jego parametry, nie gotowy tekst.</summary>
    public static class GameEvents
    {
        public static event Action<string, object[]> Toast;

        /// <summary>Efekt dźwiękowy (AudioManager): id bez przedrostka „sfx_”, np. „harvest”, „coin”.</summary>
        public static event Action<string, float> Sound;

        public static void PlaySound(string id, float volume = 1f) => Sound?.Invoke(id, volume);

        /// <summary>Głos (sylaba ogników): efekt z własną wysokością, bez losowania i bez limitu powtórek.</summary>
        public static event Action<string, float, float> Voice;

        public static void PlayVoice(string id, float volume, float pitch) => Voice?.Invoke(id, volume, pitch);

        public static void RaiseToast(string key, params object[] args) => Toast?.Invoke(key, args);

        /// <summary>Klik w obiekt na wyspie (dekoracja, lampa, skrzynia): UI pokazuje panel kontekstowy z akcjami.</summary>
        public static event Action<IContextActions> ContextRequested;

        /// <summary>
        /// Zbiór (ręczny albo przez ognika): id „crop:marchew” albo „fruit:apple” i liczba sztuk.
        /// Słuchają go kroki wprowadzenia i zamówienia z tablicy.
        /// </summary>
        public static event Action<string, int> Harvested;

        public static void RaiseHarvested(string itemId, int count)
        {
            Market.NoteHarvest(count);   // plony i owoce rejsu (podsumowanie przy rejsie we mgłę)
            Harvested?.Invoke(itemId, count);
        }

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
