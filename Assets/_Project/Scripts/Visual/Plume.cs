using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Visual
{
    /// <summary>
    /// Dym albo para nad obiektem (GDD §9 p. 124): co chwilę z ujścia (komin pieca Żaru, brzeg kotła) wychodzi kłąb, wznosi się,
    /// rośnie (3 → 6 px) i rzednie (dithering w sprite'cie, potem przezroczystość). Znosi go stały lekki wiatr w prawo,
    /// mocniej w deszczu i burzy, a przechodzący podmuch (IslandFx.GustAt) odgina smugę wyraźnie w bok.
    /// Właściciel ustawia Emitting co klatkę. Sprite'y kłębów z kodu, siatka 16 px.
    /// </summary>
    public class Plume : MonoBehaviour
    {
        public enum Kind { Smoke, Steam }

        const float Ppu = 16f;
        const int MaxPuffs = 26;

        class Puff { public SpriteRenderer sr; public Vector2 pos, vel; public float age, life, seed; }

        Kind kind;
        Vector3 mouth;          // ujście względem obiektu (jednostki świata)
        int order;
        float timer;
        readonly List<Puff> puffs = new List<Puff>();

        /// <summary>Czy z ujścia ma iść dym (piec się pali / w kotle się warzy).</summary>
        public bool Emitting { get; set; }

        public static Plume Attach(GameObject owner, Kind kind, Vector3 mouthOffset, int sortingOrder)
        {
            var p = owner.GetComponent<Plume>();
            if (p == null)
                p = owner.AddComponent<Plume>();
            p.kind = kind;
            p.mouth = mouthOffset;
            p.order = sortingOrder;
            return p;
        }

        /// <summary>Ujście i warstwa, gdy obiekt zmieni wygląd albo miejsce w kolejce rysowania.</summary>
        public void SetMouth(Vector3 mouthOffset, int sortingOrder)
        {
            mouth = mouthOffset;
            order = sortingOrder;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Emitting)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    timer = kind == Kind.Smoke ? Random.Range(0.18f, 0.3f) : Random.Range(0.25f, 0.4f);
                    Emit();
                }
            }
            float breeze = 2.5f * Weather.WeatherSystem.WindMultiplier;   // px/s w prawo
            foreach (var p in puffs)
            {
                if (p.age >= p.life)
                    continue;
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.sr.enabled = false;
                    continue;
                }
                float u = p.age / p.life;
                // podmuch: przechodząca fala pcha kłąb w prawo (wartość bezwzględna: smuga nie wraca pod wiatr)
                float gust = Mathf.Abs(IslandFx.GustAt(p.pos.x / Ppu));
                float side = breeze * (0.4f + u) + gust * 26f + Mathf.Sin(p.age * 2.3f + p.seed) * 1.2f;
                p.pos += new Vector2(p.vel.x + side, p.vel.y * (1f - 0.5f * u)) * dt;
                int size = Mathf.Min(3, Mathf.FloorToInt(u * 4f));                 // kłąb rośnie w 4 krokach
                p.sr.sprite = PuffSprite(kind, size, u > 0.55f);
                var c = p.sr.color;
                c.a = (kind == Kind.Smoke ? 0.7f : 0.5f) * Mathf.Clamp01((1f - u) * 2.2f) * Mathf.Clamp01(p.age * 6f);
                p.sr.color = c;
                p.sr.transform.position = new Vector3(Mathf.Round(p.pos.x) / Ppu, Mathf.Round(p.pos.y) / Ppu, 0f);
            }
        }

        void Emit()
        {
            var p = puffs.Find(q => q.age >= q.life);
            if (p == null)
            {
                if (puffs.Count >= MaxPuffs)
                    return;
                var go = new GameObject(kind == Kind.Smoke ? "Smoke" : "Steam");
                // w korzeniu sceny: nie trzęsie się ani nie skaluje z obiektem i nie trafia do odbić w wodzie (skan dzieci DecoRoot)
                p = new Puff { sr = go.AddComponent<SpriteRenderer>() };
                puffs.Add(p);
            }
            var start = transform.position + mouth;
            p.pos = new Vector2(start.x * Ppu + (kind == Kind.Smoke ? Random.Range(-1f, 1f) : Random.Range(-3f, 3f)), start.y * Ppu);   // para z całego kotła
            p.vel = new Vector2(Random.Range(-1.5f, 1.5f), kind == Kind.Smoke ? Random.Range(8f, 11f) : Random.Range(6f, 9f));
            p.age = 0f;
            p.life = kind == Kind.Smoke ? Random.Range(3.2f, 4.4f) : Random.Range(1.6f, 2.4f);
            p.seed = Random.value * 6.28f;
            p.sr.sortingOrder = order;
            p.sr.color = kind == Kind.Smoke ? new Color(0.36f, 0.36f, 0.4f, 0f) : new Color(0.82f, 0.86f, 0.88f, 0f);
            p.sr.enabled = true;
        }

        void OnDisable()
        {
            foreach (var p in puffs)
                if (p.sr != null) p.sr.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var p in puffs)
                if (p.sr != null) Destroy(p.sr.gameObject);
        }

        // ------------------------------------------------------------ sprite'y kłębów

        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>Kłąb: koło o średnicy 3-6 px, jaśniejszy wierzch; rzadki (thin) = dziurawy ditheringiem, gdy się rozwiewa.</summary>
        static Sprite PuffSprite(Kind kind, int size, bool thin)
        {
            int key = (int)kind * 100 + size * 10 + (thin ? 1 : 0);
            if (cache.TryGetValue(key, out var s) && s != null)
                return s;
            int d = 3 + size;
            var px = new Color32[d * d];
            float r = d / 2f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    if (dx * dx + dy * dy > r * r + 0.3f)
                        continue;
                    if (thin && Bayer[(y & 3) * 4 + (x & 3)] < 7)
                        continue;
                    byte v = (byte)(dy > 0.3f ? 255 : 205);   // górna połowa (y w górę) jaśniejsza
                    px[y * d + x] = new Color32(v, v, v, 255);
                }
            var tex = new Texture2D(d, d, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), Ppu);
            cache[key] = s;
            return s;
        }
    }
}
