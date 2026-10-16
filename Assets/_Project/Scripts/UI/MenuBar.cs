using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// Pasek przycisków w prawym dolnym rogu (GDD §9 p. 136): Księga, Sklep, Rozbudowa wyspy, Menu (opcje, zapis, menu główne,
    /// wyjście w kole przy przycisku). Widoczny zawsze na wyspie, na komputerze i na telefonie; pierścień pod prawym
    /// przyciskiem zostaje na komputerze jako skrót. W trybie stawiania pasek ma tylko „Anuluj” (na telefonie nie ma Esc).
    /// </summary>
    public class MenuBar : MonoBehaviour
    {
        const float Size = 18f;
        const float Gap = 2f;
        const float Margin = 3f;

        public class Item
        {
            public string icon, nameKey;
            /// <summary>Akcja z pozycją środka przycisku na ekranie (np. koło podmenu przy przycisku).</summary>
            public Action<Vector2> run;
            /// <summary>Przycisk podświetlony (np. tryb rozbudowy włączony).</summary>
            public Func<bool> on;
        }

        RectTransform bar;
        CanvasGroup group;
        TooltipView tooltip;
        Func<bool> hidden;
        readonly List<(Image img, Item item)> slots = new List<(Image, Item)>();
        GameObject cancelSlot;
        Func<bool> placing;

        public static MenuBar Create(RectTransform canvasRoot, TooltipView tooltip, List<Item> items, Action cancel,
                                     Func<bool> placing, Func<bool> hidden)
        {
            var root = Ui.Rect("MenuBar", canvasRoot);
            var view = root.gameObject.AddComponent<MenuBar>();
            view.tooltip = tooltip;
            view.hidden = hidden;
            view.placing = placing;
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.bar = Ui.Place(root, Ui.BottomRight, Ui.BottomRight, new Vector2(-Margin, Margin),
                                new Vector2(items.Count * (Size + Gap) - Gap, Size));
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var img = view.MakeSlot(it.icon, i, null, () => Loc.Get(it.nameKey));
                var rt = img.rectTransform;
                img.GetComponent<Button>().onClick.AddListener(() =>
                {
                    tooltip.Hide(img);
                    it.run?.Invoke(rt.TransformPoint(rt.rect.center));
                });
                view.slots.Add((img, it));
            }
            // tryb stawiania: zamiast menu jeden przycisk „Anuluj” na miejscu ostatniego
            var c = view.MakeSlot("close", items.Count - 1, cancel, () => Loc.Get("act.cancel"));
            view.cancelSlot = c.gameObject;
            view.cancelSlot.SetActive(false);
            return view;
        }

        Image MakeSlot(string icon, int index, Action click, Func<string> tip)
        {
            var slot = Ui.Img(bar, "Bar_" + icon, Ui.Theme.slot, true, true);
            Ui.Place(slot.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(index * (Size + Gap), 0f), new Vector2(Size, Size));
            var b = slot.gameObject.AddComponent<Button>();
            b.targetGraphic = slot;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { highlightedSprite = Ui.Theme.slotSelected, pressedSprite = Ui.Theme.slotSelected,
                                              selectedSprite = Ui.Theme.slot, disabledSprite = Ui.Theme.slotLocked };
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => Core.GameEvents.PlaySound("click", 0.4f));
            if (click != null)
                b.onClick.AddListener(() => click());
            var ic = Ui.Img(slot.transform, "Icon", Ui.Theme.Icon(icon), false);
            Ui.Place(ic.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
            slot.gameObject.AddComponent<HoverTip>().Init(tooltip, tip);
            return slot;
        }

        void Update()
        {
            bool show = hidden == null || !hidden();
            group.alpha = show ? 1f : 0f;
            group.blocksRaycasts = show;
            group.interactable = show;
            if (!show)
                return;
            bool place = placing != null && placing();
            cancelSlot.SetActive(place);
            foreach (var (img, item) in slots)
            {
                img.gameObject.SetActive(!place);
                // włączony tryb: ramka jak wybrany slot
                img.sprite = item.on != null && item.on() ? Ui.Theme.slotSelected : Ui.Theme.slot;
            }
        }
    }
}
