using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// Pierścień akcji (GDD §12.10, wariant C): prawy klik na wyspie otwiera koło z Księgą, Sklepem, Rozbudową i Opcjami
    /// wokół kursora. Zastępuje przyciski w rogu okna, więc całe menu jest przy wyspie (także w przezroczystym oknie).
    /// Klik obok, PPM albo Esc zamyka.
    /// </summary>
    public class ActionRing : MonoBehaviour
    {
        const float Radius = 22f;

        public class Entry
        {
            public string icon, nameKey;
            public System.Action run;
        }

        RectTransform canvasRect;
        RectTransform ring;
        GameObject blocker;
        TooltipView tooltip;
        List<Entry> actions;
        int openedFrame = -1;

        public bool IsOpen => blocker != null && blocker.activeSelf;

        public static ActionRing Create(RectTransform canvasRoot, TooltipView tooltip, List<Entry> actions)
        {
            var view = canvasRoot.gameObject.AddComponent<ActionRing>();
            view.canvasRect = canvasRoot;
            view.tooltip = tooltip;
            view.actions = actions;
            // przezroczysta zasłona: klik obok koła zamyka je
            var b = Ui.Img(canvasRoot, "ActionRingBlocker", null, false, true);
            b.color = new Color(0, 0, 0, 0);
            Ui.Stretch(b.rectTransform);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(view.Close);
            view.blocker = b.gameObject;
            float size = Radius * 2f + 24f;
            view.ring = Ui.Place(Ui.Rect("ActionRing", b.transform), Ui.BottomLeft, Ui.Center, Vector2.zero, new Vector2(size, size));
            var bg = Ui.Img(view.ring, "Ring", Ui.Theme.wheel, false);
            Ui.Stretch(bg.rectTransform);
            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                float ang = Mathf.PI / 2f - i * 2f * Mathf.PI / actions.Count;
                var pos = new Vector2(Mathf.Round(Mathf.Cos(ang) * Radius), Mathf.Round(Mathf.Sin(ang) * Radius));
                var slot = Ui.Img(view.ring, "Action_" + a.icon, Ui.Theme.slot, true, true);
                Ui.Place(slot.rectTransform, Ui.Center, Ui.Center, pos, new Vector2(20, 20));
                var sb = slot.gameObject.AddComponent<Button>();
                sb.targetGraphic = slot;
                sb.transition = Selectable.Transition.SpriteSwap;
                sb.spriteState = new SpriteState { highlightedSprite = Ui.Theme.slotSelected, pressedSprite = Ui.Theme.slotSelected,
                                                   selectedSprite = Ui.Theme.slot, disabledSprite = Ui.Theme.slotLocked };
                sb.navigation = new Navigation { mode = Navigation.Mode.None };
                var icon = Ui.Img(slot.transform, "Icon", Ui.Theme.Icon(a.icon), false);
                Ui.Place(icon.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
                sb.onClick.AddListener(() => { view.Close(); a.run?.Invoke(); });
                slot.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Loc.Get(a.nameKey));
            }
            view.blocker.SetActive(false);
            return view;
        }

        /// <summary>Koło wokół punktu na ekranie (piksele ekranu), w granicach okna.</summary>
        public void Open(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local);
            var area = canvasRect.rect.size;
            var p = local + area * 0.5f;
            float margin = Radius + 14f;
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
