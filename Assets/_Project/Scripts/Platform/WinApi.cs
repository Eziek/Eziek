using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Wyspa.Platform
{
    /// <summary>
    /// Wywołania WinAPI dla okna gry (GDD §11.3 I). Tylko build na Windows: w edytorze nie ruszamy okna Unity.
    /// </summary>
    static class WinApi
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif

        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_SYSMENU = 0x00080000,
                          WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000;
        public const long WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);
        public const uint LWA_ALPHA = 0x2;
        public const int VK_LBUTTON = 0x01;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS { public int left, right, top, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentProcessId();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hwnd, ref POINT p);
        [DllImport("user32.dll")] public static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern uint GetModuleFileNameW(IntPtr module, StringBuilder name, int size);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegSetKeyValueW(IntPtr key, string subKey, string valueName, uint type, string data, uint dataBytes);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegDeleteKeyValueW(IntPtr key, string subKey, string valueName);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegGetValueW(IntPtr key, string subKey, string valueName, uint flags, IntPtr type, StringBuilder data, ref uint dataBytes);

        public static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        public const uint REG_SZ = 1, RRF_RT_REG_SZ = 0x2;

        static IntPtr found;

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        static bool OnWindow(IntPtr hwnd, IntPtr lParam)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != (uint)lParam.ToInt64())
                return true;
            var name = new StringBuilder(64);
            GetClassNameW(hwnd, name, name.Capacity);
            if (name.ToString() != "UnityWndClass")
                return true;
            found = hwnd;
            return false;
        }

        /// <summary>Główne okno gry (klasa UnityWndClass w naszym procesie).</summary>
        public static IntPtr FindGameWindow()
        {
            found = IntPtr.Zero;
            EnumWindows(OnWindow, new IntPtr(GetCurrentProcessId()));
            return found;
        }

        public static long Style(IntPtr hwnd, int index) => GetWindowLongPtr(hwnd, index).ToInt64();
        public static void SetStyle(IntPtr hwnd, int index, long value) => SetWindowLongPtr(hwnd, index, new IntPtr(value));

        /// <summary>Obszar roboczy monitora z oknem (bez paska zadań).</summary>
        public static RECT WorkArea(IntPtr hwnd)
        {
            var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
            return info.rcWork;
        }

        public static string ExePath()
        {
            var sb = new StringBuilder(1024);
            GetModuleFileNameW(IntPtr.Zero, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
