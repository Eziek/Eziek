using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Visual
{
    /// <summary>
    /// Obiekt na wodzie (po teście gracza: „łódka powinna być w wodzie, a nie nad wodą, i brak fal przy niej i przy boi”):
    /// materiał SpriteFloat chowa dno pod linią wody, kołysze ją o piksel i rysuje pianę przy burtach,
    /// a co kilka sekund od burt rozchodzą się dwa kręgi na wodzie (sprite'y w siatce 32 px, jak woda).
    /// Łódź wypraw, łódź kupca-widma, boja sieci. Materiał z IslandArt.floatMat (Wyspa → 2).
    /// </summary>
    public class Floating : MonoBehaviour
    {
        const float Ppu = 16f;              // piksele sprite'ów świata
        const float RipplePpu = 32f;        // kręgi w siatce wody
        const float RipplePeriod = 2.6f;    // co ile sekund nowy krąg
        const float RippleLife = 2.2f;
        static readonly Color RippleColor = new Color32(0x84, 0x8e, 0x9b, 0xff);
        static readonly Dictionary<int, Sprite> RingCache = new Dictionary<int, Sprite>();
        static readonly int WaterlineId = Shader.PropertyToID("_Waterline");
        static readonly int PhaseId = Shader.PropertyToID("_BobPhase");

        SpriteRenderer body;
        readonly SpriteRenderer[] rings = new SpriteRenderer[2];
        float waterline;          // texele od dołu sprite'a
        float timer;
        float phase;

        /// <summary>waterline = ile pikseli sprite'a od dołu jest pod wodą (kadłub łodzi ok. 4).</summary>
        public static Floating Attach(SpriteRenderer body, float waterline, Material floatMat)
        {
            if (body == null)
                return null;
            var f = body.GetComponent<Floating>();
            if (f == null)
                f = body.gameObject.AddComponent<Floating>();
            f.Init(body, waterline, floatMat);
            return f;
        }

        void Init(SpriteRenderer renderer, float line, Material floatMat)
        {
            body = renderer;
            waterline = line;
            phase = (transform.position.x * 1.7f + transform.position.y * 0.9f) % 6.2831853f;
            timer = phase % RipplePeriod;
            if (floatMat != null)
                body.sharedMaterial = floatMat;
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetFloat(WaterlineId, waterline);
            block.SetFloat(PhaseId, phase);
            body.SetPropertyBlock(block);
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] != null)
                    continue;
                var go = new GameObject("Ripple");
                go.transform.SetParent(transform, false);
                rings[i] = go.AddComponent<SpriteRenderer>();
                rings[i].enabled = false;
            }
        }

        void Update()
        {
            if (body == null || body.sprite == null)
                return;
            timer += Time.unscaledDeltaTime;
            float wl = WaterlineWorldY();
            int width = Mathf.RoundToInt(body.sprite.rect.width * RipplePpu / Ppu) + 4;
            for (int i = 0; i < rings.Length; i++)
            {
                var r = rings[i];
                if (r == null)
                    continue;
                // dwa kręgi przesunięte o pół okresu; nie ma ich, gdy łódź jest niewidoczna (wyprawa, kupiec odpłynął)
                float age = (timer + i * RipplePeriod * 0.5f) % RipplePeriod;
                bool on = body.enabled && body.color.a > 0.05f && age < RippleLife;
                r.enabled = on;
                if (!on)
                    continue;
                float u = age / RippleLife;
                r.sprite = Ring(width + Mathf.RoundToInt(u * 10f) * 2);
                r.sortingOrder = body.sortingOrder - 1;
                r.transform.position = new Vector3(transform.position.x + BodyCenterOffset(), wl, transform.position.z);
                var c = RippleColor;
                c.a = (1f - u) * 0.55f * body.color.a;
                r.color = c;
            }
        }

        /// <summary>Linia wody na świecie: dół sprite'a + waterline pikseli.</summary>
        float WaterlineWorldY()
        {
            var s = body.sprite;
            float bottom = transform.position.y - s.pivot.y / s.pixelsPerUnit;
            return bottom + waterline / Ppu;
        }

        /// <summary>Środek sprite'a w poziomie względem punktu obiektu (pivot nie zawsze jest pośrodku; flipX odwraca).</summary>
        float BodyCenterOffset()
        {
            var s = body.sprite;
            float off = (s.rect.width / 2f - s.pivot.x) / s.pixelsPerUnit;
            return body.flipX ? -off : off;
        }

        /// <summary>Elipsa (obrys 1 px) o szerokości w pikselach wody, wysokość ok. 1/5 szerokości. Pamięć podręczna.</summary>
        static Sprite Ring(int width)
        {
            width = Mathf.Max(6, width);
            if (RingCache.TryGetValue(width, out var cached) && cached != null)
                return cached;
            int height = Mathf.Max(4, width / 5) | 1;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "Ripple" + width };
            var px = new Color32[width * height];
            float rx = (width - 1) / 2f, ry = (height - 1) / 2f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - rx) / rx, dy = (y - ry) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // obrys: tylko dolna i boczna część elipsy (górę zasłania łódź), co drugi piksel przerwany jak fala
                    bool edge = Mathf.Abs(d - 1f) < 1.2f / Mathf.Min(rx, ry * 3f) && y <= ry + 1 && ((x / 2) % 3 != 2);
                    px[y * width + x] = edge ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), RipplePpu, 0, SpriteMeshType.FullRect);
            RingCache[width] = sprite;
            return sprite;
        }
    }
}
