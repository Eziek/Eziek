using UnityEngine;
using Wyspa.Core;
using Wyspa.Lighting;

namespace Wyspa.Visual
{
    /// <summary>
    /// Gęstość mgły (GDD §4.6): w dzień rzadsza, w nocy gęstsza, a gdy gasną wszystkie lampy, podchodzi bliżej.
    /// Zmienia się powoli, małymi krokami.
    /// </summary>
    public class FogController : MonoBehaviour
    {
        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int FromId = Shader.PropertyToID("_FogFrom");
        static readonly int SpanId = Shader.PropertyToID("_FogSpan");
        static readonly int OuterId = Shader.PropertyToID("_FogOuter");


        [SerializeField] SpriteRenderer fog;
        [SerializeField] GameClock clock;
        [SerializeField] float day = 0.85f;
        [SerializeField] float night = 1.1f;
        [SerializeField] float lampsOut = 1.35f;
        [Tooltip("Zmiana gęstości na sekundę.")]
        [SerializeField] float speed = 0.05f;

        MaterialPropertyBlock block;
        float density = -1f;
        float timer;

        public float Density => density;
        bool band;
        float bandEdge = 18f;

        /// <summary>
        /// Przezroczyste okno: mgła tylko nad pasem wody, gęstnieje ku jego krawędzi (dalej czysty pulpit).
        /// Wyłączone = zwykła mgła nad całym morzem.
        /// </summary>
        public void SetShoreBand(bool on, float waterEdge = 18f)
        {
            band = on;
            bandEdge = waterEdge;
            Apply();
        }

        /// <summary>Sprite mgły.</summary>
        public SpriteRenderer Renderer => fog;

        public void Setup(SpriteRenderer fogRenderer, GameClock gameClock)
        {
            fog = fogRenderer;
            clock = gameClock;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 0.5f;
            // latarnia morska rozświetla mgłę (GDD §11.3 C): odsuwa ją o ćwierć
            float target = Target() * (Sea.NightEvents.LighthouseLit ? 0.75f : 1f) * Weather.WeatherSystem.FogMultiplier;   // mgła nocna, deszcz
            density = density < 0f ? target : Mathf.MoveTowards(density, target, speed * 0.5f);
            Apply();
        }

        float Target()
        {
            if (clock == null || !clock.IsNight)
                return day;
            foreach (var lamp in Lamp.All)
                if (lamp.IsOn)
                    return night;
            return Lamp.All.Count > 0 ? lampsOut : night;
        }

        void Apply()
        {
            if (fog == null)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            fog.GetPropertyBlock(block);
            block.SetFloat(DensityId, Mathf.Max(0.3f, density));
            // wartości zwykłe = domyślne z materiału (22 / 40 / bez końca)
            block.SetFloat(FromId, band ? 5f : 22f);
            block.SetFloat(SpanId, 40f);
            block.SetFloat(OuterId, band ? bandEdge : 0f);
            fog.SetPropertyBlock(block);
        }
    }
}
