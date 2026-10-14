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
                case Tab.Shop: BuildShop(); break;
                case Tab.Rituals: BuildRituals(); break;
                case Tab.Bestiary: BuildBestiary(); break;
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
            else if (current == Tab.Shop) RefreshShop();
            else if (current == Tab.Rituals) RefreshRituals();
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
            foreach (var (section, key) in new[] { (0, "ui.shop.seeds"), (4, "ui.shop.trees"), (1, "ui.shop.lamps"), (2, "ui.shop.buildings"), (3, "ui.shop.sea") })
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
                    var stats = Ui.Txt(row, "Info", Ui.Theme.textMuted);
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

        // ---- Morze (Etap 4): pomost, sieci, łódź. Koszty w monetach i drewnie (§11.2: drewno na budowle)

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
                var desc = Ui.Txt(row, "Desc", Ui.Theme.textMuted);
                desc.text = (it.descKey != null ? Loc.Get(it.descKey) : it.desc) + " - " + PlacementMode.CostText(it.cost);
                Ui.Place(desc.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(28, -pad - 8), new Vector2(240, 10));
                var item = it;
                it.button = Ui.Btn(row, "Buy", () => { if (Island.Instance != null) item.buy(Island.Instance); });
                var bt = Ui.Txt(it.button.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                LocText.On(bt, it.buyKey);
                float bw = Mathf.Max(40f, Ui.Width(bt) + 10f);
                Ui.Stretch(bt.rectTransform);
                Ui.Place((RectTransform)it.button.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -pad), new Vector2(bw, 16));
                it.status = Ui.Txt(row, "Status", Ui.Theme.textMuted, TextAlignmentOptions.TopRight);
                Ui.Place(it.status.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0, -pad - 3), new Vector2(110, 10));
                y += rowH;
            }
        }

        // ---- Nasiona (Etap 6b): jednorazowe odblokowanie za monety, potem w kole nasion

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
                string time = c.kind == Farming.CropKind.Day
                    ? Loc.Get("ui.seed.time_work", Mathf.RoundToInt(c.growSeconds / 60f))
                    : Loc.Get("ui.seed.time_night", Mathf.Round(c.growSeconds / 360f) / 10f);
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

        // ---- Budowle (Etap 6b): skrzynie, ogniki, duszki-handlarze, runy; każda kolejna sztuka droższa

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
                Sprite pic = null;
                int area = 0;
                if (art != null)
                    switch (k)
                    {
                        case Automation.AutomationSystem.Crate: pic = art.crateFill != null && art.crateFill.Length > 2 ? art.crateFill[2] : null; break;
                        case Automation.AutomationSystem.Wisp: pic = art.wispFrames != null && art.wispFrames.Length > 0 ? art.wispFrames[0] : null; area = Automation.WispGardener.Radius; break;
                        case Automation.AutomationSystem.Merchant: pic = art.merchantFrames != null && art.merchantFrames.Length > 0 ? art.merchantFrames[0] : null; break;
                        default: pic = art.rune; area = Automation.SeedRune.Radius; break;
                    }
                var cost = Automation.AutomationSystem.Cost(k, island.CountBuilding(k));
                string nameKey = Automation.AutomationSystem.NameKey(k);
                seaItems.Add(new ShopItem
                {
                    nameKey = nameKey, descKey = "ui.shop." + k + ".desc", sprite = pic, cost = cost,
                    blockedText = () => Automation.AutomationSystem.IsUnlocked(k, game.Upgrades) ? null
                        : Loc.Get("ui.shop.needs_upgrade", Loc.Get("upgrade." + Automation.AutomationSystem.RequiredUpgrade(k) + ".name")),
                    note = () => Loc.Get("ui.shop.owned", island.CountBuilding(k)),
                    buy = i => StartBuilding(i, k, nameKey, pic, cost, area),
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
                    game.Wallet.Add(c.type, c.amount);
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
                var desc = Ui.Txt(row, "Desc", Ui.Theme.textMuted);
                desc.text = Loc.Get(r.descKey) + " - " + Loc.Get("ui.rituals.points", r.points) + ", " + PlacementMode.CostText(r.cost);
                Ui.Place(desc.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -11), new Vector2(W - 110, 10));
                var def = r;
                var b = Ui.Btn(row, "Perform", () =>
                {
                    var game = GameLoop.Instance;
                    if (chronicle != null && game != null)
                        chronicle.TryPerform(def, game.Wallet);
                    RefreshRituals();
                });
                Ui.Place((RectTransform)b.transform, Ui.TopRight, Ui.TopRight, new Vector2(0, -3), new Vector2(42, 16));
                var bt = Ui.Txt(b.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
                Ui.Stretch(bt.rectTransform);
                LocText.On(bt, "ui.rituals.perform");
                var status = Ui.Txt(row, "Status", Ui.Theme.textGold, TextAlignmentOptions.TopRight);
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
                              + "   " + Loc.Get("ui.rituals.cycle", Mathf.CeilToInt(GameClock.DaysToNewMoon(System.DateTime.UtcNow)), chronicle.PendingPoints);
            foreach (var (def, b, status) in ritualRows)
            {
                bool done = chronicle != null && chronicle.Has(def.id);
                b.gameObject.SetActive(!done);
                b.interactable = chronicle != null && game != null && chronicle.MoonPoints >= def.points && game.Wallet.CanAfford(def.cost);
                status.text = done ? Loc.Get("ui.rituals.done") : "";
            }
        }

        // ------------------------------------------------------------------ Bestiariusz i lore (GDD §11.3 H)

        int bestiarySection;   // 0 rośliny, 1 ryby, 2 zdarzenia, 3 lore
        int lorePage;
        static readonly string[] FishIds = { "normal", "glow" };
        static readonly string[] EventIds = { "wisp", "star", "merchant", "monster", "fullmoon" };
        static readonly string[] LoreIds = { "lore.1", "lore.2", "lore.3", "lore.4", "lore.5", "lore.6", "lore.crypt", "lore.statue", "lore.stairs", "lore.monster" };
        // nagroda za ukończenie kategorii (punkty księżyca)
        static readonly int[] CategoryReward = { 2, 1, 2, 3 };

        void BuildBestiary()
        {
            var chronicle = Lore.Chronicle.Instance;
            float x = 0;
            string[] sections = { "ui.bestiary.plants", "ui.bestiary.fish", "ui.bestiary.events", "ui.bestiary.lore" };
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
                    foreach (var l in LoreIds) entries.Add(("lore:" + l, Loc.Get(l)));
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
                    var done = Ui.Txt(content, "Done", Ui.Theme.textGold, TextAlignmentOptions.TopRight);
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
                var t = Ui.Txt(content, "Entry", known ? Ui.Theme.textDark : Ui.Theme.textMuted);
                t.text = known ? e.label : "???";
                float col = (i % 2) * ((W - 64) / 2f);
                Ui.Place(t.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(col, -(y + (i / 2) * 13)), new Vector2((W - 64) / 2f - 4, 10));
            }
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

        int optionsSection;   // 0 ogólne, 1 okno, 2 system (GDD §12.7)
        float resetArmedUntil = -1f;
        const float OptCol = 124f, OptRow = 19f;
        static readonly int[] IdleSteps = { 15, 30, 60, 120, 300, 600 };

        void BuildOptions()
        {
            float x = 0;
            foreach (var (section, key) in new[] { (0, "ui.options.general"), (1, "ui.options.window"), (2, "ui.options.system") })
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
            else BuildGeneralOptions(y);
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

            var lighting = FindAnyObjectByType<DayNightLighting>();
            OptLabel("ui.options.brightness", y);
            Stepper(OptCol, y, lighting != null ? $"{Mathf.RoundToInt(lighting.MinBrightness * 100)}%" : "-", 30f, d =>
            {
                if (lighting == null) return;
                lighting.MinBrightness = Mathf.Clamp(lighting.MinBrightness + d * 0.05f, 0.15f, 0.7f);
                PlayerPrefs.SetFloat("minBrightness", lighting.MinBrightness);
            });
            y += OptRow;

            var clock = game != null ? game.Clock : null;
            OptLabel("ui.options.night", y);
            if (clock != null)
            {
                float after = Stepper(OptCol, y, $"{Mathf.RoundToInt(clock.NightStartsAt):00}:00", 26f, d =>
                {
                    float v = Mathf.Repeat(Mathf.Round(clock.NightStartsAt) + d, 24f);
                    if (!Mathf.Approximately(v, clock.NightEndsAt)) clock.NightStartsAt = v;
                });
                Stepper(after + 6f, y, $"{Mathf.RoundToInt(clock.NightEndsAt):00}:00", 26f, d =>
                {
                    float v = Mathf.Repeat(Mathf.Round(clock.NightEndsAt) + d, 24f);
                    if (!Mathf.Approximately(v, clock.NightStartsAt)) clock.NightEndsAt = v;
                });
            }
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
            for (int s = 2; s <= 6; s++)
            {
                int scale = s;
                x += OptButton("Scale" + s, "x" + s, x, y, 20f, () => { if (view != null) view.SetScale(scale); },
                               view != null && view.Scale == s) + 4;
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

            var dbg = Ui.Txt(content, "Debug", Ui.Theme.textMuted);
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
