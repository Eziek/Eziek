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
    /// HUD (GDD §12.2): jeden zwarty pasek tuż nad wyspą. Górny wiersz: waluty i Esencja, dolny: rytm pracy,
    /// tempo i zegar. Pasek idzie za lądem (nie przykleja się do rogów okna, więc w przezroczystym oknie nie wisi nad pulpitem).
    /// Znika po kilku sekundach bez ruchu myszy, wraca po jej poruszeniu. W mini-trybie go nie ma.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        const float HideAfter = 5f;
        const float Pad = 4f;
        const float RowH = 13f;
        const float Gap = 3f;   // piksele UI między paskiem a górą lądu

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
        Image keysIcon, mouseIcon;
        Image dayIcon;   // trzeci wiersz: słońce / księżyc, godzina gry, ile do nocy albo świtu   // wskaźnik: gra widzi pisanie / ruch myszy (zapala się na chwilę)
        RectTransform rhythmFill;
        Vector2 lastMouse;
        float lastMove;
        float refresh;
        RectTransform root;
        readonly Dictionary<CurrencyType, int> pending = new Dictionary<CurrencyType, int>();
        readonly Dictionary<Item, float> bumpUntil = new Dictionary<Item, float>();
        int flying;

        static readonly Color InputDim = new Color(0.45f, 0.45f, 0.5f, 0.55f);

        public bool ForceVisible { get; set; }
        /// <summary>Widoczność paska (0..1): karteczka wprowadzenia pokazuje się i chowa razem z nim.</summary>
        public float Alpha => group != null ? group.alpha : 0f;

        /// <summary>Prostokąt paska w układzie płótna (np. żeby toasty i karteczki go omijały).</summary>
        public RectTransform Bar => bar;

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
            var barImg = Ui.Img(root, "IslandBar", t.tooltip, true);
            bar = Ui.Place(barImg.rectTransform, Ui.BottomLeft, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120, 3 * RowH + Pad + 2));
            // --- górny wiersz: waluty ---
            items.Add(MakeItem("coin", CurrencyType.Coins));
            essenceItem = MakeItem("essence", null);
            essenceFill = Ui.Bar(essenceItem.root.transform, "EssenceBar", t.barFillEssence, Ui.TopLeft, Ui.TopLeft, new Vector2(14, -2), new Vector2(30, 8));
            items.Add(essenceItem);
            items.Add(MakeItem("nightcrop", CurrencyType.NightHarvest));
            items.Add(MakeItem("starshard", CurrencyType.StarShard));
            items.Add(MakeItem("wood", CurrencyType.Wood));
            items.Add(MakeItem("stone", CurrencyType.Stone));

            // --- dolny wiersz: rytm pracy, tempo, zegar ---
            rhythmIcon = Ui.Img(bar, "RhythmIcon", t.Icon("focus"), false);
            rhythmText = Ui.Txt(bar, "Rhythm", t.textLight);
            rhythmFill = Ui.Bar(bar, "RhythmBar", t.barFillRhythm, Ui.TopLeft, Ui.TopLeft, Vector2.zero, new Vector2(30, 7));
            tempoText = Ui.Txt(bar, "Tempo", t.textGold);
            keysIcon = Ui.Img(bar, "Keys", t.Icon("keys"), false);
            mouseIcon = Ui.Img(bar, "Mouse", t.Icon("mouse"), false);
            dayIcon = Ui.Img(bar, "DayNight", t.Icon("sun"), false);
            clockText = Ui.Txt(bar, "Clock", t.textLight);
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
            var view = Wyspa.World.ViewController.Instance;
            bool mini = view != null && view.Mini;
            bool visible = !mini && !Save.SaveSystem.InMenu && (ForceVisible || flying > 0 || Time.unscaledTime - lastMove < HideAfter);
            float target = visible ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 4f);
            group.blocksRaycasts = group.alpha > 0.5f;

            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0f)
            {
                refresh = 0.15f;   // wskaźnik pisania / myszy ma mrugać na bieżąco
                Refresh();
            }
            Follow();
        }

        /// <summary>Pasek nad środkiem lądu, w granicach płótna (przy przewiniętej dużej wyspie zostaje u góry okna).</summary>
        float shownWidth, lastGrow;   // szerokość paska z opóźnionym zwężaniem

        void Follow()
        {
            var cam = Camera.main;
            var island = Wyspa.World.Island.Instance;
            if (cam == null || island == null || root == null)
                return;
            var land = island.LandRect;
            Vector2 screen = cam.WorldToScreenPoint(new Vector3(land.center.x, land.yMax, 0f));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local))
                return;
            var r = root.rect;
            var size = bar.sizeDelta;
            // pozycja od lewego dolnego rogu płótna, pivot paska: środek dolnej krawędzi
            float x = local.x - r.xMin;
            float y = local.y - r.yMin + Gap;
            x = Mathf.Clamp(x, size.x / 2f + 2f, Mathf.Max(size.x / 2f + 2f, r.width - size.x / 2f - 2f));
            y = Mathf.Clamp(y, 2f, Mathf.Max(2f, r.height - size.y - 2f));
            bar.anchoredPosition = new Vector2(Mathf.Round(x - size.x / 2f) + size.x / 2f, Mathf.Round(y));
        }

        void Refresh()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var w = game.Wallet;
            float x = Pad;
            foreach (var it in items)
            {
                bool show;
                if (it == essenceItem)
                {
                    show = true;
                    it.text.text = $"{game.Essence.Amount:0}/{game.Essence.Capacity:0}";
                    Ui.SetBar(essenceFill, game.Essence.Normalized);
                    it.text.rectTransform.anchoredPosition = new Vector2(46, -2);
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
                float textX = it == essenceItem ? 46 : 14;
                float width = textX + Ui.Width(it.text);
                bool bump = bumpUntil.TryGetValue(it, out float until) && Time.unscaledTime < until;
                Ui.Place(it.rt, Ui.TopLeft, Ui.TopLeft, new Vector2(x, bump ? -2 : -3), new Vector2(width, 12));
                x += width + 6;
            }
            float topWidth = x - 6 + Pad;

            var r = game.Rhythm;
            string icon = r.FatigueLevel > 0 ? "fatigue" : (r.Refreshed ? "break" : "focus");
            rhythmIcon.sprite = Ui.Theme.Icon(icon);
            if (!game.Activity.IsActive)
                rhythmText.text = Loc.Get("ui.rhythm.idle");
            else if (r.FatigueLevel > 0)
                rhythmText.text = Loc.Get("rhythm.fatigue.status", r.FatigueLevel == 2 ? r.HeavyFatigue : r.Fatigue);
            else if (r.Refreshed)
                rhythmText.text = Loc.Get("rhythm.refreshed.status", 15, Mathf.CeilToInt(r.RefreshedMinutesLeft));
            else if (r.StreakBonusActive)
                rhythmText.text = Loc.Get("rhythm.streak.bonus", Mathf.RoundToInt(r.StreakBonus * 100f));
            else
                rhythmText.text = Loc.Get("ui.rhythm.normal", r.StreakMinutes);
            Ui.SetBar(rhythmFill, r.StreakMinutes / 25f);
            // czas gry: godzina na zegarze gry i odliczanie w prawdziwym czasie do nocy / świtu
            var clock = game.Clock;
            int left = Mathf.CeilToInt(clock.IsNight ? GameClock.CycleSeconds - clock.Second : GameClock.DaySeconds - clock.Second);
            string countdown = $"{left / 60}:{left % 60:00}";
            clockText.text = Loc.Get(clock.IsNight ? "ui.hud.to_day" : "ui.hud.to_night", clock.Now.ToString("HH:mm"), countdown);
            dayIcon.sprite = Ui.Theme.Icon(clock.IsNight ? "moon" : "sun");
            dayIcon.enabled = dayIcon.sprite != null;
            float tempo = game.Activity.TempoMultiplier;
            tempoText.text = tempo > 1.05f ? Loc.Get("ui.rhythm.tempo", tempo) : "";

            // dolny wiersz: ikona, opis, pasek serii, tempo, zegar
            float y2 = -3f - RowH;
            float bx = Pad;
            Ui.Place(rhythmIcon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(bx, y2), new Vector2(12, 12));
            bx += 14;
            float tw = Ui.Width(rhythmText);
            Ui.Place(rhythmText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(bx, y2 - 2), new Vector2(tw, 10));
            bx += tw + 4;
            var frame = (RectTransform)rhythmFill.parent;
            Ui.Place(frame, Ui.TopLeft, Ui.TopLeft, new Vector2(bx, y2 - 3), frame.sizeDelta);
            bx += frame.sizeDelta.x + 4;
            // tempo: w dolnym wierszu, a gdy się nie mieści w oknie - w wierszu zegara
            float tempoW = tempoText.text.Length > 0 ? Ui.Width(tempoText) : 0f;
            bool tempoBelow = tempoW > 0f && bx + tempoW + 4f + 26f + Pad > root.rect.width - 4f;
            if (tempoW > 0f && !tempoBelow)
            {
                Ui.Place(tempoText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(bx, y2 - 2), new Vector2(tempoW, 10));
                bx += tempoW + 4;
            }
            // wskaźnik pracy: klawiatura i mysz jasne przez chwilę po wykrytym inpucie, inaczej przygaszone
            var act = game.Activity;
            float now = Time.unscaledTime;
            keysIcon.color = now - act.LastKeyboardTime < 0.75f ? Color.white : InputDim;
            mouseIcon.color = now - act.LastMouseTime < 0.75f ? Color.white : InputDim;
            Ui.Place(keysIcon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(bx, y2), new Vector2(12, 12));
            bx += 12;
            Ui.Place(mouseIcon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(bx, y2), new Vector2(12, 12));
            bx += 14;
            float bottomWidth = bx - 2 + Pad;
            // trzeci wiersz: ikona pory i „14:05 noc za 23:40”
            float y3 = y2 - RowH;
            float cw = Ui.Width(clockText);
            Ui.Place(dayIcon.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad, y3), new Vector2(12, 12));
            Ui.Place(clockText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad + 14, y3 - 2), new Vector2(cw, 10));
            float timeWidth = Pad + 14 + cw + Pad;
            if (tempoBelow)
            {
                Ui.Place(tempoText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad + 14 + cw + 6, y3 - 2), new Vector2(tempoW, 10));
                timeWidth += tempoW + 6;
            }
            float width2 = Mathf.Ceil(Mathf.Max(topWidth, Mathf.Max(bottomWidth, timeWidth)));
            // szerokość od razu rośnie, a maleje dopiero po 3 s (pasek nie skacze na boki przy każdej zmianie tekstu)
            float now2 = Time.unscaledTime;
            if (width2 >= shownWidth || now2 - lastGrow > 3f)
            {
                if (width2 >= shownWidth) lastGrow = now2;
                shownWidth = width2;
            }
            bar.sizeDelta = new Vector2(shownWidth, 3 * RowH + Pad + 2);
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
            // płynnie, co klatkę (pozycja zaokrąglona do pikseli UI)
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (t >= 0f)
                {
                    img.enabled = img.sprite != null;
                    Vector2 end = (Vector2)root.InverseTransformPoint(target.rt.TransformPoint(new Vector3(6, -6, 0)));
                    var ctrl = new Vector2(Mathf.Lerp(start.x, end.x, 0.3f) - 20f, Mathf.Max(start.y, end.y) + 24f);
                    float u = Mathf.Clamp01(t / duration);
                    var pos = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * ctrl + u * u * end;
                    rt.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
                }
                yield return null;
            }
            Destroy(img.gameObject);
            pending.TryGetValue(type, out int p);
            pending[type] = Mathf.Max(0, p - share);
            bumpUntil[target] = Time.unscaledTime + 0.12f;   // licznik podskakuje o piksel
            GameEvents.PlaySound("coin", 0.6f);
            flying--;
            refresh = 0f;
            yield return new WaitForSecondsRealtime(0.13f);
            refresh = 0f;
        }
    }
}
