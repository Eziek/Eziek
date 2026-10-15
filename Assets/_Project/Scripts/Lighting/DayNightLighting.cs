using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;

namespace Wyspa.Lighting
{
    /// <summary>
    /// Kolor i jasność Global Light 2D wg pory dnia (cykl gry) i pogody (deszcz, błyski burzy).
    /// Noc i dzień jak wcześniej; wschód słońca (świt 06-08): fiolet przed świtem, róż, złoto, dzień; zachód (zmierzch 18-20):
    /// złota godzina, pomarańcz z czerwienią, fiolet, noc. Klucze w godzinach zegara gry, w kodzie (bez gradientu ze sceny).
    /// </summary>
    public class DayNightLighting : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] Light2D globalLight;

        [Tooltip("Najniższa jasność - noc nie może być czarną dziurą na jasnym pulpicie.")]
        [Range(0f, 1f)] [SerializeField] float minBrightness = 0.3f;

        public float MinBrightness
        {
            get => minBrightness;
            set => minBrightness = Mathf.Clamp01(value);
        }

        static readonly Color Night = new Color(0.48f, 0.55f, 0.95f);

        // godzina zegara gry, kolor, jasność (pierwszy i ostatni klucz domykają dobę)
        static readonly (float hour, Color color, float intensity)[] Keys =
        {
            (0f, Night, 0.34f),
            (5.2f, Night, 0.34f),
            (6f, new Color(0.62f, 0.55f, 0.92f), 0.42f),     // szarówka przed wschodem
            (6.6f, new Color(1f, 0.62f, 0.66f), 0.66f),       // różowy wschód
            (7.3f, new Color(1f, 0.84f, 0.62f), 0.9f),        // złote poranne słońce
            (8f, new Color(1f, 0.97f, 0.92f), 1f),
            (10f, Color.white, 1f),
            (16.5f, Color.white, 1f),
            (17.8f, new Color(1f, 0.93f, 0.8f), 1f),
            (18.6f, new Color(1f, 0.82f, 0.56f), 0.95f),      // złota godzina
            (19.3f, new Color(1f, 0.6f, 0.45f), 0.76f),      // zachód: pomarańcz z czerwienią
            (19.9f, new Color(0.78f, 0.52f, 0.86f), 0.52f),   // fiolet po zachodzie
            (20.7f, Night, 0.34f),
            (24f, Night, 0.34f),
        };

        void Update()
        {
            if (clock == null || globalLight == null)
                return;
            Sample(clock.Hour, out var color, out float intensity);
            // pogoda: deszcz przyciemnia, piorun na chwilę rozjaśnia wszystko na biało
            float flash = Weather.WeatherSystem.Flash;
            globalLight.color = Color.Lerp(color, new Color(0.85f, 0.9f, 1f), flash);
            globalLight.intensity = Mathf.Max(minBrightness, intensity) * Weather.WeatherSystem.LightMultiplier + flash;
        }

        /// <summary>Kolor i jasność o godzinie zegara gry (płynnie między kluczami).</summary>
        public static void Sample(float hour, out Color color, out float intensity)
        {
            hour = Mathf.Repeat(hour, 24f);
            for (int i = 1; i < Keys.Length; i++)
            {
                if (hour > Keys[i].hour)
                    continue;
                var a = Keys[i - 1];
                var b = Keys[i];
                float t = Mathf.InverseLerp(a.hour, b.hour, hour);
                t = t * t * (3f - 2f * t);
                color = Color.Lerp(a.color, b.color, t);
                intensity = Mathf.Lerp(a.intensity, b.intensity, t);
                return;
            }
            color = Night;
            intensity = 0.34f;
        }
    }
}
