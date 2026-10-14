using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.Upgrades;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Księga - główne menu (GDD §12.4): zakładki Sklep, Ulepszenia, Rytuały, Bestiariusz, Statystyki, Opcje.
    /// Działają: Ulepszenia, Statystyki, Opcje. Pozostałe mają tekst „Wkrótce”.
    /// </summary>
    public class BookView : MonoBehaviour
    {
        public enum Tab { Shop, Upgrades, Rituals, Bestiary, Stats, Options }

        static readonly (Tab tab, string icon, string key)[] Tabs =
        {
            (Tab.Shop, "shop", "menu.shop"), (Tab.Upgrades, "upgrade", "menu.upgrades"), (Tab.Rituals, "ritual", "menu.rituals"),
            (Tab.Bestiary, "bestiary", "menu.bestiary"), (Tab.Stats, "stats", "menu.stats"), (Tab.Options, "settings", "menu.options"),
        };

        const float W = 360, H = 232;
        public const float Width = W, Height = H;

        GameObject blocker;
        RectTransform content;
        TextMeshProUGUI title;
        TextMeshProUGUI info;
        TooltipView tooltip;
        readonly List<(Tab tab, Image img)> tabImages = new List<(Tab, Image)>();
        Tab current = Tab.Upgrades;
        float refresh;

        // wiersze ulepszeń (odświeżane w miejscu)
        class Row
        {
            public UpgradeDefinition def;
            public TextMeshProUGUI level;
            public Button buy;
            public TextMeshProUGUI status;
            public RectTransform cost;
        }
        readonly List<Row> rows = new List<Row>();

        public bool IsOpen => blocker != null && blocker.activeSelf;

        public static BookView Create(RectTransform canvasRoot, TooltipView tooltip)
        {
            var dim = Ui.Img(canvasRoot, "Book", null, false, true);
            dim.color = new Color(0.05f, 0.03f, 0.07f, 0.6f);
            Ui.Stretch(dim.rectTransform);
            var view = dim.gameObject.AddComponent<BookView>();
            view.blocker = dim.gameObject;
            view.tooltip = tooltip;
            view.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return view;
        }

        void Build(RectTransform root)
        {
            var t = Ui.Theme;
            var panel = Ui.Img(root, "Panel", t.panel, true, true);
            Ui.Place(panel.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(W, H));

            for (int i = 0; i < Tabs.Length; i++)
            {
                var (tab, icon, key) = Tabs[i];
                var img = Ui.Img(panel.transform, "Tab_" + tab, t.slot, true, true);
                Ui.Place(img.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(10, -10 - i * 26), new Vector2(22, 22));
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => Show(tab));
                var ic = Ui.Img(img.transform, "Icon", t.Icon(icon), false);
                Ui.Place(ic.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
                var k = key;
                img.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Loc.Get(k));
                tabImages.Add((tab, img));
            }

            var page = Ui.Img(panel.transform, "Page", t.panelPage, true);
            Ui.Place(page.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(38, -6), new Vector2(W - 44, H - 12));
            title = Ui.Txt(page.transform, "Title", t.textDark, TextAlignmentOptions.TopLeft, Ui.TextSize * 2);
            Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(10, -8), new Vector2(200, 18));
            content = Ui.Place(Ui.Rect("Content", page.transform), Ui.TopLeft, Ui.TopLeft, new Vector2(10, -30), new Vector2(W - 64, H - 64));
            info = Ui.Txt(page.transform, "Info", t.textDark);
            info.textWrappingMode = TextWrappingModes.Normal;
            Ui.Place(info.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(10, 8), new Vector2(W - 64, 20));

            var close = Ui.IconBtn(panel.transform, "Close", "close", new Vector2(16, 16), Close);
            Ui.Place((RectTransform)close.transform, Ui.TopRight, Ui.TopRight, new Vector2(-4, -4), new Vector2(16, 16));
        }

        public void Open(Tab tab)
        {
            // mały kadr (start 144x112) nie mieści Księgi: okno na chwilę rośnie (tylko w buildzie)
            if (ViewController.Instance != null)
                ViewController.Instance.RequireWindow(new Vector2Int(
                    Mathf.CeilToInt((W + 16) * UiRoot.UiScale()), Mathf.CeilToInt((H + 16) * UiRoot.UiScale())));
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
            Show(tab);
        }

        public void Close()
        {
            if (ViewController.Instance != null)
                ViewController.Instance.RequireWindow(Vector2Int.zero);
            blocker.SetActive(false);
            tooltip.Hide(this);
        }

        void OnEnable() => Loc.Changed += Rebuild;
        void OnDisable() => Loc.Changed -= Rebuild;

        void Rebuild() => Show(current);

        public void Show(Tab tab)
        {
            current = tab;
            foreach (var (tb, img) in tabImages)
                img.sprite = tb == tab ? Ui.Theme.slotSelected : Ui.Theme.slot;
            foreach (Transform c in content)
                Destroy(c.gameObject);
            rows.Clear();
            info.text = "";
            foreach (var t in Tabs)
                if (t.tab == tab)
                    title.text = Loc.Get(t.key);
            switch (tab)
            {
                case Tab.Upgrades: BuildUpgrades(); break;
                case Tab.Stats: BuildStats(); break;
                case Tab.Options: BuildOptions(); break;
                case Tab.Shop: Paragraph("ui.shop.soon", 0); break;
                default: Paragraph("ui.soon", 0); break;
            }
        }

        void Update()
        {
            if (!IsOpen)
                return;
            if (UiInput.EscapePressed)
            {
                Close();
                return;
            }
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f)
                return;
            refresh = 0.25f;
            if (current == Tab.Upgrades) RefreshUpgrades();
            else if (current == Tab.Stats) BuildStats();
        }

        // ------------------------------------------------------------------ Ulepszenia

        static string IconFor(UpgradeDefinition d)
        {
            switch (d.id)
            {
                case UpgradeIds.EssenceJar: return "essence";
                case UpgradeIds.WispGardener: return "wisp";
                case UpgradeIds.MerchantChest: return "shop";
                default: return "upgrade";
            }
        }

        void BuildUpgrades()
        {
            var game = GameLoop.Instance;
            if (game == null || game.Upgrades.Database == null)
                return;
            float y = 0;
            foreach (var def in game.Upgrades.Database.upgrades)
            {
                if (def == null)
                    continue;
                var row = Ui.Place(Ui.Rect("Row_" + def.id, content), Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 24));
                var slot = Ui.Img(row, "Slot", Ui.Theme.slot, true, true);
                Ui.Place(slot.rectTransform, Ui.TopLeft, Ui.TopLeft, Vector2.zero, new Vector2(22, 22));
                var ic = Ui.Img(slot.transform, "Icon", Ui.Theme.Icon(IconFor(def)), false);
                Ui.Place(ic.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
                var d = def;
                slot.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Describe(d));

                var name = Ui.Txt(row, "Name", Ui.Theme.textDark);
                LocText.On(name, def.nameKey);
                Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -2), new Vector2(150, 10));
                var level = Ui.Txt(row, "Level", Ui.Theme.textMuted);
                Ui.Place(level.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -12), new Vector2(80, 10));
                var cost = Ui.Place(Ui.Rect("Cost", row), Ui.TopLeft, Ui.TopLeft, new Vector2(150, -5), new Vector2(80, 12));

                var buy = Ui.Btn(row, "Buy", () => Buy(d));
                Ui.Place((RectTransform)buy.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -3), new Vector2(40, 16));
                var bt = Ui.Txt(buy.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                Ui.Stretch(bt.rectTransform);
                LocText.On(bt, "ui.upgrade.buy");
                var status = Ui.Txt(row, "Status", Ui.Theme.textWarn, TextAlignmentOptions.TopRight);
                Ui.Place(status.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0, -6), new Vector2(90, 10));

                rows.Add(new Row { def = def, level = level, buy = buy, status = status, cost = cost });
                y += 26;
            }
            RefreshUpgrades();
        }

        void RefreshUpgrades()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var ups = game.Upgrades;
            foreach (var r in rows)
            {
                int lvl = ups.Level(r.def.id);
                r.level.text = Loc.Get("ui.upgrade.level", lvl, r.def.MaxLevel);
                var next = ups.NextLevel(r.def);
                foreach (Transform c in r.cost)
                    Destroy(c.gameObject);
                if (next == null)
                {
                    r.buy.gameObject.SetActive(false);
                    r.status.text = Loc.Get("ui.upgrade.max");
                    r.status.color = Ui.Theme.textMuted;
                    continue;
                }
                float x = 0;
                foreach (var c in next.cost)
                {
                    var ic = Ui.Img(r.cost, "Icon", Ui.Theme.Icon(CurrencyIcon(c.type)), false);
                    Ui.Place(ic.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, 0), new Vector2(12, 12));
                    var tx = Ui.Txt(r.cost, "Amount", game.Wallet.Get(c.type) >= c.amount ? Ui.Theme.textDark : Ui.Theme.textWarn);
                    tx.text = c.amount.ToString("N0", LocCore.Culture(Loc.Language));
                    float w = Ui.Width(tx);
                    Ui.Place(tx.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x + 13, -2), new Vector2(w, 10));
                    x += 13 + w + 5;
                }
                bool req = ups.RequirementsMet(r.def);
                r.buy.gameObject.SetActive(req);
                r.buy.interactable = ups.CanBuy(r.def, game.Wallet);
                r.status.text = req ? "" : Loc.Get("ui.upgrade.requires", RequirementNames(r.def));
                r.status.color = Ui.Theme.textWarn;
            }
        }

        static string RequirementNames(UpgradeDefinition d)
        {
            var names = new List<string>();
            foreach (var r in d.requires)
                if (r != null && r.upgrade != null)
                    names.Add(Loc.Get(r.upgrade.nameKey));
            return string.Join(", ", names);
        }

        static string CurrencyIcon(CurrencyType t)
        {
            switch (t)
            {
                case CurrencyType.Coins: return "coin";
                case CurrencyType.NightHarvest: return "nightcrop";
                case CurrencyType.StarShard: return "starshard";
                case CurrencyType.Scale: return "scale";
                case CurrencyType.DeepGift: return "deepgift";
                case CurrencyType.Wood: return "wood";
                default: return "stone";
            }
        }

        string Describe(UpgradeDefinition d)
        {
            var game = GameLoop.Instance;
            var next = game != null ? game.Upgrades.NextLevel(d) : null;
            float value = next != null ? next.value : (d.levels.Count > 0 ? d.levels[d.levels.Count - 1].value : 0f);
            return Loc.Get(d.nameKey) + "\n" + Loc.Get(d.descriptionKey, value);
        }

        void Buy(UpgradeDefinition d)
        {
            var game = GameLoop.Instance;
            if (game != null && game.Upgrades.TryBuy(d, game.Wallet))
                GameEvents.RaiseToast("ui.toast.upgrade_bought", Loc.Get(d.nameKey));
            RefreshUpgrades();
        }

        // ------------------------------------------------------------------ Statystyki

        void BuildStats()
        {
            foreach (Transform c in content)
                Destroy(c.gameObject);
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var r = game.Rhythm;
            float y = 0;
            Line("ui.stats.today", ref y, r.DailyActiveHours);
            Line("ui.stats.streak", ref y, r.StreakMinutes);
            Line("ui.stats.multiplier", ref y, r.EssenceMultiplier);
            Line("ui.stats.essence", ref y, game.Essence.Amount, game.Essence.Capacity);
        }

        void Line(string key, ref float y, params object[] args)
        {
            var t = Ui.Txt(content, key, Ui.Theme.textDark);
            t.text = Loc.Get(key, args);
            Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 10));
            y += 12;
        }

        void Paragraph(string key, float y)
        {
            var t = Ui.Txt(content, "Text", Ui.Theme.textDark);
            t.textWrappingMode = TextWrappingModes.Normal;
            Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 70, 60));
            LocText.On(t, key);
        }

        // ------------------------------------------------------------------ Opcje

        void BuildOptions()
        {
            var game = GameLoop.Instance;
            float y = 0;
            Header("ui.options.language", ref y);
            float x = 0;
            foreach (var lang in Loc.Languages)
            {
                var code = lang;
                var b = Ui.Btn(content, "Lang_" + code, () => Loc.SetLanguage(code));
                var label = Ui.Txt(b.transform, "Text", code == Loc.Language ? Ui.Theme.textGold : Ui.Theme.textLight, TextAlignmentOptions.Center);
                label.text = Loc.Get("lang." + code);
                float w = Mathf.Max(30, Ui.Width(label) + 10);
                Ui.Stretch(label.rectTransform);
                Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -y), new Vector2(w, 16));
                x += w + 4;
            }
            y += 24;

            Header("ui.options.brightness", ref y);
            var lighting = FindAnyObjectByType<DayNightLighting>();
            var value = Ui.Txt(content, "Brightness", Ui.Theme.textDark, TextAlignmentOptions.Center);
            Ui.Place(value.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(22, -y - 3), new Vector2(30, 10));
            void ShowValue() => value.text = lighting != null ? $"{Mathf.RoundToInt(lighting.MinBrightness * 100)}%" : "-";
            void Change(float d)
            {
                if (lighting == null) return;
                lighting.MinBrightness = Mathf.Clamp(lighting.MinBrightness + d, 0.15f, 0.7f);
                PlayerPrefs.SetFloat("minBrightness", lighting.MinBrightness);
                ShowValue();
            }
            var minus = Ui.Btn(content, "Minus", () => Change(-0.05f));
            Ui.Place((RectTransform)minus.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(18, 16));
            Ui.Stretch(Ui.Txt(minus.transform, "T", Ui.Theme.textLight, TextAlignmentOptions.Center).rectTransform).GetComponent<TextMeshProUGUI>().text = "-";
            var plus = Ui.Btn(content, "Plus", () => Change(0.05f));
            Ui.Place((RectTransform)plus.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(56, -y), new Vector2(18, 16));
            Ui.Stretch(Ui.Txt(plus.transform, "T", Ui.Theme.textLight, TextAlignmentOptions.Center).rectTransform).GetComponent<TextMeshProUGUI>().text = "+";
            ShowValue();
            y += 24;

            Header("ui.options.rhythm", ref y);
            if (game != null)
            {
                var toggle = Ui.Btn(content, "Rhythm", null);
                var tl = Ui.Txt(toggle.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                Ui.Stretch(tl.rectTransform);
                void ShowRhythm() => tl.text = Loc.Get(game.Rhythm.RhythmEnabled ? "ui.on" : "ui.off");
                toggle.onClick.AddListener(() =>
                {
                    game.Rhythm.RhythmEnabled = !game.Rhythm.RhythmEnabled;
                    PlayerPrefs.SetInt("rhythm", game.Rhythm.RhythmEnabled ? 1 : 0);
                    ShowRhythm();
                });
                Ui.Place((RectTransform)toggle.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(40, 16));
                ShowRhythm();
            }
            y += 24;
            var dbg = Ui.Txt(content, "Debug", Ui.Theme.textMuted);
            LocText.On(dbg, "ui.options.debug");
            Ui.Place(dbg.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 10));
        }

        void Header(string key, ref float y)
        {
            var t = Ui.Txt(content, key, Ui.Theme.textDark);
            LocText.On(t, key);
            Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 10));
            y += 12;
        }
    }
}
