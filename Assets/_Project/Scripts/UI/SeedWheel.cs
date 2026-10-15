using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>Koło nasion (GDD §12.3): klik w pustą grządkę otwiera paczki nasion wokół niej; runa siewu wybiera w nim swoje nasiona.</summary>
    public class SeedWheel : MonoBehaviour
    {
        const float Radius = 26f;

        RectTransform canvasRect;
        RectTransform wheel;
        GameObject blocker;
        TooltipView tooltip;
        FarmPlot plot;
        CropDefinition plotCrop;   // co rosło przy otwarciu (podmiana); zmiana z zewnątrz zamyka koło
        static int forcedNight = -1;   // kategoria narzucona na czas jednego otwarcia (podmiana, siewca dzień/noc)
        Automation.ISeedChooser picker;
        readonly List<(Button b, System.Func<bool> affordable)> buttons = new List<(Button, System.Func<bool>)>();

        public bool IsOpen => blocker != null && blocker.activeSelf;

        // kategoria koła: nasiona dnia albo nocy (przycisk w środku przełącza, wybór zapamiętany)
        static int nightPref = -1;
        static bool ShowNight
        {
            get
            {
                if (nightPref < 0)
                {
                    var game = GameLoop.Instance;
                    nightPref = PlayerPrefs.GetInt("wheelNight", game != null && game.Clock.IsNight ? 1 : 0);
                }
                return forcedNight >= 0 ? forcedNight == 1 : nightPref == 1;
            }
            set
            {
                nightPref = value ? 1 : 0;
                if (forcedNight >= 0)
                    forcedNight = nightPref;   // przełącznik w środku koła działa też w trybie narzuconym
                PlayerPrefs.SetInt("wheelNight", nightPref);
            }
        }

        static bool InCategory(CropDefinition c) => (c.kind == CropKind.Night) == ShowNight;
        System.Action reopen;

        public static SeedWheel Create(RectTransform canvasRoot, TooltipView tooltip)
        {
            var view = canvasRoot.gameObject.AddComponent<SeedWheel>();
            view.canvasRect = canvasRoot;
            view.tooltip = tooltip;
            // przezroczysta zasłona: klik obok koła zamyka je
            var b = Ui.Img(canvasRoot, "SeedWheelBlocker", null, false, true);
            b.color = new Color(0, 0, 0, 0);
            Ui.Stretch(b.rectTransform);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(view.Close);
            view.blocker = b.gameObject;
            view.wheel = Ui.Place(Ui.Rect("SeedWheel", b.transform), Ui.BottomLeft, Ui.Center, Vector2.zero, new Vector2(60, 60));
            var ring = Ui.Img(view.wheel, "Ring", Ui.Theme.wheel, false);
            Ui.Stretch(ring.rectTransform);
            view.blocker.SetActive(false);
            return view;
        }

        /// <summary>
        /// Koło do sadzenia na pustej grządce: odblokowane nasiona (Sklep → Nasiona) i to, co jest w worku.
        /// Na grządce z rosnącą rośliną: podmiana (stara wraca do worka), domyślnie nasiona pasujące do pory.
        /// </summary>
        public void Open(FarmPlot target)
        {
            var game = GameLoop.Instance;
            if (game == null || game.Crops == null || target == null)
                return;
            if (!target.State.IsEmpty && forcedNight < 0)
                forcedNight = game.Clock.IsNight ? 1 : 0;
            // pierwsze miejsce: losowe nasiono pasujące do pory dnia, niezależnie od kategorii koła
            var slots = new List<Slot>
            {
                new Slot
                {
                    icon = "dice", tip = DescribeRandom, pick = PlantRandom,
                    affordable = () => game.RandomSeed(game.Clock.IsNight, target.State.crop) != null,
                },
            };
            foreach (var cd in game.Crops.crops)
            {
                if (cd == null || !InCategory(cd) || !(game.IsUnlocked(cd) || game.Seeds.Count(cd.id) > 0))
                    continue;
                var c = cd;
                slots.Add(new Slot
                {
                    icon = c.SeedIcon, count = game.Seeds.Count(c.id), crop = c,
                    tip = () => Describe(c), pick = () => Plant(c),
                    affordable = () => game.Seeds.Count(c.id) > 0 || (!c.hybrid && game.Wallet.Coins >= Economy.Market.SeedPrice(c)),
                });
            }
            plot = target;
            plotCrop = target.State.crop;
            picker = null;
            reopen = () => Open(target);
            Build(target.transform.position, slots);
        }

        /// <summary>Wybór nasion dla ognika-siewcy (gniazdo): pamięć ziemi, odblokowane rośliny, hybrydy z worka, nic.</summary>
        public void OpenPicker(Automation.ISeedChooser rune, bool? night = null)
        {
            if (night.HasValue)
                forcedNight = night.Value ? 1 : 0;
            var game = GameLoop.Instance;
            if (game == null || game.Crops == null || rune == null)
                return;
            var slots = new List<Slot>
            {
                new Slot { icon = "ritual", selected = rune.Seed == Automation.WispNest.Memory, tip = () => Loc.Get("ui.rune.memory_tip"),
                           pick = () => rune.Seed = Automation.WispNest.Memory },
                new Slot { icon = "dice", selected = rune.Seed == Automation.WispNest.Random, tip = () => Loc.Get("ui.rune.random_tip"),
                           pick = () => rune.Seed = Automation.WispNest.Random },
            };
            foreach (var cd in game.Crops.crops)
            {
                if (cd == null || !InCategory(cd) || !(game.IsUnlocked(cd) || game.Seeds.Count(cd.id) > 0))
                    continue;
                var c = cd;
                slots.Add(new Slot { icon = c.SeedIcon, count = game.Seeds.Count(c.id), selected = rune.Seed == c.id,
                                     tip = () => Describe(c), pick = () => rune.Seed = c.id });
            }
            slots.Add(new Slot { icon = "lock", selected = rune.Seed == Automation.WispNest.Off, tip = () => Loc.Get("ui.rune.off_tip"),
                                 pick = () => rune.Seed = Automation.WispNest.Off });
            plot = null;
            picker = rune;
            reopen = () => OpenPicker(rune);
            Build(rune.PickerAnchor, slots);
        }

        class Slot
        {
            public string icon;
            public int count;
            public bool selected;
            public CropDefinition crop;
            public System.Func<string> tip;
            public System.Action pick;
            public System.Func<bool> affordable;
        }

        /// <summary>Przyciski po okręgu wokół punktu na świecie; promień rośnie z liczbą nasion.</summary>
        void Build(Vector3 worldPos, List<Slot> slots)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            foreach (Transform c in wheel)
                if (c.name != "Ring")
                    Destroy(c.gameObject);
            buttons.Clear();

            float radius = Mathf.Max(Radius, Mathf.Ceil(slots.Count * 20f / (2f * Mathf.PI)));
            float size = radius * 2f + 22f;
            wheel.sizeDelta = new Vector2(size, size);
            float margin = radius + 12f;

            // pozycja obiektu na ekranie -> współrzędne płótna (od lewego dolnego rogu)
            Vector2 screen = cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            var area = canvasRect.rect.size;
            var p = local + area * 0.5f;
            p.x = Mathf.Clamp(p.x, margin, Mathf.Max(margin, area.x - margin));
            p.y = Mathf.Clamp(p.y, margin, Mathf.Max(margin, area.y - margin));
            wheel.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                float a = Mathf.PI / 2f - i * 2f * Mathf.PI / slots.Count;
                var pos = new Vector2(Mathf.Round(Mathf.Cos(a) * radius), Mathf.Round(Mathf.Sin(a) * radius));
                var img = Ui.Img(wheel, "Seed_" + slot.icon, slot.selected ? Ui.Theme.slotSelected : Ui.Theme.slot, true, true);
                Ui.Place(img.rectTransform, Ui.Center, Ui.Center, pos, new Vector2(18, 18));
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { highlightedSprite = Ui.Theme.slotSelected, pressedSprite = Ui.Theme.slotSelected,
                                                  selectedSprite = Ui.Theme.slot, disabledSprite = Ui.Theme.slotLocked };
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                var icon = Ui.Img(img.transform, "Icon", Ui.Theme.Icon(slot.icon), false);
                Ui.Place(icon.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
                if (slot.count > 0)
                {
                    var badge = Ui.Txt(img.transform, "Count", Ui.Theme.textGold, TextAlignmentOptions.BottomRight);
                    badge.text = slot.count.ToString();
                    Ui.Place(badge.rectTransform, Ui.BottomRight, Ui.BottomRight, new Vector2(1, -1), new Vector2(14, 10));
                }
                b.onClick.AddListener(() => { slot.pick(); Close(); });
                var hover = img.gameObject.AddComponent<HoverTip>();
                hover.Init(tooltip, slot.tip);
                buttons.Add((b, slot.affordable));
            }
            // środek: przełącznik dzień / noc (zamyka klik obok koła, PPM albo Esc)
            var again = reopen;
            var toggle = Ui.IconBtn(wheel, "Category", ShowNight ? "sun" : "moon", new Vector2(16, 16), () =>
            {
                ShowNight = !ShowNight;
                again?.Invoke();
            });
            Ui.Place((RectTransform)toggle.transform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(16, 16));
            toggle.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Loc.Get(ShowNight ? "ui.wheel.show_day" : "ui.wheel.show_night"));

            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
            RefreshAffordable();
        }

        public void Close()
        {
            if (blocker != null)
                blocker.SetActive(false);
            tooltip.Hide(this);
            plot = null;
            plotCrop = null;
            picker = null;
            forcedNight = -1;
        }

        void Update()
        {
            if (!IsOpen)
                return;
            if (UiInput.RightPressed || UiInput.EscapePressed || (picker == null && (plot == null || plot.State.crop != plotCrop || plot.State.IsRipe)))
                Close();
            else
                RefreshAffordable();
        }

        void RefreshAffordable()
        {
            foreach (var (b, affordable) in buttons)
                b.interactable = affordable == null || affordable();
        }

        void Plant(CropDefinition crop)
        {
            var target = plot;
            var game = GameLoop.Instance;
            // najpierw worek nasion (za darmo), potem zakup za monety (hybryd nie da się kupić)
            if (game == null || target == null || target.State.IsRipe || target.State.crop == crop)
                return;
            if (game.Seeds.Count(crop.id) > 0 || (!crop.hybrid && game.Wallet.Coins >= Economy.Market.SeedPrice(crop)))
                target.DigUp();   // podmiana: najpierw wykop (nasiono wraca do worka), dopiero potem płać za nowe
            if (target.State.IsEmpty
                && (game.Seeds.TryTake(crop.id) || (!crop.hybrid && game.Wallet.TrySpend(Economy.Market.SeedPrice(crop)))))
            {
                target.Replant(crop);
                if (game.TakeFertilizer())
                    target.State.fertilized = true;   // nawóz idzie od razu do ziemi (+1 jakości)
            }
        }

        /// <summary>Losowe nasiono na teraz: w nocy roślina nocy, w dzień roślina dnia (inna niż ta, która rośnie).</summary>
        void PlantRandom()
        {
            var game = GameLoop.Instance;
            if (game == null || plot == null)
                return;
            var crop = game.RandomSeed(game.Clock.IsNight, plot.State.crop);
            if (crop != null)
                Plant(crop);
        }

        static string DescribeRandom()
        {
            var game = GameLoop.Instance;
            bool night = game != null && game.Clock.IsNight;
            return Loc.Get("ui.wheel.random") + "\n" + Loc.Get(night ? "ui.wheel.random_night" : "ui.wheel.random_day");
        }

        static string Describe(CropDefinition c)
        {
            string time = Ui.GrowTime(c);
            var game = GameLoop.Instance;
            int inBag = game != null ? game.Seeds.Count(c.id) : 0;
            string price = c.hybrid ? Loc.Get("ui.seed.hybrid") : Loc.Get("ui.seed.price", Economy.Market.SeedPrice(c));
            string s = Loc.Get("crop." + c.id) + "\n"
                 + Loc.Get(c.kind == CropKind.Day ? "ui.seed.kind_day" : "ui.seed.kind_night") + "\n"
                 + price + "   " + Loc.Get("ui.seed.sell", c.sellPrice) + "\n"
                 + time;
            if (c.requiresFullMoon)
                s += "\n" + Loc.Get("ui.seed.fullmoon");
            if (Economy.Market.DemandCropId() == c.id)
                s += "\n" + Loc.Get("ui.seed.demand", Upgrades.UpgradeEffects.DemandMult);   // Popyt dnia
            if (c.hybrid && !string.IsNullOrEmpty(c.parentA))
                s += "\n" + Loc.Get("ui.seed.parents", Loc.Get("crop." + c.parentA), Loc.Get("crop." + c.parentB));
            if (inBag > 0)
                s += "\n" + Loc.Get("ui.seed.bag", inBag);
            if (game != null && game.Fertilizer > 0)
                s += "\n" + Loc.Get("ui.seed.fertilizer", game.Fertilizer);
            return s;
        }
    }

    /// <summary>Pokazuje dymek, gdy kursor jest nad elementem UI. Treść odświeża się co 0,25 s (np. poziom i koszt po zakupie).</summary>
    public class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        TooltipView tooltip;
        System.Func<string> content;
        bool hovered;
        float refresh;

        public void Init(TooltipView view, System.Func<string> text)
        {
            tooltip = view;
            content = text;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            refresh = 0.25f;
            if (tooltip != null && content != null)
                tooltip.Show(this, content());
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
            if (tooltip != null)
                tooltip.Hide(this);
        }

        void Update()
        {
            if (!hovered || tooltip == null || content == null)
                return;
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f)
                return;
            refresh = 0.25f;
            tooltip.Show(this, content());   // bez zmiany tekstu dymek nic nie przelicza
        }

        void OnDisable()
        {
            hovered = false;
            if (tooltip != null)
                tooltip.Hide(this);
        }
    }
}
