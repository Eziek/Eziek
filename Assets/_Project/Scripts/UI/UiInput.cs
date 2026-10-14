using UnityEngine;

namespace Wyspa.UI
{
    /// <summary>Wejście dla UI - działa z Input System i ze starym Input.</summary>
    public static class UiInput
    {
#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Mouse M => UnityEngine.InputSystem.Mouse.current;
        static UnityEngine.InputSystem.Keyboard K => UnityEngine.InputSystem.Keyboard.current;
        public static Vector2 MousePosition => M != null ? M.position.ReadValue() : Vector2.zero;
        public static bool LeftPressed => M != null && M.leftButton.wasPressedThisFrame;
        public static bool RightPressed => M != null && M.rightButton.wasPressedThisFrame;
        public static bool RightHeld => M != null && M.rightButton.isPressed;
        public static bool RightReleased => M != null && M.rightButton.wasReleasedThisFrame;
        public static bool EscapePressed => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool F1Pressed => K != null && K.f1Key.wasPressedThisFrame;
        /// <summary>Kółko myszy w tej klatce: +1 w górę, -1 w dół, 0 brak.</summary>
        public static int Scroll => M == null ? 0 : Step(M.scroll.ReadValue().y);
        public static bool CtrlHeld => K != null && (K.leftCtrlKey.isPressed || K.rightCtrlKey.isPressed);
#else
        public static Vector2 MousePosition => UnityEngine.Input.mousePosition;
        public static bool LeftPressed => UnityEngine.Input.GetMouseButtonDown(0);
        public static bool RightPressed => UnityEngine.Input.GetMouseButtonDown(1);
        public static bool RightHeld => UnityEngine.Input.GetMouseButton(1);
        public static bool RightReleased => UnityEngine.Input.GetMouseButtonUp(1);
        public static bool EscapePressed => UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        public static bool F1Pressed => UnityEngine.Input.GetKeyDown(KeyCode.F1);
        public static int Scroll => Step(UnityEngine.Input.mouseScrollDelta.y);
        public static bool CtrlHeld => UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
#endif

        // Mathf.Sign(0) zwraca 1, więc znak liczymy sami
        static int Step(float v) => v > 0.01f ? 1 : (v < -0.01f ? -1 : 0);
    }
}
