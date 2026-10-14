using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.Lighting
{
    /// <summary>Lampa: w nocy świeci, jeśli starczy Esencji. Glow = osobny sprite na materiale Unlit (piksele emisyjne).</summary>
    public class Lamp : MonoBehaviour, IContextActions
    {
        public static readonly List<Lamp> All = new List<Lamp>();

        /// <summary>Do testów: wszystkie lampy zgaszone (np. żeby sprawdzić rośliny nocy).</summary>
        public static bool DebugAllOff;

        /// <summary>Rytuał „Ciche lampy”: lampy spalają mniej Esencji.</summary>
        public static float ConsumptionMultiplier = 1f;

        [SerializeField] LampDefinition definition;
        [SerializeField] Light2D light2D;
        [SerializeField] SpriteRenderer glowRenderer;

        bool isOn;
        bool initialized;
        float phase;

        public LampDefinition Definition => definition;
        public bool IsOn => isOn;
        public Vector2 LightCenter => light2D != null ? (Vector2)light2D.transform.position : (Vector2)transform.position;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            phase = Random.value * 10f;
            SetOn(false);
        }

        public void Setup(LampDefinition def, Light2D light, SpriteRenderer glow)
        {
            definition = def;
            light2D = light;
            glowRenderer = glow;
            if (light2D != null)
            {
                // wolumetria (prośba gracza): poświata lampy widoczna w powietrzu, jak w mgle
                light2D.volumeIntensityEnabled = true;
                light2D.volumeIntensity = 0.12f;
            }
            // lampa stawiana w trakcie gry: Awake było przed Setup, więc gasimy ją jeszcze raz już z referencjami
            initialized = false;
            SetOn(false);
        }

        /// <summary>Wywoływane co tick przez GameLoop (najtańsze lampy pierwsze).</summary>
        public void Tick(float dt, bool night, EssenceStore essence)
        {
            bool on = !DebugAllOff && night && definition != null && essence.TryConsume(definition.essencePerMinute * ConsumptionMultiplier * dt / 60f);
            SetOn(on);
        }

        public void FillContext(ContextInfo info)
        {
            if (definition == null)
                return;
            info.lines.Add(Loc.Get("ui.lamp.info", definition.radius, definition.essencePerMinute * 60f));
            info.lines.Add(Loc.Get(isOn ? "ui.lamp.on" : "ui.lamp.off"));
        }

        public bool Illuminates(Vector2 point)
        {
            return isOn && definition != null && (point - LightCenter).sqrMagnitude <= definition.radius * definition.radius;
        }

        void SetOn(bool on)
        {
            if (initialized && on == isOn)
                return;
            initialized = true;
            isOn = on;
            if (light2D != null)
                light2D.enabled = on;
            if (glowRenderer != null)
                glowRenderer.enabled = on;
        }

        void Update()
        {
            if (!isOn || light2D == null || definition == null)
                return;
            if (!definition.flicker)
            {
                light2D.intensity = definition.intensity;
                return;
            }
            // migotanie skokowe ~10 kl./s, jak reszta animacji
            float t = Mathf.Floor(Time.unscaledTime * 10f) / 10f + phase;
            light2D.intensity = definition.intensity * (1f + 0.06f * Mathf.Sin(t * 1.7f * 10f) + 0.04f * Mathf.Sin(t * 3.1f * 10f));
        }
    }
}
