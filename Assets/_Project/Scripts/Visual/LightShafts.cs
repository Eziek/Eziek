using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;

namespace Wyspa.Visual
{
    /// <summary>
    /// Snopy światła: Sprite Light 2D z wygenerowanym obrazem ukośnych
    /// snopów, z włączoną wolumetrią (Light2D.volumeIntensity), więc snopy rozjaśniają wyspę jak lampa i widać je w powietrzu.
    /// Dzień: ciepłe słońce, zmierzch: pomarańczowe, noc: blade promienie księżyca (mocniejsze w pełnię). Snopy wolno dryfują.
    /// Światło idzie za kamerą, więc obejmuje zawsze cały kadr.
    /// </summary>
    public class LightShafts : MonoBehaviour
    {
        const int TexSize = 256;
        const float WorldSize = 48f;   // jednostki świata pokryte przez sprite światła

        [SerializeField] SpriteRenderer target;     // stary nakładkowy shader (sprzed Light2D) - wyłączamy go
        [SerializeField] SpriteRenderer dataSource;
        [SerializeField] GameClock clock;
        [Header("Dzień / zmierzch / noc")]
        [SerializeField] Color dayColor = new Color(1f, 0.86f, 0.6f);
        [SerializeField] Color duskColor = new Color(1f, 0.6f, 0.35f);
        [SerializeField] Color nightColor = new Color(0.6f, 0.72f, 1f);
        [SerializeField] float dayIntensity = 0.35f;
        [SerializeField] float nightIntensity = 0.3f;
        [SerializeField] float dayVolume = 0.22f;
        [SerializeField] float nightVolume = 0.18f;

        Light2D shaft;
        Color color;
        float intensity = -1f, volume;
        float timer;

        public void Setup(SpriteRenderer renderer, SpriteRenderer data, GameClock gameClock)
        {
            target = renderer;
            dataSource = data;
            clock = gameClock;
        }

        /// <summary>Zgodność z wcześniejszą wersją (nakładka shaderowa miała maskę dla przezroczystego okna).</summary>
        public void SetMask(float pixels) { }

        void Start()
        {
            if (target != null)
                target.enabled = false;
            if (clock == null)
                clock = FindAnyObjectByType<GameClock>();
            var go = new GameObject("SunShafts");
            go.transform.SetParent(transform, false);
            shaft = go.AddComponent<Light2D>();
            shaft.lightType = Light2D.LightType.Sprite;
            shaft.lightCookieSprite = BuildBeams();
            shaft.volumeIntensityEnabled = true;
            shaft.intensity = 0f;
        }

        /// <summary>Kilka miękkich, ukośnych snopów różnej szerokości (jasne u góry, wygaszane ku dołowi).</summary>
        static Sprite BuildBeams()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SunShafts",
            };
            var px = new Color32[TexSize * TexSize];
            // snopy: położenie w poprzek (0..1), szerokość, jasność
            var beams = new[] { (0.12f, 0.035f, 0.8f), (0.27f, 0.06f, 1f), (0.41f, 0.025f, 0.6f), (0.55f, 0.07f, 0.9f),
                                (0.7f, 0.03f, 0.7f), (0.84f, 0.05f, 0.85f) };
            const float slope = 0.45f;   // ukos: snopy padają z lewej górnej strony
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    float u = x / (float)TexSize, v = y / (float)TexSize;   // v = 0 na dole
                    float across = u + (1f - v) * slope - slope * 0.5f;
                    float b = 0f;
                    foreach (var (pos, width, power) in beams)
                    {
                        float k = (across - pos) / width;
                        b += power * Mathf.Exp(-k * k);
                    }
                    b *= Mathf.Lerp(0.45f, 1f, v);   // u góry jaśniej
                    byte c = (byte)(Mathf.Clamp01(b) * 255f);
                    px[y * TexSize + x] = new Color32(c, c, c, c);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize / WorldSize);
        }

        void LateUpdate()
        {
            if (shaft == null)
                return;
            // za kamerą, z wolnym dryfem w bok (snopy przesuwają się po wyspie)
            var cam = Camera.main;
            var center = cam != null ? cam.transform.position : Vector3.zero;
            float drift = Mathf.Sin(Time.unscaledTime * 0.05f) * 2.5f;
            shaft.transform.position = new Vector3(center.x + drift, center.y, 0f);

            timer -= Time.unscaledDeltaTime;
            if (timer > 0f || clock == null)
                return;
            timer = 0.1f;
            Color c; float i, v;
            if (clock.IsNight)
            {
                float moon = 1f - Mathf.Abs(clock.MoonPhase - 0.5f) * 2f;   // pełnia = 1, nów = 0
                c = nightColor; i = nightIntensity * (0.3f + 0.7f * moon); v = nightVolume * (0.3f + 0.7f * moon);
            }
            else if (clock.IsDusk)
            {
                c = duskColor; i = dayIntensity; v = dayVolume;
            }
            else
            {
                c = dayColor; i = dayIntensity; v = dayVolume;
            }
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 0.3f);
            if (intensity < 0f)
            {
                color = c; intensity = i; volume = v;
            }
            else
            {
                color = Color.Lerp(color, c, 0.1f);   // płynne przejścia między porami dnia
                intensity = Mathf.Lerp(intensity, i, 0.1f);
                volume = Mathf.Lerp(volume, v, 0.1f);
            }
            shaft.color = color;
            shaft.intensity = intensity * breathe;
            // przezroczyste okno: bez poświaty w powietrzu (byłaby widoczna nad pulpitem), zostaje światło na wyspie
            var desk = Platform.DesktopWindow.Instance;
            shaft.volumeIntensity = desk != null && desk.Transparent ? 0f : volume * breathe;
        }
    }
}
