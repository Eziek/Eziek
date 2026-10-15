using UnityEngine;
using UnityEngine.EventSystems;
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
            if (PlacementMode.Active || PlacementMode.ConsumedFrame == Time.frameCount)
                return;   // klik należy do trybu stawiania
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;   // klik w UI (księga, koło nasion, przyciski) nie trafia w wyspę

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return;

            Vector2 world = cam.ScreenToWorldPoint(screenPos);
            var hit = Pick(world);
            if (hit != null && hit.TryGetComponent(out IClickable clickable))
                clickable.OnClick();
        }

        /// <summary>Collider pod punktem: zwykłe obiekty mają pierwszeństwo przed obszarami o niskim priorytecie (łowisko).</summary>
        public static Collider2D Pick(Vector2 world)
        {
            var hits = Physics2D.OverlapPointAll(world);
            if (hits == null || hits.Length == 0)
                return null;
            foreach (var h in hits)
                if (h != null && h.TryGetComponent(out IPriorityClick _))
                    return h;
            foreach (var h in hits)
                if (h != null && !h.TryGetComponent(out ILowPriorityClick _))
                    return h;
            return hits[0];
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
