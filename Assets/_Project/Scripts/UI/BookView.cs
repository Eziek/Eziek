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
    /// </summary>
    public class BookView : MonoBehaviour
    {
        public enum Tab { Shop, Upgrades, Rituals, Bestiary, Stats, Options, Voyage }

        static readonly (Tab tab, string icon, string key)[] Tabs =
        {
            (Tab.Shop, "shop", "menu.shop"), (Tab.Upgrades, "upgrade", "menu.upgrades"), (Tab.Voyage, "pearl", "menu.voyage"),
            (Tab.Rituals, "ritual", "menu.rituals"),
            (Tab.Bestiary, "bestiary", "menu.bestiary"), (Tab.Stats, "stats", "menu.stats"), (Tab.Options, "settings", "menu.options"),
        };

        const float W = 360, H = 232;
        const float TabsW = 38;   // kolumna zakładek po lewej (strona zaczyna się za nią)
        public const float Width = W, Height = H;

        GameObject blocker;
        RectTransform content;
        RectTransform panelRect, pageRect;
        TextMeshProUGUI title;
        TextMeshProUGUI info;
        TooltipView tooltip;
        readonly List<(Tab tab, Image img)> tabImages = new List<(Tab, Image)>();
        Tab current = Tab.Upgrades;
        float refresh;

        // wiersze ulepszeń (odświeżane w miejscu)

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
            panelRect = Ui.Place(panel.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(W, H));

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
            pageRect = Ui.Place(page.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(TabsW, -6), new Vector2(W - 44, H - 12));
            title = Ui.Txt(page.transform, "Title", t.textDark, TextAlignmentOptions.TopLeft, Ui.TextSize * 2);
            Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(10, -8), new Vector2(200, 18));
            // treść w przewijanym oknie:
            // kółko myszy przewija, przeciąganie przesuwa; wszystko poza oknem jest przycięte
            viewport = Ui.Place(Ui.Rect("Viewport", page.transform), Ui.TopLeft, Ui.TopLeft, new Vector2(10, -30), new Vector2(W - 64, ViewH));
            // maska sięga 4 px poza okno w poziomie
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(-4f, -1f, -4f, -1f);
            mask.softness = Vector2Int.zero;
            var hit = Ui.Img(viewport, "DragArea", null, false, true);   // przeciąganie także w pustym miejscu strony
            hit.color = new Color(0, 0, 0, 0);
            Ui.Stretch(hit.rectTransform);
            content = Ui.Place(Ui.Rect("Content", viewport), Ui.TopLeft, Ui.TopLeft, Vector2.zero, new Vector2(W - 64, ViewH));
            scroller = viewport.gameObject.AddComponent<ScrollRect>();
            scroller.viewport = viewport;
            scroller.content = content;
            scroller.movementType = ScrollRect.MovementType.Clamped;
            scroller.inertia = false;
            scroller.scrollSensitivity = 16f;
            info = Ui.Txt(page.transform, "Info", t.textDark);
            info.textWrappingMode = TextWrappingModes.Normal;
            Ui.Place(info.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(10, 8), new Vector2(W - 64, 20));

            var close = Ui.IconBtn(panel.transform, "Close", "close", new Vector2(16, 16), Close);
            Ui.Place((RectTransform)close.transform, Ui.TopRight, Ui.TopRight, new Vector2(-4, -4), new Vector2(16, 16));
        }

        public void Open(Tab tab)
        {
            GameEvents.PlaySound("book");
            // mały kadr (start 144x112) nie mieści Księgi: okno na chwilę rośnie (tylko w buildzie)
            if (ViewController.Instance != null)
                ViewController.Instance.RequireWindow(new Vector2Int(
                    Mathf.CeilToInt((W + 16) * UiRoot.DesiredUiScale()), Mathf.CeilToInt((H + 16) * UiRoot.DesiredUiScale())));
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
            // menu główne (Ustawienia): same opcje, bez kolumny zakładek (reszta Księgi dotyczy gry); Księga węższa o kolumnę
            bool menu = Wyspa.Save.SaveSystem.InMenu;
            foreach (var (tb, img) in tabImages)
                img.gameObject.SetActive(!menu);
            float tabsW = menu ? 6f : TabsW;
            panelRect.sizeDelta = new Vector2(W - TabsW + tabsW, H);
            pageRect.anchoredPosition = new Vector2(tabsW, -6);
            if (menu)
                tab = Tab.Options;
            Show(tab);
            if (tab == Tab.Upgrades)
                CenterTree();   // drzewko jest duże: po otwarciu Księgi zawsze od środka
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

        RectTransform viewport;
        ScrollRect scroller;
        /// <summary>Wysokość okna treści: nad podpowiedzią na dole strony (wcześniej na nią zachodziła).</summary>
        const float ViewH = H - 72;

        /// <summary>Rozmiar treści = zasięg elementów strony (co najmniej okno); przewijanie tylko tam, gdzie się nie mieści.</summary>
        /// <summary>
        /// Czyści treść strony. Destroy działa dopiero na końcu klatki, więc elementy najpierw odpinamy od treści -
        /// inaczej FitContent mierzył też poprzednią zakładkę (po drzewku ulepszeń inne strony przewijały się w bok
        /// i ucinały pierwsze litery).
        /// </summary>
        void ClearContent()
        {
            var old = new List<Transform>();
            foreach (Transform c in content)
                old.Add(c);
            foreach (var c in old)
            {
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }
        }

        void FitContent(bool keepScroll)
        {
            float vw = W - 64, vh = ViewH, maxX = vw, maxY = vh;
            foreach (Transform c in content)
            {
                if (!(c is RectTransform rt) || rt.anchorMin != Ui.TopLeft || rt.anchorMax != Ui.TopLeft)
                    continue;
                float left = rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x;
                float top = -rt.anchoredPosition.y - (1f - rt.pivot.y) * rt.sizeDelta.y;
                maxX = Mathf.Max(maxX, left + rt.sizeDelta.x);
                maxY = Mathf.Max(maxY, top + rt.sizeDelta.y + 4f);
            }
            content.sizeDelta = new Vector2(maxX, maxY);
            scroller.horizontal = maxX > vw + 0.5f;
            scroller.vertical = maxY > vh + 0.5f;
            if (keepScroll)
                content.anchoredPosition = new Vector2(Mathf.Clamp(content.anchoredPosition.x, -(maxX - vw), 0f),
                                                       Mathf.Clamp(content.anchoredPosition.y, 0f, maxY - vh));
            else
                content.anchoredPosition = Vector2.zero;
            scroller.StopMovement();
        }

        public void Show(Tab tab)
        {
            bool sameTab = tab == current;
            current = tab;
            foreach (var (tb, img) in tabImages)
                img.sprite = tb == tab ? Ui.Theme.slotSelected : Ui.Theme.slot;
            ClearContent();
            treeNodes.Clear();
            info.text = "";
            foreach (var t in Tabs)
                if (t.tab == tab)
                    title.text = Loc.Get(t.key);
            switch (tab)
            {
                case Tab.Upgrades: BuildUpgrades(); break;
                case Tab.Stats: BuildStats(); break;
                case Tab.Options: BuildOptions(); break;
                case Tab.Shop: BuildShop(); break;
                case Tab.Rituals: BuildRituals(); break;
                case Tab.Bestiary: BuildBestiary(); break;
                case Tab.Voyage: BuildVoyage(); break;
                default: Paragraph("ui.soon", 0); break;
            }
            FitContent(sameTab);
            if (tab == Tab.Upgrades && !sameTab)
                CenterTree();
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
            else if (current == Tab.Shop) RefreshShop();
            else if (current == Tab.Rituals) RefreshRituals();
            else if (current == Tab.Voyage) RefreshVoyage();
            else if (current == Tab.Stats) BuildStats();
        }

        // ------------------------------------------------------------------ Ulepszenia: drzewko

        /// <summary>Stan węzła drzewka: kupiony, dostępny (wymagania spełnione), za mgłą („?”) albo jeszcze niewidoczny.</summary>
        enum NodeState { Hidden, Fog, Open, Owned }

        class TreeNode
        {
            public UpgradeDefinition def;
            public NodeState state;
            public Image frame, icon;
            public readonly List<Image> pips = new List<Image>();
        }
        readonly List<TreeNode> treeNodes = new List<TreeNode>();
        const float Cell = 28f, NodeSize = 22f;

        static NodeState StateOf(UpgradeState ups, UpgradeDefinition d)
        {
            if (ups.Level(d.id) > 0) return NodeState.Owned;
            if (ups.RequirementsMet(d)) return NodeState.Open;
            // za mgłą: rodzic jest już widoczny (dostępny), więc wiadomo, że coś tam jest
            foreach (var r in d.requires)
                if (r != null && r.upgrade != null && ups.RequirementsMet(r.upgrade)) return NodeState.Fog;
            return NodeState.Hidden;
        }

        const float TreeMargin = 18f;

        /// <summary>
        /// Środek komórki na stronie: drzewko od lewego górnego rogu, strona przewija się w obie strony;
        /// po otwarciu widok staje na ogniku-ogrodniku (CenterTree).
        /// </summary>
        static Vector2 CellCenter(Vector2Int c)
        {
            var min = UpgradeTree.Min;
            return new Vector2(TreeMargin + (c.x - min.x) * Cell + Cell / 2f, -(TreeMargin + (c.y - min.y) * Cell + Cell / 2f));
        }

        /// <summary>Przewija drzewko tak, żeby ognik-ogrodnik (środek) był na środku okna.</summary>
        void CenterTree()
        {
            var c = CellCenter(UpgradeTree.Cell(UpgradeIds.WispGardener));
            float vw = W - 64, vh = ViewH;
            var size = content.sizeDelta;
            content.anchoredPosition = new Vector2(Mathf.Clamp(vw / 2f - c.x, -(size.x - vw), 0f),
                                                   Mathf.Clamp(-c.y - vh / 2f, 0f, Mathf.Max(0f, size.y - vh)));
        }

        void BuildUpgrades()
        {
            treeNodes.Clear();
            var game = GameLoop.Instance;
            if (game == null || game.Upgrades.Database == null)
                return;
            var ups = game.Upgrades;
            info.text = Loc.Get("ui.upgrade.tree_hint");
            var defs = new List<UpgradeDefinition>();
            foreach (var def in ups.Database.upgrades)
                if (def != null && UpgradeTree.InTree(def.id)) defs.Add(def);

            // linie (pod węzłami): od rodzica poziomo, potem pionowo; złote, gdy oba końce kupione
            foreach (var def in defs)
            {
                var st = StateOf(ups, def);
                if (st == NodeState.Hidden)
                    continue;
                foreach (var r in def.requires)
                {
                    if (r == null || r.upgrade == null || !UpgradeTree.InTree(r.upgrade.id))
                        continue;
                    var a = CellCenter(UpgradeTree.Cell(r.upgrade.id));
                    var b = CellCenter(UpgradeTree.Cell(def.id));
                    var color = st == NodeState.Owned ? Ui.Theme.textGoldOnPage : (st == NodeState.Open ? Ui.Theme.textDark : Ui.Theme.textMutedOnPage);
                    Line(new Vector2(Mathf.Min(a.x, b.x) - 1, a.y + 1), new Vector2(Mathf.Abs(b.x - a.x) + 2, 2), color);
                    Line(new Vector2(b.x - 1, Mathf.Max(a.y, b.y) + 1), new Vector2(2, Mathf.Abs(b.y - a.y) + 2), color);
                }
            }

            foreach (var def in defs)
            {
                var st = StateOf(ups, def);
                if (st == NodeState.Hidden)
                    continue;
                var d = def;
                var center = CellCenter(UpgradeTree.Cell(def.id));
                var frame = Ui.Img(content, "Node_" + def.id, Ui.Theme.slot, true, true);
                Ui.Place(frame.rectTransform, Ui.TopLeft, Ui.Center, center, new Vector2(NodeSize, NodeSize));
                var btn = frame.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() => Buy(d));
                frame.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Describe(d));
                var icon = Ui.Img(frame.transform, "Icon", Ui.Theme.Icon(st == NodeState.Fog ? "lock" : UpgradeTree.Icon(def.id)), false);
                Ui.Place(icon.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
                var node = new TreeNode { def = def, state = st, frame = frame, icon = icon };
                // poziomy jako kropki pod węzłem
                int levels = st == NodeState.Fog ? 0 : def.MaxLevel;
                if (levels > 1)
                {
                    float pw = 4f, gap = 1f, total = levels * pw + (levels - 1) * gap;
                    for (int i = 0; i < levels; i++)
                    {
                        var pip = Ui.Img(content, "Pip", null, false);
                        pip.enabled = true;
                        Ui.Place(pip.rectTransform, Ui.TopLeft, Ui.TopLeft,
                                 new Vector2(center.x - total / 2f + i * (pw + gap), center.y - NodeSize / 2f - 1f), new Vector2(pw, 2));
                        node.pips.Add(pip);
                    }
                }
                treeNodes.Add(node);
            }
            RefreshUpgrades();
        }

        void Line(Vector2 topLeft, Vector2 size, Color color)
        {
            var img = Ui.Img(content, "Line", null, false);
            img.enabled = true;
            img.color = color;
            Ui.Place(img.rectTransform, Ui.TopLeft, Ui.TopLeft, topLeft, size);
        }

        void RefreshUpgrades()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var ups = game.Upgrades;
            foreach (var n in treeNodes)
            {
                if (StateOf(ups, n.def) != n.state)
                {
                    Show(Tab.Upgrades);   // kupno odkryło nowe gałęzie: zbuduj drzewko od nowa
                    return;
                }
                int lvl = ups.Level(n.def.id);
                bool max = lvl >= n.def.MaxLevel;
                bool canBuy = ups.CanBuy(n.def, game.Wallet);
                n.frame.sprite = n.state == NodeState.Fog ? Ui.Theme.slotLocked
                               : max || canBuy ? Ui.Theme.slotSelected : Ui.Theme.slot;
                // dostępne i stać cię: delikatne pulsowanie; za mgłą: przyciemnione
                float pulse = canBuy ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
                n.frame.color = n.state == NodeState.Fog ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : new Color(pulse, pulse, pulse, 1f);
                n.icon.color = n.state == NodeState.Open && !canBuy ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                for (int i = 0; i < n.pips.Count; i++)
                    n.pips[i].color = i < lvl ? Ui.Theme.textGoldOnPage : Ui.Theme.textMutedOnPage;   // kupione ciemnym złotem, reszta szara
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
            if (game == null)
                return Loc.Get(d.nameKey);
            var ups = game.Upgrades;
            if (StateOf(ups, d) == NodeState.Fog)
                return Loc.Get("ui.upgrade.hidden", RequirementNames(d));
            var next = ups.NextLevel(d);
            float value = next != null ? next.value : (d.levels.Count > 0 ? d.levels[d.levels.Count - 1].value : 0f);
            string s = Loc.Get(d.nameKey) + "\n" + Loc.Get("ui.upgrade.level", ups.Level(d.id), d.MaxLevel) + "\n"
                     + Loc.Get(d.descriptionKey, UpgradeTree.Display(d.id, value));
            if (next == null)
                return s + "\n" + Loc.Get("ui.upgrade.max");
            s += "\n" + Loc.Get("ui.upgrade.cost", PlacementMode.CostText(ups.CostOf(next)));
            if (!ups.RequirementsMet(d))
                return s + "\n" + Loc.Get("ui.upgrade.requires", RequirementNames(d));
            return s + "\n" + Loc.Get(ups.CanBuy(d, game.Wallet) ? "ui.upgrade.click_buy" : "ui.upgrade.cant_afford");
        }

        void Buy(UpgradeDefinition d)
        {
            var game = GameLoop.Instance;
            if (game != null && game.Upgrades.TryBuy(d, game.Wallet))
            {
                GameEvents.RaiseToast("ui.toast.upgrade_bought", Loc.Get(d.nameKey));
                GameEvents.PlaySound("magic", 0.6f);
            }
            RefreshUpgrades();
        }

        // ------------------------------------------------------------------ Sklep (GDD §12.4): na razie lampy

        readonly List<(LampDefinition def, Button buy, TextMeshProUGUI price)> shopRows = new List<(LampDefinition, Button, TextMeshProUGUI)>();

        int shopSection = 1;   // 0 nasiona, 1 lampy, 2 budowle, 3 morze (GDD §12.4: podzakładki sklepu)

        void BuildShop()
        {
            shopRows.Clear();
            seaItems.Clear();
            float y = 0;
            // podzakładki: Nasiona / Lampy / Budowle / Morze
            float x = 0;
            foreach (var (section, key) in new[] { (0, "ui.shop.seeds"), (4, "ui.shop.trees"), (1, "ui.shop.lamps"), (2, "ui.shop.buildings"), (3, "ui.shop.sea"), (5, "ui.shop.hats") })
            {
                int sec = section;
                var b = Ui.Btn(content, "Section", () => { shopSection = sec; Show(Tab.Shop); });
                var label = Ui.Txt(b.transform, "Text", sec == shopSection ? Ui.Theme.textGold : Ui.Theme.textLight, TextAlignmentOptions.Center);
                label.text = Loc.Get(key);
                float bw = Mathf.Max(40f, Ui.Width(label) + 12f);
                Ui.Stretch(label.rectTransform);
                Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, 0), new Vector2(bw, 14));
                x += bw + 4;
            }
            y = 20;
            info.text = Loc.Get("ui.shop.hint");
            if (shopSection == 3)
            {
                info.text = Loc.Get("ui.shop.sea_hint");
                BuildSeaShop(ref y);
                BuildItems(ref y, 28f);
                RefreshShop();
                return;
            }
            if (shopSection == 0)
            {
                info.text = Loc.Get("ui.shop.seeds_hint");
                BuildSeedShop();
                BuildItems(ref y, 24f);
                RefreshShop();
                return;
            }
            if (shopSection == 4)
            {
                info.text = Loc.Get("ui.shop.trees_hint");
                BuildTreeShop();
                BuildItems(ref y, 28f);
                RefreshShop();
                return;
            }
            if (shopSection == 5)
            {
                info.text = Loc.Get("ui.shop.hats_hint");
                BuildHatShop(y);
                return;
            }
            if (shopSection == 2)
            {
                info.text = Loc.Get("ui.shop.buildings_hint");
                BuildBuildingShop();
                BuildItems(ref y, 28f);
                RefreshShop();
                return;
            }
            var island = Island.Instance;
            var lamps = island != null && island.Art != null ? island.Art.lamps : null;
            if (lamps != null)
                foreach (var def in lamps)
                {
                    if (def == null)
                        continue;
                    var row = Ui.Place(Ui.Rect("Lamp_" + def.id, content), Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 26));
                    var slot = Ui.Img(row, "Slot", Ui.Theme.slot, true);
                    Ui.Place(slot.rectTransform, Ui.TopLeft, Ui.TopLeft, Vector2.zero, new Vector2(22, 26));
                    var pic = Ui.Img(slot.transform, "Lamp", def.sprite, false);
                    pic.preserveAspect = true;
                    Ui.Place(pic.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(18, 22));

                    var name = Ui.Txt(row, "Name", Ui.Theme.textDark);
                    LocText.On(name, def.NameKey);
                    Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -3), new Vector2(160, 10));
                    var stats = Ui.Txt(row, "Info", Ui.Theme.textMutedOnPage);
                    stats.text = Loc.Get("ui.lamp.info", def.radius, def.essencePerMinute * 60f);
                    Ui.Place(stats.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -13), new Vector2(200, 10));

                    var coin = Ui.Img(row, "Coin", Ui.Theme.Icon("coin"), false);
                    Ui.Place(coin.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-72, -6), new Vector2(12, 12));
                    var price = Ui.Txt(row, "Price", Ui.Theme.textDark, TextAlignmentOptions.TopRight);
                    price.text = def.price.ToString("N0", LocCore.Culture(Loc.Language));
                    Ui.Place(price.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-46, -8), new Vector2(24, 10));

                    var d = def;
                    var buy = Ui.Btn(row, "Buy", () => { if (UiRoot.Instance != null) UiRoot.Instance.StartBuy(d); });
                    Ui.Place((RectTransform)buy.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -5), new Vector2(40, 16));
                    var bt = Ui.Txt(buy.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                    Ui.Stretch(bt.rectTransform);
                    LocText.On(bt, "ui.upgrade.buy");
                    shopRows.Add((def, buy, price));
                    y += 28;
                }
            RefreshShop();
        }

        // ---- Czapki ogników: kosmetyka za dużo monet albo za perły, na zawsze (Economy/Hats)

        void BuildHatShop(float y)
        {
            var art = Island.Instance != null ? Island.Instance.Art : null;
            var game = GameLoop.Instance;
            var culture = LocCore.Culture(Loc.Language);
            foreach (var h in Hats.All)
            {
                var hat = h;
                var row = Ui.Place(Ui.Rect("Hat_" + h.id, content), Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 22));
                var slot = Ui.Img(row, "Slot", Ui.Theme.slot, true);
                Ui.Place(slot.rectTransform, Ui.TopLeft, Ui.TopLeft, Vector2.zero, new Vector2(22, 22));
                var pic = Ui.Img(slot.transform, "Hat", art != null ? art.Hat(h.id, false) : null, false);
                pic.preserveAspect = true;
                Ui.Place(pic.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(16, 16));
                var name = Ui.Txt(row, "Name", Ui.Theme.textDark);
                LocText.On(name, h.NameKey);
                Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -6), new Vector2(120, 10));
                if (Hats.Owns(h.id))
                {
                    var have = Ui.Txt(row, "Owned", Ui.Theme.textGoldOnPage, TextAlignmentOptions.TopRight);
                    have.text = Loc.Get("ui.shop.hat_owned");
                    Ui.Place(have.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0, -6), new Vector2(90, 10));
                }
                else
                {
                    bool pearls = h.pearls > 0;
                    var icon = Ui.Img(row, "Cur", Ui.Theme.Icon(pearls ? "pearl" : "coin"), false);
                    Ui.Place(icon.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-72, -5), new Vector2(12, 12));
                    var price = Ui.Txt(row, "Price", Ui.Theme.textDark, TextAlignmentOptions.TopRight);
                    price.text = pearls ? h.pearls.ToString() : h.coins.ToString("N0", culture);
                    Ui.Place(price.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-86, -6), new Vector2(40, 10));
                    var buy = Ui.Btn(row, "Buy", () =>
                    {
                        var g = GameLoop.Instance;
                        if (g != null && Hats.TryBuy(hat, g.Wallet))
                        {
                            GameEvents.PlaySound("coin");
                            GameEvents.RaiseToast("toast.hat_bought", Loc.Get(hat.NameKey));
                            Show(Tab.Shop);
                        }
                    });
                    Ui.Place((RectTransform)buy.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -3), new Vector2(64, 16));
                    var bt = Ui.Txt(buy.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                    Ui.Stretch(bt.rectTransform);
                    LocText.On(bt, "ui.upgrade.buy");
                    buy.interactable = game != null && Hats.CanBuy(h, game.Wallet);
                }
                y += 24;
            }
        }

        // ---- Morze: pomost, sieci, łódź. Koszty w monetach i drewnie (§11.2: drewno na budowle)

        class ShopItem
        {
            public string nameKey, descKey;
            public string desc;                               // gotowy opis (gdy brak descKey)
            public Sprite sprite;
            public string icon;                               // ikona UI zamiast sprite'a (nasiona)
            public List<CurrencyAmount> cost;
            public System.Func<Island, string> blockedKey;   // null = można kupić, inaczej klucz powodu
            public System.Func<string> blockedText;          // jak blockedKey, ale gotowy tekst (np. „Wymaga: …”)
            public System.Func<string> note;                 // tekst obok przycisku, gdy można kupić (np. „Masz: 2”)
            public string buyKey = "ui.upgrade.buy";
            public System.Action<Island> buy;
            public Button button;
            public TextMeshProUGUI status;
        }

        readonly List<ShopItem> seaItems = new List<ShopItem>();

        static List<CurrencyAmount> Cost(int coins, int wood) => new List<CurrencyAmount>
        {
            new CurrencyAmount(CurrencyType.Coins, coins), new CurrencyAmount(CurrencyType.Wood, wood),
        };

        void BuildSeaShop(ref float y)
        {
            var island = Island.Instance;
            var art = island != null ? island.Art : null;
            seaItems.Add(new ShopItem
            {
                nameKey = "bld.pier", descKey = "ui.shop.pier.desc", sprite = art != null ? art.pier : null, cost = Cost(300, 10),
                blockedKey = i => i.HasPier ? "ui.shop.built" : null,
                buy = i => StartPier(i, Cost(300, 10)),
            });
            seaItems.Add(new ShopItem
            {
                nameKey = "bld.nets", descKey = "ui.shop.nets.desc", sprite = art != null ? art.netBuoy : null, cost = Cost(150, 5),
                blockedKey = i => i.HasNets ? "ui.shop.built" : (!i.HasPier ? "ui.shop.needs_pier" : null),
                buy = i => BuyDirect(i, Cost(150, 5), i.BuildNets, "bld.nets"),
            });
            seaItems.Add(new ShopItem
            {
                nameKey = "bld.boat", descKey = "ui.shop.boat.desc", sprite = art != null ? art.boat : null, cost = Cost(600, 15),
                blockedKey = i => i.HasBoat ? "ui.shop.built" : (!i.HasPier ? "ui.shop.needs_pier" : null),
                buy = i => BuyDirect(i, Cost(600, 15), i.BuildBoat, "bld.boat"),
            });
            seaItems.Add(new ShopItem
            {
                nameKey = "bld.altar", descKey = "ui.shop.altar.desc", sprite = art != null ? art.altar : null,
                cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, 500), new CurrencyAmount(CurrencyType.Stone, 10) },
                blockedKey = i => i.HasBuilding("altar") ? "ui.shop.built" : null,
                buy = i => StartShore(i, "bld.altar", i.Art != null ? i.Art.altar : null,
                    new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, 500), new CurrencyAmount(CurrencyType.Stone, 10) },
                    t => i.SpawnAltar(t) != null),
            });
            var lighthouse = art != null ? art.lighthouse : null;
            if (lighthouse != null)
                seaItems.Add(new ShopItem
                {
                    nameKey = "bld.lighthouse", descKey = "ui.shop.lighthouse.desc", sprite = lighthouse.sprite,
                    cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, lighthouse.price), new CurrencyAmount(CurrencyType.StarShard, 10) },
                    blockedKey = i => i.HasBuilding("lighthouse") ? "ui.shop.built" : null,
                    buy = i => StartShore(i, "bld.lighthouse", lighthouse.sprite,
                        new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, lighthouse.price), new CurrencyAmount(CurrencyType.StarShard, 10) },
                        t => i.SpawnLamp(lighthouse, t) != null, lighthouse),
                });
        }

        /// <summary>
        /// Tekst „opis + końcówka” w jednej linii o szerokości maxW: gdy się nie mieści, opis skraca się z „...”,
        /// a końcówka (np. koszt) zostaje cała.
        /// </summary>
        static void FitWithSuffix(TextMeshProUGUI t, string main, string suffix, float maxW)
        {
            t.text = main + suffix;
            if (Ui.Width(t) <= maxW)
                return;
            int lo = 0, hi = main.Length;
            while (lo < hi)   // najdłuższy początek opisu, który mieści się z „...” i końcówką
            {
                int mid = (lo + hi + 1) / 2;
                t.text = main.Substring(0, mid).TrimEnd() + "..." + suffix;
                if (Ui.Width(t) <= maxW) lo = mid;
                else hi = mid - 1;
            }
            t.text = (lo > 0 ? main.Substring(0, lo).TrimEnd() + "..." : "") + suffix;
        }

        /// <summary>Wiersze sklepu (nasiona, budowle, morze): obrazek, nazwa, opis z kosztem, przycisk albo powód blokady.</summary>
        void BuildItems(ref float y, float rowH)
        {
            float pad = (rowH - 16f) / 2f;
            foreach (var it in seaItems)
            {
                var row = Ui.Place(Ui.Rect("Item_" + it.nameKey, content), Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, rowH - 2));
                var slot = Ui.Img(row, "Slot", Ui.Theme.slot, true);
                Ui.Place(slot.rectTransform, Ui.TopLeft, Ui.TopLeft, Vector2.zero, new Vector2(22, rowH - 2));
                var pic = Ui.Img(slot.transform, "Pic", it.icon != null ? Ui.Theme.Icon(it.icon) : it.sprite, false);
                pic.preserveAspect = true;
                Ui.Place(pic.rectTransform, Ui.Center, Ui.Center, Vector2.zero, it.icon != null ? new Vector2(12, 12) : new Vector2(18, rowH - 6));
                var name = Ui.Txt(row, "Name", Ui.Theme.textDark);
                LocText.On(name, it.nameKey);
                Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -pad + 2), new Vector2(160, 10));
                var item = it;
                it.button = Ui.Btn(row, "Buy", () => { if (Island.Instance != null) item.buy(Island.Instance); });
                var bt = Ui.Txt(it.button.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                LocText.On(bt, it.buyKey);
                float bw = Mathf.Max(40f, Ui.Width(bt) + 10f);
                // opis z kosztem kończy się przed przyciskiem: za długi opis skracamy („...”), koszt zostaje cały,
                // a pełny tekst pokazuje dymek po najechaniu
                var desc = Ui.Txt(row, "Desc", Ui.Theme.textMutedOnPage);
                string fullDesc = it.descKey != null ? Loc.Get(it.descKey) : it.desc;
                string costText = PlacementMode.CostText(it.cost);
                float descW = W - 64 - 28 - bw - 6;
                FitWithSuffix(desc, fullDesc, " - " + costText, descW);
                Ui.Place(desc.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -pad - 8), new Vector2(descW, 10));
                desc.raycastTarget = true;
                desc.gameObject.AddComponent<HoverTip>().Init(tooltip, () => fullDesc + "\n" + costText);
                Ui.Stretch(bt.rectTransform);
                Ui.Place((RectTransform)it.button.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -pad), new Vector2(bw, 16));
                it.status = Ui.Txt(row, "Status", Ui.Theme.textMutedOnPage, TextAlignmentOptions.TopRight);
                // powód blokady w wierszu nazwy
                Ui.Place(it.status.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0, -pad + 2), new Vector2(110, 10));
                y += rowH;
            }
        }

        // ---- Nasiona: jednorazowe odblokowanie za monety, potem w kole nasion

        void BuildSeedShop()
        {
            var game = GameLoop.Instance;
            if (game == null || game.Crops == null)
                return;
            var list = new List<Farming.CropDefinition>();
            foreach (var c in game.Crops.crops)
                if (c != null && !c.hybrid && c.unlockPrice > 0)
                    list.Add(c);
            list.Sort((a, b) => a.unlockPrice.CompareTo(b.unlockPrice));
            foreach (var c in list)
            {
                var crop = c;
                string time = Ui.GrowTime(c);
                seaItems.Add(new ShopItem
                {
                    nameKey = "crop." + c.id, icon = c.SeedIcon, buyKey = "ui.shop.unlock",
                    desc = time + ", " + Loc.Get("ui.seed.sell", c.sellPrice),
                    cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, c.unlockPrice) },
                    blockedKey = _ => game.IsUnlocked(crop) ? "ui.shop.unlocked" : null,
                    buy = _ => { if (game.TryUnlock(crop)) Show(Tab.Shop); },
                });
            }
        }

        // ---- Drzewa owocowe (GDD §15.1): sadzonka stawiana jak lampa, rośnie od pracy, owocuje cyklicznie

        void BuildTreeShop()
        {
            var island = Island.Instance;
            if (island == null)
                return;
            foreach (var sp in Farming.TreeSpecies.All)
            {
                var species = sp;
                var treeArt = island.Art != null ? island.Art.Tree(sp.id) : null;
                Sprite pic = treeArt != null && treeArt.stages != null && treeArt.stages.Length > 2 ? treeArt.stages[2] : null;
                var cost = sp.Cost();
                seaItems.Add(new ShopItem
                {
                    nameKey = sp.NameKey, sprite = pic, cost = cost,
                    desc = Loc.Get("ui.shop.tree.desc", sp.fruitCount, Loc.Get(sp.FruitKey), sp.fruitPrice),
                    buy = i => UiRoot.Instance?.StartPlacement(new PlacementMode.Request
                    {
                        nameKey = species.NameKey, sprite = pic, cost = cost,
                        valid = i.IsFree,
                        ghostAt = t => (Island.TileAnchor(t, 13, out _), false),
                        confirm = t =>
                        {
                            if (i.SpawnTree(species, t) == null)
                                return false;
                            GameEvents.RaiseToast("ui.toast.placed", Loc.Get(species.NameKey));
                            return true;
                        },
                    }),
                });
            }
        }

        // ---- Budowle: skrzynie, ogniki, duszki-handlarze, runy; każda kolejna sztuka droższa

        void BuildBuildingShop()
        {
            var game = GameLoop.Instance;
            var island = Island.Instance;
            var art = island != null ? island.Art : null;
            if (game == null || island == null)
                return;
            foreach (var kind in Automation.AutomationSystem.Kinds)
            {
                string k = kind;
                string familiar = Automation.AutomationSystem.HomeOf(k);
                if (familiar != null && !Familiars.FamiliarSystem.Has(familiar))
                    continue;   // domek pokazuje się, gdy chowaniec już przyszedł
                Sprite pic = null;
                int area = 0;
                if (art != null && familiar != null)
                    pic = art.Home(familiar, false);
                else if (art != null)
                    switch (k)
                    {
                        case Automation.AutomationSystem.Bonfire: pic = art.bonfire != null && art.bonfire.Length > 0 ? art.bonfire[0] : null; break;
                        case Automation.AutomationSystem.DreamHouse: pic = art.dreamHouse; break;
                        case Automation.AutomationSystem.Crate: pic = art.crateFill != null && art.crateFill.Length > 2 ? art.crateFill[2] : null; break;
                        case Automation.AutomationSystem.Nest: pic = art.wispNest != null && art.wispNest.Length > 0 ? art.wispNest[0] : null; area = Automation.Wisp.Radius; break;
                        case Automation.AutomationSystem.Merchant: pic = art.merchantFrames != null && art.merchantFrames.Length > 0 ? art.merchantFrames[0] : null; break;
                        case Automation.AutomationSystem.Cauldron: pic = art.cauldronIdle; break;
                        case Automation.AutomationSystem.Store: pic = art.storehouse; break;
                        case Automation.AutomationSystem.Kiln: pic = art.emberKiln != null && art.emberKiln.Length > 0 ? art.emberKiln[0] : null; break;
                        case Automation.AutomationSystem.Crystal: pic = art.emberCrystal; break;
                        case Automation.AutomationSystem.Rune: pic = art.rune; area = Automation.WildRune.Radius; break;
                    }
                var cost = Automation.AutomationSystem.Cost(k, island.CountBuilding(k));
                string nameKey = Automation.AutomationSystem.NameKey(k);
                seaItems.Add(new ShopItem
                {
                    nameKey = nameKey, sprite = pic, cost = cost,
                    descKey = familiar == null ? "ui.shop." + k + ".desc" : null,
                    desc = familiar != null ? Loc.Get("ui.shop.home.desc", Loc.Get("familiar." + familiar)) : null,
                    blockedText = () => familiar != null
                        ? (island.CountBuilding(k) > 0 ? Loc.Get("ui.shop.home_built") : null)   // jeden domek na chowańca
                        : Automation.AutomationSystem.IsUnlocked(k, game.Upgrades) ? null
                        : Loc.Get("ui.shop.needs_upgrade", Loc.Get("upgrade." + Automation.AutomationSystem.RequiredUpgrade(k) + ".name")),
                    note = () => Loc.Get("ui.shop.owned", island.CountBuilding(k)),
                    buy = i => StartBuilding(i, k, nameKey, pic, cost, area),
                });
            }
            // tablica ogłoszeń (zamówienia): tylko gdy na wyspie jej nie ma (rozebrana albo stary zapis)
            if (island.Board == null)
            {
                var boardCost = Cost(40, 3);
                seaItems.Add(new ShopItem
                {
                    nameKey = "bld.board", descKey = "ui.shop.board.desc", sprite = art != null ? art.noticeBoard : null, cost = boardCost,
                    buy = i =>
                    {
                        if (UiRoot.Instance == null)
                            return;
                        UiRoot.Instance.StartPlacement(new PlacementMode.Request
                        {
                            nameKey = "bld.board", sprite = art != null ? art.noticeBoard : null, cost = boardCost,
                            valid = i.IsFree,
                            ghostAt = t => (Island.TileAnchor(t, 13, out _), false),
                            confirm = t => i.SpawnBoard(t) != null,
                        });
                    },
                });
            }
        }

        void StartBuilding(Island island, string kind, string nameKey, Sprite sprite, List<CurrencyAmount> cost, int area)
        {
            if (UiRoot.Instance == null)
                return;
            if (kind == Automation.AutomationSystem.Merchant)
            {
                // duszek-handlarz staje przy skrzyni bez duszka (klik w skrzynię)
                if (island.CrateWithoutMerchant(Vector3.zero) == null)
                {
                    GameEvents.RaiseToast("toast.merchant.need_crate");
                    return;
                }
                UiRoot.Instance.StartPlacement(new PlacementMode.Request
                {
                    nameKey = nameKey, sprite = sprite, cost = cost, area = 0,
                    valid = t => island.FreeCrateAt(t) != null,
                    ghostAt = t => (Island.MerchantSpot(t), false),
                    confirm = t =>
                    {
                        if (island.SpawnMerchant(island.FreeCrateAt(t)) == null)
                            return false;
                        GameEvents.RaiseToast("ui.toast.placed", Loc.Get(nameKey));
                        return true;
                    },
                });
                return;
            }
            UiRoot.Instance.StartPlacement(new PlacementMode.Request
            {
                nameKey = nameKey, sprite = sprite, cost = cost, area = area,
                valid = island.IsFree,
                ghostAt = t => (Island.TileAnchor(t, 13, out _), false),
                confirm = t =>
                {
                    if (island.SpawnBuilding(kind, t) == null)
                        return false;
                    GameEvents.RaiseToast("ui.toast.placed", Loc.Get(nameKey));
                    return true;
                },
            });
        }

        void StartPier(Island island, List<CurrencyAmount> cost)
        {
            if (UiRoot.Instance == null)
                return;
            UiRoot.Instance.StartPlacement(new PlacementMode.Request
            {
                nameKey = "bld.pier",
                sprite = island.Art != null ? island.Art.pier : null,
                cost = cost,
                valid = island.PierValid,
                ghostAt = t => island.PierPlacement(t, out _, out _),
                confirm = t =>
                {
                    if (!island.BuildPier(t))
                        return false;
                    GameEvents.RaiseToast("ui.toast.placed", Loc.Get("bld.pier"));
                    return true;
                },
            });
        }

        /// <summary>Budowla na brzegu (ołtarz, latarnia): tryb stawiania na wolnym kafelku lądu przy wodzie.</summary>
        void StartShore(Island island, string nameKey, Sprite sprite, List<CurrencyAmount> cost, System.Func<TilePos, bool> build,
                        LampDefinition light = null)
        {
            if (UiRoot.Instance == null)
                return;
            UiRoot.Instance.StartPlacement(new PlacementMode.Request
            {
                nameKey = nameKey, sprite = sprite, cost = cost, light = light,
                valid = island.IsShoreFree,
                ghostAt = t => (Island.TileAnchor(t, 13, out _), false),
                confirm = t =>
                {
                    if (!build(t))
                        return false;
                    GameEvents.RaiseToast("ui.toast.placed", Loc.Get(nameKey));
                    return true;
                },
            });
        }

        void BuyDirect(Island island, List<CurrencyAmount> cost, System.Func<bool> build, string nameKey)
        {
            var game = GameLoop.Instance;
            if (game == null || !game.Wallet.TrySpend(cost))
                return;
            if (!build())
            {
                foreach (var c in cost)
                    game.Wallet.Refund(c.type, c.amount);
                return;
            }
            GameEvents.RaiseToast("ui.toast.placed", Loc.Get(nameKey));
            RefreshShop();
        }

        void RefreshShop()
        {
            var game = GameLoop.Instance;
            var island = Island.Instance;
            foreach (var it in seaItems)
            {
                if (it.button == null)
                    continue;
                string blocked = it.blockedText != null ? it.blockedText()
                    : it.blockedKey == null ? null
                    : island != null ? (it.blockedKey(island) is string key ? Loc.Get(key) : null) : Loc.Get("ui.shop.needs_pier");
                bool afford = game != null && game.Wallet.CanAfford(it.cost);
                it.button.gameObject.SetActive(blocked == null);
                it.button.interactable = afford;
                it.status.text = blocked ?? "";
                if (blocked == null && it.note != null)
                {
                    // notka (np. „Masz: 2”) na lewo od przycisku
                    it.status.text = it.note();
                    var rt = it.status.rectTransform;
                    rt.anchoredPosition = new Vector2(-((RectTransform)it.button.transform).sizeDelta.x - 4f, rt.anchoredPosition.y);
                }
            }
            foreach (var (def, buy, price) in shopRows)
            {
                bool afford = game != null && game.Wallet.Coins >= def.price;
                buy.interactable = afford;
                price.color = afford ? Ui.Theme.textDark : Ui.Theme.textWarn;
            }
        }

        // ------------------------------------------------------------------ Księga rytuałów (GDD §11.3 G)

        readonly List<(Lore.RitualDef def, Button button, TextMeshProUGUI status)> ritualRows =
            new List<(Lore.RitualDef, Button, TextMeshProUGUI)>();
        TextMeshProUGUI moonLine;

        void BuildRituals()
        {
            ritualRows.Clear();
            var chronicle = Lore.Chronicle.Instance;
            float y = 0;
            moonLine = Ui.Txt(content, "Moon", Ui.Theme.textDark);
            Ui.Place(moonLine.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, 0), new Vector2(W - 64, 10));
            y = 24;
            info.text = Loc.Get("ui.rituals.hint");
            foreach (var r in Lore.Rituals.All)
            {
                var row = Ui.Place(Ui.Rect("Ritual_" + r.id, content), Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 22));
                var name = Ui.Txt(row, "Name", Ui.Theme.textDark);
                LocText.On(name, r.nameKey);
                Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -1), new Vector2(160, 10));
                var desc = Ui.Txt(row, "Desc", Ui.Theme.textMutedOnPage);
                string ritualDesc = Loc.Get(r.descKey);
                string ritualCost = Loc.Get("ui.rituals.points", r.points) + ", " + PlacementMode.CostText(r.cost);
                FitWithSuffix(desc, ritualDesc, " - " + ritualCost, W - 110);
                Ui.Place(desc.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -11), new Vector2(W - 110, 10));
                desc.raycastTarget = true;
                desc.gameObject.AddComponent<HoverTip>().Init(tooltip, () => ritualDesc + "\n" + ritualCost);
                var def = r;
                var b = Ui.Btn(row, "Perform", () =>
                {
                    var game = GameLoop.Instance;
                    if (chronicle != null && game != null)
                        chronicle.TryPerform(def, game.Wallet);
                    RefreshRituals();
                });
                var bt = Ui.Txt(b.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                Ui.Stretch(bt.rectTransform);
                LocText.On(bt, "ui.rituals.perform");
                // szerokość z tekstu (DE „Vollziehen” dotykał ramki w 42 px)
                Ui.Place((RectTransform)b.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -3), new Vector2(Mathf.Max(42f, Ui.Width(bt) + 10f), 16));
                var status = Ui.Txt(row, "Status", Ui.Theme.textGoldOnPage, TextAlignmentOptions.TopRight);
                Ui.Place(status.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0, -6), new Vector2(60, 10));
                ritualRows.Add((r, b, status));
                y += 23;
            }
            RefreshRituals();
        }

        void RefreshRituals()
        {
            var chronicle = Lore.Chronicle.Instance;
            var game = GameLoop.Instance;
            if (moonLine != null && chronicle != null)
                moonLine.text = Loc.Get("ui.rituals.moon", chronicle.MoonPoints)
                              + "   " + Loc.Get("ui.rituals.cycle", GameClock.NightsToNewMoon(System.DateTime.UtcNow), chronicle.PendingPoints);
            foreach (var (def, b, status) in ritualRows)
            {
                bool done = chronicle != null && chronicle.Has(def.id);
                b.gameObject.SetActive(!done);
                b.interactable = chronicle != null && game != null && chronicle.MoonPoints >= def.points && game.Wallet.CanAfford(def.cost);
                status.text = done ? Loc.Get("ui.rituals.done") : "";
            }
        }

        // ------------------------------------------------------------------ Rejs we mgłę

        TextMeshProUGUI voyageHead, voyageRun, voyageGlow, sailText;
        Button sailButton;
        float sailArmedUntil;
        GiftGrid giftGrid;

        static string Num(long n) => n.ToString("N0", LocCore.Culture(Loc.Language));

        void BuildVoyage()
        {
            info.text = Loc.Get("ui.voyage.hint");
            float w = W - 64;
            voyageHead = Ui.Txt(content, "Pearls", Ui.Theme.textDark);
            Ui.Place(voyageHead.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, 0), new Vector2(w, 10));
            voyageGlow = Ui.Txt(content, "Glow", Ui.Theme.textMutedOnPage);
            Ui.Place(voyageGlow.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -11), new Vector2(w, 10));
            voyageRun = Ui.Txt(content, "Run", Ui.Theme.textMutedOnPage);
            Ui.Place(voyageRun.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -22), new Vector2(w, 10));
            sailButton = Ui.Btn(content, "Sail", Sail);
            Ui.Place((RectTransform)sailButton.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -35), new Vector2(w, 16));
            sailText = Ui.Txt(sailButton.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
            Ui.Stretch(sailText.rectTransform);
            sailButton.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Loc.Get("ui.voyage.sail_tip"));

            var head = Ui.Txt(content, "Gifts", Ui.Theme.textGoldOnPage);
            Ui.Place(head.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -57), new Vector2(w, 10));
            LocText.On(head, "ui.voyage.gifts");
            var where = Ui.Txt(content, "GiftsHint", Ui.Theme.textMutedOnPage);
            Ui.Place(where.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -68), new Vector2(w, 10));
            FitWithSuffix(where, Loc.Get("ui.voyage.gifts_where"), "", w);
            // kolekcja: zdobyte w kolorze rzadkości, niezdobyte wyszarzałe (losuje się je w Przystani we mgle)
            giftGrid = GiftGrid.Create(content, new Vector2(0, -81), w, tooltip);
            RefreshVoyage();
        }

        void RefreshVoyage()
        {
            if (voyageHead == null)
                return;
            voyageHead.text = Loc.Get("ui.voyage.pearls", Prestige.Pearls, Prestige.Earned, Prestige.Voyages + 1);
            voyageGlow.text = Loc.Get("ui.voyage.glow", Mathf.RoundToInt((Prestige.SaleMultiplier - 1f) * 100f));
            voyageRun.text = Loc.Get("ui.voyage.run", Num(Market.RunCoins), Prestige.PendingPearls, Num(Prestige.CoinsForNextPearl));
            bool armed = Time.unscaledTime < sailArmedUntil;
            sailButton.interactable = Prestige.CanSail;
            sailText.text = !Prestige.CanSail ? Loc.Get("ui.voyage.cant")
                          : Loc.Get(armed ? "ui.voyage.confirm" : "ui.voyage.sail", Prestige.PendingPearls);
            giftGrid?.Refresh();
        }

        /// <summary>Rejs: pierwszy klik uzbraja (4 s), drugi odpływa (nowa wyspa w tym samym slocie).</summary>
        void Sail()
        {
            if (!Prestige.CanSail)
                return;
            if (Time.unscaledTime >= sailArmedUntil)
            {
                sailArmedUntil = Time.unscaledTime + 4f;
                RefreshVoyage();
                return;
            }
            var save = FindAnyObjectByType<Save.SaveSystem>();
            var game = GameLoop.Instance;
            if (save == null || game == null)
                return;
            Close();
            // scenka odpłynięcia (lampy gasną, ogniki wracają, mgła zamyka okno), potem Przystań we mgle
            if (UiRoot.Instance != null && UiRoot.Instance.Scenes != null)
                UiRoot.Instance.Scenes.Depart(() => save.SailIntoMist(game));
            else
                save.SailIntoMist(game);
        }

        // ------------------------------------------------------------------ Bestiariusz i lore (GDD §11.3 H)

        int bestiarySection;   // 0 rośliny, 1 ryby, 2 zdarzenia, 3 lore, 4 chowańce
        int lorePage;
        static readonly string[] FishIds = { "normal", "glow" };
        static readonly string[] EventIds = { "wisp", "star", "merchant", "monster", "fullmoon" };
        // nagroda za ukończenie kategorii (punkty księżyca)
        static readonly int[] CategoryReward = { 2, 1, 2, 3 };

        void BuildBestiary()
        {
            var chronicle = Lore.Chronicle.Instance;
            float x = 0;
            string[] sections = { "ui.bestiary.plants", "ui.bestiary.fish", "ui.bestiary.events", "ui.bestiary.lore", "ui.bestiary.familiars" };
            for (int i = 0; i < sections.Length; i++)
            {
                int sec = i;
                var b = Ui.Btn(content, "Section", () => { bestiarySection = sec; lorePage = 0; Show(Tab.Bestiary); });
                var label = Ui.Txt(b.transform, "Text", sec == bestiarySection ? Ui.Theme.textGold : Ui.Theme.textLight, TextAlignmentOptions.Center);
                label.text = Loc.Get(sections[i]);
                float bw = Mathf.Max(40f, Ui.Width(label) + 12f);
                Ui.Stretch(label.rectTransform);
                Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, 0), new Vector2(bw, 14));
                x += bw + 4;
            }
            info.text = Loc.Get("ui.bestiary.hint");
            if (bestiarySection == 4)
            {
                BuildFamiliars();
                return;
            }
            if (chronicle == null)
                return;

            // wpisy kategorii: (id w kronice, tekst gdy odkryte)
            var entries = new List<(string id, string label)>();
            var game = GameLoop.Instance;
            switch (bestiarySection)
            {
                case 0:
                    if (game != null && game.Crops != null)
                        foreach (var c in game.Crops.crops)
                            if (c != null) entries.Add(("crop:" + c.id, Loc.Get("crop." + c.id)));
                    break;
                case 1:
                    foreach (var f in FishIds) entries.Add(("fish:" + f, Loc.Get("ui.fish." + f)));
                    break;
                case 2:
                    foreach (var e in EventIds) entries.Add(("event:" + e, Loc.Get("ui.event." + e)));
                    break;
                default:
                    foreach (var l in Lore.LoreBook.All) entries.Add(("lore:" + l, Loc.Get(l)));
                    break;
            }
            int found = 0;
            foreach (var e in entries)
                if (chronicle.Found(e.id)) found++;
            float y = 20;
            var progress = Ui.Txt(content, "Progress", Ui.Theme.textDark);
            progress.text = Loc.Get("ui.bestiary.progress", found, entries.Count);
            Ui.Place(progress.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(120, 10));
            string category = "bestiary." + bestiarySection;
            if (found == entries.Count && entries.Count > 0)
            {
                if (chronicle.Claimed(category))
                {
                    var done = Ui.Txt(content, "Done", Ui.Theme.textGoldOnPage, TextAlignmentOptions.TopRight);
                    done.text = Loc.Get("ui.bestiary.complete");
                    Ui.Place(done.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0, -y), new Vector2(160, 10));
                }
                else
                {
                    int reward = CategoryReward[Mathf.Clamp(bestiarySection, 0, CategoryReward.Length - 1)];
                    var claim = Ui.Btn(content, "Claim", () => { chronicle.Claim(category, reward); Show(Tab.Bestiary); });
                    var ct = Ui.Txt(claim.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                    ct.text = Loc.Get("ui.bestiary.claim", reward);
                    float cw = Ui.Width(ct) + 12f;
                    Ui.Stretch(ct.rectTransform);
                    Ui.Place((RectTransform)claim.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -y + 2), new Vector2(cw, 14));
                }
            }
            y += 16;

            if (bestiarySection == 3)
            {
                // lore: odkryte fragmenty w całości, po 4 na stronę
                var lines = new List<string>();
                foreach (var e in entries)
                    if (chronicle.Found(e.id)) lines.Add(e.label);
                const int perPage = 4;
                int pages = Mathf.Max(1, (lines.Count + perPage - 1) / perPage);
                lorePage = Mathf.Clamp(lorePage, 0, pages - 1);
                for (int i = lorePage * perPage; i < Mathf.Min(lines.Count, (lorePage + 1) * perPage); i++)
                {
                    var t = Ui.Txt(content, "Lore", Ui.Theme.textDark);
                    t.textWrappingMode = TextWrappingModes.Normal;
                    t.text = lines[i];
                    float h = Mathf.Ceil(t.GetPreferredValues(t.text, W - 70, 0f).y);
                    Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 70, h));
                    y += h + 6;
                }
                if (pages > 1)
                {
                    var prev = Ui.Btn(content, "Prev", () => { lorePage--; Show(Tab.Bestiary); });
                    Ui.Place((RectTransform)prev.transform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(0, 0), new Vector2(18, 14));
                    Ui.Stretch(Ui.Txt(prev.transform, "T", Ui.Theme.textLight, TextAlignmentOptions.Center).rectTransform).GetComponent<TextMeshProUGUI>().text = "<";
                    prev.interactable = lorePage > 0;
                    var next = Ui.Btn(content, "Next", () => { lorePage++; Show(Tab.Bestiary); });
                    Ui.Place((RectTransform)next.transform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(22, 0), new Vector2(18, 14));
                    Ui.Stretch(Ui.Txt(next.transform, "T", Ui.Theme.textLight, TextAlignmentOptions.Center).rectTransform).GetComponent<TextMeshProUGUI>().text = ">";
                    next.interactable = lorePage < pages - 1;
                }
                return;
            }
            // pozostałe kategorie: dwie kolumny nazw, nieodkryte jako „???”
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                bool known = chronicle.Found(e.id);
                var t = Ui.Txt(content, "Entry", known ? Ui.Theme.textDark : Ui.Theme.textMutedOnPage);
                t.text = known ? e.label : "???";
                float col = (i % 2) * ((W - 64) / 2f);
                Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(col, -(y + (i / 2) * 13)), new Vector2((W - 64) / 2f - 4, 10));
            }
        }

        /// <summary>
        /// Chowańce: karta na każdego (obrazek, imię, bonus i więź). Nieprzybyłe: ciemna sylwetka, „???” i podpowiedź,
        /// co zrobić, żeby przyszły (z postępem).
        /// </summary>
        void BuildFamiliars()
        {
            info.text = Loc.Get("ui.familiar.hint");
            var art = Island.Instance != null ? Island.Instance.Art : null;
            const int cols = 4;
            float cw = (W - 64) / cols, ch = 88f, top = 20f;
            var defs = Familiars.FamiliarSystem.Defs;
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                bool has = Familiars.FamiliarSystem.Has(d.id);
                float x = (i % cols) * cw, y = top + (i / cols) * ch;
                var fa = art != null ? art.Familiar(d.id) : null;
                var sprite = fa != null && fa.frames != null && fa.frames.Length > 0 ? fa.frames[0] : null;
                if (sprite != null)
                {
                    var pic = Ui.Img(content, "Pic_" + d.id, sprite, false);
                    pic.color = has ? Color.white : new Color(0.08f, 0.05f, 0.1f, 0.55f);   // nieznany: sylwetka
                    var size = sprite.rect.size * 2f;
                    Ui.Place(pic.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x + (cw - size.x) / 2f, -y), size);
                }
                var name = Ui.Txt(content, "Name", has ? Ui.Theme.textDark : Ui.Theme.textMutedOnPage, TextAlignmentOptions.Top);
                name.text = has ? Loc.Get("familiar." + d.id) : "???";
                Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -(y + 30)), new Vector2(cw - 4, 10));
                var t = Ui.Txt(content, "Info", has ? Ui.Theme.textDark : Ui.Theme.textMutedOnPage, TextAlignmentOptions.Top);
                t.textWrappingMode = TextWrappingModes.Normal;
                if (has)
                    t.text = Familiars.FamiliarSystem.BonusText(d.id) + "\n"
                           + Loc.Get("ui.familiar.bond", Familiars.FamiliarSystem.Bond(d.id), Familiars.FamiliarSystem.MaxBond);
                else
                {
                    var (have, need) = Familiars.FamiliarSystem.Progress(d.id);
                    t.text = Loc.Get("familiar." + d.id + ".hint", Mathf.Min(have, need), need);
                }
                Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x + 2, -(y + 41)), new Vector2(cw - 6, 46));
            }
        }

        // ------------------------------------------------------------------ Statystyki

        void BuildStats()
        {
            ClearContent();
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var r = game.Rhythm;
            float y = 0;
            Line("ui.stats.today", ref y, r.DailyActiveHours);
            Line("ui.stats.streak", ref y, r.StreakMinutes);
            Line("ui.stats.multiplier", ref y, r.EssenceMultiplier);
            Line("ui.stats.essence", ref y, Mathf.FloorToInt(game.Essence.Amount + 0.001f), game.Essence.Capacity);   // w dół, jak w HUD
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

        int optionsSection;   // 0 ogólne, 1 okno, 2 system, 3 dźwięk (GDD §12.7)
        float resetArmedUntil = -1f;
        const float OptCol = 124f, OptRow = 19f;
        static readonly int[] IdleSteps = { 15, 30, 60, 120, 300, 600 };

        void BuildOptions()
        {
            float x = 0;
            foreach (var (section, key) in new[] { (0, "ui.options.general"), (1, "ui.options.window"), (3, "ui.options.sound"), (2, "ui.options.system") })
            {
                int sec = section;
                var b = Ui.Btn(content, "Section", () => { optionsSection = sec; Show(Tab.Options); });
                var label = Ui.Txt(b.transform, "Text", sec == optionsSection ? Ui.Theme.textGold : Ui.Theme.textLight, TextAlignmentOptions.Center);
                label.text = Loc.Get(key);
                float bw = Mathf.Max(40f, Ui.Width(label) + 12f);
                Ui.Stretch(label.rectTransform);
                Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, 0), new Vector2(bw, 14));
                x += bw + 4;
            }
            float y = 20;
            if (optionsSection == 1) BuildWindowOptions(y);
            else if (optionsSection == 2) BuildSystemOptions(y);
            else if (optionsSection == 3) BuildSoundOptions(y);
            else BuildGeneralOptions(y);
        }

        /// <summary>Dźwięk: głośność tła i efektów co 10%, cisza, gdy okno nie jest aktywne.</summary>
        void BuildSoundOptions(float y)
        {
            OptLabel("ui.options.vol_ambient", y);
            Stepper(OptCol, y, $"{Mathf.RoundToInt(Audio.AudioManager.AmbientVolume * 100)}%", 30f, d =>
                Audio.AudioManager.AmbientVolume = Mathf.Round(Audio.AudioManager.AmbientVolume * 10f + d) / 10f);
            y += OptRow;
            OptLabel("ui.options.vol_sfx", y);
            Stepper(OptCol, y, $"{Mathf.RoundToInt(Audio.AudioManager.SfxVolume * 100)}%", 30f, d =>
            {
                Audio.AudioManager.SfxVolume = Mathf.Round(Audio.AudioManager.SfxVolume * 10f + d) / 10f;
                GameEvents.PlaySound("coin");   // próbka głośności
            });
            y += OptRow;
            OptLabel("ui.options.mute_bg", y);
            Toggle(OptCol, y, Audio.AudioManager.MuteInBackground, true, v => Audio.AudioManager.MuteInBackground = v);
            y += OptRow;
            OptLabel("ui.options.wisp_voices", y);
            Toggle(OptCol, y, Audio.AudioManager.WispVoices, true, v => Audio.AudioManager.WispVoices = v);
            info.text = Loc.Get("ui.options.sound_hint");
        }

        void BuildGeneralOptions(float y)
        {
            var game = GameLoop.Instance;
            Header("ui.options.language", ref y);
            float x = 0;
            foreach (var lang in Loc.Languages)
            {
                var code = lang;
                float w = OptButton("Lang_" + code, Loc.Get("lang." + code), x, y, 30f, () => Loc.SetLanguage(code), code == Loc.Language);
                x += w + 4;
            }
            y += 22;

            // czcionka: czytelna (małe litery) albo dawna 3x5; przełącza się od razu
            OptLabel("ui.options.font", y);
            float fx = OptCol;
            foreach (bool readable in new[] { true, false })
            {
                bool r = readable;
                fx += OptButton("Font_" + r, Loc.Get(r ? "ui.options.font_readable" : "ui.options.font_pixel"), fx, y, 30f,
                                () => { if (UiRoot.Instance != null) UiRoot.Instance.SetReadableFont(r); }, UiRoot.ReadableFont == r) + 4;
            }
            y += OptRow;

            var lighting = FindAnyObjectByType<DayNightLighting>();
            OptLabel("ui.options.brightness", y);
            Stepper(OptCol, y, lighting != null ? $"{Mathf.RoundToInt(lighting.MinBrightness * 100)}%" : "-", 30f, d =>
            {
                if (lighting == null) return;
                lighting.MinBrightness = Mathf.Clamp(lighting.MinBrightness + d * 0.05f, 0.15f, 0.7f);
                PlayerPrefs.SetFloat("minBrightness", lighting.MinBrightness);
            });
            y += OptRow;

            if (game != null)
            {
                OptLabel("ui.options.rhythm", y);
                Toggle(OptCol, y, game.Rhythm.RhythmEnabled, true, v =>
                {
                    game.Rhythm.RhythmEnabled = v;
                    PlayerPrefs.SetInt("rhythm", v ? 1 : 0);
                });
                y += OptRow;

                OptLabel("ui.options.idle", y);
                var activity = game.Activity;
                int seconds = Mathf.RoundToInt(activity.IdleThresholdSeconds);
                Stepper(OptCol, y, Loc.Get("ui.options.seconds", seconds), 34f, d =>
                {
                    int i = 0;
                    while (i < IdleSteps.Length - 1 && IdleSteps[i] < seconds) i++;
                    activity.IdleThresholdSeconds = IdleSteps[Mathf.Clamp(i + d, 0, IdleSteps.Length - 1)];
                });
            }
            y += OptRow;

            // płynność animacji = liczba klatek na sekundę (z fokusem / w tle)
            var perf = AppPerformance.Instance;
            if (perf != null)
            {
                OptLabel("ui.options.smooth", y);
                float x2 = OptCol;
                for (int p = 0; p < 3; p++)
                {
                    int prof = p;
                    x2 += OptButton("Smooth" + p, Loc.Get("ui.options.smooth." + p), x2, y, 24f, () => perf.Profile = prof, perf.Profile == p) + 3;
                }
            }
            info.text = Loc.Get("ui.options.idle_hint");
        }

        void BuildWindowOptions(float y)
        {
            var view = ViewController.Instance;
            var desk = Wyspa.Platform.DesktopWindow.Instance;
            bool native = Wyspa.Platform.DesktopWindow.Supported && desk != null;

            OptLabel("ui.options.scale", y);
            float x = OptCol;
            for (int s = 2; s <= 6; s += 2)   // skala parzysta (woda w 32 px): x2, x4, x6 w pikselach grafik 16 px
            {
                int scale = s;
                x += OptButton("Scale" + s, "x" + s, x, y, 20f, () => { if (view != null) view.SetScale(scale); },
                               view != null && view.Scale == s) + 4;
            }
            y += OptRow;

            OptLabel("ui.options.ui_size", y);
            x = OptCol;
            for (int s = 0; s <= 4; s++)
            {
                int size = s;
                x += OptButton("UiSize" + s, s == 0 ? Loc.Get("ui.options.ui_size.auto") : "x" + s, x, y, 20f,
                               () => UiRoot.UiScaleSetting = size, UiRoot.UiScaleSetting == s) + 4;
            }
            y += OptRow;

            OptLabel("ui.options.mini", y);
            Toggle(OptCol, y, view != null && view.Mini, view != null, v => view.Mini = v);
            y += OptRow;

            OptLabel("ui.options.frame", y);
            Toggle(OptCol, y, desk == null || !desk.Frameless, native && !desk.Transparent, v => desk.Frameless = !v);
            y += OptRow;

            OptLabel("ui.options.topmost", y);
            Toggle(OptCol, y, desk != null && desk.TopMost, native, v => desk.TopMost = v);
            y += OptRow;

            OptLabel("ui.options.transparent", y);
            Toggle(OptCol, y, desk != null && desk.Transparent, desk != null, v => desk.Transparent = v);
            y += OptRow;

            OptLabel("ui.options.corner", y);
            x = OptCol;
            string[] corners = { "none", "tl", "tr", "bl", "br" };
            for (int i = 0; i < corners.Length; i++)
            {
                var c = (Wyspa.Platform.ScreenCorner)i;
                var b = OptButtonRef("Corner" + i, Loc.Get("ui.options.corner." + corners[i]), x, y, 18f,
                                     () => desk.Corner = c, desk != null && desk.Corner == c, out float w);
                b.interactable = native;
                x += w + 3;
            }
            info.text = Loc.Get(native ? "ui.options.window_hint" : "ui.options.window_build");
        }

        void BuildSystemOptions(float y)
        {
            OptLabel("ui.options.autostart", y);
            Toggle(OptCol, y, Wyspa.Platform.Autostart.Enabled, Wyspa.Platform.Autostart.Supported,
                   v => Wyspa.Platform.Autostart.Enabled = v);
            y += OptRow;

            OptLabel("ui.options.notify", y);
            Toggle(OptCol, y, Wyspa.Platform.TrayIcon.NotificationsEnabled, Wyspa.Platform.WinApi.Supported,
                   v => Wyspa.Platform.TrayIcon.NotificationsEnabled = v);
            y += OptRow;

            if (Wyspa.Save.SaveSystem.InMenu)
            {
                // menu główne: bez resetu zapisu (nie ma wczytanej gry)
                info.text = Loc.Get(Wyspa.Platform.Autostart.Supported ? "ui.options.reset_hint" : "ui.options.window_build");
                return;
            }
            OptLabel("ui.options.reset", y);
            bool armed = Time.unscaledTime < resetArmedUntil;
            var b = OptButtonRef("Reset", Loc.Get(armed ? "ui.options.reset_confirm" : "ui.options.reset_button"), OptCol, y, 40f, () =>
            {
                if (Time.unscaledTime < resetArmedUntil)
                {
                    var game = GameLoop.Instance;
                    var save = game != null ? game.GetComponent<Wyspa.Save.SaveSystem>() : null;
                    if (save != null)
                    {
                        Close();
                        save.ResetAll();
                    }
                    return;
                }
                resetArmedUntil = Time.unscaledTime + 4f;   // drugi klik w ciągu 4 s potwierdza
            }, false, out _);
            if (armed)
                b.GetComponentInChildren<TextMeshProUGUI>().color = Ui.Theme.textWarn;
            y += OptRow + 6;

            OptLabel("act.quit", y);
            OptButton("Quit", Loc.Get("ui.options.quit_button"), OptCol, y, 40f, UiRoot.QuitGame, false);
            y += OptRow + 6;

            var dbg = Ui.Txt(content, "Debug", Ui.Theme.textMutedOnPage);
            LocText.On(dbg, "ui.options.debug");
            Ui.Place(dbg.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W - 64, 10));
            info.text = Loc.Get(Wyspa.Platform.Autostart.Supported ? "ui.options.reset_hint" : "ui.options.window_build");
        }

        // --- małe klocki opcji: etykieta po lewej, przyciski od kolumny OptCol

        void OptLabel(string key, float y)
        {
            var t = Ui.Txt(content, key, Ui.Theme.textDark);
            LocText.On(t, key);
            Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y - 3), new Vector2(OptCol - 4, 10));
        }

        float OptButton(string name, string text, float x, float y, float minW, System.Action onClick, bool selected)
        {
            OptButtonRef(name, text, x, y, minW, onClick, selected, out float w);
            return w;
        }

        /// <summary>Przycisk z tekstem; po kliknięciu strona opcji buduje się od nowa (zaznaczenia, wartości).</summary>
        Button OptButtonRef(string name, string text, float x, float y, float minW, System.Action onClick, bool selected, out float width)
        {
            var b = Ui.Btn(content, name, () => { onClick(); Show(Tab.Options); });
            var label = Ui.Txt(b.transform, "Text", selected ? Ui.Theme.textGold : Ui.Theme.textLight, TextAlignmentOptions.Center);
            label.text = text;
            width = Mathf.Max(minW, Ui.Width(label) + 8f);
            Ui.Stretch(label.rectTransform);
            Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -y), new Vector2(width, 16));
            return b;
        }

        void Toggle(float x, float y, bool on, bool interactable, System.Action<bool> set)
        {
            var b = OptButtonRef("Toggle", Loc.Get(on ? "ui.on" : "ui.off"), x, y, 40f, () => set(!on), on, out _);
            b.interactable = interactable;
        }

        /// <summary>[-] wartość [+]; zwraca x za przyciskiem „+”.</summary>
        float Stepper(float x, float y, string value, float valueW, System.Action<int> change)
        {
            OptButton("Minus", "-", x, y, 16f, () => change(-1), false);
            var t = Ui.Txt(content, "Value", Ui.Theme.textDark, TextAlignmentOptions.Center);
            t.text = value;
            Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x + 16f, -y - 3), new Vector2(valueW, 10));
            OptButton("Plus", "+", x + 16f + valueW, y, 16f, () => change(1), false);
            return x + 32f + valueW;
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
