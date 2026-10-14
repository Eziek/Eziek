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
    /// Gdy nie ma dojrzałych plonów, leci do owocującego drzewa w zasięgu: uderza w nie, owoce spadają na ziemię,
    /// a ognik zbiera je stamtąd (do 3 na kurs; reszta czeka pod drzewem na następny kurs albo na gracza).
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
        /// <summary>Drzewa, do których już leci jakiś ognik.</summary>
        static readonly HashSet<FruitTree> reservedTrees = new HashSet<FruitTree>();

        struct Carried
        {
            public CropDefinition crop;
            public int quality;
            public TreeSpecies fruit;   // owoc z drzewa zamiast plonu
            public Sprite sprite;
        }

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
        FruitTree treeTarget;
        int fruitToPick;   // ile owoców ognik jeszcze zbierze spod drzewa w tym kursie
        readonly List<Carried> carried = new List<Carried>();
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

        bool InRange(FruitTree tree)
        {
            var obj = tree.GetComponent<TileObject>();
            if (obj == null || !obj.Placed)
                return false;
            var t = Tile;
            return Mathf.Abs(obj.Tile.c - t.c) <= Radius && Mathf.Abs(obj.Tile.r - t.r) <= Radius;
        }

        static int FreeCrateSpace()
        {
            int free = 0;
            foreach (var c in HarvestCrate.All)
                free += Mathf.Max(0, c.Capacity - c.Count);
            // miejsce zajęte przez plony, które inne ogniki już niosą albo po nie lecą
            foreach (var w in All)
                free -= w.carried.Count + w.route.Count + w.fruitToPick;
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
            FruitTree tree = null;
            if (ripe.Count == 0)
                tree = NearestTree();
            if (ripe.Count == 0 && tree == null)
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

            if (tree != null)
            {
                int available = tree.FallenCount + (tree.Fruiting ? tree.Species.fruitCount : 0);
                treeTarget = tree;
                fruitToPick = Mathf.Min(space, available);
                reservedTrees.Add(tree);
                if (isActiveAndEnabled)
                    trip = StartCoroutine(TreeTrip());
                return;
            }

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

        /// <summary>Najbliższe drzewo w zasięgu z owocami na gałęziach albo na ziemi (bez innego ognika w drodze).</summary>
        FruitTree NearestTree()
        {
            FruitTree best = null;
            foreach (var t in FruitTree.All)
                if (t != null && t.HasFruitForWisp && !reservedTrees.Contains(t) && InRange(t)
                    && (best == null || (t.transform.position - home).sqrMagnitude < (best.transform.position - home).sqrMagnitude))
                    best = t;
            return best;
        }

        /// <summary>
        /// Kurs do drzewa: podlot do korony, dwa uderzenia (ognik wpada w liście i odskakuje), owoce spadają pod drzewo,
        /// potem ognik zbiera je z ziemi po kolei, niesie do skrzyni i wraca.
        /// </summary>
        IEnumerator TreeTrip()
        {
            var pos = transform.position;
            var tree = treeTarget;
            if (tree != null && tree.Fruiting)
            {
                float side = home.x < tree.transform.position.x ? -1f : 1f;
                var perch = tree.transform.position + new Vector3(side * 0.6f, 1.1f, 0f);
                yield return Flight.Arc(transform, pos, perch, Speed);
                pos = perch;
                var hit = tree.transform.position + new Vector3(side * 0.25f, 1.15f, 0f);
                for (int i = 0; i < 2 && tree != null; i++)
                {
                    yield return Flight.Arc(transform, pos, hit, 6f, 0.05f);    // szybkie wejście w koronę
                    if (i == 1 && tree != null)
                        tree.KnockDown();
                    yield return Flight.Arc(transform, hit, pos, 4f, 0.05f);    // odskok
                    yield return new WaitForSecondsRealtime(0.12f);
                }
                yield return new WaitForSecondsRealtime(0.6f);   // owoce dolatują do ziemi
            }
            while (tree != null && fruitToPick > 0 && tree.FallenCount > 0)
            {
                int nearest = 0;
                for (int i = 1; i < tree.FallenCount; i++)
                    if ((tree.FallenPosition(i) - pos).sqrMagnitude < (tree.FallenPosition(nearest) - pos).sqrMagnitude)
                        nearest = i;
                var target = tree.FallenPosition(nearest) + new Vector3(0f, 0.3f, 0f);
                yield return Flight.Arc(transform, pos, target, Speed, 0.15f);
                pos = target;
                if (tree == null)
                    break;
                var sprite = tree.PickFallen(transform.position);
                fruitToPick--;
                if (sprite == null && tree.FallenCount == 0)
                    break;
                carried.Add(new Carried { fruit = tree.Species, sprite = sprite });
                GameEvents.RaiseHarvested("fruit:" + tree.Species.id, 1);
                ShowCarried();
            }
            fruitToPick = 0;
            if (tree != null)
                reservedTrees.Remove(tree);
            treeTarget = null;
            yield return DeliverAndReturn(pos);
        }

        IEnumerator DeliverAndReturn(Vector3 pos)
        {
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
            yield return DeliverAndReturn(pos);
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
            carried.Add(new Carried { crop = crop, quality = quality, sprite = crop.StageSprite(3) });
            ShowCarried();
            var game = GameLoop.Instance;
            if (game != null)
                game.AfterHarvest(plot, crop);   // bestiariusz, liczniki cyklu, mutacje
        }

        /// <summary>Plony do skrzyni (najbliższej, potem innych z miejscem). Gdy nigdzie nie ma miejsca: sprzedaż po 90%.</summary>
        void Deliver(HarvestCrate preferred)
        {
            var game = GameLoop.Instance;
            foreach (var c in carried)
            {
                if (c.fruit != null)
                {
                    if (preferred != null && preferred.TryAddFruit(c.fruit))
                        continue;
                    var crate = HarvestCrate.NearestWithSpace(transform.position);
                    if (crate != null && crate.TryAddFruit(c.fruit))
                        continue;
                    if (game != null)
                        game.Wallet.Add(Mathf.RoundToInt(c.fruit.fruitPrice * MerchantSpirit.PriceMultiplier));
                    continue;
                }
                if (c.crop == null)
                    continue;
                if (preferred != null && preferred.TryAdd(c.crop, c.quality))
                    continue;
                var other = HarvestCrate.NearestWithSpace(transform.position);
                if (other != null && other.TryAdd(c.crop, c.quality))
                    continue;
                if (game != null)
                    game.Wallet.Add(Mathf.RoundToInt(c.crop.SellPrice(c.quality) * MerchantSpirit.PriceMultiplier));
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
            if (treeTarget != null)
                reservedTrees.Remove(treeTarget);
            treeTarget = null;
            fruitToPick = 0;
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
                carriedSprites[i].sprite = carried[i].sprite;
                carriedSprites[i].transform.localPosition = new Vector3((i - (carried.Count - 1) / 2f) * 0.35f, -0.15f, 0f);
            }
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.wisp.range", Radius * 2 + 1));
            info.lines.Add(trip != null ? Loc.Get("ui.wisp.flying", carried.Count + route.Count + fruitToPick)
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
