using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Platform
{
    /// <summary>
    /// Ikona w zasobniku Windows (Etap 6c, tylko build na Windows). Lewy klik: pokaż / schowaj okno gry. Prawy klik: menu
    /// (pokaż/schowaj, zawsze na wierzchu, mini-tryb, wyjście). Powiadomienia Windows, gdy okno jest schowane albo nieaktywne:
    /// kupiec-widmo, potwór, zamówienia, chowaniec, gotowa mikstura i dojrzałe plony (najwyżej co minutę, opcja w Opcjach → System).
    /// Komunikaty ikony odbiera osobne, nigdy niepokazywane okno, które Unity obsługuje w swojej pętli komunikatów;
    /// po restarcie Eksploratora (komunikat „TaskbarCreated”) ikona dodaje się od nowa.
    /// </summary>
    public class TrayIcon : MonoBehaviour
    {
        public static TrayIcon Instance { get; private set; }

        public static bool NotificationsEnabled
        {
            get => PlayerPrefs.GetInt("notify", 1) == 1;
            set => PlayerPrefs.SetInt("notify", value ? 1 : 0);
        }

        /// <summary>Powiadomienia, które idą też do Windows (gdy okno nieaktywne).</summary>
        static readonly HashSet<string> NotifyKeys = new HashSet<string>
        {
            "toast.merchant.arrived", "toast.monster.rises", "toast.order.done", "toast.order.new",
            "toast.familiar.arrived", "toast.potion.ready",
        };

        float lastNotify = -100f;
        float ripeCheck = 30f;
        bool ripeNotified;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const uint WM_TRAY = 0x8001, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
        const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10;
        const uint NIIF_INFO = 0x1, NIIF_NOSOUND = 0x10;
        const uint MF_STRING = 0x0, MF_SEPARATOR = 0x800, MF_CHECKED = 0x8;
        const uint TPM_RIGHTBUTTON = 0x2, TPM_NONOTIFY = 0x80, TPM_RETURNCMD = 0x100;
        const int SW_HIDE = 0, SW_SHOW = 5;
        static uint taskbarCreated;   // komunikat po restarcie Eksploratora (pasek zadań odtworzony): ikonę trzeba dodać od nowa
        static bool readd;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersionOrTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEXW
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra, cbWndExtra;
            public IntPtr hInstance, hIcon, hCursor, hbrBackground;
            public string lpszMenuName, lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIconW(uint msg, ref NOTIFYICONDATAW data);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconExW(string file, int index, IntPtr[] large, IntPtr[] small, uint icons);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassExW(ref WNDCLASSEXW cls);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint exStyle, string cls, string name, uint style,
            int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessageW(string name);

        static WndProc procKeep;   // delegat trzymany statycznie, żeby GC go nie zebrał
        static readonly Queue<uint> clicks = new Queue<uint>();
        IntPtr msgWindow, icon;
        bool added, ownIcon, gameHidden;

        [AOT.MonoPInvokeCallback(typeof(WndProc))]
        static IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAY)
            {
                uint mouse = (uint)(lParam.ToInt64() & 0xFFFF);
                if (mouse == WM_LBUTTONUP || mouse == WM_RBUTTONUP)
                    clicks.Enqueue(mouse);   // obsługa w Update (poza procedurą okna)
                return IntPtr.Zero;
            }
            if (taskbarCreated != 0 && msg == taskbarCreated)
                readd = true;   // ponowne dodanie w Update
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        void Start()
        {
            var inst = GetModuleHandleW(null);
            procKeep = OnMessage;
            var cls = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(procKeep),
                hInstance = inst,
                lpszClassName = "MistyPacklandTray",
            };
            RegisterClassExW(ref cls);   // przy drugim uruchomieniu w tym samym procesie klasa już jest - to nie błąd
            taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
            // zwykłe, nigdy niepokazywane okno (nie HWND_MESSAGE): tylko takie dostaje rozgłaszany „TaskbarCreated”
            msgWindow = CreateWindowExW(0, "MistyPacklandTray", "Misty Packland tray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
            if (msgWindow == IntPtr.Zero)
                return;
            // ikona z pliku gry (ikona ustawiona w Player Settings), inaczej domyślna ikona aplikacji
            var small = new IntPtr[1];
            if (ExtractIconExW(WinApi.ExePath(), 0, null, small, 1) > 0 && small[0] != IntPtr.Zero)
            {
                icon = small[0];
                ownIcon = true;
            }
            else
                icon = LoadIconW(IntPtr.Zero, new IntPtr(32512));
            var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP);
            added = Shell_NotifyIconW(NIM_ADD, ref data);
        }

        NOTIFYICONDATAW Data(uint flags) => new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = msgWindow,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = WM_TRAY,
            hIcon = icon,
            szTip = Loc.Get("game.title"),
            szInfo = "",
            szInfoTitle = "",
        };

        void OnDestroy()
        {
            if (added)
            {
                var data = Data(0);
                Shell_NotifyIconW(NIM_DELETE, ref data);
                added = false;
            }
            if (ownIcon && icon != IntPtr.Zero)
                DestroyIcon(icon);
            icon = IntPtr.Zero;
            if (msgWindow != IntPtr.Zero)
                DestroyWindow(msgWindow);
            msgWindow = IntPtr.Zero;
            if (Instance == this)
                Instance = null;
        }

        void OnApplicationQuit() => OnDestroy();

        void HandleClicks()
        {
            if (readd && msgWindow != IntPtr.Zero)
            {
                readd = false;
                var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP);
                added = Shell_NotifyIconW(NIM_ADD, ref data);
            }
            while (clicks.Count > 0)
            {
                uint c = clicks.Dequeue();
                if (c == WM_LBUTTONUP)
                    ToggleWindow();
                else
                    ShowMenu();
            }
        }

        void ToggleWindow()
        {
            var game = WinApi.FindGameWindow();
            if (game == IntPtr.Zero)
                return;
            gameHidden = !gameHidden;
            ShowWindow(game, gameHidden ? SW_HIDE : SW_SHOW);
            if (!gameHidden)
                SetForegroundWindow(game);
        }

        void ShowMenu()
        {
            var game = WinApi.FindGameWindow();
            var desk = DesktopWindow.Instance;
            var view = ViewController.Instance;
            var menu = CreatePopupMenu();
            AppendMenuW(menu, MF_STRING, (UIntPtr)1, Loc.Get(gameHidden ? "ui.tray.show" : "ui.tray.hide"));
            AppendMenuW(menu, MF_STRING | (desk != null && desk.TopMost ? MF_CHECKED : 0), (UIntPtr)2, Loc.Get("ui.tray.topmost"));
            AppendMenuW(menu, MF_STRING | (view != null && view.Mini ? MF_CHECKED : 0), (UIntPtr)3, Loc.Get("ui.tray.mini"));
            AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenuW(menu, MF_STRING, (UIntPtr)4, Loc.Get("act.quit"));
            WinApi.GetCursorPos(out var p);
            // menu musi mieć właściciela na pierwszym planie, inaczej nie znika po kliknięciu obok
            SetForegroundWindow(game);
            int cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_NONOTIFY | TPM_RETURNCMD, p.x, p.y, 0, game, IntPtr.Zero);
            PostMessageW(game, 0, IntPtr.Zero, IntPtr.Zero);
            DestroyMenu(menu);
            switch (cmd)
            {
                case 1: ToggleWindow(); break;
                case 2: if (desk != null) desk.TopMost = !desk.TopMost; break;
                case 3: if (view != null) view.Mini = !view.Mini; break;
                case 4: UI.UiRoot.QuitGame(); break;
            }
        }

        void Notify(string title, string text)
        {
            if (!added)
                return;
            var data = Data(NIF_INFO);
            data.szInfoTitle = title.Length > 63 ? title.Substring(0, 63) : title;
            data.szInfo = text.Length > 255 ? text.Substring(0, 255) : text;
            data.dwInfoFlags = NIIF_INFO | NIIF_NOSOUND;
            Shell_NotifyIconW(NIM_MODIFY, ref data);
        }

        bool WindowAway => gameHidden || !Application.isFocused;
