using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;

namespace Wyspa.Lighting
{
    /// <summary>Kolor i jasność Global Light 2D wg pory dnia (zegar komputera).</summary>
    public class DayNightLighting : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] Light2D globalLight;

        [Tooltip("Kolor otoczenia w ciągu doby (0 = północ, 0.5 = południe).")]
        [SerializeField] Gradient ambientColor = DefaultColor();

        [SerializeField] AnimationCurve ambientIntensity = DefaultIntensity();

        [Tooltip("Najniższa jasność - noc nie może być czarną dziurą na jasnym pulpicie.")]
        [Range(0f, 1f)] [SerializeField] float minBrightness = 0.3f;

        public float MinBrightness
        {
            get => minBrightness;
            set => minBrightness = Mathf.Clamp01(value);
        }

        void Update()
        {
            if (clock == null || globalLight == null)
                return;
            float t = clock.Hour / 24f;
            globalLight.color = ambientColor.Evaluate(t);
            globalLight.intensity = Mathf.Max(minBrightness, ambientIntensity.Evaluate(t));
        }

        static Gradient DefaultColor()
        {
            var night = new Color(0.48f, 0.55f, 0.95f);
            var dawn = new Color(0.8f, 0.7f, 0.9f);
            var dusk = new Color(1f, 0.72f, 0.8f);
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(night, 0f),
                    new GradientColorKey(night, 0.2f),
                    new GradientColorKey(dawn, 0.27f),
                    new GradientColorKey(Color.white, 0.35f),
                    new GradientColorKey(Color.white, 0.75f),
                    new GradientColorKey(dusk, 0.81f),
                    new GradientColorKey(night, 0.88f),
                    new GradientColorKey(night, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static AnimationCurve DefaultIntensity()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.34f),
                new Keyframe(0.22f, 0.34f),
                new Keyframe(0.32f, 1f),
                new Keyframe(0.76f, 1f),
                new Keyframe(0.83f, 0.62f),
                new Keyframe(0.9f, 0.34f),
                new Keyframe(1f, 0.34f));
        }
    }
}
