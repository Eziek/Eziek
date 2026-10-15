using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.Automation
{
    /// <summary>
    /// Piec Żaru (GDD §9 p. 109-110): spala drewno z paleniska na Żar, także gdy gracz nie pracuje (paliwem jest drewno).
    /// Porcja: Wood drewna → 1 Żar co Interval s; nie pali, gdy słój jest pełny. Płomień w 3 klatkach, iskry z komina.
    /// Drewno do paleniska wrzucają ogniki: wolny ognik przynosi polana ze składu (ponad rezerwę), drwal niesie świeżo
    /// ścięte drewno prosto do pieca. Bez żadnego ognika na wyspie piec sam bierze drewno ze składu.
    /// Rezerwę drewna (na budowle i ulepszenia) i wyłączenie ustawia się w panelu; oba są w zapisie.
    /// </summary>
    public class EmberKiln : MonoBehaviour, IContextActions
    {
        public const int Wood = 2;
        public const float Interval = 20f;
        public const int FuelMax = 6;        // polana w palenisku
        const int ReserveStep = 10, ReserveMax = 500;

        public static readonly List<EmberKiln> All = new List<EmberKiln>();

        SpriteRenderer body, glow;
        Sprite[] frames, glowFrames;
        int reserve = 20;
        bool on = true;
        bool burning;
        float timer, animTimer;
        int frame;
        int fuel;
        Wisp feeder;                          // ognik, który właśnie niesie drewno do tego pieca

        public int Reserve => reserve;
        public bool On => on;
        public int Fuel => fuel;
        /// <summary>Miejsce przed paleniskiem, skąd ognik wrzuca polano.</summary>
        public Vector3 Mouth => transform.position + new Vector3(-0.15f, 0.55f, 0f);
        bool NeedsFuel => on && isActiveAndEnabled && fuel <= FuelMax - Wood && (feeder == null || !feeder.isActiveAndEnabled || feeder.FeedSite != this);

        /// <summary>Piec, który czeka na drewno (najbliższy podanego miejsca). fromStore: ognik weźmie drewno ze składu, więc skład musi mieć je ponad rezerwę.</summary>
        public static EmberKiln WantingFuel(Vector3 near, bool fromStore)
        {
            var game = GameLoop.Instance;
            int wood = game != null ? game.Wallet.Get(CurrencyType.Wood) : 0;
            EmberKiln best = null;
            float bestD = float.MaxValue;
            foreach (var k in All)
            {
                if (k == null || !k.NeedsFuel || (fromStore && wood < k.reserve + Wood))
                    continue;
                float d = (k.transform.position - near).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = k;
                }
            }
            return best;
        }

        /// <summary>Ognik bierze kurs z drewnem (jeden naraz na piec).</summary>
        public bool TryClaim(Wisp wisp)
        {
            if (wisp == null || !NeedsFuel)
                return false;
            feeder = wisp;
            return true;
        }

        public void Release(Wisp wisp)
        {
            if (feeder == wisp)
                feeder = null;
        }

        /// <summary>Polana wrzucone do paleniska. Zwraca, ile się nie zmieściło (wraca do składu).</summary>
        public int Feed(int wood)
        {
            int take = Mathf.Clamp(FuelMax - fuel, 0, wood);
            fuel += take;
            if (take > 0)
            {
                Sparks(3);
                GameEvents.PlaySound("chop", 0.3f);
            }
            return wood - take;
        }

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

        public void Restore(int savedReserve, bool off, int savedFuel)
        {
            reserve = Mathf.Clamp(savedReserve, 0, ReserveMax);
            on = !off;
            fuel = Mathf.Clamp(savedFuel, 0, FuelMax);
        }

        /// <summary>Tick z AutomationSystem (czas gry, niezależnie od pracy gracza).</summary>
        public void Tick(float dt, GameLoop game)
        {
            if (game == null)
                return;
            var essence = game.Essence;
            // bez ogników na wyspie nie ma kto nosić drewna: piec dokłada sam ze składu
            if (on && Wisp.All.Count == 0 && fuel < Wood && game.Wallet.Get(CurrencyType.Wood) >= reserve + Wood
                && game.Wallet.TrySpend(new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Wood, Wood) }))
                fuel += Wood;
            burning = on && fuel >= Wood && essence.Amount <= essence.Capacity - 1f;
            if (!burning)
                return;
            timer += dt;
            if (timer < Interval)
                return;
            timer = 0f;
            fuel -= Wood;
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
            info.lines.Add(Loc.Get("ui.kiln.fuel", fuel, FuelMax));
            info.lines.Add(Loc.Get("ui.kiln.reserve", reserve));
            string state = !on ? "ui.kiln.off" : burning ? "ui.kiln.burning"
                         : game != null && game.Essence.Amount > game.Essence.Capacity - 1f ? "ui.kiln.full"
                         : game != null && game.Wallet.Get(CurrencyType.Wood) < reserve + Wood ? "ui.kiln.no_wood" : "ui.kiln.waiting";
            info.lines.Add(Loc.Get(state));
            info.Button(Loc.Get("ui.store.reserve_less", ReserveStep), () => { reserve = Mathf.Max(0, reserve - ReserveStep); GameEvents.RequestContext(this); });
            info.Button(Loc.Get("ui.store.reserve_more", ReserveStep), () => { reserve = Mathf.Min(ReserveMax, reserve + ReserveStep); GameEvents.RequestContext(this); });
            info.Button(Loc.Get("ui.kiln.toggle", Loc.Get(on ? "ui.on" : "ui.off")), () => { on = !on; GameEvents.RequestContext(this); });
        }
    }
}
