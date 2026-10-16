using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.UI;

namespace Wyspa.World
{
    /// <summary>
    /// Kadr i okno (GDD §13.6, §9 p. 133): okno ma stały rozmiar (rozmiar z Opcji albo przeciągnięty za ramkę, zapamiętany),
    /// a kadr to okno podzielone przez przybliżenie (parzyste x2-x8, kółko myszy, w stronę kursora). Okno nie zmienia się samo
    /// przy rozbudowie wyspy ani po otwarciu Księgi (rośnie na chwilę tylko, gdy Księga nie mieści się nawet w skali x1, np. w mini-trybie).
    /// Wyspa większa niż kadr: przewijanie przeciąganiem PPM. Rozmiar okna zmienia się tylko w buildzie, w edytorze kadr
    /// liczy się z okna Game.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]   // przed trybami stawiania i rozbudowy, które czytają LastRightWasDrag w tej samej klatce
    public class ViewController : MonoBehaviour
    {
        public static ViewController Instance { get; private set; }

        const float Ppu = 16f;
        /// <summary>
        /// Grafiki są w 16 px, ale woda (shader) liczy obraz w 32 px na kafelek. Cała logika kadru zostaje w pikselach 16 px,
        /// a kamera dostaje PPU 32 i dwukrotnie większy kadr. Skala okna (piksele ekranu na piksel 16) musi być parzysta,
        /// żeby piksel wody 32 px był całym pikselem ekranu (x2, x4, x6).
        /// </summary>
        public const int ArtScale = 2;
        public const int MaxZoom = 8;
        static int Even(int s) => Mathf.Clamp(s + (s & 1), 2, MaxZoom);

        [SerializeField] PixelPerfectCamera pixelCamera;
        [SerializeField] Vector2Int minView = new Vector2Int(144, 112);
        [Tooltip("Zapas wokół lądu w pikselach (woda i mgła dookoła).")]
        [SerializeField] Vector2Int margin = new Vector2Int(48, 32);
        [Tooltip("Przezroczyste okno: dodatkowy zapas na kłęby mgły wokół wyspy (piksele).")]
        [SerializeField] Vector2Int wideExtra = new Vector2Int(24, 40);   // woda 46 px + wał mgły wokół lądu (z WideWall)
        bool wide;

        // grubszy wał mgły w przezroczystym oknie (FOG_REACH 34 w Fog.shader): zapas ponad wideExtra zapisane w scenie
        static readonly Vector2Int WideWall = new Vector2Int(16, 16);
        // przesuwanie PPM sięga dalej w mgłę; w przezroczystym oknie tylko trochę, bo za wałem jest pulpit
        static readonly Vector2Int FogPan = new Vector2Int(96, 80), FogPanWide = new Vector2Int(40, 40);

        Vector2Int Margin => wide ? margin + wideExtra + WideWall : margin;
        Vector2Int PanMargin => Margin + (wide ? FogPanWide : FogPan);

        /// <summary>Przezroczyste okno: większy zapas wokół lądu, żeby kłęby mgły mieściły się w kadrze.</summary>
        public void SetWideMargin(bool on)
        {
            if (wide == on)
                return;
            wide = on;
            Refit();
        }
        [Tooltip("Największa część ekranu, jaką może zająć okno (szerokość, wysokość): rozmiar „duży”.")]
        [SerializeField] Vector2 maxScreenFraction = new Vector2(0.6f, 0.7f);
        [SerializeField] int windowScale = 3;   // przybliżenie: piksele ekranu na piksel grafik 16 px

        /// <summary>Rozmiary okna z Opcji: część ekranu (szerokość, wysokość); ostatni = maxScreenFraction.</summary>
        static readonly Vector2[] SizeFractions = { new Vector2(0.3f, 0.38f), new Vector2(0.45f, 0.55f) };
        public const int SizePresets = 3;
        Vector2Int windowSize;       // obszar gry w pikselach ekranu (zapis: winW, winH)
        int sizePreset = 1;          // -1 = rozmiar przeciągnięty przez gracza
        float ownResizeUntil;        // po naszej zmianie rozmiaru Unity przez chwilę zgłasza stary rozmiar: to nie gracz

        Vector2Int view;
        Vector2 pan;                 // przesunięcie kamery w pikselach, gdy wyspa jest większa niż kadr

        /// <summary>Scenki (rejs): dodatkowe przesunięcie kamery w pikselach, poza przeciąganiem gracza.</summary>
        public Vector2 SceneOffset { get; set; }
        /// <summary>Scenki: gracz nie przesuwa ani nie powiększa widoku.</summary>
        public bool Locked { get; set; }
        Vector2Int requiredWindow;   // np. otwarta Księga potrzebuje większego okna
        Vector2Int lastWindow;
        Vector2 dragStart;
        Vector2 panStart;
        float dragDistance;
        bool dragging;

