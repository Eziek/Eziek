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
        public ActionRing Ring { get; private set; }
        /// <summary>Menu główne (tylko gdy nie wybrano jeszcze slotu zapisu).</summary>
        public MainMenuView Menu { get; private set; }

        // prawy klik na wyspie otwiera pierścień akcji (GDD §12.10): uzbrojony przy wciśnięciu, otwiera się po puszczeniu bez przeciągania
        bool ringArmed;
        bool busyLastFrame;   // panel/tryb otwarty w poprzedniej klatce (ten sam PPM, który go zamyka, nie otwiera koła)
        Vector2 ringPress;

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
            pixelFont = CreateFont(theme.font);
            readableFont = CreateFont(theme.fontReadable, 512);
            FitLineHeight(readableFont);
            Ui.Font = ReadableFont && readableFont != null ? readableFont : pixelFont;
            SetupCjkFallbacks();
            Build();
            Instance = this;   // dopiero po zbudowaniu - grządki wołają Instance.Wheel
        }

        // W Start, bo GameLoop.Instance ustawia się w Awake innego obiektu (kolejność Awake nie jest pewna).
        void Start()
        {
            ApplySavedOptions();
            if (PlayerPrefs.GetInt("hintRing", 0) == 0 && !Save.SaveSystem.InMenu)
                StartCoroutine(RingHint());
        }

        /// <summary>Podpowiedź dla nowego gracza: menu jest pod prawym przyciskiem (aż raz go użyje).</summary>
        System.Collections.IEnumerator RingHint()
        {
            yield return new WaitForSecondsRealtime(3f);
            while (PlayerPrefs.GetInt("hintRing", 0) == 0)
            {
                // w trakcie wprowadzenia menu PPM opisuje karteczka (krok z lampą)
                if (IntroNotes.Instance == null || !IntroNotes.Instance.Active)
                    GameEvents.RaiseToast("toast.hint.ring");
                yield return new WaitForSecondsRealtime(180f);
            }
        }

        void OnDestroy()
        {
            Loc.Changed -= OrderCjkFallbacks;
            if (Instance == this)
                Instance = null;
        }

        TMP_FontAsset cjkJa, cjkZh;
        TMP_FontAsset pixelFont, readableFont;

        /// <summary>
        /// Czytelny font (po teście gracza: „UI mocno nieczytelny”): Fusion Pixel 8px z małymi literami zamiast fontu 3x5
        /// z samymi wielkimi. Domyślnie włączony, Opcje → Ogólne → Czcionka.
        /// </summary>
        public static bool ReadableFont
        {
            get => PlayerPrefs.GetInt("fontReadable", 1) == 1;
            private set => PlayerPrefs.SetInt("fontReadable", value ? 1 : 0);
        }

        /// <summary>Przełącza font całego UI od razu (otwarte widoki przebudowują się przy następnym odświeżeniu).</summary>
        public void SetReadableFont(bool on)
        {
            ReadableFont = on;
            var old = Ui.Font;
            var next = on && readableFont != null ? readableFont : pixelFont;
            if (next == null || next == old)
                return;
            Ui.Font = next;
            OrderCjkFallbacks();
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
                if (t.font == old)
                    t.font = next;
        }

        /// <summary>
        /// Fusion Pixel ma linię 12 px (wysokie akcenty i ogonki), układy UI liczą wiersze po 10 px: linia 10 px,
        /// górna krawędź tekstu 8 px nad linią bazową (jak font 3x5), więc tekst stoi w tym samym miejscu co dawniej.
        /// </summary>
        static void FitLineHeight(TMP_FontAsset fa)
        {
            if (fa == null)
                return;
            var fi = fa.faceInfo;
            fi.ascentLine = 8f;
            fi.descentLine = -2f;
            fi.lineHeight = 10f;
            fa.faceInfo = fi;
        }

        /// <summary>
        /// Znaki japońskie i chińskie (GDD §16.2): Fusion Pixel 8px jako zapasowe fonty pikselowego fontu UI.
        /// Te same znaki Han mają w JA i ZH inne kształty, więc pierwszy w kolejce jest wariant języka gry.
        /// </summary>
        void SetupCjkFallbacks()
        {
            if (Ui.Font == null)
                return;
            cjkJa = CreateFont(theme.cjkJa, 1024);
            cjkZh = CreateFont(theme.cjkZhHans, 1024);
            OrderCjkFallbacks();
            Loc.Changed += OrderCjkFallbacks;
        }

        void OrderCjkFallbacks()
        {
            if (Ui.Font == null)
                return;
            var list = new List<TMP_FontAsset>();
            bool zhFirst = Loc.Language == "zh-Hans";
            foreach (var fa in zhFirst ? new[] { cjkZh, cjkJa } : new[] { cjkJa, cjkZh })
                if (fa != null)
                    list.Add(fa);
            foreach (var fa in new[] { pixelFont, readableFont })
                if (fa != null)
                    fa.fallbackFontAssetTable = list;
        }

        static TMP_FontAsset CreateFont(Font font, int atlasSize = 256)
        {
            if (font == null)
                return null;
            // TMP tworzy materiał z shaderem „TextMeshPro/Mobile/Bitmap” (Shader.Find). Gdy build go wyciął (brak Wyspa > 1),
            // zostaje domyślny font TMP - UI działa, tylko bez pikselowego fontu.
            if (Shader.Find("TextMeshPro/Mobile/Bitmap") == null)
            {
                Debug.LogError("[Wyspa] Brak shadera TextMeshPro/Mobile/Bitmap w buildzie: uruchom Wyspa > 1 i zbuduj grę ponownie.");
                return null;
            }
            // RASTER = bez wygładzania, filtr Point = ostre piksele (GDD §12.9)
            TMP_FontAsset fa;
            try
            {
                fa = TMP_FontAsset.CreateFontAsset(font, 8, 1, GlyphRenderMode.RASTER, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się utworzyć fontu UI: {e.Message}");
                return null;
            }
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
            Hud = HudView.Create(canvasRect);
            IntroNotes.Create(canvasRect, Hud);
            Expansion = ExpansionMode.Create(canvasRect, Tooltip);
            Toasts = ToastView.Create(canvasRect);
            Wheel = SeedWheel.Create(canvasRect, Tooltip);
            Book = BookView.Create(canvasRect, Tooltip);
            Context = ContextPanel.Create(canvasRect, StartMove);
            Placement = PlacementMode.Create(transform, Tooltip);
            Ring = ActionRing.Create(canvasRect, Tooltip);
            if (Save.SaveSystem.InMenu)
                Menu = MainMenuView.Create(canvasRect);
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

        /// <summary>Rozmiar interfejsu z Opcji: 0 = automatyczny, 1-4 = stała skala.</summary>
        public static int UiScaleSetting
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("uiScale", 0), 0, 4);
            set => PlayerPrefs.SetInt("uiScale", Mathf.Clamp(value, 0, 4));
        }

        /// <summary>
        /// Skala UI (po teście: przy powiększonym oknie i małej wyspie pasek zajmował pół okna). Zależy od rozdzielczości
        /// monitora, nie od powiększenia okna: 1080p → x2, 1440p → x3, 4K → x4. Najwyżej o 1 mniej niż skala świata,
        /// żeby UI zmieściło się w oknie. Gracz może ustawić rozmiar sam (Opcje → Okno).
        /// </summary>
        public static int UiScale()
        {
            var view = ViewController.Instance;
            int world = view != null
                ? view.PixelRatio
                : Mathf.Max(1, Mathf.Min(Screen.width / 272, Screen.height / 200));
            int want = UiScaleSetting;
            if (want == 0)
                want = Mathf.Max(1, Mathf.RoundToInt(Screen.currentResolution.height / 540f));
            return Mathf.Max(1, Mathf.Min(want, world - 1));
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
            UpdateRingTrigger();
            scaler.scaleFactor = Book.IsOpen ? BookScale() : UiScale();
            Hud.ForceVisible = Book.IsOpen || Wheel.IsOpen || Ring.IsOpen || Expansion.IsActive || PlacementMode.Active;
            UpdatePlotTooltip();
        }

        bool Busy => Ring.IsOpen || Book.IsOpen || Wheel.IsOpen || Context.IsOpen || Expansion.IsActive || PlacementMode.Active;

        void LateUpdate() => busyLastFrame = Busy;

        /// <summary>
        /// PPM bez przeciągania, gdy nic innego nie jest otwarte (tryby, panele, Księga): pierścień akcji przy kursorze.
        /// Stan sprawdzamy przy wciśnięciu, bo to samo puszczenie PPM zamyka np. tryb rozbudowy.
        /// </summary>
        void UpdateRingTrigger()
        {
            if (Save.SaveSystem.InMenu)
                return;   // menu główne: wyspa w tle nie reaguje
            var mouse = UiInput.MousePosition;
            if (UiInput.RightPressed)
            {
                ringPress = mouse;
                ringArmed = !busyLastFrame && !Busy && !PointerOverUi && !DebugHud.IsPointerOverHud(mouse);
            }
            if (UiInput.RightReleased && ringArmed)
            {
                ringArmed = false;
                if ((mouse - ringPress).sqrMagnitude <= 16f)   // przeciąganie PPM przesuwa kamerę, nie otwiera menu
                {
                    Tooltip.Hide(plotHoverOwner);
                    Ring.Open(mouse, RingEntries(mouse));
                    PlayerPrefs.SetInt("hintRing", 1);
                }
            }
        }

        /// <summary>Stałe menu pierścienia + akcje tego, co leży pod kursorem (grządka: zasyp).</summary>
        List<ActionRing.Entry> RingEntries(Vector2 screen)
        {
            var list = new List<ActionRing.Entry>
            {
                new ActionRing.Entry { icon = "book", nameKey = "term.grimoire", run = () => OpenBook(BookView.Tab.Upgrades) },
                new ActionRing.Entry { icon = "shop", nameKey = "menu.shop", run = () => OpenBook(BookView.Tab.Shop) },
                new ActionRing.Entry { icon = "plus", nameKey = "menu.expand", run = () => { CloseAll(); Expansion.Toggle(); } },
                new ActionRing.Entry { icon = "settings", nameKey = "menu.options", run = () => OpenBook(BookView.Tab.Options) },
                new ActionRing.Entry { icon = "menu", nameKey = "menu.main", run = BackToMenu },
                new ActionRing.Entry { icon = "exit", nameKey = "act.quit", run = QuitGame },
            };
            // losowe sadzenie na wszystkich pustych grządkach (prośba gracza): w nocy rośliny nocy, w dzień rośliny dnia
            bool anyEmpty = false;
            foreach (var p in FarmPlot.All)
                if (p != null && p.State.IsEmpty) { anyEmpty = true; break; }
            if (anyEmpty)
                list.Insert(0, new ActionRing.Entry
                {
                    icon = "dice",
                    tip = () => Loc.Get("act.plant_random") + "\n" + Loc.Get(GameLoop.Instance != null && GameLoop.Instance.Clock.IsNight
                                                                            ? "ui.wheel.random_night" : "ui.wheel.random_day"),
                    run = () =>
                    {
                        var game = GameLoop.Instance;
                        if (game != null && game.PlantRandomEverywhere() == 0)
                            GameEvents.RaiseToast("toast.plant_random.none");
                    },
                });
            var cam = Camera.main;
            var hit = cam != null ? Wyspa.Input.ClickRouter.Pick(cam.ScreenToWorldPoint(screen)) : null;
            if (hit != null && hit.TryGetComponent(out FarmPlot grown) && !grown.State.IsEmpty && !grown.State.IsRipe)
                list.Insert(0, new ActionRing.Entry
                {
                    icon = "sapling",
                    tip = () => Loc.Get("act.dig_up") + (grown != null && !grown.State.IsEmpty ? "\n" + Loc.Get("ui.dig_up.back", Loc.Get("crop." + grown.State.crop.id)) : ""),
                    run = () => { if (grown != null) grown.DigUp(); },
                });
            if (hit != null && hit.TryGetComponent(out FarmPlot plot) && Island.Instance != null)
                list.Insert(0, new ActionRing.Entry
                {
                    icon = "fill",
                    tip = () => Loc.Get("act.fill") + (plot != null && !plot.State.IsEmpty ? "\n" + Loc.Get("ui.fill.lose", Loc.Get("crop." + plot.State.crop.id)) : ""),
                    run = () => { if (plot != null && Island.Instance != null) Island.Instance.TryFill(plot); },
                });
            return list;
        }

        /// <summary>Powrót do menu głównego (pierścień): gra zapisuje się, scena ładuje się od nowa z menu.</summary>
        void BackToMenu()
        {
            CloseAll();
            var save = FindAnyObjectByType<Save.SaveSystem>();
            if (save != null)
                save.BackToMenu();
        }

        /// <summary>Wyjście z gry (pierścień, Opcje): zapis robi SaveSystem przy zamykaniu aplikacji.</summary>
        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            if (s.crop.kind == CropKind.Day && game.Activity.TempoMultiplier > 1.05f)
                moon += "\n" + Loc.Get("ui.plot.tempo", game.Activity.TempoMultiplier);
            if (s.crop.kind == CropKind.Day && Weather.WeatherSystem.Raining)
                moon += "\n" + Loc.Get("ui.plot.rain");
            // roślina nie pasuje do pory (nocna w dzień, dzienna nocą bez lampy): podpowiedź, że da się ją podmienić
            string swap = plot.OutOfSeason(night, ctx.lit) ? "\n" + Loc.Get("ui.plot.replace_hint") : "";
            return state + "\n" + Loc.Get("ui.plot.progress", pct) + quality + moon + swap;
        }
    }
}
