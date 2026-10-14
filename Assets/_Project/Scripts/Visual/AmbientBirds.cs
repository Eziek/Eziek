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
    /// Ptaki. W dzień co jakiś czas przylatuje wrona albo ptaszek, siada na drzewie
    /// (owocowym albo dzikim) lub na wolnym kafelku trawy, dziobie, na ziemi podskakuje, po chwili odlatuje.
    /// Kursor blisko ptaka go płoszy. Tylko wygląd: bez kolizji i wpływu na grę.
    /// </summary>
    public class AmbientBirds : MonoBehaviour
    {
        const float Ppu = 16f;
        const int MaxBirds = 2;
        const float FlySpeed = 4f;   // kafelki na sekundę

        Sprite[] crow, finch;
        GameClock clock;
        float spawnTimer = 20f;
        readonly List<Bird> birds = new List<Bird>();

        class Bird
        {
            public SpriteRenderer sr;
            public Sprite[] frames;   // sit0, sit1, fly0, fly1
            public bool leaving, scared;
            public Coroutine routine;
        }

        public void Setup(Sprite[] crowFrames, Sprite[] finchFrames, GameClock gameClock)
        {
            crow = crowFrames;
            finch = finchFrames;
            clock = gameClock;
        }

        void Update()
        {
            if (clock == null && GameLoop.Instance != null)
                clock = GameLoop.Instance.Clock;
            bool night = clock != null && clock.IsNight;
            spawnTimer -= Time.unscaledDeltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = Random.Range(25f, 70f);
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
            var cam = Camera.main;
            if (island == null || !island.Ready || cam == null)
                return;
            var frames = Random.value < 0.5f ? crow : finch;
            if (frames == null || frames.Length < 4 || frames[0] == null)
                return;
            if (!PickPerch(island, out var perch, out int order, out bool onGround))
                return;
            var go = new GameObject("Bird");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[2];
            sr.sortingOrder = 4600;
            var bird = new Bird { sr = sr, frames = frames };
            birds.Add(bird);
            bird.routine = StartCoroutine(Life(bird, perch, order, onGround));
        }

        /// <summary>Gdzie usiąść: korona drzewa (owocowego albo dzikiego) albo wolny kafelek trawy.</summary>
        bool PickPerch(Island island, out Vector3 perch, out int order, out bool onGround)
        {
            var spots = new List<(Vector3 pos, int order, bool ground)>();
            foreach (var t in FruitTree.All)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (t.Adult && sr != null)
                    spots.Add((new Vector3(sr.bounds.center.x + Random.Range(-0.3f, 0.3f), sr.bounds.max.y - 0.45f, 0f), sr.sortingOrder + 3, false));
            }
            foreach (var tr in island.Trees)
            {
                var sr = tr != null ? tr.GetComponent<SpriteRenderer>() : null;
                if (sr != null)
                    spots.Add((new Vector3(sr.bounds.center.x + Random.Range(-0.3f, 0.3f), sr.bounds.max.y - 0.5f, 0f), sr.sortingOrder + 3, false));
            }
            var free = island.FreeTiles();
            for (int i = 0; i < Mathf.Min(4, free.Count); i++)
            {
                var p = free[Random.Range(0, free.Count)];
                var at = Island.TileAnchor(p, 10, out int o) + new Vector3(Random.Range(-4, 5) / Ppu, 0f, 0f);
                spots.Add((at, o + 3, true));
            }
            perch = default;
            order = 0;
            onGround = false;
            if (spots.Count == 0)
                return false;
            var s = spots[Random.Range(0, spots.Count)];
            perch = s.pos;
            order = s.order;
            onGround = s.ground;
            return true;
        }

        /// <summary>Świt (scenka): kilka ptaków siedzi już w koronach drzew i po chwili zrywa się do lotu.</summary>
        public void DawnFlock(int count)
        {
            var island = Island.Instance;
            if (island == null || !island.Ready)
                return;
            for (int i = 0; i < count && birds.Count < MaxBirds + 2; i++)
            {
                var frames = Random.value < 0.5f ? crow : finch;
                if (frames == null || frames.Length < 4 || frames[0] == null)
                    return;
                if (!PickPerch(island, out var perch, out int order, out bool onGround))
                    return;
                var go = new GameObject("Bird");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = order;
                go.transform.position = perch;
                var bird = new Bird { sr = sr, frames = frames };
                birds.Add(bird);
                bird.routine = StartCoroutine(Life(bird, perch, order, onGround, Random.Range(0.6f, 2.4f)));
            }
        }

        /// <param name="perchedFor">Większe od 0: ptak już siedzi (bez przylotu) i po tylu sekundach odlatuje (świt).</param>
        IEnumerator Life(Bird b, Vector3 perch, int perchOrder, bool onGround, float perchedFor = 0f)
        {
            var cam = Camera.main;
            float side = Random.value < 0.5f ? -1f : 1f;
            float half = cam != null ? cam.orthographicSize * cam.aspect + 1.5f : 8f;
            float cx = cam != null ? cam.transform.position.x : perch.x;
            var start = new Vector3(cx + side * half, perch.y + Random.Range(1.5f, 3f), 0f);
            if (perchedFor <= 0f)
                yield return Fly(b, start, perch, 0.8f);

            // na miejscu: dziobanie, na ziemi podskoki; po 8-18 s albo spłoszony odlatuje
            b.sr.sortingOrder = perchOrder;
            b.sr.sprite = b.frames[0];
            float stay = perchedFor > 0f ? perchedFor : Random.Range(8f, 18f);
            float t = 0f, nextPeck = Random.Range(0.6f, 1.6f), nextHop = Random.Range(1.5f, 3.5f);
            var pos = perch;
            while (t < stay && !b.scared)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                nextPeck -= dt;
                if (nextPeck <= 0f)
                {
                    nextPeck = Random.Range(0.8f, 2f);
                    b.sr.sprite = b.frames[1];
                    yield return new WaitForSecondsRealtime(0.22f);
                    b.sr.sprite = b.frames[0];
                }
                if (onGround)
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

            b.leaving = true;
            b.sr.sortingOrder = 4600;
            float away = Random.value < 0.5f ? -1f : 1f;
            var end = new Vector3(cx + away * (half + 1f), pos.y + Random.Range(2f, 4f), 0f);
            yield return Fly(b, pos, end, 1.2f);
            birds.Remove(b);
            Destroy(b.sr.gameObject);
        }

        /// <summary>Płynny lot po łuku, machanie skrzydłami (fly0/fly1), zwrot w stronę lotu.</summary>
        IEnumerator Fly(Bird b, Vector3 from, Vector3 to, float arc)
        {
            b.sr.flipX = to.x < from.x;
            float duration = Mathf.Max(0.4f, Vector3.Distance(from, to) / FlySpeed);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                float e = u * u * (3f - 2f * u);
                b.sr.transform.position = Vector3.Lerp(from, to, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * arc, 0f);
                b.sr.sprite = b.frames[2 + Mathf.FloorToInt(Time.unscaledTime * 10f) % 2];
                yield return null;
            }
            b.sr.transform.position = to;
        }
    }
}
