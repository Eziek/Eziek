using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Automation
{
    /// <summary>Budowla działająca na kafelkach wokół siebie (runa siewu, ognik): zasięg do podglądu w trybie stawiania.</summary>
    public interface IAreaEffect
    {
        /// <summary>Promień w kafelkach (1 = 3x3, 2 = 5x5).</summary>
        int AreaRadius { get; }
    }

    /// <summary>
    /// Ognik-ogrodnik (Etap 6b): stoi na własnym kafelku i co N sekund gry zbiera jeden dojrzały plon w promieniu
    /// 2 kafelków (5x5), a potem niesie go do najbliższej skrzyni z miejscem. Ogników może być wiele.
    /// Logika działa w tickach GameLoop; lot do grządki, skrzyni i z powrotem to tylko efekt wizualny.
    /// </summary>
    public class WispGardener : MonoBehaviour, IAreaEffect, IContextActions
    {
        public const int Radius = 2;
        public static readonly List<WispGardener> All = new List<WispGardener>();

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Sprite[] frames = new Sprite[4];
        [SerializeField] Sprite[] glowFrames = new Sprite[4];
        [SerializeField] float flightSeconds = 0.5f;

        Vector3 home;
        float timer;
        float lastInterval = 30f;
        Coroutine flight;
        bool warnedFull;   // toast o pełnych skrzyniach tylko raz, aż zrobi się miejsce

        public int AreaRadius => Radius;
        public float SecondsToNext(float interval) => Mathf.Max(0f, interval - timer);

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
        }

        void Awake() => home = transform.position;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Island postawił (albo przeniósł) ognika - nowy punkt powrotu.</summary>
        void OnPlaced()
        {
            home = transform.position;
            if (flight != null)
            {
                StopCoroutine(flight);
                flight = null;
            }
        }

        TilePos Tile
        {
            get
            {
                var obj = GetComponent<TileObject>();
                return obj != null && obj.Placed ? obj.Tile : Island.WorldToTile(home);
            }
        }

        bool InRange(FarmPlot plot)
        {
            var t = Tile;
            return Mathf.Abs(plot.Cell.x - t.c) <= Radius && Mathf.Abs(plot.Cell.y - t.r) <= Radius;
        }

        /// <summary>Zwraca zebraną grządkę i skrzynię, do której trafi plon (albo null), gdy minął odstęp i jest co zbierać.</summary>
        public FarmPlot Tick(float dt, float interval, out HarvestCrate crate)
        {
            lastInterval = interval;
            timer += dt;
            crate = null;
            if (timer < interval)
                return null;
            FarmPlot ripe = null;
            foreach (var plot in FarmPlot.All)
                if (plot.State.IsRipe && InRange(plot))
                {
                    ripe = plot;
                    break;
                }
            if (ripe == null)
            {
                timer = interval;   // nic dojrzałego - czeka gotowy na pierwszy plon
                return null;
            }
            crate = HarvestCrate.NearestWithSpace(ripe.transform.position);
            if (crate == null)
            {
                timer = interval;
                if (!warnedFull)
                {
                    warnedFull = true;
                    GameEvents.RaiseToast(HarvestCrate.All.Count == 0 ? "toast.wisp.no_crate" : "toast.crate_full");
                }
                return null;
            }
            warnedFull = false;
            timer = 0f;
            Fly(ripe.transform.position, crate.transform.position);
            return ripe;
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.wisp.range", Radius * 2 + 1));
            info.lines.Add(Loc.Get("ui.wisp.next", Mathf.CeilToInt(SecondsToNext(lastInterval))));
        }

        void Fly(Vector3 plot, Vector3 crate)
        {
            if (!isActiveAndEnabled)
                return;
            if (flight != null)
                StopCoroutine(flight);
            flight = StartCoroutine(Flight(plot, crate));
        }

        IEnumerator Flight(Vector3 plot, Vector3 crate)
        {
            // skokowo ok. 10 kl./s, jak reszta animacji (GDD §5.2): do grządki, do skrzyni, do domu
            const float step = 0.1f;
            var wait = new WaitForSecondsRealtime(step);
            int n = Mathf.Max(1, Mathf.RoundToInt(flightSeconds / step));
            var path = new[] { home, plot, crate, home };
            for (int leg = 0; leg < 3; leg++)
                for (int i = 1; i <= n; i++)
                {
                    transform.position = Vector3.Lerp(path[leg], path[leg + 1], i / (float)n);
                    yield return wait;
                }
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
