using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;

namespace Wyspa.Sea
{
    /// <summary>
    /// Ołtarz ofiarny na brzegu (GDD §11.3 F): potwór przychodzi częściej, a raz na noc można go przywołać ofiarą
    /// 3 plonów nocy (potem przy potworze ofiara za dar z głębin).
    /// </summary>
    public class Altar : MonoBehaviour, IContextActions
    {
        const int SummonCost = 3;

        static List<CurrencyAmount> FinaleCost => new List<CurrencyAmount>
        {
            new CurrencyAmount(CurrencyType.NightHarvest, Lore.Story.NightCost), new CurrencyAmount(CurrencyType.StarShard, Lore.Story.ShardCost),
        };

        /// <summary>Zakończenie historii: w noc pełni zapal na ołtarzu Światło Opiekunów (Lore.Story).</summary>
        void FillFinale(ContextInfo info, GameLoop game, NightEvents events)
        {
            info.lines.Add(Loc.Get("ui.altar.finale_hint"));
            var clock = game.Clock;
            bool ready = clock.IsNight && clock.IsFullMoon;
            if (!ready)
            {
                // pełnia co 8 nocy (cykl 1 h): ile godzin do najbliższej nocy pełni
                float cycles = Mathf.Repeat(0.5f - clock.MoonPhase, 1f) * GameClock.MoonCycleNights;
                info.lines.Add(Loc.Get("ui.altar.finale_wait", Mathf.Max(0.1f, cycles)));
            }
            var cost = FinaleCost;
            info.Button(Loc.Get("ui.altar.finale", UI.PlacementMode.CostText(cost)), () =>
            {
                if (!Lore.Story.Ready || !game.Clock.IsNight || !game.Clock.IsFullMoon || !game.Wallet.CanAfford(FinaleCost))
                    return;
                if (events.Monster != null || !events.SpawnMonster(true))
                    return;   // zwykły potwór jeszcze w wodzie: najpierw musi odejść
                game.Wallet.TrySpend(FinaleCost);
                Lore.Story.Finish();
                foreach (var lamp in Lighting.Lamp.All)
                    if (lamp != null) Visual.IslandFx.FindPopped(lamp.transform.position + new Vector3(0f, 1f, 0f));
                Visual.IslandFx.FindPopped(transform.position + new Vector3(0f, 1f, 0f));
                GameEvents.PlaySound("magic", 1f);
                GameEvents.RaiseToast("toast.finale");
                if (events.Monster != null)
                    GameEvents.RequestContext(events.Monster);
            }, ready && game.Wallet.CanAfford(cost));
        }

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            var events = NightEvents.Instance;
            info.lines.Add(Loc.Get("ui.altar.desc"));
            if (game == null || events == null)
                return;
            if (Lore.Story.Finished)
                info.lines.Add(Loc.Get("ui.altar.finale_done"));
            else if (Lore.Story.Ready)
                FillFinale(info, game, events);
            bool night = game.Clock.IsNight;
            string key = game.Clock.NightKey;
            bool used = events.MonsterNight == key;
            if (!night)
                info.lines.Add(Loc.Get("ui.altar.day"));
            else if (used)
                info.lines.Add(Loc.Get("ui.altar.used"));
            bool can = night && !used && game.Wallet.Get(CurrencyType.NightHarvest) >= SummonCost;
            info.Button(Loc.Get("ui.altar.summon", SummonCost), () =>
            {
                // panel mógł wisieć otwarty: warunki sprawdzamy w chwili kliknięcia, a płacimy dopiero, gdy potwór się pojawił
                var cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.NightHarvest, SummonCost) };
                if (!game.Clock.IsNight || events.MonsterNight == game.Clock.NightKey || !game.Wallet.CanAfford(cost))
                    return;
                if (events.SpawnMonster())
                    game.Wallet.TrySpend(cost);
            }, can);
        }
    }
}
