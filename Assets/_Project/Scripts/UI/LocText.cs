using TMPro;
using UnityEngine;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>Tekst z klucza lokalizacji - odświeża się sam po zmianie języka.</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocText : MonoBehaviour
    {
        string key;
        object[] args;
        TextMeshProUGUI tmp;

        public static LocText On(TextMeshProUGUI t, string key, params object[] args)
        {
            var lt = t.GetComponent<LocText>();
            if (lt == null)
                lt = t.gameObject.AddComponent<LocText>();
            lt.Set(key, args);
            return lt;
        }

        public void Set(string newKey, params object[] newArgs)
        {
            key = newKey;
            args = newArgs;
            Apply();
        }

        void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        void OnDisable() => Loc.Changed -= Apply;

        void Apply()
        {
            if (tmp == null)
                tmp = GetComponent<TextMeshProUGUI>();
            if (tmp != null && key != null)
                tmp.text = Loc.Get(key, args);
        }
    }
}
