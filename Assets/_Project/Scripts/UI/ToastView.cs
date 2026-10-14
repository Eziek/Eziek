using TMPro;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>Powiadomienia w prawym górnym rogu (GDD §12.6): maks. 3 naraz, ok. 4 s.</summary>
    public class ToastView : MonoBehaviour
    {
        const float Lifetime = 4f;
        const int MaxVisible = 3;
        const float MaxWidth = 150f;

        readonly List<(RectTransform rt, float until)> items = new List<(RectTransform, float)>();
        RectTransform root;

        public static ToastView Create(RectTransform canvasRoot)
        {
            var rt = Ui.Place(Ui.Rect("Toasts", canvasRoot), Ui.TopRight, Ui.TopRight, new Vector2(-4, -4), new Vector2(160, 80));
            var view = rt.gameObject.AddComponent<ToastView>();
            view.root = rt;
            return view;
        }

        void OnEnable() => GameEvents.Toast += Show;
        void OnDisable() => GameEvents.Toast -= Show;

        public void Show(string key, object[] args)
        {
            var img = Ui.Img(root, "Toast", Ui.Theme.toast, true);
            var text = Ui.Txt(img.transform, "Text", Ui.Theme.textLight);
            text.text = Loc.Get(key, args);
            float w = Ui.Width(text);
            float h = 10f;
            // długie teksty (np. fragmenty lore) łamiemy, żeby toast zmieścił się w małym oknie
            float maxW = Mathf.Max(60f, Mathf.Min(MaxWidth, root.rect.width - 13f));
            if (w > maxW)
            {
                text.textWrappingMode = TextWrappingModes.Normal;
                w = maxW;
                h = Mathf.Ceil(text.GetPreferredValues(text.text, w, 0f).y);
            }
            Ui.Place(text.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(8, -4), new Vector2(w, h));
            Ui.Place(img.rectTransform, Ui.TopRight, Ui.TopRight, Vector2.zero, new Vector2(w + 13, h + 6));
            items.Add((img.rectTransform, Time.unscaledTime + Lifetime));
            while (items.Count > MaxVisible)
            {
                Destroy(items[0].rt.gameObject);
                items.RemoveAt(0);
            }
            Relayout();
        }

        void Update()
        {
            bool changed = false;
            for (int i = items.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > items[i].until)
                {
                    Destroy(items[i].rt.gameObject);
                    items.RemoveAt(i);
                    changed = true;
                }
            if (changed)
                Relayout();
        }

        void Relayout()
        {
            // najnowszy na górze
            float y = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                items[i].rt.anchoredPosition = new Vector2(0, -y);
                y += items[i].rt.sizeDelta.y + 2;
            }
        }
    }
}
