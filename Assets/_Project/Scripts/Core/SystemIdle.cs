using System.Runtime.InteropServices;

namespace Wyspa.Core
{
    /// <summary>
    /// Czas od ostatniego ruchu myszy / klawisza w CAŁYM systemie (nie tylko w oknie gry).
    /// Windows: GetLastInputInfo z user32.dll - bez globalnych hooków, antywirusy nie reagują.
    /// </summary>
    public static class SystemIdle
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT p);

        /// <summary>Znacznik czasu ostatniego inputu w systemie (zmienia się przy każdym klawiszu, kliknięciu, ruchu myszy).</summary>
        public static uint LastInputStamp()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            return GetLastInputInfo(ref info) ? info.dwTime : 0u;
        }

        /// <summary>Pozycja kursora na ekranie (do odróżnienia ruchu myszy od pisania).</summary>
        public static bool CursorPosition(out int x, out int y)
        {
            bool ok = GetCursorPos(out var p);
            x = p.x;
            y = p.y;
            return ok;
        }

        public static bool IsSupported => true;

        public static float SecondsSinceLastInput()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info))
                return 0f;
            // unchecked: GetTickCount przekręca się co ~49 dni, odejmowanie uint to obsługuje
            return unchecked(GetTickCount() - info.dwTime) / 1000f;
        }
#else
        public static bool IsSupported => false;
        public static float SecondsSinceLastInput() => 0f;
        public static uint LastInputStamp() => 0u;
        public static bool CursorPosition(out int x, out int y) { x = y = 0; return false; }
#endif
    }
}
