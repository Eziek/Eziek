using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;

namespace Wyspa.Sea
{
    /// <summary>Sieci rybackie przy pomoście (GDD §11.3 E4): bierny połów co godzinę gry - łuski, w nocy czasem świecąca ryba.</summary>
    public class FishingNets : MonoBehaviour
    {
        const float IntervalSeconds = 3600f;

        float timer;

        public float SecondsToNext => Mathf.Max(0f, IntervalSeconds - timer);

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
    }
}
