using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Lighting;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Światło „wolumetryczne” (shader LightShafts): w dzień ciepłe ukośne snopy słońca, o zmierzchu pomarańczowe,
    /// w nocy blade promienie księżyca (mocniejsze w pełnię) i poświata wokół zapalonych lamp, jakby świeciły w mgle.
    /// Wartości zmieniają się płynnie; co 0.1 s, bo animacja i tak jest skokowa.
    /// </summary>
    public class LightShafts : MonoBehaviour
    {
        const int MaxLamps = 16;
        static readonly int ColorId = Shader.PropertyToID("_ShaftColor");
        static readonly int StrengthId = Shader.PropertyToID("_ShaftStrength");
        static readonly int AngleId = Shader.PropertyToID("_ShaftAngle");
        static readonly int MaskId = Shader.PropertyToID("_MaskRange");
        static readonly int HaloId = Shader.PropertyToID("_HaloStrength");
        static readonly int LampsId = Shader.PropertyToID("_Lamps");
        static readonly int LampColorsId = Shader.PropertyToID("_LampColors");
        static readonly int LampCountId = Shader.PropertyToID("_LampCount");

        [SerializeField] SpriteRenderer target;
        [Tooltip("Sprite mgły: Island podmienia w nim mapę danych w locie, my bierzemy tę samą.")]
        [SerializeField] SpriteRenderer dataSource;
        [SerializeField] GameClock clock;
        [Header("Dzień / zmierzch / noc")]
        [SerializeField] Color dayColor = new Color(0.13f, 0.10f, 0.055f);
        [SerializeField] Color duskColor = new Color(0.15f, 0.075f, 0.035f);
        [SerializeField] Color nightColor = new Color(0.045f, 0.06f, 0.10f);
        [SerializeField] float dayStrength = 0.55f;
        [SerializeField] float nightStrength = 0.3f;

        Material material;
        Color color;
        float strength = -1f, halo, angle = 0.5f;
        float timer;
        readonly Vector4[] lamps = new Vector4[MaxLamps];
        readonly Vector4[] lampColors = new Vector4[MaxLamps];

        public void Setup(SpriteRenderer renderer, SpriteRenderer data, GameClock gameClock)
        {
            target = renderer;
            dataSource = data;
            clock = gameClock;
        }

        /// <summary>Przezroczyste okno: światło tylko nad lądem i pasem wody (piksele od brzegu, 0 = wszędzie).</summary>
        public void SetMask(float pixels)
        {
            var m = EnsureMaterial();
            if (m != null)
                m.SetFloat(MaskId, pixels);
        }

        // kopia materiału tylko dla tej sceny (tablice lamp z kodu nie zapisują się w assecie)
        Material EnsureMaterial()
        {
            if (material == null && target != null && target.sharedMaterial != null)
            {
                material = new Material(target.sharedMaterial);
                target.sharedMaterial = material;
            }
            return material;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 0.1f;
            if (target != null && dataSource != null && target.sprite != dataSource.sprite)
                target.sprite = dataSource.sprite;   // nowa mapa danych po rozbudowie wyspy
            var m = EnsureMaterial();
            if (m == null || clock == null)
                return;

            bool night = clock.IsNight;
            Color c; float s, a, h;
            if (night)
            {
                // księżyc: w pełnię jasne promienie, w nów prawie żadnych
                float moon = 1f - Mathf.Abs(clock.MoonPhase - 0.5f) * 2f;
                c = nightColor; s = nightStrength * (0.35f + 0.65f * moon); a = -0.35f; h = 1f;
            }
            else if (clock.IsDusk)
            {
                c = duskColor; s = dayStrength; a = 0.9f; h = 0.6f;
            }
            else
            {
                c = dayColor; s = dayStrength; a = 0.5f; h = 0f;
            }
            if (strength < 0f)
            {
                color = c; strength = s; angle = a; halo = h;
            }
            else
            {
                // płynne przejścia między porami dnia (ok. 1-2 s)
                const float k = 0.01f;
                color = Color.Lerp(color, c, k * 10f);
                strength = Mathf.Lerp(strength, s, k * 10f);
                angle = Mathf.Lerp(angle, a, k * 10f);
                halo = Mathf.Lerp(halo, h, k * 10f);
            }
            m.SetColor(ColorId, color);
            m.SetFloat(StrengthId, strength);
            m.SetFloat(AngleId, angle);
            m.SetFloat(HaloId, halo);

            int n = 0;
            foreach (var lamp in Lamp.All)
            {
                if (n >= MaxLamps || lamp == null || !lamp.IsOn || lamp.Definition == null)
                    continue;
                var p = Island.WorldToPixel(lamp.LightCenter);
                lamps[n] = new Vector4(p.x, p.y, lamp.Definition.radius * 16f * 0.7f, Mathf.Clamp01(lamp.Definition.intensity));
                var lc = lamp.Definition.color;
                lampColors[n] = new Vector4(lc.r, lc.g, lc.b, 1f);
                n++;
            }
            m.SetVectorArray(LampsId, lamps);
            m.SetVectorArray(LampColorsId, lampColors);
            m.SetFloat(LampCountId, n);
        }
    }
}
