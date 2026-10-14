using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Efekty wyspy (GDD §5.5): zbiór (plon wyskakuje, sypie się ziemia), sadzenie, „pop” wzrostu (listki),
    /// iskierki nad dojrzałymi, świetliki w nocy, spadające liście w dzień. Gra woła metody statyczne (bez sceny nic nie robią).
    /// </summary>
    public class IslandFx : MonoBehaviour
    {
        public static IslandFx Instance { get; private set; }

        const float Ppu = 16f;

        [SerializeField] PixelParticles particles;
        [SerializeField] GameClock clock;
        [SerializeField] Material glowMaterial;
        [SerializeField] Sprite[] soil;
        [SerializeField] Sprite[] leaves;
        [SerializeField] Sprite leafFall;
        [SerializeField] Sprite[] sparkle;     // 3 klatki: kropka, krzyżyk, gwiazdka
        [SerializeField] Sprite[] firefly;     // 2 klatki: kropka, z poświatą
        [SerializeField] Transform[] trees;
        [Tooltip("Obszar świetlików (jednostki świata).")]
        [SerializeField] Rect fireflyArea = new Rect(-5f, -3f, 10f, 6f);
        [SerializeField] int fireflyCount = 10;

        SpriteRenderer[] flies;
        Vector2[] flyBase;
        int[] flyPhase;
        float acc;
        int frame;
        float sparkleTimer;
        float leafTimer = 4f;

        public void Setup(PixelParticles pool, GameClock gameClock, Material glow, Sprite[] soilSprites, Sprite[] leafSprites,
                          Sprite fallingLeaf, Sprite[] sparkleFrames, Sprite[] fireflyFrames, Transform[] treeTransforms, Rect area)
        {
            particles = pool;
            clock = gameClock;
            glowMaterial = glow;
            soil = soilSprites;
            leaves = leafSprites;
            leafFall = fallingLeaf;
            sparkle = sparkleFrames;
            firefly = fireflyFrames;
            trees = treeTransforms;
            fireflyArea = area;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Island.Instance != null)
                Island.Instance.Rebuilt -= PlaceFireflies;
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (firefly == null || firefly.Length == 0)
                return;
            var island = Island.Instance;
            if (island != null)
                island.Rebuilt += PlaceFireflies;
            var rnd = new System.Random(5);
            flies = new SpriteRenderer[fireflyCount];
            flyBase = new Vector2[fireflyCount];
            flyPhase = new int[fireflyCount];
            for (int i = 0; i < fireflyCount; i++)
            {
                var go = new GameObject("Firefly");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = firefly[0];
                sr.sortingOrder = 3000;
                if (glowMaterial != null)
                    sr.sharedMaterial = glowMaterial;
                sr.enabled = false;
                flies[i] = sr;
                flyBase[i] = new Vector2(
                    Mathf.Round((fireflyArea.xMin + (float)rnd.NextDouble() * fireflyArea.width) * Ppu),
                    Mathf.Round((fireflyArea.yMin + (float)rnd.NextDouble() * fireflyArea.height) * Ppu));
                flyPhase[i] = rnd.Next(48);
            }
            PlaceFireflies();
        }

        /// <summary>Świetliki krążą nad lądem: po każdej rozbudowie dostają nowe miejsca w prostokącie wyspy.</summary>
        void PlaceFireflies()
        {
            if (flies == null)
                return;
            var island = Island.Instance;
            var area = island != null && island.Ready ? island.LandRect : fireflyArea;
            var rnd = new System.Random(5);
            for (int i = 0; i < flyBase.Length; i++)
                flyBase[i] = new Vector2(
                    Mathf.Round((area.xMin + (float)rnd.NextDouble() * area.width) * Ppu),
                    Mathf.Round((area.yMin + 0.3f + (float)rnd.NextDouble() * Mathf.Max(0f, area.height - 0.3f)) * Ppu));
        }

        // ------------------------------------------------------------ wywołania z gry

        public static void Harvest(Vector3 cropPos, Sprite ripe, int order)
        {
            var fx = Instance;
            if (fx == null || fx.particles == null)
                return;
            if (ripe != null)
                fx.particles.Spawn(cropPos, new[] { ripe }, new Vector2(0f, 42f), 170f, 0.45f, order + 2);
            fx.particles.Burst(cropPos, fx.soil, 6, 38f, 160f, 0.5f, order + 1);
        }

        public static void Planted(Vector3 cropPos, int order)
        {
            var fx = Instance;
            if (fx != null && fx.particles != null)
                fx.particles.Burst(cropPos, fx.soil, 3, 26f, 150f, 0.35f, order + 1);
        }

        /// <summary>Kupiony kafelek: piana i woda tryskają wokół, ląd wynurza się z morza.</summary>
        public static void TileClaimed(Vector3 tileCenter, Sprite[] foam)
        {
            var fx = Instance;
            if (fx == null || fx.particles == null)
                return;
            var sprites = foam != null && foam.Length > 0 ? foam : fx.soil;
            for (int i = 0; i < 4; i++)
            {
                var corner = tileCenter + new Vector3((i % 2 == 0 ? -6 : 6) / Ppu, (i < 2 ? -6 : 6) / Ppu, 0f);
                fx.particles.Burst(corner, sprites, 3, 34f, 150f, 0.5f, 1000);
            }
        }

        /// <summary>Znalezisko wyskakuje z kafelka jak plon przy zbiorze (iskierki).</summary>
        /// <summary>Zbiór owoców z drzewa: owoce spadają z korony na ziemię i znikają (zamiast podskakującego drzewa).</summary>
        public static void FruitDrop(Vector3 canopy, float dropPixels, Sprite fruit, int count, int order)
        {
            var fx = Instance;
            if (fx == null || fx.particles == null || fruit == null)
                return;
            for (int i = 0; i < count; i++)
            {
                var at = canopy + new Vector3(Random.Range(-7, 8) / Ppu, Random.Range(-3, 4) / Ppu, 0f);
                var v = new Vector2(Random.Range(-14f, 14f), Random.Range(8f, 22f));
                fx.particles.Spawn(at, new[] { fruit }, v, 220f, 1.1f, order + 2, false, 10f, false, dropPixels + Random.Range(-2f, 3f));
            }
        }

        /// <summary>Liście sypią się z korony (ścięte drzewo, podmuch).</summary>
        public static void LeafBurst(Vector3 at, int count, int order)
        {
            var fx = Instance;
            if (fx != null && fx.particles != null && fx.leaves != null && fx.leaves.Length > 0)
                fx.particles.Burst(at, fx.leaves, count, 34f, 120f, 0.9f, order + 2);
        }

        public static void FindPopped(Vector3 at)
        {
            var fx = Instance;
            if (fx == null || fx.particles == null || fx.sparkle == null || fx.sparkle.Length < 3)
                return;
            var frames = new[] { fx.sparkle[0], fx.sparkle[1], fx.sparkle[2], fx.sparkle[1], fx.sparkle[0] };
            for (int i = 0; i < 3; i++)
                fx.particles.Spawn(at + new Vector3((i - 1) * 5 / Ppu, (4 + i % 2 * 4) / Ppu, 0f), frames,
                    new Vector2(0f, 20f), 0f, 0.6f, 2000, true);
        }

        public static void Grew(Vector3 cropPos, int order)
        {
            var fx = Instance;
            if (fx != null && fx.particles != null)
                fx.particles.Burst(cropPos + new Vector3(0f, 6f / Ppu, 0f), fx.leaves, 4, 30f, 120f, 0.6f, order + 1);
        }

        // ------------------------------------------------------------ tło

        void Update()
        {
            bool night = clock != null && clock.IsNight;
            UpdateFireflies(night);   // płynnie, co klatkę
            UpdateWind(night);
            UpdateMotes(night);
            acc += Time.unscaledDeltaTime;
            if (acc < 0.1f)
                return;
            float dt = acc;
            acc = 0f;
            frame++;

            sparkleTimer -= dt;
            if (sparkleTimer <= 0f)
            {
                sparkleTimer = Random.Range(0.8f, 1.6f);
                SparkleOverRipe();
            }

            leafTimer -= dt;
            if (leafTimer <= 0f)
            {
                leafTimer = Random.Range(5f, 12f);
                if (!night)
                    DropLeaf();
            }
        }

        // jak w prototypie (anim.py): ósemka 3x2 px, widoczny 11 z 16 klatek, poświata w klatkach 3-6
        void UpdateFireflies(bool night)
        {
            if (flies == null)
                return;
            float ft = Time.unscaledTime * 10f;   // klatki jak w prototypie, ale ruch ciągły
            for (int i = 0; i < flies.Length; i++)
            {
                float tf = ft + flyPhase[i];
                int t = Mathf.FloorToInt(tf);
                bool visible = night && t % 16 < 11;
                var sr = flies[i];
                sr.enabled = visible;
                if (!visible)
                    continue;
                bool halo = t % 16 >= 3 && t % 16 <= 6 && firefly.Length > 1;
                sr.sprite = firefly[halo ? 1 : 0];
                float x = flyBase[i].x + Mathf.Round(3f * Mathf.Sin(tf * 0.25f));
                float y = flyBase[i].y + Mathf.Round(2f * Mathf.Cos(tf * 0.18f));
                sr.transform.position = new Vector3((x + 0.5f) / Ppu, (y + 0.5f) / Ppu, 0f);
            }
        }

        // ------------------------------------------------------------ wiatr i pyłki (prośba gracza: więcej życia w powietrzu)

        class Streak { public SpriteRenderer sr; public Vector2 pos; public float speed, age, life, phase; }
        readonly List<Streak> streaks = new List<Streak>();
        class Mote { public SpriteRenderer sr; public Vector2 pos; public float speed, phase, age, life; }
        readonly List<Mote> motes = new List<Mote>();
        float gustTimer = 3f;
        // trwający podmuch: czoło przesuwa się nad wyspą (piksele świata), rośliny pochylają się, gdy do nich dotrze
        float gustFront, gustEnd, gustPower;
        bool gusting;
        const float GustSpeed = 55f;   // px/s, jak smugi wiatru
        static readonly int WindFrontId = Shader.PropertyToID("_WyspaWindFront");
        static readonly int WindPowerId = Shader.PropertyToID("_WyspaWindPower");
        Sprite streakSprite, moteSprite;
        static readonly Color DayMote = new Color(1f, 0.9f, 0.6f, 0.8f);
        static readonly Color NightMote = new Color(0.45f, 1f, 0.9f, 0.7f);

        /// <summary>Sprite z kodu: pozioma smuga (jaśniejsza z przodu) albo jeden piksel.</summary>
        static Sprite MakeSprite(int w, bool gradient)
        {
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var px = new Color32[w];
            for (int x = 0; x < w; x++)
            {
                byte a = gradient ? (byte)(60 + 195 * x / Mathf.Max(1, w - 1)) : (byte)255;
                px[x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), Ppu);
        }

        Rect Area()
        {
            var island = Island.Instance;
            return island != null && island.Ready ? island.LandRect : fireflyArea;
        }

        /// <summary>Podmuchy: co kilka sekund 2-4 jasne smugi przelatują nad wyspą z lewej na prawą, po łagodnej fali.</summary>
        void UpdateWind(bool night)
        {
            float dt = Time.unscaledDeltaTime;
            if (!gusting)
                gustTimer -= dt;   // nowy podmuch dopiero po poprzednim (inaczej rośliny przeskoczyłyby w pół drgania)
            if (gustTimer <= 0f)
            {
                gustTimer = Random.Range(3f, 8f);
                if (streakSprite == null)
                    streakSprite = MakeSprite(10, true);
                var area = Area();
                int n = Random.Range(2, 5);
                for (int i = 0; i < n; i++)
                {
                    var s = streaks.Find(q => q.age >= q.life);
                    if (s == null)
                    {
                        var go = new GameObject("Wind");
                        go.transform.SetParent(transform, false);
                        s = new Streak { sr = go.AddComponent<SpriteRenderer>() };
                        s.sr.sprite = streakSprite;
                        s.sr.sortingOrder = 4500;
                        if (glowMaterial != null)
                            s.sr.sharedMaterial = glowMaterial;
                        streaks.Add(s);
                    }
                    s.pos = new Vector2((area.xMin - 1.5f) * Ppu - i * 14f, (area.yMin + Random.value * area.height) * Ppu);
                    s.speed = Random.Range(45f, 70f);
                    s.age = 0f;
                    s.life = (area.width + 3f) * Ppu / s.speed;
                    s.phase = Random.value * 6.28f;
                    s.sr.color = night ? new Color(0.6f, 0.75f, 0.9f, 0f) : new Color(1f, 1f, 0.95f, 0f);
                    s.sr.enabled = true;
                }
                // czoło podmuchu startuje za lewym brzegiem; koniec, gdy ostatnie rośliny przestaną się bujać (ok. 4 szerokości fali)
                gustFront = (area.xMin - 1.5f) * Ppu;
                gustEnd = (area.xMax + 1f) * Ppu + 160f;
                gustPower = Random.Range(0.6f, 1f);
                gusting = true;
            }
            if (gusting)
            {
                gustFront += GustSpeed * dt;
                if (gustFront > gustEnd)
                    gusting = false;
                Shader.SetGlobalFloat(WindFrontId, gustFront);
                Shader.SetGlobalFloat(WindPowerId, gusting ? gustPower : 0f);
            }
            foreach (var s in streaks)
            {
                if (s.age >= s.life)
                {
                    s.sr.enabled = false;
                    continue;
                }
                s.age += dt;
                s.pos.x += s.speed * dt;
                float y = s.pos.y + Mathf.Sin(s.age * 2.2f + s.phase) * 3f;
                float u = s.age / s.life;
                var c = s.sr.color;
                c.a = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (night ? 0.25f : 0.4f);   // pojawia się i znika płynnie
                s.sr.color = c;
                s.sr.transform.position = new Vector3(Mathf.Round(s.pos.x) / Ppu, Mathf.Round(y) / Ppu, 0f);
            }
        }

        /// <summary>Pyłki: w dzień ciepłe drobinki w powietrzu, w nocy rzadsze turkusowe zarodniki. Unoszą się i znikają.</summary>
        void UpdateMotes(bool night)
        {
            int want = night ? 6 : 14;
            if (moteSprite == null)
                moteSprite = MakeSprite(1, false);
            while (motes.Count < 14)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                var m = new Mote { sr = go.AddComponent<SpriteRenderer>() };
                m.sr.sprite = moteSprite;
                m.sr.sortingOrder = 4400;
                if (glowMaterial != null)
                    m.sr.sharedMaterial = glowMaterial;
                m.sr.enabled = false;
                motes.Add(m);
            }
            float dt = Time.unscaledDeltaTime;
            var area = Area();
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                if (i >= want)
                {
                    m.sr.enabled = false;
                    m.age = m.life;
                    continue;
                }
                if (m.age >= m.life)
                {
                    m.pos = new Vector2((area.xMin + Random.value * area.width) * Ppu, (area.yMin + Random.value * area.height) * Ppu);
                    m.speed = Random.Range(2f, 5f);
                    m.phase = Random.value * 6.28f;
                    m.age = 0f;
                    m.life = Random.Range(4f, 9f);
                }
                m.age += dt;
                m.pos.y += m.speed * dt;
                float x = m.pos.x + Mathf.Sin(m.age * 0.9f + m.phase) * 4f;
                var c = night ? NightMote : DayMote;
                c.a *= Mathf.Sin(Mathf.Clamp01(m.age / m.life) * Mathf.PI);
                m.sr.color = c;
                m.sr.enabled = true;
                m.sr.transform.position = new Vector3((Mathf.Round(x) + 0.5f) / Ppu, (Mathf.Round(m.pos.y) + 0.5f) / Ppu, 0f);
            }
        }

        void SparkleOverRipe()
        {
            if (particles == null || sparkle == null || sparkle.Length < 3)
                return;
            int n = 0;
            foreach (var p in FarmPlot.All)
                if (p.State.IsRipe) n++;
            // owocujące drzewa też iskrzą (połowa szans, iskra w koronie)
            foreach (var t in FruitTree.All)
                if (t.Fruiting && Random.value < 0.5f)
                {
                    var at = t.transform.position + new Vector3(Random.Range(-9, 10) / Ppu, Random.Range(14, 30) / Ppu, 0f);
                    var fr = new[] { sparkle[0], sparkle[1], sparkle[2], sparkle[1], sparkle[0] };
                    particles.Spawn(at, fr, Vector2.zero, 0f, 0.5f, 2000, true);
                    return;
                }
            if (n == 0)
                return;
            int pick = Random.Range(0, n);
            foreach (var p in FarmPlot.All)
            {
                if (!p.State.IsRipe || pick-- > 0)
                    continue;
                var pos = p.CropPosition + new Vector3(Random.Range(-6, 7) / Ppu, Random.Range(6, 17) / Ppu, 0f);
                var frames = new[] { sparkle[0], sparkle[1], sparkle[2], sparkle[1], sparkle[0] };
                particles.Spawn(pos, frames, Vector2.zero, 0f, 0.5f, 2000, true);
                return;
            }
        }

        void DropLeaf()
        {
            var list = new System.Collections.Generic.List<Transform>();
            if (Island.Instance != null)
                list.AddRange(Island.Instance.Trees);
            else if (trees != null)
                list.AddRange(trees);
            if (particles == null || leafFall == null || list.Count == 0)
                return;
            var tree = list[Random.Range(0, list.Count)];
            if (tree == null)
                return;
            float startUp = Random.Range(18, 29);
            float landDown = Random.Range(0, 6);
            var pos = tree.position + new Vector3(Random.Range(-8, 9) / Ppu, startUp / Ppu, 0f);
            var sr = tree.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder + 1 : 500;
            particles.Spawn(pos, new[] { leafFall }, new Vector2(0f, -10f), 0f, 5f, order, false, 10f, true, startUp + landDown);
        }
    }
}
