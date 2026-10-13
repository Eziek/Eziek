using UnityEngine;
using Wyspa.UI;

namespace Wyspa.Input
{
    /// <summary>Klik myszą -> Collider2D pod kursorem -> IClickable. Działa z Input System i starym Input.</summary>
    public class ClickRouter : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;

        void Update()
        {
            if (!PointerPressed(out Vector2 screenPos))
                return;
            if (DebugHud.IsPointerOverHud(screenPos))
                return;

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return;

            Vector2 world = cam.ScreenToWorldPoint(screenPos);
            var hit = Physics2D.OverlapPoint(world);
            if (hit != null && hit.TryGetComponent(out IClickable clickable))
                clickable.OnClick();
        }

        static bool PointerPressed(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
            screenPos = default;
            return false;
#else
            screenPos = UnityEngine.Input.mousePosition;
            return UnityEngine.Input.GetMouseButtonDown(0);
#endif
        }
    }
}
