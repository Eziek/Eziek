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
            Hud = HudView.Create(canvasRect, () => Book.Open(BookView.Tab.Upgrades), () => Book.Open(BookView.Tab.Options));
            Toasts = ToastView.Create(canvasRect);
            Wheel = SeedWheel.Create(canvasRect, Tooltip);
            Book = BookView.Create(canvasRect, Tooltip);
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

        /// <summary>Skala UI: o 1 mniejsza od skali świata (świat 272x200 w całkowitej skali).</summary>
        public static int UiScale()
        {
            int world = Mathf.Max(1, Mathf.Min(Screen.width / 272, Screen.height / 200));
            return Mathf.Max(1, world - 1);
        }

        public bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        void Update()
        {
            scaler.scaleFactor = UiScale();
            Hud.ForceVisible = Book.IsOpen || Wheel.IsOpen;
            UpdatePlotTooltip();
        }

        /// <summary>Dymek nad grządką (GDD §12.3): co rośnie i dlaczego nie rośnie.</summary>
        void UpdatePlotTooltip()
        {
            var game = GameLoop.Instance;
            var cam = Camera.main;
            FarmPlot.Hovered = null;
            if (game == null || cam == null || PointerOverUi || Book.IsOpen || Wheel.IsOpen)
            {
                Tooltip.Hide(plotHoverOwner);
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(UiInput.MousePosition);
            var hit = Physics2D.OverlapPoint(world);
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
            if (s.IsRipe)
                return Loc.Get("ui.plot.ripe", name);
            bool night = game.Clock.IsNight;
            var ctx = game.ContextFor(plot, night);
            string state;
            if (s.CanGrow(ctx))
                state = Loc.Get("ui.plot.growing", name);
            else if (s.crop.kind == CropKind.Day)
                state = Loc.Get(!ctx.lit ? "ui.plot.need_light" : "ui.plot.need_work", name);
            else
                state = Loc.Get(!night ? "ui.plot.need_night" : "ui.plot.too_bright", name);
            int pct = Mathf.FloorToInt(100f * s.growth / Mathf.Max(1f, s.crop.growSeconds));
            return state + "\n" + Loc.Get("ui.plot.progress", pct);
        }
    }
}
