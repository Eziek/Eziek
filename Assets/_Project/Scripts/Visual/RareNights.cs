using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Lighting;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Rzadkie noce (GDD §9 p. 116): na początku nocy z szansą ok. 30% (w pełnię 50%) zdarza się jedno zjawisko,
    /// które trwa do świtu: świecący przypływ (brzeg, fale i ryby jarzą się turkusem, Water.shader), deszcz spadających
    /// gwiazd, zorza nad mgłą, procesja widmowych ogników sunąca po wodzie (raz albo dwa razy na noc) i krwawy księżyc
    /// (tylko w pełnię, czerwone nocne światło). Zjawisko trafia do bestiariusza („Widziane”, z licznikiem).
    /// Tylko wygląd. Wczytanie gry w środku nocy nie powtarza losowania tej nocy.
    /// </summary>
    public class RareNights : MonoBehaviour
    {
        public static readonly string[] Ids = { "biolum", "stars", "aurora", "procession", "bloodmoon" };
        static readonly float[] Weights = { 3f, 3f, 2f, 2f, 0f };   // krwawy księżyc osobno, tylko w pełnię
        static readonly int BiolumId = Shader.PropertyToID("_WyspaBiolum");
        const float Ppu = 16f;
        const int SkyOrder = 5100;          // nad mgłą (5000)

        public static RareNights Instance { get; private set; }
        public string Current { get; private set; }

        GameClock clock;
        bool wasNight, initialized;
        Sprite streak, wisp;
        SpriteRenderer aurora;
        Texture2D auroraTex;
        Color32[] auroraPx;
        Color tint = Color.white, tintTarget = Color.white;   // krwawy księżyc wchodzi i schodzi płynnie
        float biolum, biolumTarget;
        float starTimer, auroraTimer, processionTimer;
        int processions;
        readonly List<GameObject> spawned = new List<GameObject>();

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Stop();
            Shader.SetGlobalFloat(BiolumId, 0f);           // bez płynnego zejścia: obiekt znika (np. powrót do menu)
            DayNightLighting.NightTint = Color.white;
            if (auroraTex != null)
                Destroy(auroraTex);
        }

        void Update()
        {
            if (clock == null && GameLoop.Instance != null)
                clock = GameLoop.Instance.Clock;
            if (clock == null || Save.SaveSystem.InMenu)
                return;
            bool night = clock.IsNight;
            if (!initialized)
            {
                initialized = true;      // pierwsza klatka po wczytaniu: bez losowania w środku nocy
                wasNight = night;
                return;
            }
            if (night && !wasNight)
                Roll();
            else if (!night && wasNight)
                Stop();
            wasNight = night;
            if (Current != null)
                Run();
            float dt = Time.unscaledDeltaTime;
            biolum = Mathf.MoveTowards(biolum, biolumTarget, dt * 0.25f);
            Shader.SetGlobalFloat(BiolumId, biolum);
            tint = Color.Lerp(tint, tintTarget, 1f - Mathf.Exp(-dt * 0.6f));
            DayNightLighting.NightTint = tint;
        }

        void Roll()
        {
            bool full = clock.IsFullMoon;
            if (full && Random.value < 0.35f)
            {
                Begin("bloodmoon");
                return;
            }
            if (Random.value > (full ? 0.5f : 0.3f))
                return;
            float total = 0f;
            foreach (var w in Weights) total += w;
            float r = Random.value * total;
            for (int i = 0; i < Ids.Length; i++)
                if ((r -= Weights[i]) <= 0f)
                {
                    Begin(Ids[i]);
                    return;
                }
        }

        /// <summary>F1: następne zjawisko od razu (po kolei).</summary>
        public void DebugNext()
        {
            int i = Current == null ? 0 : (System.Array.IndexOf(Ids, Current) + 1) % Ids.Length;
            Stop();
            Begin(Ids[i]);
        }

        void Begin(string id)
        {
            Stop();
            Current = id;
            GameEvents.RaiseToast("toast.sight." + id);
            Lore.Chronicle.Note("sight:" + id);
            Lore.Chronicle.Count("seen:sight:" + id);
            switch (id)
            {
                case "biolum": biolumTarget = 1f; break;
                case "bloodmoon": tintTarget = new Color(1f, 0.42f, 0.4f); break;
                case "aurora": MakeAurora(); break;
                case "procession": processions = 0; processionTimer = Random.Range(5f, 40f); break;
            }
        }

        void Stop()
        {
            Current = null;
            biolumTarget = 0f;
            tintTarget = Color.white;
            StopAllCoroutines();
            foreach (var go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
            aurora = null;
        }

        void Run()
        {
            float dt = Time.unscaledDeltaTime;
            switch (Current)
            {
                case "stars":
                    starTimer -= dt;
                    if (starTimer <= 0f)
                    {
                        starTimer = Random.Range(0.25f, 1.4f);
                        StartCoroutine(ShootingStar());
                    }
                    break;
                case "aurora":
                    auroraTimer -= dt;
                    if (auroraTimer <= 0f)
                    {
                        auroraTimer = 0.12f;
                        PaintAurora();
                    }
                    break;
                case "procession":
                    processionTimer -= dt;
                    if (processionTimer <= 0f && processions < 2)
                    {
                        processions++;
                        processionTimer = Random.Range(240f, 420f);
                        StartCoroutine(Procession());
                    }
                    break;
            }
        }

        // ------------------------------------------------------------ deszcz gwiazd

        Sprite Streak()
        {
            if (streak != null)
                return streak;
            const int w = 9, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            // głowa w lewym dolnym rogu, ogon w górę w prawo, coraz bledszy
            for (int i = 0; i < w; i++)
            {
                int y = Mathf.Min(h - 1, i / 2);
                byte a = (byte)Mathf.Clamp(255 - i * 28, 30, 255);
                px[y * w + i] = i == 0 ? new Color32(255, 250, 220, 255) : new Color32(210, 230, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            streak = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0f), Ppu);
            return streak;
        }

        IEnumerator ShootingStar()
        {
            var cam = Camera.main;
            if (cam == null)
                yield break;
            float hw = cam.orthographicSize * cam.aspect, hh = cam.orthographicSize;
            var c = cam.transform.position;
            bool left = Random.value < 0.5f;   // leci w lewo w dół albo (odbity) w prawo w dół
            var start = new Vector3(c.x + Random.Range(-hw, hw), c.y + hh * Random.Range(0.35f, 0.95f), 0f);
            var vel = new Vector3(left ? -1f : 1f, -0.45f, 0f) * Random.Range(7f, 11f);
            var go = new GameObject("ShootingStar");
            go.transform.SetParent(transform, false);
            spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Streak();
            sr.flipX = !left;
            sr.sortingOrder = SkyOrder;
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (art != null && art.glow != null)
                sr.sharedMaterial = art.glow;
            float life = Random.Range(0.35f, 0.7f);
            for (float t = 0f; t < life && go != null; t += Time.unscaledDeltaTime)
            {
                var p = start + vel * t;
                go.transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
                sr.color = new Color(1f, 1f, 1f, t > life * 0.7f ? 0.5f : 1f);
                yield return null;
            }
            spawned.Remove(go);
            if (go != null)
                Destroy(go);
        }

        // ------------------------------------------------------------ zorza

        void MakeAurora()
        {
            var go = new GameObject("Aurora");
            go.transform.SetParent(transform, false);
            spawned.Add(go);
            aurora = go.AddComponent<SpriteRenderer>();
            aurora.sortingOrder = SkyOrder - 1;
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (art != null && art.glow != null)
                aurora.sharedMaterial = art.glow;
            auroraTimer = 0f;
        }

        /// <summary>
        /// Kurtyny zorzy: kolumny o jasnej dolnej krawędzi, wygasające ku górze, falujące w czasie; turkus przechodzi
        /// w fiolet. Przezroczystość schodkowo (dithering Bayera), żeby została pikselowa. Przyklejona do górnej części kadru.
        /// </summary>
        void PaintAurora()
        {
            var cam = Camera.main;
            if (aurora == null || cam == null)
                return;
            int w = Mathf.Clamp(Mathf.CeilToInt(cam.orthographicSize * cam.aspect * 2f * Ppu), 32, 512);
            const int h = 44;
            if (auroraTex == null || auroraTex.width != w)
            {
                if (auroraTex != null)
                    Destroy(auroraTex);
                auroraTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
                auroraPx = new Color32[w * h];
            }
            if (aurora.sprite == null || aurora.sprite.texture != auroraTex)
                aurora.sprite = Sprite.Create(auroraTex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), Ppu);
            float t = Time.unscaledTime;
            var px = auroraPx;
            System.Array.Clear(px, 0, px.Length);
            var teal = new Color(0.37f, 0.81f, 0.72f);
            var violet = new Color(0.55f, 0.4f, 0.85f);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                float curtain = Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(x * 0.045f + t * 0.25f) * Mathf.Sin(x * 0.11f - t * 0.4f + 1.3f));
                float edge = 10f + 8f * Mathf.Sin(x * 0.03f + t * 0.2f);          // dolna krawędź kurtyny (wiersz od dołu)
                var col = Color.Lerp(teal, violet, 0.5f + 0.5f * Mathf.Sin(u * 5f + t * 0.1f));
                for (int y = 0; y < h; y++)
                {
                    float above = y - edge;
                    if (above < 0f)
                        continue;
                    float a = curtain * Mathf.Exp(-above / 14f) * (above < 2f ? 1.2f : 0.8f);
                    float th = Bayer((x & 3) + ((y & 3) << 2));
                    if (a * 0.9f > th)
                        px[y * w + x] = new Color(col.r, col.g, col.b, above < 2f ? 0.7f : 0.45f);
                }
            }
            auroraTex.SetPixels32(px);
            auroraTex.Apply(false);
            var c = cam.transform.position;
            aurora.transform.position = new Vector3(Mathf.Round(c.x * Ppu) / Ppu, Mathf.Round((c.y + cam.orthographicSize) * Ppu) / Ppu, 0f);
        }

        static readonly int[] BayerMap = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Bayer(int i) => (BayerMap[i] + 0.5f) / 16f;

        // ------------------------------------------------------------ procesja ogników

        /// <summary>
        /// Procesja: 5-7 bladych, półprzezroczystych ogników sunie gęsiego po wodzie pod wyspą, z jednej krawędzi kadru
        /// na drugą, kołysząc się. Każdy niesie słabe światło.
        /// </summary>
        IEnumerator Procession()
        {
            var island = Island.Instance;
            var cam = Camera.main;
            var art = island != null ? island.Art : null;
            var frames = art != null ? art.wispFrames : null;
            if (island == null || cam == null || frames == null || frames.Length == 0 || frames[0] == null)
                yield break;
            float hw = cam.orthographicSize * cam.aspect + 1f;
            float dir = Random.value < 0.5f ? 1f : -1f;
            float y = island.WaterBelow(Random.Range(20, 34)).y;
            float x0 = cam.transform.position.x - dir * hw, x1 = cam.transform.position.x + dir * hw;
            int n = Random.Range(5, 8);
            var lights = new List<SpriteRenderer>();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Procession");
                go.transform.SetParent(transform, false);
                spawned.Add(go);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.flipX = dir < 0f;
                sr.sortingOrder = SkyOrder - 50;   // nad wodą i mgłą przy brzegu
                if (art.glow != null)
                    sr.sharedMaterial = art.glow;
                sr.color = new Color(0.75f, 0.9f, 1f, 0f);
                NightGlow.Add(go, new Vector2(0f, 0.4f), new Color(0.6f, 0.85f, 1f), 0.9f, 0.3f);
                lights.Add(sr);
            }
            const float speed = 0.7f;   // kafelki na sekundę
            float length = Mathf.Abs(x1 - x0) + n * 0.8f;
            for (float t = 0f; t < length / speed; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < lights.Count; i++)
                {
                    var sr = lights[i];
                    if (sr == null)
                        continue;
                    float x = x0 + dir * (t * speed - i * 0.8f);
                    float bob = Mathf.Sin(Time.unscaledTime * 2f + i) * 0.12f;
                    sr.transform.position = new Vector3(Mathf.Round(x * Ppu) / Ppu, Mathf.Round((y + 0.3f + bob) * Ppu) / Ppu, 0f);
                    sr.sprite = frames[Mathf.FloorToInt(Time.unscaledTime * 8f + i) % frames.Length];
                    float inside = Mathf.Clamp01(1f - Mathf.Abs(x - cam.transform.position.x) / hw);
                    sr.color = new Color(0.75f, 0.9f, 1f, 0.75f * Mathf.Clamp01(inside * 3f));   // wyłaniają się i znikają przy krawędzi
                }
                yield return null;
            }
            foreach (var sr in lights)
                if (sr != null)
                {
                    spawned.Remove(sr.gameObject);
                    Destroy(sr.gameObject);
                }
        }
    }
}
