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

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            var events = NightEvents.Instance;
            info.lines.Add(Loc.Get("ui.altar.desc"));
            if (game == null || events == null)
                return;
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
                var cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.NightHarvest, SummonCost) };
                if (game.Wallet.TrySpend(cost))
                    events.SpawnMonster();
            }, can);
        }
    }
}
