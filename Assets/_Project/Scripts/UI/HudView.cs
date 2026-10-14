using System.Collections;
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
    /// HUD (GDD §12.2): u góry waluty i Esencja, w lewym dolnym rogu rytm pracy i zegar. Menu jest w pierścieniu akcji (PPM, §12.10).
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
        TextMeshProUGUI rhythmText, clockText, tempoText;
        RectTransform rhythmFill;
        Vector2 lastMouse;
        float lastMove;
        float refresh;
        RectTransform root;
        readonly Dictionary<CurrencyType, int> pending = new Dictionary<CurrencyType, int>();
        readonly Dictionary<Item, float> bumpUntil = new Dictionary<Item, float>();
        int flying;

        public bool ForceVisible { get; set; }

        public static HudView Create(RectTransform canvasRoot)
        {
            var root = Ui.Stretch(Ui.Rect("HUD", canvasRoot));
            var view = root.gameObject.AddComponent<HudView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.Build(root);
            return view;
        }

        void OnEnable() => GameEvents.CurrencyGained += FlyCurrency;
        void OnDisable()
        {
            GameEvents.CurrencyGained -= FlyCurrency;
            pending.Clear();   // przerwane loty: liczniki pokazują od razu prawdziwy stan
            flying = 0;
        }

        void Build(RectTransform root)
        {
            this.root = root;
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

            // --- rytm pracy ---
            var rp = Ui.Img(root, "Rhythm", t.tooltip, true);
            Ui.Place(rp.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(4, 4), new Vector2(172, 22));
            rhythmIcon = Ui.Img(rp.transform, "Icon", t.Icon("focus"), false);
            Ui.Place(rhythmIcon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(3, -3), new Vector2(12, 12));
            rhythmText = Ui.Txt(rp.transform, "Text", t.textLight);
            Ui.Place(rhythmText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(18, -3), new Vector2(100, 10));
            rhythmFill = Ui.Bar(rp.transform, "RhythmBar", t.barFillRhythm, Ui.TopLeft, Ui.TopLeft, new Vector2(18, -12), new Vector2(90, 7));
            clockText = Ui.Txt(rp.transform, "Clock", t.textMuted, TextAlignmentOptions.TopRight);
            Ui.Place(clockText.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-4, -3), new Vector2(30, 10));
            // tempo pracy (pisanie, mysz): mnożnik wzrostu roślin dnia pod zegarem
            tempoText = Ui.Txt(rp.transform, "Tempo", t.textGold, TextAlignmentOptions.TopRight);
            Ui.Place(tempoText.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-4, -12), new Vector2(50, 10));
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
            bool visible = ForceVisible || flying > 0 || Time.unscaledTime - lastMove < HideAfter;
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
                    int real = w.Get(it.currency.Value);
                    pending.TryGetValue(it.currency.Value, out int p);
                    int v = Mathf.Max(0, real - p);   // monety w locie doliczają się po dolocie
                    show = it.currency == CurrencyType.Coins || real > 0;   // waluty pokazujemy dopiero po zdobyciu
                    it.text.text = v.ToString("N0", Wyspa.Localization.LocCore.Culture(Loc.Language));
                }
                it.root.SetActive(show);
                if (!show)
                    continue;
                float textX = it == essenceItem ? 50 : 14;
                float width = textX + Ui.Width(it.text);
                bool bump = bumpUntil.TryGetValue(it, out float until) && Time.unscaledTime < until;
                Ui.Place(it.rt, Ui.TopLeft, Ui.TopLeft, new Vector2(x, bump ? -2 : -3), new Vector2(width, 12));
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
            float tempo = game.Activity.TempoMultiplier;
            tempoText.text = tempo > 1.05f ? Loc.Get("ui.rhythm.tempo", tempo) : "";
        }
            // ------------------------------------------------------------ monety lecą łukiem do licznika (GDD §5.5)

        Item ItemFor(CurrencyType type)
        {
            foreach (var it in items)
                if (it.currency == type)
                    return it;
            return null;
        }

        void FlyCurrency(Vector3 world, CurrencyType type, int amount)
        {
            var target = ItemFor(type);
            var cam = Camera.main;
            if (target == null || cam == null || root == null || !isActiveAndEnabled)
                return;
            Vector2 screen = cam.WorldToScreenPoint(world);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var start))
                return;
            int n = Mathf.Clamp(Mathf.CeilToInt(amount / 10f), 1, 5);
            pending.TryGetValue(type, out int p);
            pending[type] = p + amount;
            string icon = type == CurrencyType.NightHarvest ? "nightcrop" : type == CurrencyType.StarShard ? "starshard"
                        : type == CurrencyType.Wood ? "wood" : type == CurrencyType.Stone ? "stone" : "coin";
            for (int i = 0; i < n; i++)
            {
                int share = i < n - 1 ? amount / n : amount - (amount / n) * (n - 1);
                StartCoroutine(Flight(start, target, type, share, icon, i * 0.08f));
            }
            refresh = 0f;
        }

        IEnumerator Flight(Vector2 start, Item target, CurrencyType type, int share, string icon, float delay)
        {
            flying++;
            var img = Ui.Img(root, "FlyingCoin", Ui.Theme.Icon(icon), false);
            var rt = Ui.Place(img.rectTransform, Ui.Center, Ui.Center, start, new Vector2(12, 12));
            img.enabled = false;
            float t = -delay;
            const float duration = 0.7f;
            const float step = 1f / 12f;   // skokowo, jak reszta animacji
            var wait = new WaitForSecondsRealtime(step);
            while (t < duration)
            {
                t += step;
                if (t >= 0f)
                {
                    img.enabled = img.sprite != null;
                    Vector2 end = (Vector2)root.InverseTransformPoint(target.rt.TransformPoint(new Vector3(6, -6, 0)));
                    var ctrl = new Vector2(Mathf.Lerp(start.x, end.x, 0.3f) - 20f, Mathf.Max(start.y, end.y) + 24f);
                    float u = Mathf.Clamp01(t / duration);
                    var pos = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * ctrl + u * u * end;
                    rt.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
                }
                yield return wait;
            }
            Destroy(img.gameObject);
            pending.TryGetValue(type, out int p);
            pending[type] = Mathf.Max(0, p - share);
            bumpUntil[target] = Time.unscaledTime + 0.12f;   // licznik podskakuje o piksel
            flying--;
            refresh = 0f;
            yield return new WaitForSecondsRealtime(0.13f);
            refresh = 0f;
        }
    }
}