        bool mini;

        public Vector2Int View => view;
        /// <summary>Przybliżenie (piksele ekranu na piksel grafik 16 px): x2, x4, x6, x8.</summary>
        public int Scale => windowScale;
        /// <summary>Rozmiar okna z Opcji: 0 mały, 1 średni, 2 duży, -1 własny (przeciągnięty za ramkę).</summary>
        public int SizePreset => sizePreset;
        /// <summary>Mini-tryb (GDD §12.7): najmniejszy kadr w skali x2, np. w rogu ekranu.</summary>
        public bool Mini
        {
            get => mini;
            set
            {
                mini = value;
                PlayerPrefs.SetInt("viewMini", value ? 1 : 0);
                Refit();
            }
        }
        int EffectiveScale => mini ? Mathf.Min(2, windowScale) : windowScale;
        /// <summary>Ile pikseli ekranu na piksel gry (skala świata).</summary>
        public int PixelRatio => Mathf.Max(1, Mathf.Min(Screen.width / Mathf.Max(1, view.x), Screen.height / Mathf.Max(1, view.y)));
        /// <summary>Ostatnie puszczenie PPM było przeciąganiem kamery (nie kliknięciem).</summary>
        public bool LastRightWasDrag { get; private set; }
        /// <summary>Gracz właśnie przesuwa widok prawym przyciskiem (kursor przeciągania).</summary>
        public bool Dragging => dragging && dragDistance > 4f && CanPan();

        public void Setup(PixelPerfectCamera ppc) => pixelCamera = ppc;

