using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// Pierścień akcji (GDD §12.10, wariant C): prawy klik na wyspie otwiera koło z Księgą, Sklepem, Opcjami, Rozbudową
    /// i Wyjściem wokół kursora, a do tego akcje tego, co jest pod kursorem (np. zasypanie grządki). Zastępuje przyciski w rogu okna, więc całe menu jest przy wyspie (także w przezroczystym oknie).
    /// Wpis z podmenu (children) otwiera w tym samym miejscu drugie koło, a środek koła wraca do pierwszego.
    /// Ikony stoją co Step px po obwodzie, więc koło rośnie z liczbą wpisów i ikony nie nachodzą na siebie.
    /// Klik obok, PPM albo Esc zamyka.
    /// </summary>
    public class ActionRing : MonoBehaviour
    {
        const float Radius = 22f;
        const float SlotSize = 20f;
        const float Step = 27f;   // odstęp środków ikon po obwodzie (ikona 20 px + 7 px przerwy)

        public class Entry
        {
            public string icon, nameKey;
            public System.Func<string> tip;   // zamiast nameKey, gdy opis zależy od stanu (np. „zasyp: roślina przepadnie”)
            public System.Action run;
            public List<Entry> children;   // zamiast run: drugie koło w tym samym miejscu
        }

        RectTransform canvasRect;
        RectTransform ring;
        GameObject blocker;
        TooltipView tooltip;
        int openedFrame = -1;
        Vector2 openPos;
        List<Entry> rootEntries;

        public bool IsOpen => blocker != null && blocker.activeSelf;

        public static ActionRing Create(RectTransform canvasRoot, TooltipView tooltip)
        {
            var view = canvasRoot.gameObject.AddComponent<ActionRing>();
            view.canvasRect = canvasRoot;
            view.tooltip = tooltip;
            // przezroczysta zasłona: klik obok koła zamyka je
            var b = Ui.Img(canvasRoot, "ActionRingBlocker", null, false, true);
            b.color = new Color(0, 0, 0, 0);
            Ui.Stretch(b.rectTransform);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(view.Close);
            view.blocker = b.gameObject;
            view.ring = Ui.Place(Ui.Rect("ActionRing", b.transform), Ui.BottomLeft, Ui.Center, Vector2.zero, new Vector2(68, 68));
            var bg = Ui.Img(view.ring, "Ring", Ui.Theme.wheel, false);
            Ui.Stretch(bg.rectTransform);
            view.blocker.SetActive(false);
            return view;
        }

        /// <summary>Koło wokół punktu na ekranie (piksele ekranu), w granicach okna. Akcje: stałe menu + to, co pod kursorem.</summary>
        public void Open(Vector2 screenPos, List<Entry> entries)
        {
            rootEntries = entries;
            openPos = screenPos;
            Show(entries, null);
        }

        /// <summary>Koło z wpisami; parent != null: podmenu, w środku przycisk powrotu do pierwszego koła.</summary>
        void Show(List<Entry> entries, Entry parent)
        {
            tooltip.Hide(this);
            foreach (Transform c in ring)
                if (c.name != "Ring")
                    Destroy(c.gameObject);
            // promień tak, by sąsiednie ikony (także po zaokrągleniu do pikseli) miały przerwę
            float radius = Mathf.Max(parent != null ? SlotSize + 7f : Radius, Mathf.Ceil(entries.Count * Step / (2f * Mathf.PI)));   // podmenu: miejsce na środek
            float size = radius * 2f + SlotSize + 6f;
            ring.sizeDelta = new Vector2(size, size);
            for (int i = 0; i < entries.Count; i++)
            {
                var a = entries[i];
                float ang = Mathf.PI / 2f - i * 2f * Mathf.PI / entries.Count;
                var pos = new Vector2(Mathf.Round(Mathf.Cos(ang) * radius), Mathf.Round(Mathf.Sin(ang) * radius));
                System.Action click = a.children != null ? () => { Core.GameEvents.PlaySound("click", 0.4f); Show(a.children, a); } : () => { Close(); a.run?.Invoke(); };
                Slot(a.icon, pos, click, () => a.tip != null ? a.tip() : Loc.Get(a.nameKey));
            }
            if (parent != null)
                Slot(parent.icon, Vector2.zero, () => { Core.GameEvents.PlaySound("click", 0.4f); Show(rootEntries, null); }, () => Loc.Get("act.back"));
            Place(radius);
        }

        void Slot(string iconName, Vector2 pos, System.Action click, System.Func<string> tip)
        {
            var slot = Ui.Img(ring, "Action_" + iconName, Ui.Theme.slot, true, true);
            Ui.Place(slot.rectTransform, Ui.Center, Ui.Center, pos, new Vector2(SlotSize, SlotSize));
            var sb = slot.gameObject.AddComponent<Button>();
            sb.targetGraphic = slot;
            sb.transition = Selectable.Transition.SpriteSwap;
            sb.spriteState = new SpriteState { highlightedSprite = Ui.Theme.slotSelected, pressedSprite = Ui.Theme.slotSelected,
                                               selectedSprite = Ui.Theme.slot, disabledSprite = Ui.Theme.slotLocked };
            sb.navigation = new Navigation { mode = Navigation.Mode.None };
            var icon = Ui.Img(slot.transform, "Icon", Ui.Theme.Icon(iconName), false);
            Ui.Place(icon.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
            sb.onClick.AddListener(() => click());
            slot.gameObject.AddComponent<HoverTip>().Init(tooltip, tip);
        }

        /// <summary>Koło w miejscu kliknięcia, w granicach okna.</summary>
        void Place(float radius)
        {
            var screenPos = openPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local);
            var area = canvasRect.rect.size;
            var p = local + area * 0.5f;
            float margin = radius + SlotSize / 2f + 4f;
            p.x = Mathf.Clamp(p.x, margin, Mathf.Max(margin, area.x - margin));
            p.y = Mathf.Clamp(p.y, margin, Mathf.Max(margin, area.y - margin));
            ring.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
            openedFrame = Time.frameCount;
        }

        public void Close()
        {
            if (blocker != null)
                blocker.SetActive(false);
            tooltip.Hide(this);
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame)
                return;
            if (UiInput.RightPressed || UiInput.EscapePressed)
                Close();
        }
    }
}
