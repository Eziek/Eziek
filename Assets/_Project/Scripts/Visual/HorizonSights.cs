using System.Collections;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Sylwetki na horyzoncie (GDD §9 p. 120): co kilka minut daleko na wodzie, między kłębami mgły (22-40 px od brzegu,
    /// tuż pod mgłą, więc chmury częściowo je zasłaniają), pojawia się coś dużego:
    /// - wieloryb: wynurza grzbiet, wypuszcza fontannę, chowa się i na koniec pokazuje płetwę ogonową,
    /// - wąż morski: trzy garby falują jeden po drugim, na końcu wychyla się łeb,
    /// - statek-widmo: blady, podarte żagle, powoli przepływa (nocą z zapaloną latarnią).
    /// Kursor nad sylwetką pokazuje nazwę i dopisuje ją do „Widziane” (CursorPlay). Grafika z map znaków w kodzie.
    /// </summary>
    public class HorizonSights : MonoBehaviour
    {
        public static readonly string[] Ids = { "whale", "serpent", "ship" };
        const float Ppu = 16f;
        const int Order = 4990;                 // nad wszystkim na wodzie, pod mgłą (5000)
        const float MinDist = 22f, MaxDist = 40f;   // od 22 px zaczyna się mgła (Fog.shader _FogFrom)

        public static HorizonSights Instance { get; private set; }
        public string Current { get; private set; }

        SpriteRenderer sr;
        float timer;
        Texture2D serpentTex;
        Sprite serpentSprite;

        void Awake()
        {
            Instance = this;
            timer = Random.Range(90f, 200f);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (Current != null || Save.SaveSystem.InMenu)
                return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = Random.Range(150f, 360f);
            var clock = GameLoop.Instance != null ? GameLoop.Instance.Clock : null;
            bool night = clock != null && clock.IsNight;
            float r = Random.value * (night ? 8f : 9f);
            Show(r < 5f ? "whale" : r < 7f ? "serpent" : "ship");   // statek częściej nocą (wieloryb i wąż rzadziej)
        }

        /// <summary>F1: kolejna sylwetka od razu.</summary>
        public void DebugNext()
        {
            int i = Current == null ? 0 : (System.Array.IndexOf(Ids, Current) + 1) % Ids.Length;
            StopAllCoroutines();
            Clear();
            Show(Ids[i]);
        }

        /// <summary>Id sylwetki pod punktem świata (dla podpisu kursora) albo null.</summary>
        public string SightAt(Vector2 world, out object who)
        {
            who = null;
            if (Current == null || sr == null || sr.sprite == null || sr.color.a < 0.1f)
                return null;
            var b = sr.bounds;
            b.Expand(0.2f);
            if (!b.Contains(new Vector3(world.x, world.y, b.center.z)))
                return null;
            who = sr;
            return Current;
        }

        void Show(string id)
        {
            if (!FindSpot(id == "ship" ? 1.2f : 0.9f, out Vector2 at))
                return;
            Current = id;
            var go = new GameObject("Horizon_" + id);
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order;
            bool right = Random.value < 0.5f;
            sr.flipX = !right;
            switch (id)
            {
                case "whale": StartCoroutine(Whale(go.transform, at, right ? 1f : -1f)); break;
                case "serpent": StartCoroutine(Serpent(go.transform, at, right ? 1f : -1f)); break;
                default: StartCoroutine(Ship(go.transform, at, right ? 1f : -1f)); break;
            }
        }

        void Clear()
        {
            if (sr != null)
                Destroy(sr.gameObject);
            sr = null;
            Current = null;
        }

        /// <summary>Miejsce w kadrze daleko na wodzie, z wodą także na boki (halfWidth w kafelkach), poza pasem przy brzegu.</summary>
        bool FindSpot(float halfWidth, out Vector2 at)
        {
            at = default;
            var island = Island.Instance;
            var pic = island != null ? island.Picture : null;
            var cam = Camera.main;
            if (pic == null || cam == null)
                return false;
            float maxDist = MaxDist;
            var water = FindAnyObjectByType<WaterAnimator>();
            if (water != null && water.ShoreRange > 0f)
                maxDist = Mathf.Min(maxDist, water.ShoreRange - 6f);   // przezroczyste okno: tylko w pasie wody
            if (maxDist < MinDist)
                return false;
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            var c = (Vector2)cam.transform.position;
            for (int i = 0; i < 80; i++)
            {
                var p = c + new Vector2(Random.Range(-hw + halfWidth, hw - halfWidth), Random.Range(-hh + 0.6f, hh - 1.2f));
                var px = Island.WorldToPixel(p);
                int x = Mathf.RoundToInt(px.x), y = Mathf.RoundToInt(px.y);
                float d = pic.ShoreDistance(x, y);
                if (d < MinDist || d > maxDist)
                    continue;
                int side = Mathf.RoundToInt(halfWidth * Ppu);
                if (pic.ShoreDistance(x - side, y) < 16f || pic.ShoreDistance(x + side, y) < 16f || pic.ShoreDistance(x, y - 12) < 14f)
                    continue;
                at = new Vector2(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu);
                return true;
            }
            return false;
        }

        float Fade(float t, float life, float edge) => Mathf.Clamp01(Mathf.Min(t / edge, (life - t) / edge));

        void Place(Transform t, Vector2 at, float dx, float dy = 0f) =>
            t.position = new Vector3(Mathf.Round((at.x + dx) * Ppu) / Ppu, Mathf.Round((at.y + dy) * Ppu) / Ppu, 0f);

        // ------------------------------------------------------------ wieloryb

        IEnumerator Whale(Transform t, Vector2 at, float dir)
        {
            // (klatka, czas): zmarszczka, grzbiet, grzbiet z płetwą, fontanna, znów grzbiet, zanurzenie, ogon
            var seq = new (string[] map, float time)[]
            {
                (WhaleRipple, 0.8f), (WhaleHump, 1.2f), (WhaleBack, 1.5f),
                (WhaleSpout0, 0.25f), (WhaleSpout1, 0.25f), (WhaleSpout0, 0.25f), (WhaleSpout1, 0.25f), (WhaleSpout0, 0.25f),
                (WhaleBack, 1.2f), (WhaleHump, 0.8f), (WhaleRipple, 0.8f), (null, 1.2f),
                (WhaleRipple, 0.5f), (WhaleFluke, 2f), (WhaleFlukeLow, 0.8f), (WhaleRipple, 1f),
            };
            float total = 0f;
            foreach (var s in seq) total += s.time;
            float elapsed = 0f;
            foreach (var (map, time) in seq)
            {
                sr.sprite = map != null ? Make(map) : null;
                for (float k = 0f; k < time; k += Time.unscaledDeltaTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    Place(t, at, dir * elapsed * 0.04f);
                    sr.color = new Color(1f, 1f, 1f, 0.85f * Fade(elapsed, total, 0.6f));
                    yield return null;
                }
            }
            Clear();
        }

        // ------------------------------------------------------------ wąż morski

        IEnumerator Serpent(Transform t, Vector2 at, float dir)
        {
            const float life = 18f;
            const int w = 46, h = 11;
            if (serpentTex == null)
            {
                serpentTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
                serpentSprite = Sprite.Create(serpentTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu);
            }
            sr.sprite = serpentSprite;
            var px = new Color32[w * h];
            float frame = 0f;
            for (float k = 0f; k < life; k += Time.unscaledDeltaTime)
            {
                frame -= Time.unscaledDeltaTime;
                if (frame <= 0f)
                {
                    frame = 0.12f;   // 8 kl./s jak reszta pikseli
                    PaintSerpent(px, w, h, k * 1.4f);
                    serpentTex.SetPixels32(px);
                    serpentTex.Apply(false);
                }
                Place(t, at, dir * k * 0.06f);
                sr.color = new Color(1f, 1f, 1f, 0.85f * Fade(k, life, 1.5f));
                yield return null;
            }
            Clear();
        }

        /// <summary>Trzy garby (od ogona) wynurzają się i chowają po kolei, łeb wychyla się po ostatnim. Wiersz 0 = linia wody.</summary>
        static void PaintSerpent(Color32[] px, int w, int h, float p)
        {
            for (int i = 0; i < px.Length; i++) px[i] = default;
            void Put(int x, int y, char c)
            {
                if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = Col(c);
            }
            for (int i = 0; i < 3; i++)
            {
                int cx = 6 + 11 * i;
                float hump = Mathf.Max(0f, Mathf.Sin(p - i * 0.9f));
                int hgt = Mathf.RoundToInt(5.5f * hump);
                if (hgt <= 0)
                    continue;
                for (int dx = -5; dx <= 5; dx++)
                {
                    int top = Mathf.RoundToInt(hgt * Mathf.Sqrt(Mathf.Max(0f, 1f - (dx / 5.2f) * (dx / 5.2f))));
                    for (int y = 1; y <= top; y++)
                        Put(cx + dx, y, y == top ? 'm' : 'd');
                    if (top > 0) Put(cx + dx, 0, 'd');
                }
                Put(cx - 6, 0, 'f'); Put(cx + 6, 0, 'f'); Put(cx - 7, 0, 'f');
            }
            // łeb: wychyla się, gdy garby idą w dół
            float head = Mathf.Sin(p - 3.0f);
            if (head > 0.1f)
            {
                int hy = Mathf.RoundToInt(6f * Mathf.Min(1f, head * 1.6f));
                for (int y = 0; y <= hy; y++) { Put(40, y, 'd'); Put(41, y, y == hy ? 'm' : 'd'); }
                Put(42, hy, 'd'); Put(43, hy, 'd'); Put(42, hy + 1, 'm'); Put(41, hy + 1, 'm'); Put(42, hy - 1, 'd');
                Put(42, hy + 1, 'w');   // blade oko
                Put(39, 0, 'f'); Put(43, 0, 'f');
            }
        }

        // ------------------------------------------------------------ statek-widmo

        IEnumerator Ship(Transform t, Vector2 at, float dir)
        {
            const float life = 40f;
            sr.sprite = Make(ShipMap, new Vector2(0.5f, 0f));
            var clock = GameLoop.Instance != null ? GameLoop.Instance.Clock : null;
            if (clock != null && clock.IsNight)
            {
                var art = Island.Instance != null ? Island.Instance.Art : null;
                if (art != null && art.glow != null)
                    sr.sharedMaterial = art.glow;   // nocą widmo lekko jaśnieje (bez oświetlenia), dzień: zwykłe światło
                // latarnia na rufie (lewy koniec mapy, flipX odwraca stronę)
                float lx = (0.5f - ShipMap[0].Length / 2f) / Ppu * (sr.flipX ? -1f : 1f);
                NightGlow.Add(t.gameObject, new Vector2(lx, 4.5f / Ppu), new Color(1f, 0.75f, 0.45f), 0.8f, 0.35f);
            }
            for (float k = 0f; k < life; k += Time.unscaledDeltaTime)
            {
                float bob = Mathf.Sin(k * 1.3f) > 0.3f ? 1f / Ppu : 0f;
                Place(t, at, dir * k * 0.025f, bob);
                sr.color = new Color(0.85f, 0.95f, 1f, 0.5f * Fade(k, life, 4f));
                yield return null;
            }
            Clear();
        }

        // ------------------------------------------------------------ grafika

        static Color32 Col(char c)
        {
            switch (c)
            {
                case 'o': return new Color32(15, 10, 16, 255);
                case 'd': return new Color32(11, 9, 16, 255);        // ciemne ciało (ciemniejsze od najgłębszej wody)
                case 'm': return new Color32(31, 48, 68, 255);       // grzbiet łapie światło
                case 'f': return new Color32(132, 142, 155, 255);    // piana
                case 's': return new Color32(86, 105, 122, 255);     // mgiełka fontanny
                case 'w': return new Color32(146, 171, 177, 255);
                case 'g': return new Color32(86, 105, 122, 255);     // blade drewno i żagle statku-widma
                case 'h': return new Color32(132, 142, 155, 255);
                case 'k': return new Color32(51, 38, 58, 255);       // kadłub
                case 'l': return new Color32(242, 184, 102, 255);    // latarnia
                default: return new Color32(0, 0, 0, 0);
            }
        }

        static readonly System.Collections.Generic.Dictionary<string[], Sprite> cache =
            new System.Collections.Generic.Dictionary<string[], Sprite>();

        /// <summary>Sprite z mapy znaków (wiersz 0 = góra), zaczepiony w środku dolnej krawędzi (linia wody).</summary>
        static Sprite Make(string[] map) => Make(map, new Vector2(0.5f, 0f));

        static Sprite Make(string[] map, Vector2 pivot)
        {
            if (cache.TryGetValue(map, out var s) && s != null)
                return s;
            int h = map.Length, w = map[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[(h - 1 - y) * w + x] = Col(map[y][x]);
            tex.SetPixels32(px);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu);
            cache[map] = s;
            return s;
        }

        // wieloryb płynie w prawo (łeb po prawej); wiersz ostatni = linia wody z pianą
        static readonly string[] WhaleRipple =
        {
            "........................",
            "........ff.fff.ff.......",
        };
        static readonly string[] WhaleHump =
        {
            "..........mmmm..........",
            ".......ddddddddd........",
            ".....ffdddddddddff......",
        };
        static readonly string[] WhaleBack =
        {
            "......d.................",
            ".....dd.................",
            "....mmmmmmmmmmmm........",
            "...dddddddddddddmm......",
            "..ddddddddddddddddd.....",
            "ffdddddddddddddddddddff.",
        };
        static readonly string[] WhaleSpout0 =
        {
            "...............w.w......",
            "..............w.s.w.....",
            "...............sws......",
            "................s.......",
            "................s.......",
            "......d.........s.......",
            ".....dd.................",
            "....mmmmmmmmmmmm........",
            "...dddddddddddddmm......",
            "..ddddddddddddddddd.....",
            "ffdddddddddddddddddddff.",
        };
        static readonly string[] WhaleSpout1 =
        {
            "..............s...s.....",
            "...............w.w......",
            "..............s.w.s.....",
            "...............sws......",
            "................s.......",
            "......d.........s.......",
            ".....dd.................",
            "....mmmmmmmmmmmm........",
            "...dddddddddddddmm......",
            "..ddddddddddddddddd.....",
            "ffdddddddddddddddddddff.",
        };
        static readonly string[] WhaleFluke =
        {
            ".......mm.......mm......",
            ".......ddd.....ddd......",
            "........ddd...ddd.......",
            ".........ddddddd........",
            "..........ddddd.........",
            "...........ddd..........",
            "...........ddd..........",
            "...........ddd..........",
            ".........ffdddff........",
        };
        static readonly string[] WhaleFlukeLow =
        {
            ".......mm.......mm......",
            ".......ddd.....ddd......",
            "........ddd...ddd.......",
            ".........ddddddd........",
            "........ffdddddff.......",
        };

        // statek-widmo płynie w prawo (dziób po prawej), latarnia na rufie
        static readonly string[] ShipMap =
        {
            "...............h............",
            "...............hh...........",
            "..h............g............",
            "..g.........hhhghhh.........",
            "hhghh.......hgggggh....h....",
            "hgggh.......hgg.ggh....g....",
            "hg.gh.......hgggggh..hhghh..",
            "hgggh.......hggg.gh..hgggh..",
            "hgg.h........gg.gg...hg.gh..",
            "..g............g.....hgggh..",
            "..g............g.......g....",
            "l.g............g.......g..kk",
            "kkkkkkkkkkkkkkkkkkkkkkkkkkk.",
            ".kkgkgkgkgkgkgkgkgkgkgkgkk..",
            "..kkkkkkkkkkkkkkkkkkkkkkk...",
            "ffffffffffffffffffffffffffff",
        };
    }
}
