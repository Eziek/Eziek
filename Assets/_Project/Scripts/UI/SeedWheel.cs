using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>Koło nasion (GDD §12.3): klik w pustą grządkę otwiera paczki nasion wokół niej.</summary>
    public class SeedWheel : MonoBehaviour
    {
        const float Radius = 26f;

        RectTransform canvasRect;
        RectTransform wheel;
        GameObject blocker;
        TooltipView tooltip;
        FarmPlot plot;
        readonly List<(Button b, CropDefinition crop)> buttons = new List<(Button, CropDefinition)>();

        public bool IsOpen => blocker != null && blocker.activeSelf;

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

        public void Open(FarmPlot target)
        {
            var game = GameLoop.Instance;
            var cam = Camera.main;
            if (game == null || game.Crops == null || cam == null)
                return;
            plot = target;
            foreach (Transform c in wheel)
                if (c.name != "Ring")
                    Destroy(c.gameObject);
            buttons.Clear();

            // pozycja grządki na ekranie -> współrzędne płótna (od lewego dolnego rogu)
            Vector2 screen = cam.WorldToScreenPoint(target.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            var size = canvasRect.rect.size;
            var p = local + size * 0.5f;
            p.x = Mathf.Clamp(p.x, 34, size.x - 34);
            p.y = Mathf.Clamp(p.y, 34, size.y - 34);
            wheel.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));

            // hybrydy tylko, gdy są w worku nasion (z mutacji)
            var crops = new List<CropDefinition>();
            foreach (var cd in game.Crops.crops)
                if (cd != null && (!cd.hybrid || game.Seeds.Count(cd.id) > 0))
                    crops.Add(cd);
            for (int i = 0; i < crops.Count; i++)
            {
                var crop = crops[i];
                float a = Mathf.PI / 2f - i * 2f * Mathf.PI / crops.Count;
                var pos = new Vector2(Mathf.Round(Mathf.Cos(a) * Radius), Mathf.Round(Mathf.Sin(a) * Radius));
                var img = Ui.Img(wheel, "Seed_" + crop.id, Ui.Theme.slot, true, true);
                Ui.Place(img.rectTransform, Ui.Center, Ui.Center, pos, new Vector2(18, 18));
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { highlightedSprite = Ui.Theme.slotSelected, pressedSprite = Ui.Theme.slotSelected,
                                                  selectedSprite = Ui.Theme.slot, disabledSprite = Ui.Theme.slotLocked };
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                var icon = Ui.Img(img.transform, "Icon", Ui.Theme.Icon(crop.SeedIcon), false);
                Ui.Place(icon.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
                int inBag = game.Seeds.Count(crop.id);
                if (inBag > 0)
                {
                    var badge = Ui.Txt(img.transform, "Count", Ui.Theme.textGold, TextAlignmentOptions.BottomRight);
                    badge.text = inBag.ToString();
                    Ui.Place(badge.rectTransform, Ui.BottomRight, Ui.BottomRight, new Vector2(1, -1), new Vector2(14, 10));
                }
                var c = crop;
                b.onClick.AddListener(() => Plant(c));
                var hover = img.gameObject.AddComponent<HoverTip>();
                hover.Init(tooltip, () => Describe(c));
                buttons.Add((b, crop));
            }
            var close = Ui.IconBtn(wheel, "Close", "close", new Vector2(16, 16), Close);
            Ui.Place((RectTransform)close.transform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(16, 16));

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
        }

        void Update()
        {
            if (!IsOpen)
                return;
            if (UiInput.RightPressed || UiInput.EscapePressed || plot == null || !plot.State.IsEmpty)
                Close();
            else
                RefreshAffordable();
        }

        void RefreshAffordable()
        {
            var game = GameLoop.Instance;
            foreach (var (b, crop) in buttons)
                b.interactable = game != null && (game.Seeds.Count(crop.id) > 0 || (!crop.hybrid && game.Wallet.Coins >= crop.seedPrice));
        }

        void Plant(CropDefinition crop)
        {
            var game = GameLoop.Instance;
            // najpierw worek nasion (za darmo), potem zakup za monety (hybryd nie da się kupić)
            if (game != null && plot != null && plot.State.IsEmpty
                && (game.Seeds.TryTake(crop.id) || (!crop.hybrid && game.Wallet.TrySpend(crop.seedPrice))))
            {
                plot.Replant(crop);
                if (game.TakeFertilizer())
                    plot.State.fertilized = true;   // nawóz idzie od razu do ziemi (+1 jakości)
            }
            Close();
        }

        static string Describe(CropDefinition c)
        {
            string time = c.kind == CropKind.Day
                ? Loc.Get("ui.seed.time_work", Mathf.RoundToInt(c.growSeconds / 60f))
                : Loc.Get("ui.seed.time_night", Mathf.Round(c.growSeconds / 360f) / 10f);
            var game = GameLoop.Instance;
            int inBag = game != null ? game.Seeds.Count(c.id) : 0;
            string price = c.hybrid ? Loc.Get("ui.seed.hybrid") : Loc.Get("ui.seed.price", c.seedPrice);
            string s = Loc.Get("crop." + c.id) + "\n"
                 + Loc.Get(c.kind == CropKind.Day ? "ui.seed.kind_day" : "ui.seed.kind_night") + "\n"
                 + price + "   " + Loc.Get("ui.seed.sell", c.sellPrice) + "\n"
                 + time;
            if (c.requiresFullMoon)
                s += "\n" + Loc.Get("ui.seed.fullmoon");
            if (inBag > 0)
                s += "\n" + Loc.Get("ui.seed.bag", inBag);
            if (game != null && game.Fertilizer > 0)
                s += "\n" + Loc.Get("ui.seed.fertilizer", game.Fertilizer);
            return s;
        }
    }

    /// <summary>Pokazuje dymek, gdy kursor jest nad elementem UI.</summary>
    public class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        TooltipView tooltip;
        System.Func<string> content;

        public void Init(TooltipView view, System.Func<string> text)
        {
            tooltip = view;
            content = text;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (tooltip != null && content != null)
                tooltip.Show(this, content());
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (tooltip != null)
                tooltip.Hide(this);
        }

        void OnDisable()
        {
            if (tooltip != null)
                tooltip.Hide(this);
        }
    }
}
