using TMPro;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// Powiadomienia w prawym górnym rogu (GDD §12.6): maks. 3 naraz, 3 s + czas na przeczytanie długiego tekstu.
    /// Zawsze nad Księgą i panelami, pod paskiem HUD, gdy ten jest w tym samym miejscu.
    /// </summary>
    public class ToastView : MonoBehaviour
    {
        const float Lifetime = 3f, PerChar = 0.05f, MaxLifetime = 12f;
        const int MaxVisible = 3;
        const float MaxWidth = 150f;

        readonly List<(RectTransform rt, float until)> items = new List<(RectTransform, float)>();
        RectTransform root;
        float lastToastSound = -10f;
        float relayoutTimer;
        static readonly Vector3[] corners = new Vector3[4];

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
            if (Save.SaveSystem.InMenu)
                return;   // menu główne: zdarzenia wyspy w tle bez powiadomień
            if (Time.unscaledTime - lastToastSound > 1.5f)
            {
                lastToastSound = Time.unscaledTime;
                GameEvents.PlaySound("toast", 0.5f);
            }
            root.SetAsLastSibling();   // nad Księgą (otwarta Księga przesuwa się na wierzch); dymek i tak wraca nad toasty
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
            items.Add((img.rectTransform, Time.unscaledTime + Mathf.Min(MaxLifetime, Lifetime + text.text.Length * PerChar)));
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
            relayoutTimer -= Time.unscaledDeltaTime;
            if (changed || (items.Count > 0 && relayoutTimer <= 0f))
            {
                relayoutTimer = 0.25f;   // pasek HUD idzie za wyspą: co chwilę sprawdzamy, czy nie zasłania toastów
                Relayout();
            }
        }

        void Relayout()
        {
            // najnowszy na górze; pod paskiem HUD, gdy ten wchodzi w kolumnę toastów
            float widest = 0f;
            foreach (var it in items)
                widest = Mathf.Max(widest, it.rt.sizeDelta.x);
            float y = HudClearance(widest);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                items[i].rt.anchoredPosition = new Vector2(0, -y);
                y += items[i].rt.sizeDelta.y + 2;
            }
        }

        /// <summary>Ile zejść w dół, żeby toasty nie zasłaniały paska HUD (0, gdy pasek jest gdzie indziej).</summary>
        float HudClearance(float widest)
        {
            var hud = UiRoot.Instance != null ? UiRoot.Instance.Hud : null;
            var bar = hud != null ? hud.Bar : null;
            if (bar == null || !bar.gameObject.activeInHierarchy || widest <= 0f)
                return 0f;
            bar.GetWorldCorners(corners);
            Vector3 bl = root.InverseTransformPoint(corners[0]), tr = root.InverseTransformPoint(corners[2]);
            // kolumna toastów: od -widest do 0 w poziomie, w dół od 0 (pivot w prawym górnym rogu)
            bool overlapsX = tr.x > -widest && bl.x < 0f;
            bool nearTop = tr.y > -40f && bl.y < 0f;
            return overlapsX && nearTop ? Mathf.Ceil(-bl.y) + 2f : 0f;
        }
    }
}
