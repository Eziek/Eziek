using System.Collections;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;

namespace Wyspa.Automation
{
    /// <summary>
    /// Ognik-ogrodnik: co N sekund gry zbiera jeden dojrzały plon do skrzyni.
    /// Logika działa w tickach GameLoop; lot do grządki i z powrotem to tylko efekt wizualny.
    /// </summary>
    public class WispGardener : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Sprite[] frames = new Sprite[4];
        [SerializeField] Sprite[] glowFrames = new Sprite[4];
        [SerializeField] float flightSeconds = 0.5f;

        Vector3 home;
        float timer;
        Coroutine flight;
        bool warnedFull;   // toast o pełnej skrzyni tylko raz, aż skrzynia się opróżni

        public float SecondsToNext(float interval) => Mathf.Max(0f, interval - timer);

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
        }

        void Awake() => home = transform.position;

        /// <summary>Zwraca zebraną grządkę (albo null), gdy minął odstęp i jest co zbierać.</summary>
        public FarmPlot Tick(float dt, float interval, HarvestCrate crate)
        {
            timer += dt;
            if (crate == null)
                return null;
            if (crate.IsFull)
            {
                if (!warnedFull && AnyRipe())
                {
                    warnedFull = true;
                    GameEvents.RaiseToast("toast.crate_full");
                }
                return null;
            }
            warnedFull = false;
            if (timer < interval)
                return null;
            foreach (var plot in FarmPlot.All)
            {
                if (!plot.State.IsRipe)
                    continue;
                timer = 0f;
                FlyTo(plot.transform.position);
                return plot;
            }
            timer = interval;   // nic dojrzałego - czeka gotowy na pierwszy plon
            return null;
        }

        static bool AnyRipe()
        {
            foreach (var plot in FarmPlot.All)
                if (plot.State.IsRipe)
                    return true;
            return false;
        }

        void FlyTo(Vector3 target)
        {
            if (!isActiveAndEnabled)
                return;
            if (flight != null)
                StopCoroutine(flight);
            flight = StartCoroutine(Flight(target));
        }

        IEnumerator Flight(Vector3 target)
        {
            // skokowo ok. 10 kl./s, jak reszta animacji (GDD §5.2)
            const float step = 0.1f;
            var wait = new WaitForSecondsRealtime(step);
            int n = Mathf.Max(1, Mathf.RoundToInt(flightSeconds / step));
            for (int i = 1; i <= n; i++) { transform.position = Vector3.Lerp(home, target, i / (float)n); yield return wait; }
            for (int i = 1; i <= n; i++) { transform.position = Vector3.Lerp(target, home, i / (float)n); yield return wait; }
            transform.position = home;
            flight = null;
        }

        void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            int f = Mathf.FloorToInt(Time.unscaledTime * 8f) % frames.Length;   // falowanie płomienia
            if (body != null) body.sprite = frames[f];
            if (glow != null && glowFrames != null && f < glowFrames.Length) glow.sprite = glowFrames[f];
        }
    }
}
