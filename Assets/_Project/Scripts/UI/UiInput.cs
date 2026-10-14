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
        public static bool RightPressed => M != null && M.rightButton.wasPressedThisFrame;
        public static bool EscapePressed => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool F1Pressed => K != null && K.f1Key.wasPressedThisFrame;
#else
        public static Vector2 MousePosition => UnityEngine.Input.mousePosition;
        public static bool RightPressed => UnityEngine.Input.GetMouseButtonDown(1);
        public static bool EscapePressed => UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        public static bool F1Pressed => UnityEngine.Input.GetKeyDown(KeyCode.F1);
#endif
    }
}
