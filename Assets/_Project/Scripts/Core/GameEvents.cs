using System;

namespace Wyspa.Core
{
    /// <summary>Zdarzenia gry dla UI (np. toasty). Argumentem jest klucz lokalizacji i jego parametry, nie gotowy tekst.</summary>
    public static class GameEvents
    {
        public static event Action<string, object[]> Toast;

        public static void RaiseToast(string key, params object[] args) => Toast?.Invoke(key, args);
    }
}