        void Awake()
        {
            Instance = this;
            windowScale = Even(PlayerPrefs.GetInt("viewScale", windowScale));
            autoZoom = PlayerPrefs.GetInt("viewAutoZoom", 1) == 1;
            mini = PlayerPrefs.GetInt("viewMini", 0) == 1;
            sizePreset = Mathf.Clamp(PlayerPrefs.GetInt("winPreset", 1), -1, SizePresets - 1);
            windowSize = new Vector2Int(PlayerPrefs.GetInt("winW", 0), PlayerPrefs.GetInt("winH", 0));
            if (sizePreset >= 0 || windowSize.x <= 0 || windowSize.y <= 0)
                windowSize = PresetSize(Mathf.Max(0, sizePreset));
            if (GetComponent<Wyspa.Platform.DesktopWindow>() == null)
                gameObject.AddComponent<Wyspa.Platform.DesktopWindow>();
            view = minView;
            Apply(true);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable() => SubscribeIsland(true);
        void OnDisable() => SubscribeIsland(false);
        void Start() => SubscribeIsland(true);

        bool subscribed;
        void SubscribeIsland(bool on)
        {
            var island = Island.Instance;
            if (island == null || subscribed == on)
                return;
            subscribed = on;
            if (on) island.Rebuilt += Refit;
            else island.Rebuilt -= Refit;
            if (on) Refit();
        }

        /// <summary>Okno co najmniej tej wielkości (piksele ekranu), np. na czas otwartej Księgi. Zero = bez wymagań.</summary>
        public void RequireWindow(Vector2Int screenPixels)
        {
            requiredWindow = screenPixels;
            Apply(false);
        }

        /// <summary>Ponowne ustawienie rozmiaru okna (np. po przełączeniu z pełnego ekranu na okno).</summary>
        public void ForceWindowSize()
        {
            lastWindow = Vector2Int.zero;
            Apply(true);
        }

        /// <summary>Przybliżenie z Opcji (względem środka okna).</summary>
        public void SetScale(int scale) => ZoomTo(scale, new Vector2(Screen.width / 2f, Screen.height / 2f));

        bool autoZoom;

        /// <summary>
        /// Przybliżenie samo dopasowane do wyspy (ląd z zapasem mieści się w kadrze, jak dawny kadr rosnący z wyspą). Wyłącza się,
        /// gdy gracz przybliży kółkiem, palcami albo w Opcjach; w menu głównym zawsze automatyczne.
        /// </summary>
        public bool AutoZoom
        {
            get => autoZoom;
            set
            {
                autoZoom = value;
                PlayerPrefs.SetInt("viewAutoZoom", value ? 1 : 0);
                Refit();
            }
        }

        /// <summary>Największe parzyste przybliżenie, przy którym ląd z zapasem (Margin) mieści się w oknie.</summary>
        int FitZoom(Vector2Int win)
        {
            var land = LandPixels();
            float w = land.width + 2 * Margin.x, h = land.height + 2 * Margin.y;
            for (int s = MaxZoom; s > 2; s -= 2)
                if (win.x / s >= w && win.y / s >= h)
                    return Mathf.Max(s, MinZoom());
            return MinZoom();
        }

        /// <summary>
        /// Przybliżenie z punktem pod kursorem w miejscu: przesunięcie kamery o różnicę odległości kursora od środka okna
        /// w pikselach gry przed i po zmianie skali.
        /// </summary>
        void ZoomTo(int scale, Vector2 screenPoint)
        {
            int old = windowScale;
            int next = Mathf.Max(Even(scale), MinZoom());
            if (autoZoom)
            {
                autoZoom = false;   // gracz sam wybrał przybliżenie
                PlayerPrefs.SetInt("viewAutoZoom", 0);
            }
            if (next != old)
            {
                var off = screenPoint - new Vector2(Screen.width / 2f, Screen.height / 2f);
                pan += off * (1f / old - 1f / next);
            }
            windowScale = next;
            PlayerPrefs.SetInt("viewScale", windowScale);
            Refit();
        }

        /// <summary>Najmniejsze przybliżenie, przy którym kadr nie wychodzi poza mapę wyspy (za nią byłaby pustka).</summary>
        int MinZoom()
        {
            var w = WindowSize;
            for (int s = 2; s < MaxZoom; s += 2)
                if (w.x / s <= IslandPainter.Size && w.y / s <= IslandPainter.Size)
                    return s;
            return MaxZoom;
        }

        /// <summary>Rozmiar okna z Opcji: mały, średni, duży (część ekranu, wielokrotność 16 px).</summary>
        public void SetSizePreset(int preset)
        {
            sizePreset = Mathf.Clamp(preset, 0, SizePresets - 1);
            windowSize = PresetSize(sizePreset);
            SaveWindowSize();
            Refit();
        }

        Vector2Int PresetSize(int preset)
        {
            var res = Screen.currentResolution;
            var f = preset < SizeFractions.Length ? SizeFractions[preset] : maxScreenFraction;
            int w = Mathf.FloorToInt(res.width * f.x / 16f) * 16, h = Mathf.FloorToInt(res.height * f.y / 16f) * 16;
            return new Vector2Int(Mathf.Max(minView.x * 2, w), Mathf.Max(minView.y * 2, h));
        }

        void SaveWindowSize()
        {
            PlayerPrefs.SetInt("winPreset", sizePreset);
            PlayerPrefs.SetInt("winW", windowSize.x);
            PlayerPrefs.SetInt("winH", windowSize.y);
        }

        /// <summary>Obszar gry bez wymagań Księgi: mini-tryb ma najmniejszy kadr w x2.</summary>
        Vector2Int WindowSize => mini ? minView * 2 : windowSize;

        Rect LandPixels()
        {
            var island = Island.Instance;
            var r = island != null && island.Ready ? island.LandRect : new Rect(-1f, -1f, 2f, 2f);
            return new Rect(r.x * Ppu, r.y * Ppu, r.width * Ppu, r.height * Ppu);
        }

        /// <summary>Kadr = okno podzielone przez przybliżenie (w edytorze: okno Game). Rozbudowa wyspy nie zmienia okna.</summary>
        void Refit()
        {
            if (mini)
            {
                view = minView;
                Apply(true);
                return;
            }
#if UNITY_EDITOR
            var win = new Vector2Int(Screen.width, Screen.height);
#else
            // pełny ekran: kadr z całego ekranu (inaczej Pixel Perfect Camera powiększyłaby obraz ponad przybliżenie)
            var win = Screen.fullScreenMode == FullScreenMode.Windowed ? WindowSize : new Vector2Int(Screen.width, Screen.height);
#endif
            windowScale = autoZoom || Save.SaveSystem.InMenu ? FitZoom(win) : Mathf.Max(windowScale, MinZoom());
            view = new Vector2Int(Mathf.Max(32, win.x / windowScale), Mathf.Max(32, win.y / windowScale));
            Apply(true);
        }

        void Apply(bool resizeWindow)
        {
            if (pixelCamera != null)
            {
                pixelCamera.assetsPPU = (int)Ppu * ArtScale;
                pixelCamera.refResolutionX = view.x * ArtScale;
                pixelCamera.refResolutionY = view.y * ArtScale;
            }
#if !UNITY_EDITOR
            int scale = EffectiveScale;
            var want = new Vector2Int(Mathf.Max(view.x * scale, requiredWindow.x), Mathf.Max(view.y * scale, requiredWindow.y));
            if (Screen.fullScreenMode == FullScreenMode.Windowed && want != lastWindow)
            {
                lastWindow = want;
                ownResizeUntil = Time.unscaledTime + 1f;
                // okno bez ramki i w rogu ekranu zmienia rozmiar przez WinAPI (Screen.SetResolution przywróciłby ramkę)
                var desktop = Wyspa.Platform.DesktopWindow.Instance;
                if (desktop == null || !desktop.SetClientSize(want))
                    Screen.SetResolution(want.x, want.y, FullScreenMode.Windowed);
            }
#endif
            Place();
        }

        void Update()
        {
            // kółko myszy: przybliżenie (x2-x8) w stronę kursora; nie nad UI (przewijanie list) i nie przy otwartej Księdze
            int scroll = UiInput.Scroll;
            if (!Locked && scroll != 0 && !mini && !OverUi())
                ZoomTo(windowScale + scroll * 2, UiInput.MousePosition);
            // dotyk (§9 p. 134): szczypanie dwoma palcami = przybliżenie, przeciąganie palcem = przesuwanie widoku
            int pinch = UiInput.PinchStep;
            if (!Locked && pinch != 0 && !mini && !OverUi())
                ZoomTo(windowScale + pinch * 2, UiInput.MousePosition);
            var touchPan = UiInput.TouchPan;
            if (!Locked && touchPan != Vector2.zero && CanPan() && !OverUi())
                pan -= touchPan / PixelRatio;
            WatchWindow();
            if (!Locked)
                HandleDrag();
            Place();
        }

        static bool OverUi()
        {
            var ui = UiRoot.Instance;
            return ui != null && (ui.PointerOverUi || (ui.Book != null && ui.Book.IsOpen));
        }

        Vector2Int seenScreen;

        /// <summary>
        /// Gracz zmienił rozmiar okna (przeciągnięcie ramki): nowy rozmiar zostaje i jest zapamiętany, kadr się dopasowuje.
        /// W edytorze: zmiana okna Game. Nasze własne zmiany rozmiaru (i chwilę po nich) się nie liczą.
        /// </summary>
        void WatchWindow()
        {
            var now = new Vector2Int(Screen.width, Screen.height);
            if (now == seenScreen)
                return;
            seenScreen = now;
#if UNITY_EDITOR
            Refit();
#else
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Refit();
                return;
            }
            if (mini || requiredWindow != Vector2Int.zero || Time.unscaledTime < ownResizeUntil || now == lastWindow)
                return;
            windowSize = now;
            sizePreset = -1;
            lastWindow = now;
            SaveWindowSize();
            Refit();
#endif
        }

