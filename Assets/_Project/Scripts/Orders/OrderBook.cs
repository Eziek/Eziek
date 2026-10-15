using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Orders
{
    [Serializable]
    public class OrderItem
    {
        public string id;     // „crop:poppy” albo „fruit:apple”
        public int need, have;
    }

    [Serializable]
    public class Order
    {
        public string requester;
        public List<OrderItem> items = new List<OrderItem>();
        public long deadline;      // numer cyklu gry, po którym karteczka butwieje
        public int coins;
        public string bonus;       // fertilizer, seeds, shard, wood, stone
        public int bonusAmount;
        public string bonusSeed;   // dla „seeds”: która roślina nocy

        public bool Done
        {
            get
            {
                foreach (var it in items)
                    if (it.have < it.need) return false;
                return true;
            }
        }
    }

    [Serializable]
    public class OrderSave
    {
        public List<Order> orders = new List<Order>();
        public long nextCycle = -1;
        public int completed;
    }

    /// <summary>
    /// Zamówienia na tablicy ogłoszeń. Po wprowadzeniu co 2 cykle gry przybija się karteczka
    /// od kogoś z mgły (najwyżej 3 naraz): „zbierz N sztuk” jednego albo dwóch plonów / owoców w terminie kilku cykli.
    /// Postęp liczy się sam ze zbiorów (także ogników), zbiór dalej daje zwykłe monety. Za wykonanie: monety i bonus
    /// (nawóz, nasiona nocy, odłamek gwiazdy, drewno, kamień). Przeterminowana karteczka po prostu znika, bez kary.
    /// Bez tablicy na wyspie zamówienia czekają (tablicę można postawić ze Sklep → Budowle).
    /// </summary>
    public class OrderBook : MonoBehaviour
    {
        /// <summary>Najwięcej zamówień naraz: 3, „Pełna tablica” do 5.</summary>
        public static int MaxOrders => 3 + Upgrades.UpgradeEffects.OrderSlots;
        /// <summary>Nowe zamówienie co dwie doby gry, „Częste zlecenia” co dobę.</summary>
        static int CyclesBetween => Mathf.Max(1, 2 - Upgrades.UpgradeEffects.OrderFaster);
        public static readonly string[] Requesters = { "herbalist", "gravedigger", "keeper", "witch", "monk", "fisher" };

        public static OrderBook Instance { get; private set; }

        readonly List<Order> orders = new List<Order>();
        long nextCycle = -1;
        int completed;
        float timer;

        public IReadOnlyList<Order> Orders => orders;

        /// <summary>Zamówienia działają po wprowadzeniu i tylko z tablicą na wyspie.</summary>
        public static bool BoardActive
        {
            get
            {
                var island = Island.Instance;
                var intro = UI.IntroNotes.Instance;
                return island != null && island.Ready && island.Board != null && (intro == null || !intro.Active || intro.Finished)
                       && !Save.SaveSystem.InMenu;
            }
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnEnable() => GameEvents.Harvested += OnHarvested;
        void OnDisable() => GameEvents.Harvested -= OnHarvested;

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 1f;
            var game = GameLoop.Instance;
            if (game == null || !BoardActive)
                return;
            long cycle = game.Clock.CycleNumber;
            for (int i = orders.Count - 1; i >= 0; i--)
                if (cycle > orders[i].deadline)
                {
                    GameEvents.RaiseToast("toast.order.expired", Loc.Get("npc." + orders[i].requester));
                    orders.RemoveAt(i);
                }
            if (nextCycle < 0)
                nextCycle = cycle;   // pierwsza karteczka zaraz po wprowadzeniu
            if (orders.Count < MaxOrders && cycle >= nextCycle)
            {
                var o = Generate(game, cycle);
                nextCycle = cycle + CyclesBetween;
                if (o != null)
                {
                    orders.Add(o);
                    GameEvents.RaiseToast("toast.order.new", Loc.Get("npc." + o.requester));
                    BoardNote();
                }
            }
        }

        /// <summary>Do testów (HUD debugowy): następna karteczka przy najbliższym sprawdzeniu (gdy tablica działa).</summary>
        public void DebugNextNow()
        {
            nextCycle = 0;
            timer = 0f;
        }

        // ------------------------------------------------------------ postęp i nagrody

        void OnHarvested(string id, int count)
        {
            if (!BoardActive || string.IsNullOrEmpty(id) || count <= 0)
                return;
            // najstarsze zamówienie dostaje pierwsze; nadwyżka idzie do następnych
            for (int i = 0; i < orders.Count && count > 0; i++)
                foreach (var it in orders[i].items)
                    if (it.id == id && it.have < it.need)
                    {
                        int take = Mathf.Min(count, it.need - it.have);
                        it.have += take;
                        count -= take;
                    }
            for (int i = orders.Count - 1; i >= 0; i--)
                if (orders[i].Done)
                {
                    var o = orders[i];
                    orders.RemoveAt(i);
                    Reward(o);
                }
        }

        void Reward(Order o)
        {
            var game = GameLoop.Instance;
            var board = Island.Instance != null ? Island.Instance.Board : null;
            var at = board != null ? board.transform.position + new Vector3(0f, 0.8f, 0f) : Vector3.zero;
            if (game == null)
                return;
            completed++;
            game.Wallet.Add(o.coins);
            GameEvents.RaiseCurrencyGained(at, CurrencyType.Coins, o.coins);
            switch (o.bonus)
            {
                case "fertilizer": game.AddFertilizer(o.bonusAmount); break;
                case "seeds": if (!string.IsNullOrEmpty(o.bonusSeed)) game.Seeds.Add(o.bonusSeed, o.bonusAmount); break;
                case "shard":
                    game.Wallet.Add(CurrencyType.StarShard, o.bonusAmount);
                    GameEvents.RaiseCurrencyGained(at, CurrencyType.StarShard, o.bonusAmount);
                    break;
                case "wood":
                    game.Wallet.Add(CurrencyType.Wood, o.bonusAmount);
                    GameEvents.RaiseCurrencyGained(at, CurrencyType.Wood, o.bonusAmount);
                    break;
                case "stone":
                    game.Wallet.Add(CurrencyType.Stone, o.bonusAmount);
                    GameEvents.RaiseCurrencyGained(at, CurrencyType.Stone, o.bonusAmount);
                    break;
            }
            GameEvents.RaiseToast("toast.order.done", Loc.Get("npc." + o.requester), RewardText(o));
            if (UnityEngine.Random.value < Upgrades.UpgradeEffects.OrderSeedChance)
            {
                var h = Sea.Expedition.RandomHybrid(game, new System.Random());   // Zapłata w nasionach
                if (h != null)
                {
                    game.Seeds.Add(h.id);
                    GameEvents.RaiseToast("toast.order_seed", Loc.Get("crop." + h.id));
                }
            }
            GameEvents.PlaySound("magic");
            Lore.Chronicle.Count("order");
            if (board != null && board.TryGetComponent(out UI.NoticeBoard nb))
                nb.TearNote();
        }

        static void BoardNote()
        {
            var board = Island.Instance != null ? Island.Instance.Board : null;
            if (board != null && board.TryGetComponent(out UI.NoticeBoard nb))
                nb.Refresh();
        }

        // ------------------------------------------------------------ losowanie karteczki

        /// <summary>Co da się zamówić: odblokowane nasiona (bez krwawej róży i hybryd) i owoce dorosłych drzew na wyspie.</summary>
        static List<(string id, int price, bool night)> Pool(GameLoop game)
        {
            var list = new List<(string, int, bool)>();
            if (game.Crops != null)
                foreach (var c in game.Crops.crops)
                    if (c != null && game.IsUnlocked(c) && !c.requiresFullMoon)
                        list.Add(("crop:" + c.id, c.sellPrice, c.kind == CropKind.Night));
            var seen = new HashSet<string>();
            foreach (var t in FruitTree.All)
                if (t != null && t.Adult && t.Species != null && !t.Species.fruitsAtFullMoon && seen.Add(t.Species.id))
                    list.Add(("fruit:" + t.Species.id, t.Species.fruitPrice, t.Species.fruitsAtNight));
            return list;
        }

        Order Generate(GameLoop game, long cycle)
        {
            var pool = Pool(game);
            if (pool.Count == 0)
                return null;
            var o = new Order { requester = Requesters[UnityEngine.Random.Range(0, Requesters.Length)] };
            int kinds = pool.Count > 1 && UnityEngine.Random.value < 0.4f ? 2 : 1;
            int value = 0;
            bool anyNight = false;
            for (int k = 0; k < kinds; k++)
            {
                (string id, int price, bool night) pick;
                int guard = 0;
                do pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                while (o.items.Exists(i => i.id == pick.id) && ++guard < 10);
                if (o.items.Exists(i => i.id == pick.id))
                    break;
                bool fruit = pick.id.StartsWith("fruit:");
                // ilość rośnie z liczbą wykonanych zamówień (rośliny nocy i owoce mniej: rosną tylko w nocy albo na drzewach)
                int need = fruit ? UnityEngine.Random.Range(5, 11)
                         : pick.night ? Mathf.Min(12, 3 + completed / 3 + UnityEngine.Random.Range(0, 3))
                         : Mathf.Min(30, 6 + completed + UnityEngine.Random.Range(0, 5));
                o.items.Add(new OrderItem { id = pick.id, need = need });
                value += pick.price * need;
                anyNight |= pick.night;
            }
            o.deadline = cycle + 3 + o.items.Count + (anyNight ? 2 : 0);
            o.coins = Mathf.Max(20, Mathf.RoundToInt(value * 1.8f / 5f) * 5);
            // Sakiewka (+%) i Szlachetni klienci (szansa na podwójną zapłatę); widać to od razu na karteczce
            float pay = (1f + Upgrades.UpgradeEffects.OrderBonus) * Economy.Prestige.SaleMultiplier;
            if (UnityEngine.Random.value < Upgrades.UpgradeEffects.OrderVip)
                pay *= 2f;
            o.coins = Mathf.RoundToInt(o.coins * pay / 5f) * 5;
            PickBonus(o, game);
            return o;
        }

        static void PickBonus(Order o, GameLoop game)
        {
            float r = UnityEngine.Random.value;
            if (r < 0.15f) { o.bonus = "shard"; o.bonusAmount = 1; return; }
            if (r < 0.35f) { o.bonus = "fertilizer"; o.bonusAmount = UnityEngine.Random.Range(1, 3); return; }
            if (r < 0.55f && game.Crops != null)
            {
                var night = game.Crops.crops.FindAll(c => c != null && c.kind == CropKind.Night && game.IsUnlocked(c) && !c.requiresFullMoon);
                if (night.Count > 0)
                {
                    o.bonus = "seeds";
                    o.bonusSeed = night[UnityEngine.Random.Range(0, night.Count)].id;
                    o.bonusAmount = UnityEngine.Random.Range(2, 4);
                    return;
                }
            }
            o.bonus = r < 0.8f ? "wood" : "stone";
            o.bonusAmount = UnityEngine.Random.Range(3, 6);
        }

        // ------------------------------------------------------------ teksty (panel tablicy)

        public static string ItemName(string id)
        {
            if (id == null) return "";
            if (id.StartsWith("crop:")) return Loc.Get("crop." + id.Substring(5));
            if (id.StartsWith("fruit:")) return Loc.Get("fruit." + id.Substring(6));
            return id;
        }

        public static string RewardText(Order o)
        {
            string coins = Loc.Get("ui.merchant.price", o.coins);
            string bonus;
            switch (o.bonus)
            {
                case "fertilizer": bonus = Loc.Get("ui.order.bonus.fertilizer", o.bonusAmount); break;
                case "seeds": bonus = Loc.Get("ui.order.bonus.seeds", o.bonusAmount, Loc.Get("crop." + o.bonusSeed)); break;
                case "shard": bonus = Loc.Get("ui.order.bonus.shard", o.bonusAmount); break;
                case "wood": bonus = Loc.Get("ui.order.bonus.wood", o.bonusAmount); break;
                case "stone": bonus = Loc.Get("ui.order.bonus.stone", o.bonusAmount); break;
                default: return coins;
            }
            return coins + " + " + bonus;
        }

        /// <summary>Linie do panelu tablicy: kto, czego ile (postęp), ile cykli zostało, nagroda.</summary>
        public void Describe(List<string> lines, long cycle)
        {
            if (orders.Count == 0)
            {
                lines.Add(Loc.Get("ui.order.none", Mathf.Max(0, (int)(nextCycle - cycle))));
                return;
            }
            foreach (var o in orders)
            {
                var parts = new List<string>();
                foreach (var it in o.items)
                    parts.Add(Loc.Get("ui.order.item", ItemName(it.id), it.have, it.need));
                lines.Add(Loc.Get("ui.order.line", Loc.Get("npc." + o.requester), string.Join(", ", parts),
                                  Mathf.Max(0, (int)(o.deadline - cycle) + 1)));
                lines.Add("  " + Loc.Get("ui.order.reward", RewardText(o)));
            }
        }

        // ------------------------------------------------------------ zapis

        public OrderSave Snapshot() => new OrderSave { orders = new List<Order>(orders), nextCycle = nextCycle, completed = completed };

        public void Restore(OrderSave save)
        {
            orders.Clear();
            nextCycle = -1;
            completed = 0;
            if (save == null)
                return;
            if (save.orders != null)
                foreach (var o in save.orders)
                    if (o != null && o.items != null && o.items.Count > 0)
                        orders.Add(o);
            nextCycle = save.nextCycle;
            completed = save.completed;
        }
    }
}
