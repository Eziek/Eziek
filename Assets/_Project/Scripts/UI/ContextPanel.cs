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

        public void Show(IContextActions what)
        {
            if (what == null)
                return;
            var info = new ContextInfo();
            what.FillContext(info);
            if (what is TileObject obj && obj.NeedsTile && obj.Placed && startMove != null)
                info.Button(Loc.Get("act.move"), () => startMove(obj));
            target = what;
            anchor = info.worldAnchor;
            Build(info);
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
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
            float y = 4, w = 40;
            var texts = new List<(TextMeshProUGUI tmp, float y)>();
            void Line(string s, Color c)
            {
                var tx = Ui.Txt(panel, "Line", c);
                tx.text = s;
                w = Mathf.Max(w, Ui.Width(tx));
                texts.Add((tx, y));
                y += 10;
            }
            if (!string.IsNullOrEmpty(info.title))
                Line(info.title, t.textGold);
            foreach (var l in info.lines)
                Line(l, t.textLight);
            float bx = 0;
            var buttons = new List<(Button b, float w)>();
            foreach (var cb in info.buttons)
            {
                var c = cb;
                var b = Ui.Btn(panel, "Button", () => { c.action?.Invoke(); Close(); });
                var label = Ui.Txt(b.transform, "Text", t.textLight, TextAlignmentOptions.Center);
                label.text = c.label;
                float bw = Mathf.Max(30f, Ui.Width(label) + 10f);
                Ui.Stretch(label.rectTransform);
                b.interactable = c.enabled;
                buttons.Add((b, bw));
                bx += bw + 4;
            }
            if (buttons.Count > 0)
            {
                float widest = 0;
                foreach (var (_, bw) in buttons)
                    widest = Mathf.Max(widest, bw);
                w = Mathf.Max(w, info.verticalButtons ? widest : bx - 4);
                y += 2;
            }
            foreach (var (tmp, ty) in texts)
                Ui.Place(tmp.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -ty), new Vector2(w, 10));
            float x = 5;
            foreach (var (b, bw) in buttons)
            {
                if (info.verticalButtons)
                {
                    Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -y), new Vector2(w, 14));
                    y += 16;
                    continue;
                }
                Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -y), new Vector2(bw, 14));
                x += bw + 4;
            }
            if (buttons.Count > 0 && !info.verticalButtons)
                y += 14;
            Ui.Place(panel, Ui.BottomLeft, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(w + 10, y + 4));
        }

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
