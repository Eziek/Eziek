using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
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

    /// <summary>Płynny lot duszków (prośba gracza: bez skoków): łuk w górę między punktami, wygładzony start i lądowanie.</summary>
    public static class Flight
    {
        /// <summary>Lot z a do b z prędkością kafelki/s; łuk wznosi się o arc (jednostki świata) w połowie drogi.</summary>
        public static IEnumerator Arc(Transform t, Vector3 a, Vector3 b, float tilesPerSecond, float arc = 0.35f)
        {
            float duration = Mathf.Max(0.25f, Vector3.Distance(a, b) / Mathf.Max(0.1f, tilesPerSecond));
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                float u = time / duration;
                float e = u * u * (3f - 2f * u);   // smoothstep: łagodny start i lądowanie
                t.position = Vector3.Lerp(a, b, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * arc, 0f);
                yield return null;
            }
            t.position = b;
        }

        /// <summary>Delikatne unoszenie w miejscu (zamiast skaczących klatek sprite'a).</summary>
        public static Vector3 Hover(Vector3 home, float seed) =>
            home + new Vector3(Mathf.Sin(Time.unscaledTime * 0.7f + seed) * 0.04f, Mathf.Sin(Time.unscaledTime * 1.6f + seed) * 0.07f, 0f);
    }

    /// <summary>
    /// Ognik-ogrodnik (Etap 6b, kursy od prośby gracza): co N sekund gry wylatuje na kurs - zbiera do 3 dojrzałych plonów
    /// w promieniu 2 kafelków (5x5), niesie je do najbliższej skrzyni z miejscem i wraca. Lot jest płynny (po łuku),
    /// a plon trafia do zbioru w chwili, gdy ognik dotrze do grządki. Ogników może być wiele (grządki rezerwują się na kurs).
    /// </summary>
    public class WispGardener : MonoBehaviour, IAreaEffect, IContextActions
    {
        public const int Radius = 2;
        /// <summary>Ile plonów ognik zbiera na jeden kurs.</summary>
        public const int CarryCapacity = 3;
        const float Speed = 3f;   // kafelki na sekundę

        public static readonly List<WispGardener> All = new List<WispGardener>();
        /// <summary>Grządki, po które już leci jakiś ognik.</summary>
        static readonly HashSet<FarmPlot> reserved = new HashSet<FarmPlot>();

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Sprite[] frames = new Sprite[4];
        [SerializeField] Sprite[] glowFrames = new Sprite[4];

        Vector3 home;
        float timer;
        float lastInterval = 30f;
        float seed;
        Coroutine trip;
        bool warnedFull;   // toast o pełnych skrzyniach tylko raz, aż zrobi się miejsce
        readonly List<FarmPlot> route = new List<FarmPlot>();
        readonly List<(CropDefinition crop, int quality)> carried = new List<(CropDefinition, int)>();
        readonly List<SpriteRenderer> carriedSprites = new List<SpriteRenderer>();

        public int AreaRadius => Radius;
        public bool OnTrip => trip != null;
        public float SecondsToNext(float interval) => Mathf.Max(0f, interval - timer);

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
        }

        void Awake()
        {
            home = transform.position;
            seed = Random.value * 10f;
        }

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            EndTrip(true);
        }

        /// <summary>Island postawił (albo przeniósł) ognika - nowy punkt powrotu; niesione plony od razu do skrzyni.</summary>
        void OnPlaced()
        {
            EndTrip(true);
            home = transform.position;
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

        static int FreeCrateSpace()
        {
            int free = 0;
            foreach (var c in HarvestCrate.All)
                free += Mathf.Max(0, c.Capacity - c.Count);
            // miejsce zajęte przez plony, które inne ogniki już niosą albo po nie lecą
            foreach (var w in All)
                free -= w.carried.Count + w.route.Count;
            return free;
        }

        /// <summary>Gdy minął odstęp: kurs po dojrzałe plony w zasięgu (najbliższy, potem najbliższy od niego).</summary>
        public void Tick(float dt, float interval)
        {
            lastInterval = interval;
            if (trip != null)
                return;
            timer += dt;
            if (timer < interval)
                return;

            int space = Mathf.Min(CarryCapacity, FreeCrateSpace());
            var ripe = new List<FarmPlot>();
            foreach (var plot in FarmPlot.All)
                if (plot.State.IsRipe && !reserved.Contains(plot) && InRange(plot))
                    ripe.Add(plot);
            if (ripe.Count == 0)
            {
                timer = interval;   // nic dojrzałego - czeka gotowy na pierwszy plon
                return;
            }
            if (space <= 0)
            {
                timer = interval;
                if (!warnedFull)
                {
                    warnedFull = true;
                    GameEvents.RaiseToast(HarvestCrate.All.Count == 0 ? "toast.wisp.no_crate" : "toast.crate_full");
                }
                return;
            }
            warnedFull = false;
            timer = 0f;

            route.Clear();
            var from = home;
            while (route.Count < space && ripe.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < ripe.Count; i++)
                    if ((ripe[i].transform.position - from).sqrMagnitude < (ripe[best].transform.position - from).sqrMagnitude)
                        best = i;
                route.Add(ripe[best]);
                reserved.Add(ripe[best]);
                from = ripe[best].transform.position;
                ripe.RemoveAt(best);
            }
            if (isActiveAndEnabled)
                trip = StartCoroutine(Trip());
        }

        IEnumerator Trip()
        {
            var pos = transform.position;
            while (route.Count > 0)
            {
                var plot = route[0];
                if (plot != null && plot.isActiveAndEnabled)
                {
                    var target = plot.CropPosition + new Vector3(0f, 0.4f, 0f);
                    yield return Flight.Arc(transform, pos, target, Speed);
                    pos = target;
                    Collect(plot);
                }
                route.RemoveAt(0);
                reserved.Remove(plot);
            }
            if (carried.Count > 0)
            {
                var crate = HarvestCrate.NearestWithSpace(pos);
                if (crate != null)
                {
                    var target = crate.transform.position + new Vector3(0f, 0.7f, 0f);
                    yield return Flight.Arc(transform, pos, target, Speed);
                    pos = target;
                }
                Deliver(crate);
            }
            yield return Flight.Arc(transform, pos, home, Speed);
            trip = null;
        }

        /// <summary>Zbiór na miejscu: grządka pusta, plon w „koszyku” ognika (mały sprite pod nim).</summary>
        void Collect(FarmPlot plot)
        {
            if (!plot.State.IsRipe)
                return;
            int quality = plot.State.Quality;
            var crop = plot.HarvestSilently();
            if (crop == null)
                return;
            carried.Add((crop, quality));
            ShowCarried();
            var game = GameLoop.Instance;
            if (game != null)
                game.AfterHarvest(plot, crop);   // bestiariusz, liczniki cyklu, mutacje
        }

        /// <summary>Plony do skrzyni (najbliższej, potem innych z miejscem). Gdy nigdzie nie ma miejsca: sprzedaż po 90%.</summary>
        void Deliver(HarvestCrate preferred)
        {
            var game = GameLoop.Instance;
            foreach (var (crop, quality) in carried)
            {
                if (preferred != null && preferred.TryAdd(crop, quality))
                    continue;
                var other = HarvestCrate.NearestWithSpace(transform.position);
                if (other != null && other.TryAdd(crop, quality))
                    continue;
                if (game != null)
                    game.Wallet.Add(Mathf.RoundToInt(crop.SellPrice(quality) * MerchantSpirit.PriceMultiplier));
            }
            carried.Clear();
            ShowCarried();
        }

        void EndTrip(bool deliverNow)
        {
            if (trip != null)
            {
                StopCoroutine(trip);
                trip = null;
            }
            foreach (var p in route)
                reserved.Remove(p);
            route.Clear();
            if (deliverNow && carried.Count > 0)
                Deliver(HarvestCrate.NearestWithSpace(transform.position));
        }

        /// <summary>Niesione plony jako małe sprite'y pod ognikiem (pół rozmiaru, jeden obok drugiego).</summary>
        void ShowCarried()
        {
            while (carriedSprites.Count < carried.Count)
            {
                var go = new GameObject("Carried");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = body != null ? body.sortingOrder - 1 : 0;
                carriedSprites.Add(sr);
            }
            for (int i = 0; i < carriedSprites.Count; i++)
            {
                bool on = i < carried.Count;
                carriedSprites[i].enabled = on;
                if (!on)
                    continue;
                carriedSprites[i].sprite = carried[i].crop.StageSprite(3);
                carriedSprites[i].transform.localPosition = new Vector3((i - (carried.Count - 1) / 2f) * 0.35f, -0.15f, 0f);
            }
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.wisp.range", Radius * 2 + 1));
            info.lines.Add(trip != null ? Loc.Get("ui.wisp.flying", carried.Count + route.Count)
                                        : Loc.Get("ui.wisp.next", Mathf.CeilToInt(SecondsToNext(lastInterval))));
        }

        void Update()
        {
            if (trip == null)
                transform.position = Flight.Hover(home, seed);
            if (frames == null || frames.Length == 0)
                return;
            int f = Mathf.FloorToInt(Time.unscaledTime * 8f) % frames.Length;   // falowanie płomienia
            if (body != null) body.sprite = frames[f];
            if (glow != null && glowFrames != null && f < glowFrames.Length) glow.sprite = glowFrames[f];
        }
    }
}
