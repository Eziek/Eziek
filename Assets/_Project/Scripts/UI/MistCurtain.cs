using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Wyspa.UI
{
    /// <summary>
    /// Kurtyna mgły do scenek rejsu: pikselowe kłęby (2x2 piksele UI, paleta mgły) zamykają okno od brzegów do środka
    /// (Close) albo rozstępują się od środka (Open). Pokrycie liczone co klatkę na małej teksturze: próg = odległość od
    /// brzegu okna + szum kłębów, który powoli dryfuje. Jasny brzeg kłębu, w głębi ciemniej (jak mgła w Fog.shader).
    /// </summary>
    public class MistCurtain : MonoBehaviour
    {
        const int Texel = 2;   // piksele UI na piksel mgły
        static readonly Color32[] Tones =
        {
            new Color32(0xa4, 0xac, 0xb6, 0xff),   // brzeg kłębu
            new Color32(0x84, 0x8e, 0x9b, 0xff),
            new Color32(0x56, 0x69, 0x7a, 0xff),
            new Color32(0x30, 0x48, 0x5f, 0xff),   // głęboko w mgle
        };

        RawImage image;
        Texture2D tex;
        Color32[] px;
        float[] edge, grain;
        int w, h;
        float coverage;

        /// <summary>0 = czysto, 1 = całe okno w mgle.</summary>
        public float Coverage
        {
            get => coverage;
            set
            {
                coverage = Mathf.Clamp01(value);
                Paint();
            }
        }

        public static MistCurtain Create(RectTransform canvasRoot, float startCoverage)
        {
            var img = Ui.Rect("MistCurtain", canvasRoot).gameObject.AddComponent<RawImage>();
            Ui.Stretch(img.rectTransform);
            img.raycastTarget = false;
            var c = img.gameObject.AddComponent<MistCurtain>();
            c.image = img;
            c.Allocate(canvasRoot.rect.size);
            c.Coverage = startCoverage;
            return c;
        }

        void Allocate(Vector2 size)
        {
            w = Mathf.Max(8, Mathf.CeilToInt(size.x / Texel));
            h = Mathf.Max(8, Mathf.CeilToInt(size.y / Texel));
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "MistCurtain" };
            image.texture = tex;
            px = new Color32[w * h];
            edge = new float[w * h];
            grain = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 0 przy brzegu okna, 1 w środku (elipsa dopasowana do okna)
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    edge[y * w + x] = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.25f);
                    grain[y * w + x] = Hash(x, y);
                }
        }

        void Update()
        {
            if (coverage > 0f && coverage < 1f)
                Paint();   // kłęby dryfują
            var parent = (RectTransform)transform.parent;
            if (parent != null && (Mathf.CeilToInt(parent.rect.width / Texel) != w || Mathf.CeilToInt(parent.rect.height / Texel) != h))
            {
                Allocate(parent.rect.size);   // okno zmieniło rozmiar
                Paint();
            }
        }

        void Paint()
        {
            if (tex == null)
                return;
            image.enabled = coverage > 0f;
            if (coverage <= 0f)
                return;
            float t = Time.unscaledTime * 0.6f;
            float u = coverage * 1.25f;   // przy 1 pokrywa też środek mimo szumu
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    // szum kłębów: dwie oktawy, wolny dryf w bok
                    float n = Cloud((x + t * 3f) / 9f, y / 7f) * 0.7f + Cloud((x - t * 2f) / 4f, (y + t) / 3f) * 0.3f;
                    float threshold = edge[i] * 0.78f + n * 0.22f;
                    float depth = u - threshold;
                    if (depth <= 0f)
                    {
                        px[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    // brzeg jaśniejszy, w głębi ciemniej; ziarno daje dithering na granicach tonów
                    float k = depth * 4.5f - (n - 0.5f) * 1.4f + (grain[i] - 0.5f) * 0.6f;   // wypukłe kłęby jaśniejsze
                    int tone = k < 0.6f ? 0 : k < 1.4f ? 1 : k < 2.6f ? 2 : 3;
                    px[i] = Tones[tone];
                }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        public IEnumerator To(float target, float seconds)
        {
            float from = coverage;
            for (float time = 0f; time < seconds; time += Time.unscaledDeltaTime)
            {
                float k = time / seconds;
                Coverage = Mathf.Lerp(from, target, k * k * (3f - 2f * k));
                yield return null;
            }
            Coverage = target;
        }

        public IEnumerator Close(float seconds) => To(1f, seconds);
        public IEnumerator Open(float seconds) => To(0f, seconds);

        void OnDestroy()
        {
            if (tex != null)
                Destroy(tex);
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>Szum wartości z gładką interpolacją (0..1).</summary>
        static float Cloud(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0), b = Hash(x0 + 1, y0), c = Hash(x0, y0 + 1), d = Hash(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
