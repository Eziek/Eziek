using System;
using System.Collections;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Input;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Sea
{
    /// <summary>
    /// Kupiec-widmo (GDD §4.7): raz na noc, o losowej porze (21:00-3:00), łódź wypływa z mgły pod wyspą i czeka ok. 10 minut.
    /// Sprzedaje tanie paczki nasion nocy, nawóz i czasem nasiono hybrydy; skupuje zawartość skrzyni drożej (150%).
    /// Nagradza, nie karze: przegapiony kupiec to tylko brak okazji.
    /// </summary>
    public class GhostMerchant : MonoBehaviour, IClickable, IContextActions
    {
        const float BaseStaySeconds = 600f;
        /// <summary>Rytuał „Szept mgły”: kupiec zostaje dłużej.</summary>
        public static float StayMultiplier = 1f;
        static float StaySeconds => BaseStaySeconds * StayMultiplier;
        const float SellMultiplier = 1.5f;
        const float Ppu = 16f;

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Collider2D clickArea;

        string visitedNight = "";      // noc, w której kupiec już był (data początku nocy)
        float leaveAt = -1f;
        bool present;
        bool fading;
        Offer[] offers;

        /// <summary>Do testów (HUD debugowy).</summary>
        public static GhostMerchant Instance { get; private set; }
        public bool Present => present;
        public string VisitedNight { get => visitedNight; set => visitedNight = value ?? ""; }

        class Offer
        {
            public string label;
            public int coins, night;
            public Action give;
            public bool sold;
        }

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Collider2D area)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            clickArea = area;
        }

        void Awake()
        {
            Instance = this;
            Show(false, 0f);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // --------------------------------------------------------- harmonogram

        float timer;

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 1f;
            var game = GameLoop.Instance;
            if (game == null)
                return;
            var now = game.Clock.Now;
            if (present)
            {
                if (Time.unscaledTime >= leaveAt)
                    Leave();
                return;
            }
            if (!game.Clock.IsNight)
                return;
            string night = NightKey(now);
            if (night == visitedNight)
                return;
            // pora przybycia losowana z daty nocy (ta sama po restarcie gry)
            var start = NightStart(now);
            var rnd = new WRandom(DateSeed(start));
            var arrive = start.AddHours(21 + rnd.Range(0.0, 6.0));
            if (now >= arrive && now < arrive.AddSeconds(StaySeconds))
                Arrive(night, (float)(arrive.AddSeconds(StaySeconds) - now).TotalSeconds);
            else if (now >= arrive.AddSeconds(StaySeconds))
                visitedNight = night;   // przegapiony: następna szansa jutro
        }

        static DateTime NightStart(DateTime now) => now.Hour < 12 ? now.Date.AddDays(-1) : now.Date;
        // stałe ziarno z daty (string.GetHashCode bywa losowany przy każdym uruchomieniu)
        static int DateSeed(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;
        static string NightKey(DateTime now) => NightStart(now).ToString("yyyy-MM-dd");

        /// <summary>Przybycie (także z HUD debugowego): oferta na tę noc, łódź wyłania się z mgły.</summary>
        public void Arrive(string night = null, float stay = -1f)
        {
            if (stay < 0f)
                stay = StaySeconds;
            var game = GameLoop.Instance;
            if (game == null)
                return;
            visitedNight = night ?? NightKey(game.Clock.Now);
            leaveAt = Time.unscaledTime + Mathf.Max(30f, stay);
            BuildOffers(game, DateSeed(NightStart(game.Clock.Now)) + 17);
            var island = Island.Instance;
            transform.position = island != null ? island.WaterBelow(18) : new Vector3(0f, -3f, 0f);
            present = true;
            StartCoroutine(Fade(true));
            GameEvents.RaiseToast("toast.merchant.arrived");
            Lore.Chronicle.Note("event:merchant");
            Lore.Chronicle.Count("event");
        }

        void Leave()
        {
            if (!present)
                return;
            present = false;
            StartCoroutine(Fade(false));
            GameEvents.RaiseToast("toast.merchant.left");
        }

        IEnumerator Fade(bool appear)
        {
            fading = true;
            // płynnie wynurza się z mgły (albo w niej znika)
            const float duration = 0.8f;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                float u = time / duration;
                Show(true, appear ? u : 1f - u);
                yield return null;
            }
            Show(appear, appear ? 1f : 0f);
            fading = false;
        }

        void Show(bool visible, float alpha)
        {
            var c = new Color(1f, 1f, 1f, alpha);
            if (body != null) { body.enabled = visible; body.color = c; }
            if (glow != null) { glow.enabled = visible; glow.color = c; }
            if (clickArea != null) clickArea.enabled = visible && alpha > 0.9f;
        }

        void LateUpdate()
        {
            // łódź płynnie kołysze się na wodzie
            if (!present || fading || body == null)
                return;
            float bob = (Mathf.Sin(Time.unscaledTime * 1.6f) * 0.8f + 0.5f) / Ppu;
            body.transform.localPosition = new Vector3(0f, bob, 0f);
        }

        // --------------------------------------------------------- oferta

        void BuildOffers(GameLoop game, int seed)
        {
            var rnd = new WRandom(seed);
            var night = new System.Collections.Generic.List<CropDefinition>();
            CropDefinition hybrid = null;
            if (game.Crops != null)
                foreach (var c in game.Crops.crops)
                {
                    if (c == null) continue;
                    if (c.hybrid) hybrid = c;
                    else if (c.kind == CropKind.Night && !c.requiresFullMoon) night.Add(c);
                }
            var list = new System.Collections.Generic.List<Offer>();
            // dwie paczki po 3 nasiona nocy za 60% ceny
            for (int i = 0; i < 2 && night.Count > 0; i++)
            {
                var c = night[rnd.Range(0, night.Count)];
                night.Remove(c);
                var crop = c;
                list.Add(new Offer
                {
                    label = Loc.Get("ui.merchant.seeds", 3, Loc.Get("crop." + crop.id)),
                    coins = Mathf.RoundToInt(crop.seedPrice * 3 * 0.6f),
                    give = () => game.Seeds.Add(crop.id, 3),
                });
            }
            list.Add(new Offer
            {
                label = Loc.Get("ui.merchant.fertilizer", 3),
                coins = 30, night = 1,
                give = () => game.AddFertilizer(3),
            });
            if (hybrid != null && rnd.Value() < 0.35)
            {
                var h = hybrid;
                list.Add(new Offer
                {
                    label = Loc.Get("ui.merchant.hybrid", Loc.Get("crop." + h.id)),
                    coins = 120, night = 3,
                    give = () => game.Seeds.Add(h.id, 1),
                });
            }
            offers = list.ToArray();
        }

        public void OnClick()
        {
            if (present && !fading)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            info.title = Loc.Get("npc.ghost_merchant");
            info.worldAnchor = transform.position + new Vector3(0f, 2.4f, 0f);
            info.verticalButtons = true;
            if (game == null || offers == null)
                return;
            int minutes = Mathf.Max(1, Mathf.CeilToInt((leaveAt - Time.unscaledTime) / 60f));
            info.lines.Add(Loc.Get("ui.merchant.leaves", minutes));
            foreach (var o in offers)
            {
                if (o.sold)
                    continue;
                var offer = o;
                string price = offer.night > 0
                    ? Loc.Get("ui.merchant.price2", offer.coins, offer.night)
                    : Loc.Get("ui.merchant.price", offer.coins);
                bool afford = game.Wallet.Coins >= offer.coins && game.Wallet.Get(CurrencyType.NightHarvest) >= offer.night;
                info.Button(offer.label + " - " + price, () => Buy(offer), afford);
            }
            int inCrates = HarvestCrate.TotalCount();
            if (inCrates > 0)
                info.Button(Loc.Get("ui.merchant.sell_crate", inCrates), () => SellCrates(game));
        }

        void Buy(Offer o)
        {
            var game = GameLoop.Instance;
            if (game == null || o.sold)
                return;
            var cost = new System.Collections.Generic.List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, o.coins) };
            if (o.night > 0)
                cost.Add(new CurrencyAmount(CurrencyType.NightHarvest, o.night));
            if (!game.Wallet.TrySpend(cost))
                return;
            o.sold = true;
            o.give?.Invoke();
            GameEvents.RaiseToast("ui.toast.upgrade_bought", o.label);
        }

        /// <summary>Kupiec skupuje zawartość wszystkich skrzyń na plony.</summary>
        void SellCrates(GameLoop game)
        {
            int coins = 0;
            foreach (var crate in HarvestCrate.All.ToArray())
                coins += crate.SellAll(game.Wallet, game.Crops, SellMultiplier);
            if (coins <= 0)
                return;
            GameEvents.RaiseToast("toast.merchant.bought", coins);
            GameEvents.RaiseCurrencyGained(transform.position, CurrencyType.Coins, coins);
        }
    }
}
