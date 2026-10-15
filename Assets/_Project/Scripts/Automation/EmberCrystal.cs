using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.Automation
{
    /// <summary>
    /// Kryształ Żaru (GDD §9 p. 109): sam oddaje Żar (1 co SecondsPerEmber s), także gdy gracz nie pracuje i nocą,
    /// i powiększa słój o CapacityBonus. Świeci pulsując. Pojemność słoja liczy GameLoop.RefreshEssenceCapacity.
    /// </summary>
    public class EmberCrystal : MonoBehaviour, IContextActions
    {
        public const float SecondsPerEmber = 120f;
        public const int CapacityBonus = 20;

        public static readonly List<EmberCrystal> All = new List<EmberCrystal>();
        public static int TotalCapacityBonus => All.Count * CapacityBonus;

        SpriteRenderer glow;
        float phase;

        void OnEnable()
        {
            All.Add(this);
            if (GameLoop.Instance != null) GameLoop.Instance.RefreshEssenceCapacity();
        }

        void OnDisable()
        {
            All.Remove(this);
            if (GameLoop.Instance != null) GameLoop.Instance.RefreshEssenceCapacity();
        }

        public void Setup(SpriteRenderer glowRenderer)
        {
            glow = glowRenderer;
            phase = Random.value * 6.28f;
        }

        /// <summary>Tick z AutomationSystem: Żar w ułamkach (słój trzyma ułamki).</summary>
        public void Tick(float dt, GameLoop game)
        {
            if (game == null)
                return;
            var essence = game.Essence;
            if (essence.Amount < essence.Capacity)
                essence.Set(essence.Amount + dt / SecondsPerEmber);
        }

        void Update()
        {
            if (glow == null)
                return;
            // powolny puls, skokowo (4 poziomy jasności, pikselowo)
            float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.6f + phase);
            glow.color = new Color(1f, 1f, 1f, 0.55f + Mathf.Round(p * 3f) / 3f * 0.45f);
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.crystal.desc", Mathf.RoundToInt(SecondsPerEmber / 60f), CapacityBonus));
        }
    }
}
