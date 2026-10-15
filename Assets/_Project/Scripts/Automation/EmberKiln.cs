using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.Automation
{
    /// <summary>
    /// Piec Żaru (GDD §9 p. 109): spala drewno ponad rezerwę na Żar, także gdy gracz nie pracuje (paliwem jest drewno).
    /// Porcja: Wood drewna → 1 Żar co Interval s; nie pali, gdy słój jest pełny. Płomień w 3 klatkach, iskry z komina.
    /// Rezerwę drewna (na budowle i ulepszenia) i wyłączenie ustawia się w panelu; oba są w zapisie.
    /// </summary>
    public class EmberKiln : MonoBehaviour, IContextActions
    {
        public const int Wood = 2;
        public const float Interval = 20f;
        const int ReserveStep = 10, ReserveMax = 500;

        public static readonly List<EmberKiln> All = new List<EmberKiln>();

        SpriteRenderer body, glow;
        Sprite[] frames, glowFrames;
        int reserve = 20;
        bool on = true;
        bool burning;
        float timer, animTimer;
        int frame;

        public int Reserve => reserve;
        public bool On => on;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
            Show();
        }

        public void Restore(int savedReserve, bool off)
        {
            reserve = Mathf.Clamp(savedReserve, 0, ReserveMax);
            on = !off;
        }

        /// <summary>Tick z AutomationSystem (czas gry, niezależnie od pracy gracza).</summary>
        public void Tick(float dt, GameLoop game)
        {
            if (game == null)
                return;
            var essence = game.Essence;
            burning = on && game.Wallet.Get(CurrencyType.Wood) >= reserve + Wood && essence.Amount <= essence.Capacity - 1f;
            if (!burning)
                return;
            timer += dt;
            if (timer < Interval)
                return;
            timer = 0f;
            if (!game.Wallet.TrySpend(new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Wood, Wood) }))
                return;
            essence.Set(essence.Amount + 1f);
            Sparks(5);
        }

        void Sparks(int n)
        {
            var fx = PixelParticles.Instance;
            var island = World.Island.Instance;
            var dust = island != null && island.Art != null ? island.Art.dust : null;
            if (fx == null || body == null)
                return;
            var chimney = transform.position + new Vector3(4.5f / 16f, 24f / 16f, 0f);
            IslandFx.FindPopped(chimney);
            if (dust != null)
                for (int i = 0; i < n; i++)
                    fx.Spawn(chimney, dust, new Vector2(Random.Range(-4f, 4f), Random.Range(10f, 18f)), 0f, 0.7f, body.sortingOrder + 2, false, 8f);
        }

        void Update()
        {
            animTimer -= Time.unscaledDeltaTime;
            if (animTimer > 0f)
                return;
            animTimer = Random.Range(0.12f, 0.2f);
            frame = burning ? (frame + 1 + Random.Range(0, 2)) % 3 : 0;
            Show();
        }

        void Show()
        {
            if (body != null && frames != null && frames.Length > 0)
                body.sprite = frames[Mathf.Min(frame, frames.Length - 1)];
            if (glow != null)
            {
                glow.sprite = glowFrames != null && glowFrames.Length > 0 ? glowFrames[Mathf.Min(frame, glowFrames.Length - 1)] : null;
                glow.enabled = burning && glow.sprite != null;
            }
        }

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            info.verticalButtons = true;
            info.lines.Add(Loc.Get("ui.kiln.desc", Wood, Mathf.RoundToInt(Interval)));
            info.lines.Add(Loc.Get("ui.kiln.reserve", reserve));
            string state = !on ? "ui.kiln.off" : burning ? "ui.kiln.burning"
                         : game != null && game.Essence.Amount > game.Essence.Capacity - 1f ? "ui.kiln.full" : "ui.kiln.no_wood";
            info.lines.Add(Loc.Get(state));
            info.Button(Loc.Get("ui.store.reserve_less", ReserveStep), () => { reserve = Mathf.Max(0, reserve - ReserveStep); GameEvents.RequestContext(this); });
            info.Button(Loc.Get("ui.store.reserve_more", ReserveStep), () => { reserve = Mathf.Min(ReserveMax, reserve + ReserveStep); GameEvents.RequestContext(this); });
            info.Button(Loc.Get("ui.kiln.toggle", Loc.Get(on ? "ui.on" : "ui.off")), () => { on = !on; GameEvents.RequestContext(this); });
        }
    }
}
