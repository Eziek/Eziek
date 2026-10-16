using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// Pasek przycisków na dole ekranu, pośrodku (GDD §9 p. 136, 139, 140): mała ikona z podpisem obok, jeden ciemny pasek.
    /// Księga, Sklep, Rozbudowa, Losowe nasiona, Menu (opcje, zapis, menu główne, wyjście w kole przy przycisku).
    /// Widoczny zawsze na wyspie, na komputerze i na telefonie; pierścień pod prawym przyciskiem zostaje na komputerze
    /// jako skrót. W trybie stawiania pasek ma tylko „Anuluj” (na telefonie nie ma Esc).
    /// </summary>
    public class MenuBar : MonoBehaviour
    {
        const float H = 14f;       // wysokość przycisku
        const float Pad = 2f;      // zapas w pasku wokół przycisków
        const float Margin = 3f;   // od dolnej krawędzi płótna

        public class Item
        {
            public string icon, labelKey;
            /// <summary>Dymek po najechaniu (opcjonalny, np. co robi losowe sadzenie w dzień i w nocy).</summary>
            public Func<string> tip;
            /// <summary>Akcja z pozycją środka przycisku na ekranie (np. koło podmenu przy przycisku).</summary>
            public Action<Vector2> run;
            /// <summary>Przycisk podświetlony (np. tryb rozbudowy włączony).</summary>
            public Func<bool> on;
        }

        class Slot
        {
            public Item item;
            public RectTransform rt;
            public Image icon;
            public TextMeshProUGUI label;
        }

        RectTransform bar;
        CanvasGroup group;
        TooltipView tooltip;
        Func<bool> hidden, placing;
        readonly List<Slot> slots = new List<Slot>();
        Slot cancel;
        bool laidOutPlacing;
        bool compact;          // za wąskie płótno: same ikony, nazwy w dymkach
        float laidOutWidth;

        /// <summary>Ikona przycisku, który miga (samouczek wskazuje rozbudowę albo sklep); null = żaden.</summary>
        public string Highlight { get; set; }

        /// <summary>Ile pikseli UI od dołu płótna zajmuje pasek (karteczki i panele stoją nad nim).</summary>
        public const float Height = H + 2 * Pad + Margin + 2f;

        public static MenuBar Create(RectTransform canvasRoot, TooltipView tooltip, List<Item> items, Action cancel,
                                     Func<bool> placing, Func<bool> hidden)
        {
            var root = Ui.Rect("MenuBar", canvasRoot);
            var view = root.gameObject.AddComponent<MenuBar>();
            view.tooltip = tooltip;
            view.hidden = hidden;
            view.placing = placing;
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            var bottom = new Vector2(0.5f, 0f);
            Ui.Place(root, bottom, bottom, new Vector2(0f, Margin), new Vector2(100f, H + 2 * Pad));
            var bg = Ui.Img(root, "Back", Ui.Theme.tooltip, true, true);   // raycast: klik w pasek nie trafia w wyspę
            Ui.Stretch(bg.rectTransform);
            view.bar = root;
            foreach (var it in items)
                view.slots.Add(view.MakeSlot(it));
            view.cancel = view.MakeSlot(new Item { icon = "close", labelKey = "act.cancel", run = _ => cancel?.Invoke() });
            view.Layout();
            return view;
        }

        float CanvasWidth => bar != null && bar.parent is RectTransform p ? p.rect.width : 1000f;

        void OnEnable() => Loc.Changed += Layout;
        void OnDisable() => Loc.Changed -= Layout;

        Slot MakeSlot(Item it)
        {
            var t = Ui.Theme;
            var hit = Ui.Img(bar, "Bar_" + it.icon, null, false, true);
            hit.color = new Color(0, 0, 0, 0);   // niewidoczne pole do klikania
            var s = new Slot { item = it, rt = hit.rectTransform };
            s.icon = Ui.Img(hit.transform, "Icon", t.Icon(it.icon), false);
            Ui.Place(s.icon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(1f, -1f), new Vector2(12, 12));
            s.label = Ui.Txt(hit.transform, "Label", t.textLight);
            var b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = s.icon;
            b.transition = Selectable.Transition.ColorTint;
            b.colors = new ColorBlock
            {
                normalColor = new Color(0.85f, 0.85f, 0.85f), highlightedColor = Color.white, pressedColor = t.textGold,
                selectedColor = new Color(0.85f, 0.85f, 0.85f), disabledColor = new Color(0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f, fadeDuration = 0f,
            };
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() =>
            {
                Core.GameEvents.PlaySound("click", 0.4f);
                tooltip.Hide(hit);
                it.run?.Invoke(s.rt.TransformPoint(s.rt.rect.center));
            });
            // dymek: opis przycisku (losowe nasiona), a w wąskim oknie bez podpisów sama nazwa
            hit.gameObject.AddComponent<HoverTip>().Init(tooltip, () => it.tip != null ? it.tip() : compact ? Loc.Get(it.labelKey) : null);
            return s;
        }

        /// <summary>Przyciski obok siebie: ikona 12 px, podpis, szerokość z tekstu (po zmianie języka od nowa).</summary>
        void Layout()
        {
            bool place = placing != null && placing();
            laidOutPlacing = place;
            laidOutWidth = CanvasWidth;
            for (int pass = 0; pass < 2; pass++)
            {
                compact = pass == 1;
                float x = Pad;
                foreach (var s in slots)
                    x = Put(s, !place, x);
                x = Put(cancel, place, x);
                bar.sizeDelta = new Vector2(Mathf.Max(20f, x - 2f + Pad), H + 2 * Pad);
                if (bar.sizeDelta.x <= laidOutWidth - 8f)
                    break;   // z podpisami się mieści
            }
        }

        float Put(Slot s, bool show, float x)
        {
            s.rt.gameObject.SetActive(show);
            if (!show)
                return x;
            s.label.text = Loc.Get(s.item.labelKey);
            s.label.gameObject.SetActive(!compact);
            if (compact)
            {
                Ui.Place(s.rt, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -Pad), new Vector2(14f, H));
                return x + 16f;
            }
            float lw = Ui.Width(s.label);
            Ui.Place(s.label.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(15f, -3f), new Vector2(lw, 10));
            float w = 15f + lw + 3f;
            Ui.Place(s.rt, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -Pad), new Vector2(w, H));
            return x + w + 2f;
        }

        void Update()
        {
            bool show = hidden == null || !hidden();
            group.alpha = show ? 1f : 0f;
            group.blocksRaycasts = show;
            group.interactable = show;
            if (!show)
                return;
            if ((placing != null && placing()) != laidOutPlacing || !Mathf.Approximately(CanvasWidth, laidOutWidth))
                Layout();
            var t = Ui.Theme;
            foreach (var s in slots)
            {
                // włączony tryb: złoty podpis; samouczek: przycisk miga (pół sekundy złoty, pół zwykły)
                bool blink = Highlight != null && s.item.icon == Highlight && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
                s.label.color = (s.item.on != null && s.item.on()) || blink ? t.textGold : t.textLight;
            }
        }
    }
}
