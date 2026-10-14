using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Sea
{
    /// <summary>
    /// Sieci rybackie przy pomoście (GDD §11.3 E4): bierny połów co godzinę gry - łuski, w nocy czasem świecąca ryba.
    /// Klik w boję: panel z czasem do połowu i przeniesieniem (sieci płyną razem z pomostem).
    /// </summary>
    public class FishingNets : MonoBehaviour, IClickable, IContextActions
    {
        const float IntervalSeconds = 3600f;

        Island island;
        float timer;

        public float SecondsToNext => Mathf.Max(0f, IntervalSeconds - timer);
        /// <summary>Czas od ostatniego połowu (przenoszony razem z pomostem).</summary>
        public float Elapsed { get => timer; set => timer = Mathf.Clamp(value, 0f, IntervalSeconds); }

        public void Init(Island owner) => island = owner;

        void Update()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            timer += Time.unscaledDeltaTime * Mathf.Max(0f, game.DebugTimeScale);
            if (timer < IntervalSeconds)
                return;
            timer = 0f;
            int scales = 2;
            bool glow = game.Clock.IsNight && Random.value < 0.3f;
            if (glow)
                scales += 2;
            game.Wallet.Add(CurrencyType.Scale, scales);
            Lore.Chronicle.Count("fish");
            GameEvents.RaiseCurrencyGained(transform.position, CurrencyType.Scale, scales);
            GameEvents.RaiseToast(glow ? "toast.nets.glow" : "toast.nets", scales);
        }

        public void OnClick() => GameEvents.RequestContext(this);

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get("bld.nets");
            info.worldAnchor = transform.position + new Vector3(0f, 1.2f, 0f);
            info.lines.Add(Loc.Get("ui.nets.next", Mathf.CeilToInt(SecondsToNext / 60f)));
            info.lines.Add(Loc.Get("ui.nets.moves_with_pier"));
            info.Button(Loc.Get("act.move"), () => Pier.StartMove(island));
        }
    }
}
