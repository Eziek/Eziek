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

        /// <summary>Ognik-latarnik „dolał światła”: do tej chwili (Time.unscaledTime) lampa pali o połowę mniej Esencji.</summary>
        public float RefueledUntil { get; set; }
        public bool Refueled => Time.unscaledTime < RefueledUntil;

        /// <summary>Do testów: wszystkie lampy zgaszone (np. żeby sprawdzić rośliny nocy).</summary>
        public static bool DebugAllOff;

        // ---- scenki: zmierzch zapala lampy łańcuchem, rejs gasi je po kolei, przypłynięcie rozpala latarnię
        /// <summary>Łańcuch zapalania (zmierzch): lampa dalej od Origin zapala się później (PerUnit s na jednostkę świata).</summary>
        public static float IgniteStart = -100f, IgnitePerUnit = 0.35f;
        public static Vector2 IgniteOrigin;
        /// <summary>Łańcuch gaszenia (odpłynięcie): od ExtinguishStart lampy gasną po kolei, od najdalszej.</summary>
        public static float ExtinguishStart = float.MaxValue, ExtinguishPerUnit = 0.3f;
        public static Vector2 ExtinguishOrigin;
        float flareUntil;

        /// <summary>Nowa scena: bez łańcuchów z poprzedniej (statyczne pola przeżywają przeładowanie).</summary>
        public static void ResetScenes()
        {
            IgniteStart = -100f;
            ExtinguishStart = float.MaxValue;
        }

        /// <summary>Zmierzch: lampy zapalą się po kolei od origin (najbliższa pierwsza).</summary>
        public static void ChainIgnite(Vector2 origin, float perUnit = 0.35f)
        {
            IgniteOrigin = origin;
            IgnitePerUnit = perUnit;
            IgniteStart = Time.unscaledTime;
        }

        /// <summary>Odpłynięcie: lampy gasną po kolei, od najdalszej od origin do najbliższej.</summary>
        public static void ChainExtinguish(Vector2 origin, float perUnit = 0.3f)
        {
            ExtinguishOrigin = origin;
            ExtinguishPerUnit = perUnit;
            ExtinguishStart = Time.unscaledTime;
        }

        /// <summary>Lampa świeci przez chwilę bez względu na porę i Żar (scenka przypłynięcia).</summary>
        public void Flare(float seconds) => flareUntil = Time.unscaledTime + seconds;

        /// <summary>Największa odległość lampy od punktu (do odwrócenia kolejności gaszenia).</summary>
        static float MaxDistance(Vector2 origin)
        {
            float m = 0f;
            foreach (var l in All)
                if (l != null) m = Mathf.Max(m, Vector2.Distance(origin, l.transform.position));
            return m;
        }

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
                // wolumetria: poświata lampy widoczna w powietrzu, jak w mgle
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
            // mikstura „Płynne światło”: lampy świecą bez Esencji
            // nietoperz: o 20% mniej
            float burn = Alchemy.Potions.LampsFree ? 0f : definition != null
                ? definition.essencePerMinute * ConsumptionMultiplier * Upgrades.UpgradeEffects.LampBurn * Lore.Story.LampBurn * Familiars.FamiliarSystem.LampBurn * (Refueled ? 0.5f : 1f) * dt / 60f : 0f;
            float now = Time.unscaledTime;
            if (now < flareUntil)
            {
                SetOn(true);
                return;
            }
            if (now >= ExtinguishStart)
            {
                // odpłynięcie: najdalsza gaśnie pierwsza, latarnia przy origin ostatnia
                float d = Vector2.Distance(ExtinguishOrigin, transform.position);
                if (now >= ExtinguishStart + (MaxDistance(ExtinguishOrigin) - d) * ExtinguishPerUnit)
                {
                    SetOn(false);
                    return;
                }
            }
            // zmierzch: w łańcuchu lampa czeka na swoją kolej (jeszcze nie pali Żaru)
            if (!isOn && night && now < IgniteStart + Vector2.Distance(IgniteOrigin, transform.position) * IgnitePerUnit)
                return;
            bool on = !DebugAllOff && night && definition != null && essence.TryConsume(burn);
            if (on && !isOn && now < IgniteStart + 30f)
                Visual.IslandFx.FindPopped(LightCenter);   // błysk przy zapaleniu w łańcuchu
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
