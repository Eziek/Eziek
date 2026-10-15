using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.UI;

namespace Wyspa.World
{
    /// <summary>
    /// Kadr i okno (GDD §13.6): natywny kadr rośnie z wyspą krokami po 16 px (start 144x112), skala okna zostaje całkowita.
    /// Gdy wyspa przerośnie limit (część ekranu), kamera przewija się przeciąganiem PPM. Okno zmienia rozmiar tylko w buildzie,
    /// w edytorze rośnie sam kadr (okno Game ma rozmiar ustawiony w edytorze).
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
        static int Even(int s) => Mathf.Clamp(s + (s & 1), 2, 6);

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
        [Tooltip("Największa część ekranu, jaką może zająć okno (szerokość, wysokość).")]
        [SerializeField] Vector2 maxScreenFraction = new Vector2(0.6f, 0.7f);
        [SerializeField] int windowScale = 3;

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
        public int Scale => windowScale;
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
            mini = PlayerPrefs.GetInt("viewMini", 0) == 1;
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

        public void SetScale(int scale)
        {
            windowScale = Even(scale);
            PlayerPrefs.SetInt("viewScale", windowScale);
            Refit();
        }

        Vector2Int MaxView()
        {
            var res = Screen.currentResolution;
            int w = Mathf.FloorToInt(res.width * maxScreenFraction.x / EffectiveScale / 16f) * 16;
            int h = Mathf.FloorToInt(res.height * maxScreenFraction.y / EffectiveScale / 16f) * 16;
            return new Vector2Int(Mathf.Max(minView.x, w), Mathf.Max(minView.y, h));
        }

        Rect LandPixels()
        {
            var island = Island.Instance;
            var r = island != null && island.Ready ? island.LandRect : new Rect(-1f, -1f, 2f, 2f);
            return new Rect(r.x * Ppu, r.y * Ppu, r.width * Ppu, r.height * Ppu);
        }

        /// <summary>Po rozbudowie: kadr dopasowany do lądu (krokami po 16 px, w granicach limitu).</summary>
        void Refit()
        {
            if (mini)
            {
                view = minView;
                Apply(true);
                return;
            }
            var land = LandPixels();
            var max = MaxView();
            int w = Mathf.CeilToInt((land.width + 2 * Margin.x) / 16f) * 16;
            int h = Mathf.CeilToInt((land.height + 2 * Margin.y) / 16f) * 16;
            view = new Vector2Int(Mathf.Clamp(w, minView.x, max.x), Mathf.Clamp(h, minView.y, max.y));
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
            // Ctrl + kółko myszy: powiększenie okna (x1..x3 w pikselach 32 px, jak w Opcjach)
            int scroll = UiInput.Scroll;
            if (!Locked && scroll != 0 && UiInput.CtrlHeld && !mini)
                SetScale(Mathf.Clamp(windowScale + scroll * 2, 2, 6));
            if (!Locked)
                HandleDrag();
            Place();
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
