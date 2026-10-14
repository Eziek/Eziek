using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;

namespace Wyspa.Visual
{
    /// <summary>
    /// Mała poświata świecących obiektów (ogniki, duszki, runy): Point Light 2D z wolumetrią, płynnie rozjaśniana po zmroku
    /// i gaszona w dzień. Światło jest dzieckiem obiektu, więc leci razem z ognikiem. Nie liczy się do wzrostu roślin.
    /// </summary>
    public class NightGlow : MonoBehaviour
    {
        Light2D light2D;
        float maxIntensity;
        float level;
        GameClock clock;

        public static NightGlow Add(GameObject owner, Vector2 offset, Color color, float radius, float intensity)
        {
            var go = new GameObject("NightGlow");
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = offset;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.falloffIntensity = 0.7f;
            l.intensity = 0f;
            l.volumeIntensityEnabled = true;
            l.volumeIntensity = 0f;
            var glow = go.AddComponent<NightGlow>();
            glow.light2D = l;
            glow.maxIntensity = intensity;
            return glow;
        }

        void Update()
        {
            if (light2D == null)
                return;
            if (clock == null)
                clock = GameLoop.Instance != null ? GameLoop.Instance.Clock : null;
            float target = clock == null ? 0f : clock.IsNight ? 1f : clock.IsDusk ? 0.5f : 0f;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * 0.5f);
            // delikatne pulsowanie, jak płomień
            float pulse = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 2.3f + transform.position.x);
            light2D.intensity = maxIntensity * level * pulse;
            light2D.volumeIntensity = 0.15f * level * pulse;
            light2D.enabled = level > 0.01f;
        }
    }
}
