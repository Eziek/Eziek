using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Localization
{
    /// <summary>
    /// Lokalizacja gry (GDD §16): tabele CSV z Assets/_Project/Localization, język z systemu albo z opcji.
    /// Każdy napis widoczny dla gracza idzie przez Loc.Get(klucz, argumenty).
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "lang";

        static readonly LocCore core = new LocCore();
        static readonly HashSet<string> warned = new HashSet<string>();
        static string language = "en";
        static bool initialized;

        public static event Action Changed;

        public static string Language => language;
        public static IReadOnlyList<string> Languages => LocCore.Languages;
        public static bool Initialized => initialized;

        public static void Init(IEnumerable<TextAsset> tables)
        {
            if (initialized)
                return;
            if (tables != null)
                foreach (var t in tables)
                    if (t != null)
                        core.AddCsv(t.text);
            language = PlayerPrefs.GetString(PrefKey, FromSystem());
            if (Array.IndexOf(LocCore.Languages, language) < 0)
                language = "en";
            initialized = true;
        }

        public static void SetLanguage(string code)
        {
            if (Array.IndexOf(LocCore.Languages, code) < 0 || code == language)
                return;
            language = code;
            PlayerPrefs.SetString(PrefKey, code);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return "";
            if (!core.Has(key) && warned.Add(key))
                Debug.LogWarning($"[Loc] Brak klucza '{key}' w tabelach lokalizacji.");
            return core.Get(key, language, args);
        }

        static string FromSystem()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh-Hans";
                default: return "en";
            }
        }
    }
}
