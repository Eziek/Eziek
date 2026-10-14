using TMPro;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Wprowadzenie bez postaci (GDD §12.8): kroki z tablicy ogłoszeń. Karteczka z bieżącym zadaniem wisi pod wyspą
    /// (pokazuje się i chowa razem z paskiem HUD), a nad celem unosi się złota strzałka. Kroki: posadź, pracuj, zbierz,
    /// dokup kafelek, kup lampę (najpierw kafelek: na wyspie 2x2 nie ma wolnego miejsca na lampę); na końcu karteczka „gotowe”.
    /// Rozebranie tablicy kończy wprowadzenie w każdej chwili.
    /// </summary>
    public class IntroNotes : MonoBehaviour
    {
        public const int Done = 6;
        const int Steps = 5;   // zadania; szósta karteczka to „gotowe”
        const float Ppu = 16f;
        const float MaxWidth = 200f;
        static readonly string[] Keys = { "intro.plant", "intro.grow", "intro.harvest", "intro.expand", "intro.lamp", "intro.done" };

        public static IntroNotes Instance { get; private set; }

        /// <summary>Bieżący krok (0 = posadź, Done = koniec). Ustawia go zapis; nowa gra zaczyna od 0.</summary>
        public int Step { get; set; }
        public bool Active => Step < Done;
        /// <summary>Wszystkie zadania wykonane, została karteczka „gotowe”.</summary>
        public bool Finished => Step == Steps;
        public string CurrentKey => Active ? Keys[Step] : null;

        HudView hud;
        RectTransform root, note;
        TextMeshProUGUI text, counter;
        CanvasGroup group;
        SpriteRenderer arrow;
        int harvests;
        int shownStep = -1;
        float check;

        public static IntroNotes Create(RectTransform canvasRoot, HudView hudView)
        {
            var rt = Ui.Stretch(Ui.Rect("Intro", canvasRoot));
            var view = rt.gameObject.AddComponent<IntroNotes>();
            view.root = rt;
            view.hud = hudView;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            view.Build();
            Instance = view;
            return view;
        }

        void Build()
        {
            var t = Ui.Theme;
            var img = Ui.Img(root, "Note", t.panelPage != null ? t.panelPage : t.tooltip, true);
            note = Ui.Place(img.rectTransform, Ui.BottomLeft, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(80, 20));
            text = Ui.Txt(note, "Text", t.panelPage != null ? t.textDark : t.textLight);
            text.textWrappingMode = TextWrappingModes.Normal;
            counter = Ui.Txt(note, "Counter", t.textMutedOnPage, TextAlignmentOptions.TopRight);
        }

        void OnEnable()
        {
            GameEvents.Harvested += OnHarvest;
            Loc.Changed += Relayout;
        }

        void OnDisable()
        {
            GameEvents.Harvested -= OnHarvest;
            Loc.Changed -= Relayout;
        }

        void Relayout() => shownStep = -1;
        float shownWidth;
        float finishedAt = -1f;
        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (arrow != null)
                Destroy(arrow.gameObject);
        }

        void OnHarvest(string id, int count)
        {
            if (id != null && id.StartsWith("crop:"))
                harvests++;
        }

        void Update()
        {
            var island = Island.Instance;
            var game = GameLoop.Instance;
            bool ready = island != null && island.Ready && game != null;
            // tablica rozebrana (albo jej nie ma, np. stary zapis): koniec wprowadzenia
            if (ready && Active && island.Board == null)
                Step = Done;
            if (!ready || !Active)
            {
                group.alpha = 0f;
                ShowArrow(null, 0f);
                return;
            }
            check -= Time.unscaledDeltaTime;
            if (check <= 0f)
            {
                check = 0.3f;
                if (Completed(island))
                    Advance(island);
            }
            if (shownStep != Step || !Mathf.Approximately(shownWidth, root.rect.width))
                Layout();
            float alpha = hud != null ? hud.Alpha : 1f;
            group.alpha = alpha;
            Follow(island);
            ShowArrow(Target(island), alpha);
        }

        bool Completed(Island island)
        {
            switch (Step)
            {
                case 0:
                    if (harvests > 0) return true;
                    foreach (var p in FarmPlot.All)
                        if (!p.State.IsEmpty) return true;
                    return false;
                case 1:
                    if (harvests > 0) return true;
                    foreach (var p in FarmPlot.All)
                        if (p.State.IsRipe) return true;
                    return false;
                case 2:
                    return harvests > 0;
                case 3:
                    return island.Map.Purchased >= 1;
                case 4:
                    return island.CountLamps() >= 2;   // startowa latarnia + kupiona (także kupiona wcześniej)
                default:
                    // „gotowe” wisi chwilę, potem wprowadzenie się kończy, gdy na tablicy jest pierwsze zamówienie
                    if (finishedAt < 0f) finishedAt = Time.unscaledTime;
                    var orders = Orders.OrderBook.Instance;
                    return Time.unscaledTime - finishedAt > 20f && orders != null && orders.Orders.Count > 0;
            }
        }

        void Advance(Island island)
        {
            Step++;
            var board = island.Board;
            if (board != null && board.TryGetComponent(out NoticeBoard nb))
                nb.TearNote();
        }

        void Layout()
        {
            shownStep = Step;
            shownWidth = root.rect.width;
            text.text = Loc.Get(CurrentKey);
            counter.text = Finished ? "" : Loc.Get("ui.intro.counter", Step + 1, Steps);
            float cw = counter.text.Length > 0 ? Ui.Width(counter) : 0f;
            // pergamin ma grube brzegi (9-slice): tekst z zapasem 8 px, licznik w prawym górnym rogu obok tekstu
            float maxW = Mathf.Max(60f, Mathf.Min(MaxWidth, root.rect.width - 28f - (cw > 0 ? cw + 6 : 0)));
            float w = Mathf.Min(maxW, Ui.Width(text));
            float h = Mathf.Ceil(text.GetPreferredValues(text.text, w, 0f).y);
            Ui.Place(text.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(8, -7), new Vector2(w, h));
            Ui.Place(counter.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(-8, -7), new Vector2(cw, 10));
            note.sizeDelta = new Vector2(Mathf.Ceil(w + 16 + (cw > 0 ? cw + 6 : 0)), h + 14);
        }

        /// <summary>Karteczka pod środkiem lądu (pasek HUD jest nad wyspą), w granicach okna.</summary>
        void Follow(Island island)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            var land = island.LandRect;
            Vector2 screen = cam.WorldToScreenPoint(new Vector3(land.center.x, land.yMin, 0f));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local))
                return;
            var r = root.rect;
            var size = note.sizeDelta;
            float x = Mathf.Clamp(local.x - r.xMin, size.x / 2f + 2f, Mathf.Max(size.x / 2f + 2f, r.width - size.x / 2f - 2f));
            float y = Mathf.Clamp(local.y - r.yMin - 2f, size.y + 2f, Mathf.Max(size.y + 2f, r.height - 2f));
            note.anchoredPosition = new Vector2(Mathf.Round(x - size.x / 2f) + size.x / 2f, Mathf.Round(y));
        }

        /// <summary>Nad czym unosi się strzałka w tym kroku (null = bez strzałki).</summary>
        Vector3? Target(Island island)
        {
            switch (Step)
            {
                case 0:
                    foreach (var p in FarmPlot.All)
                        if (p.State.IsEmpty) return p.CropPosition;
                    return null;
                case 1:
                    foreach (var p in FarmPlot.All)
                        if (!p.State.IsEmpty) return p.CropPosition;
                    return null;
                case 2:
                    foreach (var p in FarmPlot.All)
                        if (p.State.IsRipe) return p.CropPosition;
                    return null;
                case Steps:
                    var b = island.Board;
                    return b != null ? b.transform.position : (Vector3?)null;
                default:
                    return null;
            }
        }

        void ShowArrow(Vector3? at, float alpha)
        {
            if (at == null || alpha <= 0.01f)
            {
                if (arrow != null)
                    arrow.enabled = false;
                return;
            }
            if (arrow == null)
            {
                var art = Island.Instance != null ? Island.Instance.Art : null;
                if (art == null || art.introArrow == null)
                    return;
                var go = new GameObject("IntroArrow");
                arrow = go.AddComponent<SpriteRenderer>();
                arrow.sprite = art.introArrow;
                arrow.sortingOrder = 4500;
            }
            arrow.enabled = true;
            // unosi się nad celem o 2 px w górę i w dół (pozycja przyciągnięta do pikseli)
            float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 4f) * 2f);
            var p = at.Value + new Vector3(0f, 1.15f + bob / Ppu, 0f);
            arrow.transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
            var c = arrow.color;
            c.a = alpha;
            arrow.color = c;
        }
    }
}
