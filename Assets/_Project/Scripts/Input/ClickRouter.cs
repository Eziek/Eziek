using UnityEngine;
using UnityEngine.EventSystems;
using Wyspa.UI;
using Wyspa.World;

namespace Wyspa.Input
{
    /// <summary>
    /// Klik myszą albo stuknięcie palcem -> Collider2D pod kursorem -> IClickable. Działa z Input System i starym Input.
    /// Obiekty do ścięcia (IHoldAction, GDD §9 p. 136): wciśnięcie zaczyna przytrzymanie z kółkiem postępu, krótki klik
    /// (puszczenie przed TapTime) to zwykły OnClick, puszczenie później albo zjazd z obiektu kasuje ścinanie.
    /// </summary>
    public class ClickRouter : MonoBehaviour
    {
        /// <summary>Krótszy wciśnięty klik w obiekt do ścięcia to zwykły klik (zbiór owoców, panel).</summary>
        public const float TapTime = 0.25f;
        /// <summary>Kółko pojawia się dopiero po tylu sekundach (krótki klik nie miga kółkiem).</summary>
        const float ShowAfter = 0.12f;

        [SerializeField] Camera targetCamera;

        /// <summary>Każde kliknięcie w świat: punkt i trafiony collider (null = nic, np. woda). Dla efektów (Visual/CursorPlay).</summary>
        public static event System.Action<Vector2, Collider2D> Clicked;

        /// <summary>Trwa przytrzymanie, by ściąć (np. kursor gry, dymki).</summary>
        public static bool Holding => holdCollider != null;

        static Collider2D holdCollider;
        static IHoldAction holdTarget;
        static float holdStart;

        void Update()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (holdCollider != null)
            {
                UpdateHold(cam);
                return;
            }
            bool touch = UiInput.TouchUsed;
            var screenPos = UiInput.MousePosition;
            if (UiInput.PointerDown && !Blocked(screenPos) && cam != null)
            {
                Vector2 world = cam.ScreenToWorldPoint(screenPos);
                var hit = Pick(world);
                if (hit != null && hit.TryGetComponent(out IHoldAction hold) && hold.CanHold)
                {
                    // przytrzymanie, by ściąć: dotyk nie będzie już stuknięciem ani panelem informacji
                    holdCollider = hit;
                    holdTarget = hold;
                    holdStart = Time.unscaledTime;
                    UiInput.ClaimPointer();
                    return;
                }
                if (!touch)
                {
                    Click(world, hit);   // mysz: klik od razu przy wciśnięciu, jak dotąd
                    return;
                }
            }
            // dotyk: stuknięcie liczy się przy podniesieniu palca (przytrzymanie to panel informacji)
            if (touch && UiInput.LeftPressed && !Blocked(screenPos) && cam != null)
            {
                Vector2 world = cam.ScreenToWorldPoint(screenPos);
                Click(world, Pick(world));
            }
        }

        static void Click(Vector2 world, Collider2D hit)
        {
            if (hit != null && hit.TryGetComponent(out IClickable clickable))
                clickable.OnClick();
            Clicked?.Invoke(world, hit);
        }

        static bool Blocked(Vector2 screenPos)
        {
            if (DebugHud.IsPointerOverHud(screenPos))
                return true;
            if (PlacementMode.Active || PlacementMode.ConsumedFrame == Time.frameCount)
                return true;   // klik należy do trybu stawiania
            // klik w UI (księga, koło nasion, przyciski) nie trafia w wyspę
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void UpdateHold(Camera cam)
        {
            float t = Time.unscaledTime - holdStart;
            var target = holdTarget;
            var col = holdCollider;
            Vector2 world = cam != null ? (Vector2)cam.ScreenToWorldPoint(UiInput.MousePosition) : Vector2.zero;
            if (UiInput.PointerUp)
            {
                // krótko: zwykły klik (drzewo z owocami zbiera, kamień pokazuje panel); dłużej: puszczone przed końcem, nic
                bool quick = t < TapTime;
                EndHold();
                if (quick && col != null)
                    Click(world, col);
                return;
            }
            // palec przesunięty (przesuwanie widoku), kursor zjechał z obiektu, obiekt zniknął albo już się nie da: koniec
            if (!UiInput.PointerHeld || col == null || !col.enabled || !(target is Object o && o != null) || !target.CanHold
                || cam == null || !col.OverlapPoint(world) || PlacementMode.Active
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                EndHold();
                return;
            }
            float progress = t / Mathf.Max(0.1f, target.HoldSeconds);
            if (t >= ShowAfter)
            {
                ChopProgress.For(col.gameObject).Show(target.HoldAnchor, progress);
                target.HoldTick(Mathf.Clamp01(progress));
            }
            if (progress >= 1f)
            {
                EndHold();
                target.HoldComplete();
            }
        }

        static void EndHold()
        {
            if (holdCollider != null)
            {
                var cp = holdCollider.GetComponent<ChopProgress>();
                if (cp != null)
                    cp.Hide();
            }
            holdCollider = null;
            holdTarget = null;
        }

        void OnDisable() => EndHold();

        /// <summary>Collider pod punktem: zwykłe obiekty mają pierwszeństwo przed obszarami o niskim priorytecie (łowisko).</summary>
        public static Collider2D Pick(Vector2 world)
        {
            // kursor, dymek i okno pytają co klatkę o ten sam punkt: jedno zapytanie fizyki (i jedna tablica) na klatkę
            if (cacheFrame == Time.frameCount && cachePoint == world)
                return cacheHit;
            cacheFrame = Time.frameCount;
            cachePoint = world;
            cacheHit = PickNow(world);
            return cacheHit;
        }

        static int cacheFrame = -1;
        static Vector2 cachePoint;
        static Collider2D cacheHit;

        static Collider2D PickNow(Vector2 world)
        {
            var hits = Physics2D.OverlapPointAll(world);
            if (hits == null || hits.Length == 0)
                return null;
            // chowaniec wygrywa tylko klik blisko środka (nie zasłania grządki ani lampy, przy której akurat siedzi)
            foreach (var h in hits)
                if (h != null && h.TryGetComponent(out IPriorityClick _) && ((Vector2)h.bounds.center - world).sqrMagnitude < 0.3f * 0.3f)
                    return h;
            foreach (var h in hits)
                if (h != null && !h.TryGetComponent(out ILowPriorityClick _))
                    return h;
            return hits[0];
        }
    }
}