        void HandleDrag()
        {
            var mouse = UiInput.MousePosition;
            if (UiInput.RightPressed)
            {
                dragStart = mouse;
                panStart = pan;
                dragDistance = 0f;
                dragging = true;
            }
            if (dragging && UiInput.RightHeld)
            {
                var delta = mouse - dragStart;
                dragDistance = Mathf.Max(dragDistance, delta.magnitude);
                if (dragDistance > 4f && CanPan())
                    pan = panStart - delta / PixelRatio;
            }
            if (dragging && UiInput.RightReleased)
            {
                dragging = false;
                LastRightWasDrag = dragDistance > 4f && CanPan();
            }
        }

        bool CanPan()
        {
            var land = LandPixels();
            return land.width + 2 * PanMargin.x > view.x || land.height + 2 * PanMargin.y > view.y;
        }

        /// <summary>
        /// Kamera na środku lądu, przesunięta o pan w granicach (wyspa + zapas + pas mgły), przyciągnięta do pikseli.
        /// Kadr nie wychodzi poza mapę wyspy (tekstury wody i mgły), żeby za mgłą nie było pustki.
        /// </summary>
        void Place()
        {
            var land = LandPixels();
            var c = land.center;
            float hx = Mathf.Max(0f, (land.width + 2 * PanMargin.x - view.x) / 2f);
            float hy = Mathf.Max(0f, (land.height + 2 * PanMargin.y - view.y) / 2f);
            pan = new Vector2(Mathf.Clamp(pan.x, -hx, hx), Mathf.Clamp(pan.y, -hy, hy));
            float mx = Mathf.Max(0f, IslandPainter.Size / 2f - view.x / 2f), my = Mathf.Max(0f, IslandPainter.Size / 2f - view.y / 2f);
            pan = new Vector2(Mathf.Clamp(c.x + pan.x, -mx, mx) - c.x, Mathf.Clamp(c.y + pan.y, -my, my) - c.y);
            var at = new Vector2(Mathf.Clamp(c.x + pan.x + SceneOffset.x, -mx, mx), Mathf.Clamp(c.y + pan.y + SceneOffset.y, -my, my));
            float x = Mathf.Round(at.x) / Ppu;
            float y = Mathf.Round(at.y) / Ppu;
            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
