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
    public class ViewController : MonoBehaviour
    {
        public static ViewController Instance { get; private set; }

        const float Ppu = 16f;

        [SerializeField] PixelPerfectCamera pixelCamera;
        [SerializeField] Vector2Int minView = new Vector2Int(144, 112);
        [Tooltip("Zapas wokół lądu w pikselach (woda i mgła dookoła).")]
        [SerializeField] Vector2Int margin = new Vector2Int(48, 32);
        [Tooltip("Największa część ekranu, jaką może zająć okno (szerokość, wysokość).")]
        [SerializeField] Vector2 maxScreenFraction = new Vector2(0.6f, 0.7f);
        [SerializeField] int windowScale = 3;

        Vector2Int view;
        Vector2 pan;                 // przesunięcie kamery w pikselach, gdy wyspa jest większa niż kadr
        Vector2Int requiredWindow;   // np. otwarta Księga potrzebuje większego okna
        Vector2Int lastWindow;
        Vector2 dragStart;
        Vector2 panStart;
        float dragDistance;
        bool dragging;

        public Vector2Int View => view;
        /// <summary>Ile pikseli ekranu na piksel gry (skala świata).</summary>
        public int PixelRatio => Mathf.Max(1, Mathf.Min(Screen.width / Mathf.Max(1, view.x), Screen.height / Mathf.Max(1, view.y)));
        /// <summary>Ostatnie puszczenie PPM było przeciąganiem kamery (nie kliknięciem).</summary>
        public bool LastRightWasDrag { get; private set; }

        public void Setup(PixelPerfectCamera ppc) => pixelCamera = ppc;

        void Awake()
        {
            Instance = this;
            windowScale = Mathf.Clamp(PlayerPrefs.GetInt("viewScale", windowScale), 1, 6);
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

        public void SetScale(int scale)
        {
            windowScale = Mathf.Clamp(scale, 1, 6);
            PlayerPrefs.SetInt("viewScale", windowScale);
            Apply(true);
        }

        Vector2Int MaxView()
        {
            var res = Screen.currentResolution;
            int w = Mathf.FloorToInt(res.width * maxScreenFraction.x / windowScale / 16f) * 16;
            int h = Mathf.FloorToInt(res.height * maxScreenFraction.y / windowScale / 16f) * 16;
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
            var land = LandPixels();
            var max = MaxView();
            int w = Mathf.CeilToInt((land.width + 2 * margin.x) / 16f) * 16;
            int h = Mathf.CeilToInt((land.height + 2 * margin.y) / 16f) * 16;
            view = new Vector2Int(Mathf.Clamp(w, minView.x, max.x), Mathf.Clamp(h, minView.y, max.y));
            Apply(true);
        }

        void Apply(bool resizeWindow)
        {
            if (pixelCamera != null)
            {
                pixelCamera.refResolutionX = view.x;
                pixelCamera.refResolutionY = view.y;
            }
#if !UNITY_EDITOR
            var want = new Vector2Int(Mathf.Max(view.x * windowScale, requiredWindow.x), Mathf.Max(view.y * windowScale, requiredWindow.y));
            if ((resizeWindow || want != lastWindow) && Screen.fullScreenMode == FullScreenMode.Windowed && want != lastWindow)
            {
                lastWindow = want;
                Screen.SetResolution(want.x, want.y, FullScreenMode.Windowed);
            }
#endif
            Place();
        }

        void Update()
        {
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
            return land.width + 2 * margin.x > view.x || land.height + 2 * margin.y > view.y;
        }

        /// <summary>Kamera na środku lądu, przesunięta o pan w granicach (wyspa + zapas), przyciągnięta do pikseli.</summary>
        void Place()
        {
            var land = LandPixels();
            var c = land.center;
            float hx = Mathf.Max(0f, (land.width + 2 * margin.x - view.x) / 2f);
            float hy = Mathf.Max(0f, (land.height + 2 * margin.y - view.y) / 2f);
            pan = new Vector2(Mathf.Clamp(pan.x, -hx, hx), Mathf.Clamp(pan.y, -hy, hy));
            float x = Mathf.Round(c.x + pan.x) / Ppu;
            float y = Mathf.Round(c.y + pan.y) / Ppu;
            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
