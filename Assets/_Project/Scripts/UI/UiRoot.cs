using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Korzeń UI (GDD §12): buduje płótno, EventSystem, HUD, koło nasion, Księgę, toasty i dymki.
    /// Skala UI = skala świata - 1 (np. świat ×3, UI ×2), jak w GDD §16.3.
    /// </summary>
    public class UiRoot : MonoBehaviour
    {
        public static UiRoot Instance { get; private set; }

        [SerializeField] UiTheme theme;

        CanvasScaler scaler;
        RectTransform canvasRect;
        object plotHoverOwner = new object();

        public HudView Hud { get; private set; }
        public SeedWheel Wheel { get; private set; }
        public BookView Book { get; private set; }
        public ToastView Toasts { get; private set; }
        public ExpansionMode Expansion { get; private set; }
        public ContextPanel Context { get; private set; }
        public PlacementMode Placement { get; private set; }
        public TooltipView Tooltip { get; private set; }

        public void Setup(UiTheme uiTheme) => theme = uiTheme;

        void Awake()
        {
            if (theme == null)
            {
                Debug.LogError("[Wyspa] UiRoot: brak motywu UI (Data/UiTheme). Uruchom Wyspa > 3.");
                enabled = false;
                return;
            }
            if (TMP_Settings.instance == null)
                Debug.LogError("[Wyspa] Brak zasobów TextMeshPro: Window > TextMeshPro > Import TMP Essential Resources, potem Play ponownie.");

            Ui.Theme = theme;
            Loc.Init(theme.tables);
            Ui.Font = CreateFont(theme.font);
            Build();
            Instance = this;   // dopiero po zbudowaniu - grządki wołają Instance.Wheel
        }

        // W Start, bo GameLoop.Instance ustawia się w Awake innego obiektu (kolejność Awake nie jest pewna).
        void Start() => ApplySavedOptions();

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        static TMP_FontAsset CreateFont(Font font)
        {
            if (font == null)
                return null;
            // RASTER = bez wygładzania, filtr Point = ostre piksele (GDD §12.9)
            var fa = TMP_FontAsset.CreateFontAsset(font, 8, 1, GlyphRenderMode.RASTER, 256, 256, AtlasPopulationMode.Dynamic, false);
            if (fa != null && fa.atlasTexture != null)
                fa.atlasTexture.filterMode = FilterMode.Point;
            return fa;
        }

        void Build()
        {
            var canvasGo = new GameObject("UI Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 10;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 16;
            scaler.scaleFactor = UiScale();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)canvasGo.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            Tooltip = TooltipView.Create(canvasRect);
            Hud = HudView.Create(canvasRect, () => OpenBook(BookView.Tab.Upgrades), () => OpenBook(BookView.Tab.Options),
                () => { Wheel.Close(); Context.Close(); Placement.Cancel(); Expansion.Toggle(); });
            Expansion = ExpansionMode.Create(canvasRect, Tooltip);
            Hud.ExpandButton.gameObject.AddComponent<HoverTip>().Init(Tooltip, () => Loc.Get("menu.expand"));
            Toasts = ToastView.Create(canvasRect);
            Wheel = SeedWheel.Create(canvasRect, Tooltip);
            Book = BookView.Create(canvasRect, Tooltip);
            Context = ContextPanel.Create(canvasRect, StartMove);
            Placement = PlacementMode.Create(transform, Tooltip);
            Tooltip.transform.SetAsLastSibling();
        }

        void ApplySavedOptions()
        {
            var lighting = FindAnyObjectByType<DayNightLighting>();
            if (lighting != null && PlayerPrefs.HasKey("minBrightness"))
                lighting.MinBrightness = PlayerPrefs.GetFloat("minBrightness");
            var game = GameLoop.Instance;
            if (game != null && game.Rhythm != null && PlayerPrefs.HasKey("rhythm"))
                game.Rhythm.RhythmEnabled = PlayerPrefs.GetInt("rhythm") == 1;
        }

        /// <summary>Skala UI: o 1 mniejsza od skali świata (piksele ekranu na piksel gry), co najmniej 1.</summary>
        public static int UiScale()
        {
            var view = ViewController.Instance;
            int world = view != null
                ? view.PixelRatio
                : Mathf.Max(1, Mathf.Min(Screen.width / 272, Screen.height / 200));
            return Mathf.Max(1, world - 1);
        }

        /// <summary>Skala na czas Księgi: zwykła, chyba że okno jest za małe (np. okno Game w edytorze) - wtedy mniejsza.</summary>
        static int BookScale()
        {
            int fit = Mathf.Min(Mathf.FloorToInt(Screen.width / (BookView.Width + 16f)), Mathf.FloorToInt(Screen.height / (BookView.Height + 16f)));
            return Mathf.Max(1, Mathf.Min(UiScale(), fit));
        }

        void OpenBook(BookView.Tab tab)
        {
            CloseAll();
            Book.Open(tab);
        }

        void CloseAll()
        {
            Expansion.Close();
            Wheel.Close();
            Context.Close();
            Placement.Cancel();
        }

        /// <summary>Lampa kupiona w sklepie: Księga się zamyka, duch lampy idzie za kursorem.</summary>
        public void StartBuy(Wyspa.Lighting.LampDefinition def)
        {
            CloseAll();
            Book.Close();
            Placement.StartBuy(def);
        }

        /// <summary>Stawianie z innego miejsca (np. pomost ze sklepu).</summary>
        public void StartPlacement(PlacementMode.Request request)
        {
            CloseAll();
            Book.Close();
            Placement.Begin(request);
        }

        public void StartMove(TileObject obj)
        {
            CloseAll();
            Placement.StartMove(obj);
        }

        public bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        void Update()
        {
            scaler.scaleFactor = Book.IsOpen ? BookScale() : UiScale();
            Hud.ForceVisible = Book.IsOpen || Wheel.IsOpen || Expansion.IsActive || PlacementMode.Active;
            UpdatePlotTooltip();
        }

        /// <summary>Dymek nad grządką (GDD §12.3): co rośnie i dlaczego nie rośnie.</summary>
        void UpdatePlotTooltip()
        {
            var game = GameLoop.Instance;
            var cam = Camera.main;
            FarmPlot.Hovered = null;
            if (game == null || cam == null || PointerOverUi || Book.IsOpen || Wheel.IsOpen || Expansion.IsActive
                || Context.IsOpen || PlacementMode.Active)
            {
                Tooltip.Hide(plotHoverOwner);
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(UiInput.MousePosition);
            var hit = Wyspa.Input.ClickRouter.Pick(world);
            if (hit != null && hit.TryGetComponent(out Wyspa.Sea.Pier pier))
            {
                string tip = pier.HoverText(world);
                if (tip != null) Tooltip.Show(plotHoverOwner, tip);
                else Tooltip.Hide(plotHoverOwner);
                return;
            }
            if (hit != null && hit.TryGetComponent(out Decoration deco) && !deco.Removing)
            {
                Tooltip.Show(plotHoverOwner, Loc.Get(deco.NameKey) + "\n" + Loc.Get("ui.deco.hint"));
                return;
            }
            if (hit != null && hit.TryGetComponent(out TileObject obj))
            {
                Tooltip.Show(plotHoverOwner, Loc.Get(obj.NameKey) + "\n" + Loc.Get("ui.object.hint"));
                return;
            }
            if (hit == null || !hit.TryGetComponent(out FarmPlot plot))
            {
                Tooltip.Hide(plotHoverOwner);
                return;
            }
            FarmPlot.Hovered = plot;
            Tooltip.Show(plotHoverOwner, Describe(plot, game));
        }

        static string Describe(FarmPlot plot, GameLoop game)
        {
            var s = plot.State;
            if (s.IsEmpty)
                return Loc.Get("ui.plot.empty");
            string name = Loc.Get("crop." + s.crop.id);
            string quality = s.Quality > 1 ? "\n" + Loc.Get("ui.plot.quality", s.Quality) : "";
            if (s.IsRipe)
                return Loc.Get("ui.plot.ripe", name) + quality;
            bool night = game.Clock.IsNight;
            var ctx = game.ContextFor(plot, night);
            string state;
            if (s.CanGrow(ctx))
                state = Loc.Get("ui.plot.growing", name);
            else if (s.crop.kind == CropKind.Day)
                state = Loc.Get(!ctx.lit ? "ui.plot.need_light" : "ui.plot.need_work", name);
            else if (!night)
                state = Loc.Get("ui.plot.need_night", name);
            else if (ctx.lit)
                state = Loc.Get("ui.plot.too_bright", name);
            else
                state = Loc.Get("ui.plot.need_fullmoon", name);   // krwawa róża poza pełnią
            int pct = Mathf.FloorToInt(100f * s.growth / Mathf.Max(1f, s.crop.growSeconds));
            string moon = s.crop.kind == CropKind.Night && ctx.fullMoon ? "\n" + Loc.Get("ui.plot.fullmoon") : "";
            if (game.Island != null && game.Island.ZoneBonus(plot) > 1f)
                moon += "\n" + Loc.Get("ui.plot.zone_bonus");
            return state + "\n" + Loc.Get("ui.plot.progress", pct) + quality + moon;
        }
    }
}
