using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Core;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Mały panel przy obiekcie (GDD §12.3): nazwa, opis, przyciski (Usuń, Sprzedaj, Przenieś).
    /// Klik obok, PPM albo Esc zamyka. Obiekt na kafelku dostaje też „Przenieś” (tryb stawiania).
    /// </summary>
    public class ContextPanel : MonoBehaviour
    {
        RectTransform canvasRect;
        GameObject blocker;
        RectTransform panel;
        IContextActions target;
        Vector3 anchor;
        Action<TileObject> startMove;

        public bool IsOpen => blocker != null && blocker.activeSelf;
        int selectedTab;
        IContextActions tabOwner;   // wybrana zakładka zostaje, dopóki panel dotyczy tego samego obiektu

        public static ContextPanel Create(RectTransform canvasRoot, Action<TileObject> moveObject)
        {
            var view = canvasRoot.gameObject.AddComponent<ContextPanel>();
            view.canvasRect = canvasRoot;
            view.startMove = moveObject;
            var b = Ui.Img(canvasRoot, "ContextBlocker", null, false, true);
            b.color = new Color(0, 0, 0, 0);
            Ui.Stretch(b.rectTransform);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(view.Close);
            view.blocker = b.gameObject;
            view.blocker.SetActive(false);
            return view;
        }

        void OnEnable() => GameEvents.ContextRequested += Show;
        void OnDisable() => GameEvents.ContextRequested -= Show;

        TileObject armedDismantle;   // pierwszy klik „Rozbierz” uzbraja, drugi (w tym samym panelu) rozbiera

        /// <summary>„Rozbierz” dla budowli kupionych w grze; potwierdzenie drugim kliknięciem (zwrot połowy monet).</summary>
        void AddDismantle(IContextActions what, ContextInfo info)
        {
            var island = Island.Instance;
            var game = Core.GameLoop.Instance;
            if (!(what is TileObject obj) || island == null || game == null || !island.CanDismantle(obj))
                return;
            if (armedDismantle == obj)
                info.Button(Loc.Get("act.dismantle_confirm"), () =>
                {
                    armedDismantle = null;
                    island.Dismantle(obj, game.Wallet, game.Crops);
                });
            else
                info.Button(Loc.Get("act.dismantle"), () =>
                {
                    armedDismantle = obj;
                    StartCoroutine(Reopen(obj));   // przycisk zamyka panel - otwieramy go od nowa w następnej klatce
                });
        }

        System.Collections.IEnumerator Reopen(TileObject obj)
        {
            yield return null;
            if (obj != null)
                Show(obj);
        }

        public void Show(IContextActions what)
        {
            if (what == null)
                return;
            if (!ReferenceEquals(what, armedDismantle))
                armedDismantle = null;
            if (!ReferenceEquals(what, tabOwner))
            {
                tabOwner = what;
                selectedTab = 0;
            }
            var info = new ContextInfo { tab = selectedTab };
            what.FillContext(info);
            if (info.tabs.Count > 0 && selectedTab >= info.tabs.Count)
            {
                // zakładka zniknęła (np. ostatni siewca odleciał): pierwsza
                selectedTab = 0;
                info = new ContextInfo { tab = 0 };
                what.FillContext(info);
            }
            if (what is TileObject obj && obj.NeedsTile && obj.Placed && !obj.Fixed && startMove != null)
                info.Button(Loc.Get("act.move"), () => startMove(obj));
            AddDismantle(what, info);
            target = what;
            anchor = info.worldAnchor;
            // najpierw aktywny: TextMeshPro źle mierzy teksty na nieaktywnych obiektach (za wąskie przyciski)
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
            Build(info);
            Follow();
        }

        public void Close()
        {
            if (blocker != null)
                blocker.SetActive(false);
            target = null;
        }

        void Build(ContextInfo info)
        {
            if (panel != null)
                Destroy(panel.gameObject);
            var t = Ui.Theme;
            var img = Ui.Img(blocker.transform, "Context", t.tooltip, true, true);   // raycast: klik w panel go nie zamyka
            panel = img.rectTransform;
            // najszerszy panel mieści się w oknie; dłuższe napisy łamią się na kilka linii
            float maxW = Mathf.Max(80f, canvasRect.rect.width - 20f);
            float w = 40;
            var texts = new List<TextMeshProUGUI>();
            var buttons = new List<(Button b, TextMeshProUGUI label, float w)>();

            TextMeshProUGUI Measure(TextMeshProUGUI tx, string s, float pad)
            {
                tx.text = s;
                tx.ForceMeshUpdate();
                w = Mathf.Max(w, Mathf.Min(maxW, Ui.Width(tx) + pad));
                return tx;
            }

            TextMeshProUGUI titleText = null;
            if (!string.IsNullOrEmpty(info.title))
                texts.Add(titleText = Measure(Ui.Txt(panel, "Title", t.textGold), info.title, 0));
            // zakładki: rząd przycisków pod tytułem, wybrana złotym napisem
            var tabButtons = new List<(Button b, float w)>();
            float tabsW = -4;
            for (int i = 0; i < info.tabs.Count; i++)
            {
                int index = i;
                var tb = Ui.Btn(panel, "Tab", () => { selectedTab = index; if (target != null) Show(target); });
                var tl = Ui.Txt(tb.transform, "Text", i == info.tab ? t.textGold : t.textMuted, TextAlignmentOptions.Center);
                tl.text = info.tabs[i];
                tl.ForceMeshUpdate();
                Ui.Stretch(tl.rectTransform);
                float tw = Mathf.Max(24f, Ui.Width(tl) + 10f);
                tabButtons.Add((tb, tw));
                tabsW += tw + 4;
            }
            if (tabButtons.Count > 0)
                w = Mathf.Max(w, Mathf.Min(maxW, tabsW));
            foreach (var l in info.lines)
                texts.Add(Measure(Ui.Txt(panel, "Line", t.textLight), l, 0));
            float rowW = -4;
            foreach (var cb in info.buttons)
            {
                var c = cb;
                var b = Ui.Btn(panel, "Button", () =>
                {
                    c.action?.Invoke();
                    if (c.keepOpen && target != null && IsOpen)
                        Show(target);   // zostaje otwarty, z nowym stanem
                    else
                        Close();
                });
                var label = Ui.Txt(b.transform, "Text", t.textLight, TextAlignmentOptions.Center);
                label.text = c.label;
                label.ForceMeshUpdate();
                float bw = Mathf.Min(maxW, Mathf.Max(30f, Ui.Width(label) + 10f));
                b.interactable = c.enabled;
                buttons.Add((b, label, bw));
                rowW += bw + 4;
                if (info.verticalButtons)
                    w = Mathf.Max(w, bw);
            }
            if (!info.verticalButtons && rowW > maxW)
            {
                // przyciski nie mieszczą się w jednym rzędzie (małe okno): jeden pod drugim
                info.verticalButtons = true;
                foreach (var (_, _, bw) in buttons)
                    w = Mathf.Max(w, bw);
            }
            if (!info.verticalButtons && buttons.Count > 0)
                w = Mathf.Max(w, Mathf.Min(maxW, rowW));

            // układ z góry na dół; teksty szersze niż panel łamią się (wysokość z TextMeshPro)
            float y = 4;
            tabsPlaced = false;
            void PlaceTabs()
            {
                // zakładki w rzędach (gdy się nie mieszczą, kolejny rząd)
                tabsPlaced = true;
                float tx0 = 5;
                y += 2;
                foreach (var (tb, tw) in tabButtons)
                {
                    float width = Mathf.Min(tw, w);
                    if (tx0 > 5 && tx0 + width > w + 5)
                    {
                        tx0 = 5;
                        y += 16;
                    }
                    Ui.Place((RectTransform)tb.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(tx0, -y), new Vector2(width, 14));
                    tx0 += width + 4;
                }
                y += 18;
            }
            foreach (var tx in texts)
            {
                if (tx != titleText && tabButtons.Count > 0 && !tabsPlaced)
                    PlaceTabs();
                float h = 10;
                if (Ui.Width(tx) > w)
                {
                    tx.textWrappingMode = TextWrappingModes.Normal;
                    h = Mathf.Ceil(tx.GetPreferredValues(tx.text, w, 0f).y);
                }
                Ui.Place(tx.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -y), new Vector2(w, h));
                y += h;
            }
            if (tabButtons.Count > 0 && !tabsPlaced)
                PlaceTabs();
            if (buttons.Count > 0)
                y += 2;
            float x = 5;
            foreach (var (b, label, bw) in buttons)
            {
                float width = info.verticalButtons ? w : Mathf.Min(bw, w);
                float h = 14;
                if (Ui.Width(label) + 10f > width)
                {
                    label.textWrappingMode = TextWrappingModes.Normal;
                    h = Mathf.Ceil(label.GetPreferredValues(label.text, width - 8f, 0f).y) + 4f;
                }
                Ui.Stretch(label.rectTransform, 0f);
                label.rectTransform.offsetMin = new Vector2(4f, 0f);
                label.rectTransform.offsetMax = new Vector2(-4f, 0f);
                if (info.verticalButtons)
                {
                    Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -y), new Vector2(width, h));
                    y += h + 2;
                    continue;
                }
                Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -y), new Vector2(width, h));
                x += width + 4;
            }
            if (buttons.Count > 0 && !info.verticalButtons)
                y += 14;
            Ui.Place(panel, Ui.BottomLeft, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(w + 10, y + 4));
        }

        bool tabsPlaced;

        void Update()
        {
            if (!IsOpen)
                return;
            if (UiInput.EscapePressed || UiInput.RightPressed || (target is UnityEngine.Object o && o == null))
                Close();
        }

        void LateUpdate()
        {
            if (IsOpen)
                Follow();
        }

        // panel nad obiektem, w granicach okna
        void Follow()
        {
            var cam = Camera.main;
            if (cam == null || panel == null)
                return;
            Vector2 screen = cam.WorldToScreenPoint(anchor);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            var size = canvasRect.rect.size;
            var p = local + size * 0.5f;
            float hw = panel.sizeDelta.x / 2f;
            p.x = Mathf.Clamp(p.x, hw + 2, size.x - hw - 2);
            p.y = Mathf.Clamp(p.y, 2, size.y - panel.sizeDelta.y - 2);
            panel.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
        }
    }
}