#else
        void HandleClicks() { }
        void Notify(string title, string text) { }
        bool WindowAway => false;
#endif

        void Awake() => Instance = this;
#if !(UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        void OnDestroy() { if (Instance == this) Instance = null; }
#endif
        void OnEnable() => GameEvents.Toast += OnToast;
        void OnDisable() => GameEvents.Toast -= OnToast;

        void OnToast(string key, object[] args)
        {
            if (!NotifyKeys.Contains(key))
                return;
            TryNotify(Loc.Get(key, args));
        }

        void TryNotify(string text)
        {
            if (!NotificationsEnabled || !WindowAway || Save.SaveSystem.InMenu || Time.unscaledTime - lastNotify < 60f)
                return;
            lastNotify = Time.unscaledTime;
            Notify(Loc.Get("game.title"), text);
        }

        void Update()
        {
            HandleClicks();
            // dojrzałe plony: raz, gdy czekają co najmniej 3 (i znowu dopiero po zbiorze)
            ripeCheck -= Time.unscaledDeltaTime;
            if (ripeCheck > 0f)
                return;
            ripeCheck = 30f;
            int ripe = 0;
            foreach (var p in FarmPlot.All)
                if (p.State.IsRipe) ripe++;
            if (ripe >= 3 && !ripeNotified)
            {
                ripeNotified = true;
                TryNotify(Loc.Get("ui.tray.ripe", ripe));
            }
            else if (ripe == 0)
                ripeNotified = false;
        }
    }
}
