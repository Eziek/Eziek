using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// HUD (GDD §12.2): u góry waluty i Esencja, w prawym górnym rogu Księga i opcje, w lewym dolnym rytm pracy i zegar.
    /// Znika po kilku sekundach bez ruchu myszy, wraca po jej poruszeniu.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        const float HideAfter = 5f;

        class Item
        {
            public GameObject root;
            public RectTransform rt;
            public TextMeshProUGUI text;
            public CurrencyType? currency;
        }

        CanvasGroup group;
        RectTransform bar;
        readonly List<Item> items = new List<Item>();
        Item essenceItem;
        RectTransform essenceFill;
        Image rhythmIcon;
        TextMeshProUGUI rhythmText, clockText;
        RectTransform rhythmFill;
        Vector2 lastMouse;
        float lastMove;
        float refresh;

        public bool ForceVisible { get; set; }

        public static HudView Create(RectTransform canvasRoot, System.Action openBook, System.Action openOptions)
        {
            var root = Ui.Stretch(Ui.Rect("HUD", canvasRoot));
            var view = root.gameObject.AddComponent<HudView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.Build(root, openBook, openOptions);
            return view;
        }

        void Build(RectTransform root, System.Action openBook, System.Action openOptions)
        {
            var t = Ui.Theme;
            // --- pasek walut ---
            var barImg = Ui.Img(root, "TopBar", t.tooltip, true);
            bar = Ui.Place(barImg.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(4, -4), new Vector2(100, 18));
            items.Add(MakeItem("coin", CurrencyType.Coins));
            essenceItem = MakeItem("essence", null);
            essenceFill = Ui.Bar(essenceItem.root.transform, "EssenceBar", t.barFillEssence, Ui.TopLeft, Ui.TopLeft, new Vector2(14, -2), new Vector2(34, 8));
            items.Add(essenceItem);
            items.Add(MakeItem("nightcrop", CurrencyType.NightHarvest));
            items.Add(MakeItem("starshard", CurrencyType.StarShard));
            items.Add(MakeItem("wood", CurrencyType.Wood));
            items.Add(MakeItem("stone", CurrencyType.Stone));

            // --- przyciski ---
            var book = Ui.IconBtn(root, "BookButton", "book", new Vector2(18, 18), openBook);
            Ui.Place((RectTransform)book.transform, Ui.TopRight, Ui.TopRight, new Vector2(-24, -4), new Vector2(18, 18));
            var opt = Ui.IconBtn(root, "OptionsButton", "settings", new Vector2(18, 18), openOptions);
            Ui.Place((RectTransform)opt.transform, Ui.TopRight, Ui.TopRight, new Vector2(-4, -4), new Vector2(18, 18));

            // --- rytm pracy ---
            var rp = Ui.Img(root, "Rhythm", t.tooltip, true);
            Ui.Place(rp.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(4, 4), new Vector2(150, 22));
            rhythmIcon = Ui.Img(rp.transform, "Icon", t.Icon("focus"), false);
            Ui.Place(rhythmIcon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(3, -3), new Vector2(12, 12));
            rhythmText = Ui.Txt(rp.transform, "Text", t.textLight);
            Ui.Place(rhythmText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(18, -3), new Vector2(100, 10));
            rhythmFill = Ui.Bar(rp.transform, "RhythmBar", t.barFillRhythm, Ui.TopLeft, Ui.TopLeft, new Vector2(18, -12), new Vector2(90, 7));
            clockText = Ui.Txt(rp.transform, "Clock", t.textMuted, TextAlignmentOptions.TopRight);
            Ui.Place(clockText.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-4, -3), new Vector2(30, 10));
        }

        Item MakeItem(string icon, CurrencyType? currency)
        {
            var rt = Ui.Rect("Item_" + icon, bar);
            var i = Ui.Img(rt, "Icon", Ui.Theme.Icon(icon), false);
            Ui.Place(i.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, 0), new Vector2(12, 12));
            var text = Ui.Txt(rt, "Text", Ui.Theme.textLight);
            Ui.Place(text.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(14, -2), new Vector2(40, 10));
            return new Item { root = rt.gameObject, rt = rt, text = text, currency = currency };
        }

        void Update()
        {
            var mouse = UiInput.MousePosition;
            if ((mouse - lastMouse).sqrMagnitude > 0.5f)
            {
                lastMouse = mouse;
                lastMove = Time.unscaledTime;
            }
            bool visible = ForceVisible || Time.unscaledTime - lastMove < HideAfter;
            float target = visible ? 1f : 0f;
            // skokowe zanikanie (ok. 10 kl./s)
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 4f);
            group.blocksRaycasts = group.alpha > 0.5f;

            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0f)
            {
                refresh = 0.25f;
                Refresh();
            }
        }

        void Refresh()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var w = game.Wallet;
            float x = 4;
            foreach (var it in items)
            {
                bool show;
                if (it == essenceItem)
                {
                    show = true;
                    it.text.text = $"{game.Essence.Amount:0}/{game.Essence.Capacity:0}";
                    Ui.SetBar(essenceFill, game.Essence.Normalized);
                    it.text.rectTransform.anchoredPosition = new Vector2(50, -2);
                }
                else
                {
                    int v = w.Get(it.currency.Value);
                    show = it.currency == CurrencyType.Coins || v > 0;   // waluty pokazujemy dopiero po zdobyciu
                    it.text.text = v.ToString("N0", Wyspa.Localization.LocCore.Culture(Loc.Language));
                }
                it.root.SetActive(show);
                if (!show)
                    continue;
                float textX = it == essenceItem ? 50 : 14;
                float width = textX + Ui.Width(it.text);
                Ui.Place(it.rt, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -3), new Vector2(width, 12));
                x += width + 6;
            }
            bar.sizeDelta = new Vector2(x - 2, 18);

            var r = game.Rhythm;
            string icon = r.FatigueLevel > 0 ? "fatigue" : (r.Refreshed ? "break" : "focus");
            rhythmIcon.sprite = Ui.Theme.Icon(icon);
            if (!game.Activity.IsActive)
                rhythmText.text = Loc.Get("ui.rhythm.idle");
            else if (r.FatigueLevel > 0)
                rhythmText.text = Loc.Get("rhythm.fatigue.status", r.FatigueLevel == 2 ? 0.25f : 0.5f);
            else if (r.Refreshed)
                rhythmText.text = Loc.Get("rhythm.refreshed.status", 15, Mathf.CeilToInt(r.RefreshedMinutesLeft));
            else if (r.StreakBonusActive)
                rhythmText.text = Loc.Get("rhythm.streak.bonus", 25);
            else
                rhythmText.text = Loc.Get("ui.rhythm.normal", r.StreakMinutes);
            Ui.SetBar(rhythmFill, r.StreakMinutes / 25f);
            clockText.text = game.Clock.Now.ToString("HH:mm");
        }
    }
}
