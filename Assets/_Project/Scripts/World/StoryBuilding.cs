using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.World
{
    /// <summary>
    /// Budowla odbudowana na wysepce fabularnej (§13.11): przy pracy gracza (z tempem pracy) co jakiś czas daje walutę
    /// historii (plony nocy, monety, drewno, odłamek gwiazdy). Nie da się jej przenieść ani rozebrać.
    /// Tick z AutomationSystem; czas do następnego daru jest w zapisie (ObjectSave.storyTimer).
    /// </summary>
    public class StoryBuilding : MonoBehaviour, IContextActions
    {
        public static readonly List<StoryBuilding> All = new List<StoryBuilding>();

        StorySite site;
        float timer;

        public StorySite Site => site;
        public float Timer => timer;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Setup(StorySite storySite, float savedTimer)
        {
            site = storySite;
            timer = Mathf.Clamp(savedTimer, 0f, site != null ? site.interval : 0f);
        }

        public void Tick(float dt, GameLoop game)
        {
            if (site == null || game == null || game.Activity == null || !game.Activity.IsActive)
                return;
            timer += dt * game.Activity.TempoMultiplier;
            if (timer < site.interval)
                return;
            timer -= site.interval;
            game.Wallet.Add(site.product, site.amount);
            GameEvents.RaiseCurrencyGained(transform.position + new Vector3(0f, 1.5f, 0f), site.product, site.amount);
            GameEvents.PlaySound("coin", 0.4f);
        }

        public void FillContext(ContextInfo info)
        {
            if (site == null)
                return;
            info.lines.Add(Loc.Get(site.DescKey, site.amount, Mathf.RoundToInt(site.interval / 60f)));
            info.lines.Add(Loc.Get("ui.story.next", Mathf.Max(1, Mathf.CeilToInt((site.interval - timer) / 60f))));
            info.lines.Add(Loc.Get(site.EndKey));
        }
    }
}
