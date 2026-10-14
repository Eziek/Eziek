using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Wyspa.UI;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Platform
{
    public enum ScreenCorner { None, TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>
    /// Okno na pulpicie (GDD §11.3 I, §12.7): bez ramki (przeciąganie LPM za puste miejsce), zawsze na wierzchu,
    /// przyklejenie do rogu ekranu, przezroczyste tło (bez morza i mgły, klik przez puste miejsca idzie do pulpitu).
    /// WinAPI działa tylko w buildzie na Windows; w edytorze ustawienia się zapisują, a przezroczystość chowa tylko morze.
    /// </summary>
    public class DesktopWindow : MonoBehaviour
    {
        public static DesktopWindow Instance { get; private set; }
        public static bool Supported => WinApi.Supported;

        const int CornerMargin = 8;
        const int SnapDistance = 32;
        /// <summary>Szerokość pasa wody przy brzegu w przezroczystym oknie (piksele gry).</summary>
        const float ShoreWaterPixels = 18f;

        bool frameless, topMost, transparent;
        ScreenCorner corner;
        IntPtr hwnd;
        bool clickThrough;
        bool dragging, dragMoved;
        WinApi.POINT dragCursor;
        WinApi.RECT dragWindow;
        bool cameraSaved;
        Color cameraColor;
        bool cameraHdr;
        PointerEventData pointer;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public event Action Changed;

        /// <summary>Bez ramki systemowej (przezroczyste okno zawsze jest bez ramki).</summary>
        public bool Frameless
        {
            get => frameless || transparent;
            set { frameless = value; PlayerPrefs.SetInt("winFrameless", value ? 1 : 0); ApplyStyle(); }
        }

        public bool TopMost
        {
            get => topMost;
            set { topMost = value; PlayerPrefs.SetInt("winTop", value ? 1 : 0); ApplyStyle(); }
        }

        public bool Transparent
        {
            get => transparent;
            set { transparent = value; PlayerPrefs.SetInt("winTransparent", value ? 1 : 0); ApplyStyle(); ApplyTransparency(); }
        }

        public ScreenCorner Corner
        {
            get => corner;
            set { corner = value; PlayerPrefs.SetInt("winCorner", (int)value); ApplyStyle(); }
        }

        IntPtr Hwnd
        {
            get
            {
                if (hwnd == IntPtr.Zero && Supported)
                    hwnd = WinApi.FindGameWindow();
                return hwnd;
            }
        }

        void Awake()
        {
            Instance = this;
            frameless = PlayerPrefs.GetInt("winFrameless", 0) == 1;
            topMost = PlayerPrefs.GetInt("winTop", 0) == 1;
            transparent = PlayerPrefs.GetInt("winTransparent", 0) == 1;
            corner = (ScreenCorner)Mathf.Clamp(PlayerPrefs.GetInt("winCorner", 0), 0, 4);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start() => StartCoroutine(Init());

        /// <summary>
        /// Build pamięta tryb ekranu z poprzednich uruchomień (domyślnie Unity: pełny ekran w oknie). Gra to małe okno na pulpicie,
        /// więc najpierw przełączamy na tryb okienkowy, czekamy, aż Unity go zastosuje, a dopiero potem ramka, róg i rozmiar.
        /// </summary>
        System.Collections.IEnumerator Init()
        {
            if (Supported && Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Screen.SetResolution(Mathf.Max(320, Screen.width / 3), Mathf.Max(240, Screen.height / 3), FullScreenMode.Windowed);
                yield return null;
                yield return null;
                hwnd = IntPtr.Zero;   // Unity mógł odtworzyć okno
            }
            if (Supported && Hwnd != IntPtr.Zero && corner == ScreenCorner.None && PlayerPrefs.HasKey("winX"))
                WinApi.SetWindowPos(hwnd, IntPtr.Zero, PlayerPrefs.GetInt("winX"), PlayerPrefs.GetInt("winY"), 0, 0,
                    WinApi.SWP_NOSIZE | WinApi.SWP_NOZORDER | WinApi.SWP_NOACTIVATE);
            ApplyStyle();
            ApplyTransparency();
            if (ViewController.Instance != null)
                ViewController.Instance.ForceWindowSize();
        }

        // ------------------------------------------------------------ rozmiar i położenie

        /// <summary>Rozmiar obszaru gry (piksele ekranu). Zwraca false, gdy okna nie da się ruszyć (edytor, inny system).</summary>
        public bool SetClientSize(Vector2Int size)
        {
            if (!Supported || Hwnd == IntPtr.Zero)
                return false;
            Place(size.x, size.y);
            return true;
        }

        /// <summary>Okno o danym obszarze gry: w rogu ekranu albo w obecnym miejscu, zawsze w obszarze roboczym monitora.</summary>
        void Place(int clientW, int clientH)
        {
            WinApi.GetWindowRect(hwnd, out var wr);
            WinApi.GetClientRect(hwnd, out var cr);
            // ramka = okno - obszar gry (liczone z rzeczywistych prostokątów, więc zgadza się przy każdym DPI)
            int w = clientW + (wr.right - wr.left) - (cr.right - cr.left);
            int h = clientH + (wr.bottom - wr.top) - (cr.bottom - cr.top);
            var work = WinApi.WorkArea(hwnd);
            int x = wr.left, y = wr.top;
            switch (corner)
            {
                case ScreenCorner.TopLeft: x = work.left + CornerMargin; y = work.top + CornerMargin; break;
                case ScreenCorner.TopRight: x = work.right - CornerMargin - w; y = work.top + CornerMargin; break;
                case ScreenCorner.BottomLeft: x = work.left + CornerMargin; y = work.bottom - CornerMargin - h; break;
                case ScreenCorner.BottomRight: x = work.right - CornerMargin - w; y = work.bottom - CornerMargin - h; break;
            }
            x = Mathf.Clamp(x, work.left, Mathf.Max(work.left, work.right - w));
            y = Mathf.Clamp(y, work.top, Mathf.Max(work.top, work.bottom - h));
            WinApi.SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, WinApi.SWP_NOZORDER | WinApi.SWP_NOACTIVATE);
        }

        void ApplyStyle()
        {
            if (!Supported || Hwnd == IntPtr.Zero)
            {
                Changed?.Invoke();
                return;
            }
            WinApi.GetClientRect(hwnd, out var cr);
            long style = WinApi.Style(hwnd, WinApi.GWL_STYLE);
            const long frame = WinApi.WS_CAPTION | WinApi.WS_THICKFRAME | WinApi.WS_MAXIMIZEBOX;
            long want = Frameless ? style & ~frame : style | frame | WinApi.WS_SYSMENU | WinApi.WS_MINIMIZEBOX;
            if (want != style)
            {
                WinApi.SetStyle(hwnd, WinApi.GWL_STYLE, want);
                WinApi.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    WinApi.SWP_NOMOVE | WinApi.SWP_NOSIZE | WinApi.SWP_NOZORDER | WinApi.SWP_NOACTIVATE | WinApi.SWP_FRAMECHANGED);
            }
            Place(cr.right - cr.left, cr.bottom - cr.top);   // ten sam obszar gry, nowa ramka i róg
            WinApi.SetWindowPos(hwnd, topMost ? WinApi.HWND_TOPMOST : WinApi.HWND_NOTOPMOST, 0, 0, 0, 0,
                WinApi.SWP_NOMOVE | WinApi.SWP_NOSIZE | WinApi.SWP_NOACTIVATE);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ przezroczystość

        void ApplyTransparency()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                if (!cameraSaved)
                {
                    cameraSaved = true;
                    cameraColor = cam.backgroundColor;
                    cameraHdr = cam.allowHDR;
                }
                // tło z alfą 0; HDR wyłączony, bo bufor HDR (R11G11B10) nie ma kanału alfa
                cam.backgroundColor = transparent ? new Color(0f, 0f, 0f, 0f) : cameraColor;
                cam.allowHDR = !transparent && cameraHdr;
            }
            // morze zostaje pasem przy brzegu (shader wody), a mgła gęstnieje ku jego krawędzi
            var water = FindAnyObjectByType<WaterAnimator>();
            if (water != null)
                water.SetShoreOnly(transparent ? ShoreWaterPixels : 0f);
            var fog = FindAnyObjectByType<FogController>();
            if (fog != null)
                fog.SetShoreBand(transparent, ShoreWaterPixels);
            var shafts = FindAnyObjectByType<LightShafts>();
            if (shafts != null)
                shafts.SetMask(transparent ? ShoreWaterPixels : 0f);

            if (!Supported || Hwnd == IntPtr.Zero)
                return;
            var m = transparent ? new WinApi.MARGINS { left = -1, right = -1, top = -1, bottom = -1 } : new WinApi.MARGINS();
            WinApi.DwmExtendFrameIntoClientArea(hwnd, ref m);
            if (!transparent)
                SetClickThrough(false);
        }

        void SetClickThrough(bool on)
        {
            if (on == clickThrough || Hwnd == IntPtr.Zero)
                return;
            clickThrough = on;
            long ex = WinApi.Style(hwnd, WinApi.GWL_EXSTYLE);
            if (on)
            {
                WinApi.SetStyle(hwnd, WinApi.GWL_EXSTYLE, ex | WinApi.WS_EX_LAYERED | WinApi.WS_EX_TRANSPARENT);
                WinApi.SetLayeredWindowAttributes(hwnd, 0, 255, WinApi.LWA_ALPHA);
            }
            else
                WinApi.SetStyle(hwnd, WinApi.GWL_EXSTYLE, ex & ~(WinApi.WS_EX_LAYERED | WinApi.WS_EX_TRANSPARENT));
        }

        /// <summary>Czy pod punktem (współrzędne ekranu Unity) jest coś z gry: UI, ląd, obiekt.</summary>
        bool IsSolidAt(Vector2 pos)
        {
            if (DebugHud.IsPointerOverHud(pos) || PlacementMode.Active)
                return true;
            var ui = UiRoot.Instance;
            if (ui != null && ui.Expansion.IsActive)
                return true;   // ramki rozbudowy leżą na wodzie
            var es = EventSystem.current;
            if (es != null)
            {
                if (pointer == null)
                    pointer = new PointerEventData(es);
                pointer.position = pos;
                uiHits.Clear();
                es.RaycastAll(pointer, uiHits);
                if (uiHits.Count > 0)
                    return true;
            }
            var cam = Camera.main;
            if (cam == null)
                return true;
            Vector2 world = cam.ScreenToWorldPoint(pos);
            if (Wyspa.Input.ClickRouter.Pick(world) != null)
                return true;
            var island = Island.Instance;
            return island != null && island.Ready && island.Map.Owns(Island.WorldToTile(world));
        }

        // ------------------------------------------------------------ przeciąganie i klik przez tło

        void Update()
        {
            if (!Supported || Hwnd == IntPtr.Zero)
                return;
            if (Frameless)
                HandleDrag();
            if (transparent && !dragging)
                UpdateClickThrough();
        }

        void UpdateClickThrough()
        {
            WinApi.GetCursorPos(out var p);
            var c = p;
            WinApi.ScreenToClient(hwnd, ref c);
            WinApi.GetClientRect(hwnd, out var cr);
            if (c.x < 0 || c.y < 0 || c.x >= cr.right || c.y >= cr.bottom)
                return;   // kursor poza oknem: zostaje jak było
            SetClickThrough(!IsSolidAt(new Vector2(c.x, cr.bottom - 1 - c.y)));
        }

        bool CanDragFrom(Vector2 pos)
        {
            if (DebugHud.IsPointerOverHud(pos) || PlacementMode.Active)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            var ui = UiRoot.Instance;
            if (ui != null && (ui.Expansion.IsActive || ui.Book.IsOpen || ui.Wheel.IsOpen))
                return false;
            var cam = Camera.main;
            return cam != null && Wyspa.Input.ClickRouter.Pick(cam.ScreenToWorldPoint(pos)) == null;
        }

        void HandleDrag()
        {
            if (!dragging)
            {
                if (UiInput.LeftPressed && CanDragFrom(UiInput.MousePosition))
                {
                    dragging = true;
                    dragMoved = false;
                    WinApi.GetCursorPos(out dragCursor);
                    WinApi.GetWindowRect(hwnd, out dragWindow);
                }
                return;
            }
            if ((WinApi.GetAsyncKeyState(WinApi.VK_LBUTTON) & 0x8000) == 0)
            {
                dragging = false;
                if (dragMoved)
                    EndDrag();
                return;
            }
            WinApi.GetCursorPos(out var p);
            int dx = p.x - dragCursor.x, dy = p.y - dragCursor.y;
            if (!dragMoved && dx * dx + dy * dy > 16)
                dragMoved = true;
            if (dragMoved)
                WinApi.SetWindowPos(hwnd, IntPtr.Zero, dragWindow.left + dx, dragWindow.top + dy, 0, 0,
                    WinApi.SWP_NOSIZE | WinApi.SWP_NOZORDER | WinApi.SWP_NOACTIVATE);
        }

        /// <summary>Puszczone blisko rogu ekranu: okno przykleja się do niego. Inaczej zapamiętujemy położenie.</summary>
        void EndDrag()
        {
            WinApi.GetWindowRect(hwnd, out var r);
            var work = WinApi.WorkArea(hwnd);
            bool left = Mathf.Abs(r.left - work.left) <= SnapDistance;
            bool right = Mathf.Abs(work.right - r.right) <= SnapDistance;
            bool top = Mathf.Abs(r.top - work.top) <= SnapDistance;
            bool bottom = Mathf.Abs(work.bottom - r.bottom) <= SnapDistance;
            var snap = ScreenCorner.None;
            if (top && left) snap = ScreenCorner.TopLeft;
            else if (top && right) snap = ScreenCorner.TopRight;
            else if (bottom && left) snap = ScreenCorner.BottomLeft;
            else if (bottom && right) snap = ScreenCorner.BottomRight;
            if (snap != ScreenCorner.None)
            {
                Corner = snap;
                return;
            }
            corner = ScreenCorner.None;
            PlayerPrefs.SetInt("winCorner", 0);
            PlayerPrefs.SetInt("winX", r.left);
            PlayerPrefs.SetInt("winY", r.top);
            Changed?.Invoke();
        }
    }

    /// <summary>Start razem z Windows: wpis w HKCU\...\Run (bez uprawnień administratora). Tylko build.</summary>
    public static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "Pockland";

        public static bool Supported => WinApi.Supported;

        public static bool Enabled
        {
            get
            {
                if (!Supported)
                    return false;
                var sb = new System.Text.StringBuilder(1024);
                uint size = (uint)sb.Capacity * 2;
                return WinApi.RegGetValueW(WinApi.HKEY_CURRENT_USER, RunKey, ValueName, WinApi.RRF_RT_REG_SZ, IntPtr.Zero, sb, ref size) == 0;
            }
            set
            {
                if (!Supported)
                    return;
                if (value)
                {
                    string cmd = "\"" + WinApi.ExePath() + "\"";
                    WinApi.RegSetKeyValueW(WinApi.HKEY_CURRENT_USER, RunKey, ValueName, WinApi.REG_SZ, cmd, (uint)(cmd.Length + 1) * 2);
                }
                else
                    WinApi.RegDeleteKeyValueW(WinApi.HKEY_CURRENT_USER, RunKey, ValueName);
            }
        }
    }
}
