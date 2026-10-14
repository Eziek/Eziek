using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;

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
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (firefly == null || firefly.Length == 0)
                return;
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

        public static void Grew(Vector3 cropPos, int order)
        {
            var fx = Instance;
            if (fx != null && fx.particles != null)
                fx.particles.Burst(cropPos + new Vector3(0f, 6f / Ppu, 0f), fx.leaves, 4, 30f, 120f, 0.6f, order + 1);
        }

        // ------------------------------------------------------------ tło

        void Update()
        {
            acc += Time.unscaledDeltaTime;
            if (acc < 0.1f)
                return;
            float dt = acc;
            acc = 0f;
            frame++;
            bool night = clock != null && clock.IsNight;
            UpdateFireflies(night);

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
            for (int i = 0; i < flies.Length; i++)
            {
                int t = frame + flyPhase[i];
                bool visible = night && t % 16 < 11;
                var sr = flies[i];
                sr.enabled = visible;
                if (!visible)
                    continue;
                bool halo = t % 16 >= 3 && t % 16 <= 6 && firefly.Length > 1;
                sr.sprite = firefly[halo ? 1 : 0];
                float x = flyBase[i].x + Mathf.Round(3f * Mathf.Sin(t * 0.25f));
                float y = flyBase[i].y + Mathf.Round(2f * Mathf.Cos(t * 0.18f));
                sr.transform.position = new Vector3((x + 0.5f) / Ppu, (y + 0.5f) / Ppu, 0f);
            }
        }

        void SparkleOverRipe()
        {
            if (particles == null || sparkle == null || sparkle.Length < 3)
                return;
            int n = 0;
            foreach (var p in FarmPlot.All)
                if (p.State.IsRipe) n++;
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
            if (particles == null || leafFall == null || trees == null || trees.Length == 0)
                return;
            var tree = trees[Random.Range(0, trees.Length)];
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
