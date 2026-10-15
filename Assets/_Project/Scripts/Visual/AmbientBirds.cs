using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.UI;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Ptaki (GDD §9 p. 113). W dzień co jakiś czas przylatuje ptak albo mała gromadka i siada tam, gdzie lubi:
    /// wrona i zięba gdziekolwiek, sroka głównie na ziemi (podskakuje), gil i sikora w koronach drzew (parami, trójkami),
    /// mewa na pomoście albo na wodzie przy brzegu, czapla brodzi na płyciźnie i co chwilę uderza dziobem w wodę.
    /// Dziobią, po chwili odlatują. Kursor blisko ptaka go płoszy. Tylko wygląd: bez kolizji i wpływu na grę.
    /// </summary>
    public class AmbientBirds : MonoBehaviour
    {
        const float Ppu = 16f;
        const int MaxBirds = 3;

        enum Habitat { Any, Tree, Ground, Shore, Wader }

        class Kind
        {
            public Sprite[] frames;   // sit0, sit1, fly0, fly1
            public Habitat habitat;
            public float weight, speed, stayMin, stayMax;
            public int flockMax;
        }

        class Bird
        {
            public SpriteRenderer sr;
            public Kind kind;
            public bool leaving, scared;
        }

        public static AmbientBirds Instance { get; private set; }
        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Klik w drzewo: siedzące w pobliżu ptaki zrywają się do lotu.</summary>
        public void ScareNear(Vector2 world, float radius)
        {
            foreach (var b in birds)
                if (!b.leaving && b.sr != null && ((Vector2)b.sr.transform.position - world).sqrMagnitude < radius * radius)
                    b.scared = true;
        }

        readonly List<Kind> kinds = new List<Kind>();
        GameClock clock;
        float spawnTimer = 20f;
        readonly List<Bird> birds = new List<Bird>();

        public void Setup(IslandArt art, GameClock gameClock)
        {
            clock = gameClock;
            kinds.Clear();
            if (art == null)
                return;
            Add(art.birdCrow, Habitat.Any, 3f, 4f, 8f, 18f, 1);
            Add(art.birdFinch, Habitat.Any, 3f, 4.5f, 8f, 16f, 3);
            Add(art.Bird("magpie"), Habitat.Ground, 2f, 4f, 10f, 20f, 1);
            Add(art.Bird("bullfinch"), Habitat.Tree, 2f, 4.5f, 8f, 18f, 3);
            Add(art.Bird("tit"), Habitat.Tree, 2f, 5f, 6f, 14f, 2);
            Add(art.Bird("gull"), Habitat.Shore, 2f, 3.5f, 12f, 25f, 2);
            Add(art.Bird("heron"), Habitat.Wader, 1f, 2.5f, 20f, 40f, 1);
        }

        void Add(Sprite[] frames, Habitat habitat, float weight, float speed, float stayMin, float stayMax, int flockMax)
        {
            if (frames == null || frames.Length < 4 || frames[0] == null)
                return;
            kinds.Add(new Kind { frames = frames, habitat = habitat, weight = weight, speed = speed, stayMin = stayMin, stayMax = stayMax, flockMax = flockMax });
        }

        Kind PickKind(bool treesOnly = false)
        {
            float total = 0f;
            foreach (var k in kinds)
                if (!treesOnly || k.habitat == Habitat.Any || k.habitat == Habitat.Tree) total += k.weight;
            float r = Random.value * total;
            foreach (var k in kinds)
            {
                if (treesOnly && k.habitat != Habitat.Any && k.habitat != Habitat.Tree)
                    continue;
                if ((r -= k.weight) <= 0f)
                    return k;
            }
            return kinds.Count > 0 ? kinds[0] : null;
        }

        void Update()
        {
            if (clock == null && GameLoop.Instance != null)
                clock = GameLoop.Instance.Clock;
            bool night = clock != null && clock.IsNight;
            spawnTimer -= Time.unscaledDeltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = Random.Range(20f, 55f);
                if (!night && birds.Count < MaxBirds)
                    TrySpawn();
            }
            // kursor blisko siedzącego ptaka: zrywa się do lotu
            var cam = Camera.main;
            if (cam == null)
                return;
            Vector2 mouse = cam.ScreenToWorldPoint(UiInput.MousePosition);
            foreach (var b in birds)
                if (!b.leaving && b.sr != null && ((Vector2)b.sr.transform.position - mouse).sqrMagnitude < 1.2f * 1.2f)
                    b.scared = true;
        }

        void TrySpawn()
        {
            var island = Island.Instance;
            if (island == null || !island.Ready || Camera.main == null || kinds.Count == 0)
                return;
            var kind = PickKind();
            if (kind == null)
                return;
            int flock = Random.Range(1, kind.flockMax + 1);
            float side = Random.value < 0.5f ? -1f : 1f;   // gromadka przylatuje z jednej strony
            for (int i = 0; i < flock && birds.Count < MaxBirds + 2; i++)
            {
                if (!PickPerch(island, kind.habitat, out var perch, out int order))
                    return;
                Spawn(kind, perch, order, side, i * Random.Range(0.3f, 0.8f), 0f);
            }
        }

        void Spawn(Kind kind, Vector3 perch, int order, float side, float delay, float perchedFor)
        {
            var go = new GameObject("Bird");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = kind.frames[perchedFor > 0f ? 0 : 2];
            sr.sortingOrder = perchedFor > 0f ? order : 4600;
            go.transform.position = perchedFor > 0f ? perch : new Vector3(9999f, 9999f, 0f);
            var bird = new Bird { sr = sr, kind = kind };
            birds.Add(bird);
            StartCoroutine(Life(bird, perch, order, side, delay, perchedFor));
        }

        // ------------------------------------------------------------ miejsca

        /// <summary>Gdzie usiąść, zależnie od upodobań ptaka.</summary>
        bool PickPerch(Island island, Habitat habitat, out Vector3 perch, out int order)
        {
            var spots = new List<(Vector3 pos, int order)>();
            if (habitat == Habitat.Any || habitat == Habitat.Tree || (habitat == Habitat.Ground && Random.value < 0.3f))
                TreeSpots(island, spots);
            if (habitat == Habitat.Any || habitat == Habitat.Ground)
                GroundSpots(island, spots, habitat == Habitat.Ground ? 6 : 3);
            if (habitat == Habitat.Shore)
            {
                var pier = island.PierObject;
                if (pier != null && Random.value < 0.5f)
                    spots.Add(((Vector3)pier.Tip + new Vector3(Random.Range(-3, 2) / Ppu, 1f / Ppu, 0f), pier.GetComponent<SpriteRenderer>().sortingOrder + 2));
                WaterSpots(island, spots, 4f, 12f);
            }
            if (habitat == Habitat.Wader)
                WaterSpots(island, spots, 2f, 5f);
            perch = default;
            order = 0;
            if (spots.Count == 0)
                return false;
            var s = spots[Random.Range(0, spots.Count)];
            perch = s.pos;
            order = s.order;
            return true;
        }

        static void TreeSpots(Island island, List<(Vector3, int)> spots)
        {
            foreach (var t in FruitTree.All)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (t.Adult && sr != null)
                    spots.Add((new Vector3(sr.bounds.center.x + Random.Range(-0.3f, 0.3f), sr.bounds.max.y - 0.45f, 0f), sr.sortingOrder + 3));
            }
            foreach (var tr in island.Trees)
            {
                var sr = tr != null ? tr.GetComponent<SpriteRenderer>() : null;
                if (sr != null)
                    spots.Add((new Vector3(sr.bounds.center.x + Random.Range(-0.3f, 0.3f), sr.bounds.max.y - 0.5f, 0f), sr.sortingOrder + 3));
            }
        }

        static void GroundSpots(Island island, List<(Vector3, int)> spots, int count)
        {
            var free = island.FreeTiles();
            for (int i = 0; i < Mathf.Min(count, free.Count); i++)
            {
                var p = free[Random.Range(0, free.Count)];
                var at = Island.TileAnchor(p, 10, out int o) + new Vector3(Random.Range(-4, 5) / Ppu, 0f, 0f);
                spots.Add((at, o + 3));
            }
        }

        /// <summary>Woda przy brzegu: od minD do maxD pikseli od lądu (mapa odległości wody).</summary>
        static void WaterSpots(Island island, List<(Vector3, int)> spots, float minD, float maxD)
        {
            var pic = island.Picture;
            if (pic == null)
                return;
            for (int tries = 0; tries < 60 && spots.Count < 6; tries++)
            {
                int x = Random.Range(pic.minX - 12, pic.maxX + 13), y = Random.Range(pic.minY - 12, pic.maxY + 13);
                float d = pic.ShoreDistance(x, y);
                if (d < minD || d > maxD || island.SeaBlocked(Island.WorldToTile(Island.PixelToWorld(x, y))))
                    continue;
                spots.Add((Island.AnchorWorld(x, y), y + 1));
            }
        }

        // ------------------------------------------------------------ życie ptaka

        /// <summary>Świt (scenka): kilka ptaków siedzi już w koronach drzew i po chwili zrywa się do lotu.</summary>
        public void DawnFlock(int count)
        {
            var island = Island.Instance;
            if (island == null || !island.Ready || kinds.Count == 0)
                return;
            for (int i = 0; i < count && birds.Count < MaxBirds + 2; i++)
            {
                var kind = PickKind(true);
                if (kind == null || !PickPerch(island, Habitat.Tree, out var perch, out int order))
                    return;
                Spawn(kind, perch, order, 1f, 0f, Random.Range(0.6f, 2.4f));
            }
        }

        /// <param name="perchedFor">Większe od 0: ptak już siedzi (bez przylotu) i po tylu sekundach odlatuje (świt).</param>
        IEnumerator Life(Bird b, Vector3 perch, int perchOrder, float side, float delay, float perchedFor)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            var cam = Camera.main;
            float half = cam != null ? cam.orthographicSize * cam.aspect + 1.5f : 8f;
            float cx = cam != null ? cam.transform.position.x : perch.x;
            var habitat = b.kind.habitat;
            bool wader = habitat == Habitat.Wader, water = wader || habitat == Habitat.Shore;
            if (perchedFor <= 0f)
            {
                var start = new Vector3(cx + side * half, perch.y + Random.Range(1.5f, 3f), 0f);
                if (habitat == Habitat.Shore && Random.value < 0.6f)
                {
                    // mewa: najpierw krąży nad wodą, potem siada
                    var over = perch + new Vector3(-side * Random.Range(1.5f, 3f), Random.Range(1.5f, 2.5f), 0f);
                    yield return Fly(b, start, over, 0.6f);
                    start = over;
                }
                yield return Fly(b, start, perch, wader ? 0.4f : 0.8f);
            }

            // na miejscu: dziobanie (czapla: uderzenie w wodę z pluskiem), na ziemi podskoki
            b.sr.sortingOrder = perchOrder;
            b.sr.sprite = b.kind.frames[0];
            float stay = perchedFor > 0f ? perchedFor : Random.Range(b.kind.stayMin, b.kind.stayMax);
            float t = 0f, nextPeck = Random.Range(0.6f, 1.6f), nextHop = Random.Range(1.5f, 3.5f);
            var pos = perch;
            bool hops = !water && habitat != Habitat.Tree;
            while (t < stay && !b.scared && b.sr != null)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                nextPeck -= dt;
                if (nextPeck <= 0f)
                {
                    nextPeck = wader ? Random.Range(2.5f, 5f) : Random.Range(0.8f, 2f);
                    b.sr.sprite = b.kind.frames[1];
                    if (water)
                        Splash(pos + new Vector3((b.sr.flipX ? -5f : 5f) / Ppu, 0f, 0f), perchOrder);
                    yield return new WaitForSecondsRealtime(wader ? 0.5f : 0.22f);
                    if (b.sr == null)
                        yield break;
                    b.sr.sprite = b.kind.frames[0];
                }
                if (hops)
                {
                    nextHop -= dt;
                    if (nextHop <= 0f)
                    {
                        nextHop = Random.Range(1.5f, 3.5f);
                        float dir = Random.value < 0.5f ? -1f : 1f;
                        b.sr.flipX = dir < 0f;
                        var to = pos + new Vector3(dir * Random.Range(2, 5) / Ppu, 0f, 0f);
                        for (float h = 0f; h < 0.18f; h += Time.unscaledDeltaTime)
                        {
                            float u = h / 0.18f;
                            b.sr.transform.position = Vector3.Lerp(pos, to, u) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 2f / Ppu, 0f);
                            yield return null;
                        }
                        pos = to;
                        b.sr.transform.position = pos;
                    }
                }
                yield return null;
            }

            if (b.sr == null)
                yield break;
            b.leaving = true;
            b.sr.sortingOrder = 4600;
            float away = Random.value < 0.5f ? -1f : 1f;
            var end = new Vector3(cx + away * (half + 1f), pos.y + Random.Range(2f, 4f), 0f);
            yield return Fly(b, pos, end, 1.2f);
            birds.Remove(b);
            if (b.sr != null)
                Destroy(b.sr.gameObject);
        }

        /// <summary>Plusk przy dziobie: kilka kropel piany w górę.</summary>
        static void Splash(Vector3 at, int order)
        {
            var fx = PixelParticles.Instance;
            var island = Island.Instance;
            var foam = island != null && island.Art != null ? island.Art.foam : null;
            if (fx != null && foam != null && foam.Length > 0)
                fx.Burst(at, foam, 2, 18f, 120f, 0.35f, order + 1);
        }

        /// <summary>Płynny lot po łuku, machanie skrzydłami (fly0/fly1), zwrot w stronę lotu. Duże ptaki machają wolniej.</summary>
        IEnumerator Fly(Bird b, Vector3 from, Vector3 to, float arc)
        {
            if (b.sr == null)
                yield break;
            b.sr.flipX = to.x < from.x;
            float speed = b.kind.speed;
            float flap = speed < 3f ? 5f : 10f;
            float duration = Mathf.Max(0.4f, Vector3.Distance(from, to) / speed);
            for (float t = 0f; t < duration && b.sr != null; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                float e = u * u * (3f - 2f * u);
                b.sr.transform.position = Vector3.Lerp(from, to, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * arc, 0f);
                b.sr.sprite = b.kind.frames[2 + Mathf.FloorToInt(Time.unscaledTime * flap) % 2];
                yield return null;
            }
            if (b.sr != null)
                b.sr.transform.position = to;
        }
    }
}
