using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wyspa.UI
{
    /// <summary>Fabryka elementów UI budowanych z kodu (pikselowo: 1 jednostka UI = 1 piksel przed skalą ×N).</summary>
    public static class Ui
    {
        public static UiTheme Theme;
        public static TMP_FontAsset Font;

        public const int TextSize = 8;   // font WyspaPixel: 8 px na em, znak 3x5

        public static readonly Vector2 TopLeft = new Vector2(0, 1), TopRight = new Vector2(1, 1);
        public static readonly Vector2 BottomLeft = new Vector2(0, 0), BottomRight = new Vector2(1, 0);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>Czas trwania do napisów: „40 s”, „3 min”, „1 min 30 s”.</summary>
        public static string Duration(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            if (s < 60)
                return Localization.Loc.Get("ui.time.sec", s);
            return s % 60 == 0 ? Localization.Loc.Get("ui.time.min", s / 60) : Localization.Loc.Get("ui.time.min_sec", s / 60, s % 60);
        }

        /// <summary>Czas wzrostu rośliny: „40 s pracy” (dzień) albo „3 min nocy” (noc).</summary>
        public static string GrowTime(Farming.CropDefinition c) =>
            Localization.Loc.Get(c.kind == Farming.CropKind.Day ? "ui.seed.time_light" : "ui.seed.time_night", Duration(c.growSeconds));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, bool sliced, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f;
            img.raycastTarget = raycast;
            img.enabled = sprite != null || raycast;   // brak ikony = nic zamiast białego kwadratu
            return img;
        }

        public static TextMeshProUGUI Txt(Transform parent, string name, Color color,
                                          TextAlignmentOptions align = TextAlignmentOptions.TopLeft, int size = TextSize)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null)
                t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = false;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Przycisk 9-slice z podmianą sprite'ów (normalny / najechany / wciśnięty / wyłączony).</summary>
        public static Button Btn(Transform parent, string name, Action onClick)
        {
            var img = Img(parent, name, Theme.buttonNormal, true, true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = Theme.buttonHover,
                pressedSprite = Theme.buttonPressed,
                selectedSprite = Theme.buttonNormal,
                disabledSprite = Theme.buttonDisabled,
            };
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => Core.GameEvents.PlaySound("click", 0.5f));
            if (onClick != null)
                b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>Przycisk z ikoną na środku.</summary>
        public static Button IconBtn(Transform parent, string name, string icon, Vector2 size, Action onClick)
        {
            var b = Btn(parent, name, onClick);
            ((RectTransform)b.transform).sizeDelta = size;
            var i = Img(b.transform, "Icon", Theme.Icon(icon), false);
            Place(i.rectTransform, Center, Center, Vector2.zero, new Vector2(12, 12));
            return b;
        }

        /// <summary>Szerokość tekstu w pikselach UI (zaokrąglona w górę).</summary>
        public static float Width(TextMeshProUGUI t) => Mathf.Ceil(t.GetPreferredValues(t.text).x);

        /// <summary>Pasek: ramka 9-slice + wypełnienie od lewej. Zwraca RectTransform wypełnienia (szerokość ustawia SetBar).</summary>
        public static RectTransform Bar(Transform parent, string name, Sprite fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var frame = Img(parent, name, Theme.barFrame, true);
            Place(frame.rectTransform, anchor, pivot, pos, size);
            var f = Img(frame.transform, "Fill", fill, false);
            f.type = Image.Type.Tiled;
            Place(f.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(0, size.y - 4));
            return f.rectTransform;
        }

        public static void SetBar(RectTransform fill, float t)
        {
            var frame = (RectTransform)fill.parent;
            float w = Mathf.Floor((frame.sizeDelta.x - 4) * Mathf.Clamp01(t));
            fill.sizeDelta = new Vector2(w, fill.sizeDelta.y);
        }
    }
}
