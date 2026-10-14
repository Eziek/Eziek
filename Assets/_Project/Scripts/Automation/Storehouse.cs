using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;

namespace Wyspa.Automation
{
    /// <summary>
    /// Skład (prośba gracza: „za dużo drewna i kamienia, może konwerter na pieniądze”): sam sprzedaje nadwyżkę drewna
    /// i kamienia ponad ustawioną rezerwę (porcja co 10 s), żeby drwal i runy dziczy dawały też monety.
    /// Rezerwę ustawia się w panelu (zostaje na budowle i ulepszenia), można też sprzedać nadwyżkę od razu.
    /// </summary>
    public class Storehouse : MonoBehaviour, IContextActions
    {
        public const int WoodPrice = 3, StonePrice = 5;
        const float Interval = 10f;
        const int Batch = 10;
        const int ReserveStep = 10, ReserveMax = 500;

        public static readonly List<Storehouse> All = new List<Storehouse>();

        int reserve = 30;
        bool auto = true;
        float timer;

        public int Reserve => reserve;
        public bool Auto => auto;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Restore(int savedReserve, bool off)
        {
            reserve = Mathf.Clamp(savedReserve, 0, ReserveMax);
            auto = !off;
        }

        /// <summary>Tick z AutomationSystem (czas gry). Przy kilku składach rezerwa liczy się od najwyższej.</summary>
        public void Tick(float dt, GameLoop game)
        {
            if (!auto || game == null)
                return;
            timer += dt;
            if (timer < Interval)
                return;
            timer = 0f;
            int coins = Sell(game, CurrencyType.Wood, WoodPrice, Batch) + Sell(game, CurrencyType.Stone, StonePrice, Batch);
            Pay(game, coins);
        }

        int Sell(GameLoop game, CurrencyType type, int price, int limit)
        {
            int excess = game.Wallet.Get(type) - reserve;
            int n = Mathf.Min(excess, limit);
            if (n <= 0 || !game.Wallet.TrySpend(new List<CurrencyAmount> { new CurrencyAmount(type, n) }))
                return 0;
            return n * price;
        }

        void Pay(GameLoop game, int coins)
        {
            if (coins <= 0)
                return;
            game.Wallet.Add(coins);
            GameEvents.RaiseCurrencyGained(transform.position + new Vector3(0f, 1f, 0f), CurrencyType.Coins, coins);
            GameEvents.PlaySound("coin", 0.4f);
        }

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            info.verticalButtons = true;
            info.lines.Add(Loc.Get("ui.store.desc", WoodPrice, StonePrice));
            info.lines.Add(Loc.Get("ui.store.reserve", reserve));
            info.Button(Loc.Get("ui.store.reserve_less", ReserveStep), () => { reserve = Mathf.Max(0, reserve - ReserveStep); GameEvents.RequestContext(this); });
            info.Button(Loc.Get("ui.store.reserve_more", ReserveStep), () => { reserve = Mathf.Min(ReserveMax, reserve + ReserveStep); GameEvents.RequestContext(this); });
            info.Button(Loc.Get("ui.store.auto", Loc.Get(auto ? "ui.on" : "ui.off")), () => { auto = !auto; GameEvents.RequestContext(this); });
            if (game == null)
                return;
            int wood = Mathf.Max(0, game.Wallet.Get(CurrencyType.Wood) - reserve);
            int stone = Mathf.Max(0, game.Wallet.Get(CurrencyType.Stone) - reserve);
            int value = wood * WoodPrice + stone * StonePrice;
            info.Button(Loc.Get("ui.store.sell_now", value), () =>
            {
                Pay(game, Sell(game, CurrencyType.Wood, WoodPrice, int.MaxValue) + Sell(game, CurrencyType.Stone, StonePrice, int.MaxValue));
                GameEvents.RequestContext(this);
            }, value > 0);
        }
    }
}
