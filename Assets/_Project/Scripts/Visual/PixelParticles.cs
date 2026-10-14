using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Visual
{
    /// <summary>
    /// Cząsteczki pikselowe (GDD §5.5): pula SpriteRendererów, ruch co klatkę, pozycje przyciągnięte do siatki pikseli.
    /// Własny system zamiast ParticleSystem, bo daje ostre piksele i ten sam „skokowy” rytm co reszta animacji.
    /// </summary>
    public class PixelParticles : MonoBehaviour
    {
        public static PixelParticles Instance { get; private set; }

        const float Ppu = 16f;
        // ruch co klatkę (płynnie), pozycje dalej przyciągnięte do siatki pikseli
        const float Step = 0f;
        const int MaxParticles = 160;

        [SerializeField] Material glowMaterial;

        class P
        {
            public SpriteRenderer sr;
            public Vector2 pos;      // piksele świata
            public Vector2 vel;      // piksele / s
            public float gravity;    // piksele / s²
            public float life, age;
            public Sprite[] frames;
            public float fps;
            public bool flutter;     // spadający liść: kołysze się na boki
            public float groundY;    // poniżej tej wysokości (piksele) cząstka leży
            public bool alive;
        }

        readonly List<P> pool = new List<P>();
        Material defaultMaterial;
        float acc;

        public void Setup(Material glow) => glowMaterial = glow;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        P Get()
        {
            foreach (var p in pool)
                if (!p.alive)
                    return p;
            if (pool.Count >= MaxParticles)
                return null;
            var go = new GameObject("Particle");
            go.transform.SetParent(transform, false);
            var p2 = new P { sr = go.AddComponent<SpriteRenderer>() };
            if (defaultMaterial == null)
                defaultMaterial = p2.sr.sharedMaterial;   // domyślny materiał sprite'ów URP 2D (oświetlany)
            pool.Add(p2);
            return p2;
        }

        /// <summary>Jedna cząstka. Pozycja w świecie, prędkość i grawitacja w pikselach na sekundę.</summary>
        public void Spawn(Vector3 world, Sprite[] frames, Vector2 velocity, float gravity, float life, int order,
                          bool glow = false, float fps = 10f, bool flutter = false, float fallPixels = -1f)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                return;
            var p = Get();
            if (p == null)
                return;
            p.pos = new Vector2(world.x * Ppu, world.y * Ppu);
            p.vel = velocity;
            p.gravity = gravity;
            p.life = life;
            p.age = 0f;
            p.frames = frames;
            p.fps = fps;
            p.flutter = flutter;
            p.groundY = fallPixels >= 0f ? p.pos.y - fallPixels : float.NegativeInfinity;
            p.alive = true;
            p.sr.sprite = frames[0];
            p.sr.sortingOrder = order;
            p.sr.sharedMaterial = glow && glowMaterial != null ? glowMaterial : defaultMaterial;
            p.sr.transform.localScale = Vector3.one;
            p.sr.gameObject.SetActive(true);
            Place(p);
        }

        /// <summary>Kilka cząstek rozrzuconych w górę (wachlarz), np. ziemia przy zbiorze.</summary>
        public void Burst(Vector3 world, Sprite[] sprites, int count, float speed, float gravity, float life, int order, bool glow = false)
        {
            if (sprites == null || sprites.Length == 0)
                return;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(0.25f, 0.75f, (i + Random.value) / count) * Mathf.PI;   // 45°..135°
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.6f, 1.1f);
                var s = sprites[Random.Range(0, sprites.Length)];
                Spawn(world, new[] { s }, v, gravity, life * Random.Range(0.8f, 1.2f), order, glow);
            }
        }

        void Update()
        {
            acc += Time.unscaledDeltaTime;
            if (acc < Step)
                return;
            float dt = acc;
            acc = 0f;
            foreach (var p in pool)
            {
                if (!p.alive)
                    continue;
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.alive = false;
                    p.sr.gameObject.SetActive(false);
                    continue;
                }
                if (p.pos.y > p.groundY)
                {
                    p.vel.y -= p.gravity * dt;
                    p.pos += p.vel * dt;
                    if (p.pos.y <= p.groundY)
                        p.pos.y = p.groundY;
                }
                int f = p.frames.Length > 1 ? Mathf.Min(p.frames.Length - 1, Mathf.FloorToInt(p.age * p.fps)) : 0;
                p.sr.sprite = p.frames[f];
                // ostatnie 20% życia: miganie zamiast przezroczystości (pikselowo)
                p.sr.enabled = p.age < p.life * 0.8f || Mathf.FloorToInt(p.age * 12f) % 2 == 0;
                Place(p);
            }
        }

        void Place(P p)
        {
            float x = p.pos.x;
            if (p.flutter && p.pos.y > p.groundY)
                x += Mathf.Round(2f * Mathf.Sin(p.age * 6f));
            // piksele sprite'a mają leżeć na siatce: dodajemy ułamek pivota (np. 0.5 dla środka nieparzystej szerokości)
            var pv = p.sr.sprite != null ? p.sr.sprite.pivot : Vector2.zero;
            float ox = pv.x - Mathf.Floor(pv.x), oy = pv.y - Mathf.Floor(pv.y);
            p.sr.transform.position = new Vector3((Mathf.Round(x) + ox) / Ppu, (Mathf.Round(p.pos.y) + oy) / Ppu, 0f);
        }
    }
}
