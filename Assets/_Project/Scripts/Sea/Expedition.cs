using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Input;
using Wyspa.Localization;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Sea
{
    [Serializable]
    public class ExpeditionSave
    {
        public int state;          // 0 przy pomoście, 1 w drodze, 2 wróciła z łupem
        public float progress;     // sekundy aktywnej pracy
        public float need;
        public bool longTrip;      // zapisy sprzed celów wypraw
        public string dest;        // v13: cel wyprawy (Destinations)
        public int trips;          // v13: odbyte wyprawy (odblokowują kolejne cele)
    }

    /// <summary>
    /// Łódź i wyprawy (GDD §11.3 F): łódź płynie w mgłę na N godzin AKTYWNEJ pracy i wraca z łupem. Gracz wybiera cel
    /// (wrak, rafa, zatopiona latarnia, cmentarzysko statków, Serce mgły): każdy ma własny czas, tabelę łupów i zapiski lore
    /// (jeden nowy na wyprawę, po kolei). Jedna wyprawa naraz.
    /// </summary>
    public class Expedition : MonoBehaviour, IClickable, IContextActions
    {
        /// <summary>Rytuał „Pamięć przypływu”: wyprawy krótsze.</summary>
        public static float DurationMultiplier = 1f;
        public static float Hours(Destination d) => d.hours * DurationMultiplier * Upgrades.UpgradeEffects.BoatDuration;
        const float Ppu = 16f;

        enum State { Docked, Away, Returned }

        SpriteRenderer body;
        SpriteRenderer glow;
        Collider2D clickArea;
        Vector3 dock;
        State state;
        float progress, need;
        string dest;
        int trips;
        bool animating;

        Destination Current => Destinations.Find(dest) ?? Destinations.All[0];

        public void Init(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Collider2D area)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            clickArea = area;
            dock = transform.position;
            Show(true);
        }

        public ExpeditionSave Snapshot() => new ExpeditionSave { state = (int)state, progress = progress, need = need, dest = dest, trips = trips };

        public void Restore(ExpeditionSave s)
        {
            if (s == null)
                return;
            state = (State)Mathf.Clamp(s.state, 0, 2);
            progress = s.progress;
            need = s.need;
            dest = !string.IsNullOrEmpty(s.dest) ? s.dest : (s.longTrip ? "graveyard" : "wreck");   // zapis sprzed celów
            trips = Mathf.Max(0, s.trips);
            Show(state != State.Away);
        }

        void Show(bool visible)
        {
            if (body != null) { body.enabled = visible; body.color = Color.white; }
            if (glow != null) glow.enabled = visible;
            if (clickArea != null) clickArea.enabled = visible;
            transform.position = dock;
        }

        // --------------------------------------------------------- postęp: liczy się tylko aktywna praca

        void Update()
        {
            if (state != State.Away)
                return;
            if (progress >= need && !animating)
            {
                StartCoroutine(Return());
                return;
            }
            var game = GameLoop.Instance;
            if (game == null || !game.Activity.IsActive)
                return;
            progress += Time.unscaledDeltaTime * Mathf.Max(0f, game.DebugTimeScale);
            if (progress >= need && !animating)
                StartCoroutine(Return());
        }

        /// <summary>Do testów (HUD debugowy): wyprawa w drodze wraca od razu.</summary>
        public void DebugFinish()
        {
            if (state == State.Away)
                progress = need;
        }

        /// <summary>Do testów: odblokowanie celów (dolicza wyprawy).</summary>
        public void DebugAddTrips(int n) => trips += n;

        public void OnClick()
        {
            if (!animating)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get("bld.boat");
            info.worldAnchor = transform.position + new Vector3(0f, 1.5f, 0f);
            info.verticalButtons = true;
            switch (state)
            {
                case State.Docked:
                    info.lines.Add(Loc.Get("ui.boat.docked"));
                    info.lines.Add(Loc.Get("ui.boat.choose"));
                    foreach (var d in Destinations.All)
                    {
                        var target = d;
                        if (Destinations.Unlocked(d, trips))
                            info.Button(Loc.Get("ui.boat.dest", Loc.Get(d.NameKey), Hours(d), Lore.LoreBook.FoundIn(d.lore), d.lore.Length),
                                        () => Send(target));
                        else if (d.needsAllLore)
                        {
                            var (found, total) = Destinations.OtherLore();
                            info.lines.Add(Loc.Get("ui.boat.locked_lore", Loc.Get(d.NameKey), found, total));
                        }
                        else
                            info.lines.Add(Loc.Get("ui.boat.locked", Loc.Get(d.NameKey), d.minTrips - trips));
                    }
                    break;
                case State.Away:
                    info.lines.Add(Loc.Get("ui.boat.target", Loc.Get(Current.NameKey)));
                    info.lines.Add(Loc.Get("ui.boat.away", Mathf.Max(0f, (need - progress) / 3600f)));
                    break;
                case State.Returned:
                    info.lines.Add(Loc.Get("ui.boat.returned"));
                    info.Button(Loc.Get("ui.boat.unload"), Unload);
                    break;
            }
        }

        void Send(Destination d)
        {
            if (state != State.Docked || animating || d == null || !Destinations.Unlocked(d, trips))
                return;
            dest = d.id;
            need = Hours(d) * 3600f;
            progress = 0f;
            state = State.Away;
            GameEvents.RaiseToast("toast.boat.sent");
            StartCoroutine(Sail(true));
        }

        IEnumerator Return()
        {
            state = State.Returned;
            GameEvents.RaiseToast("toast.boat.back");
            yield return Sail(false);
        }

        /// <summary>Łódź odpływa w mgłę (albo z niej wraca), płynnie znikając.</summary>
        IEnumerator Sail(bool away)
        {
            animating = true;
            var dir = new Vector3(0f, -1f, 0f);
            if (body != null) body.enabled = true;
            if (glow != null) glow.enabled = true;
            if (clickArea != null) clickArea.enabled = false;
            // płynnie: łódź odpływa 24 px w mgłę, znikając (albo z niej wypływa)
            const float duration = 1.4f;
            for (float time = 0f; time <= duration; time += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(time / duration);
                float e = u * u * (3f - 2f * u);
                float k = away ? e : 1f - e;
                transform.position = dock + dir * (k * 24f / Ppu);
                if (body != null) body.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            transform.position = away ? dock + dir * (24f / Ppu) : dock;
            Show(!away);
            animating = false;
        }

        // --------------------------------------------------------- łup

        void Unload()
        {
            var game = GameLoop.Instance;
            if (state != State.Returned || game == null)
                return;
            var summary = new LootSummary { anchor = transform.position + new Vector3(0f, 1.5f, 0f) };
            var rnd = new System.Random();
            var d = Current;
            summary.title = Loc.Get(d.NameKey);
            // zapisek z tego miejsca (po kolei), potem łup z tabeli celu
            string lore = Lore.LoreBook.NextOf(d.lore);
            if (lore != null)
            {
                Lore.Chronicle.Note("lore:" + lore);
                summary.lines.Add(Loc.Get("ui.loot.lore", Loc.Get(lore)));
                if (lore == Lore.Story.LastFragment)
                    GameEvents.RaiseToast("toast.finale_ready");   // zakończenie historii czeka przy ołtarzu
            }
            int rolls = rnd.Next(d.rollsMin, d.rollsMax + 1);
            for (int i = 0; i < rolls; i++)
                Give(game, rnd, summary, Destinations.Roll(d, rnd, false));
            if (d.rareRoll)
                Give(game, rnd, summary, Destinations.Roll(d, rnd, true));   // daleka wyprawa: zawsze coś rzadkiego
            if (Upgrades.UpgradeEffects.ExpCoins > 0)
                Give(game, summary, CurrencyType.Coins, Upgrades.UpgradeEffects.ExpCoins, summary.anchor);   // Morski handel
            var before = UnlockedSet();
            trips++;
            foreach (var n in Destinations.All)
                if (!before.Contains(n) && Destinations.Unlocked(n, trips))
                    GameEvents.RaiseToast("toast.boat.new_dest", Loc.Get(n.NameKey));
            state = State.Docked;
            progress = 0f;
            need = 0f;
            IslandFx.FindPopped(transform.position);
            GameEvents.RaiseToast("toast.boat.unloaded");
            GameEvents.RequestContext(summary);
        }

        HashSet<Destination> UnlockedSet()
        {
            var set = new HashSet<Destination>();
            foreach (var n in Destinations.All)
                if (Destinations.Unlocked(n, trips)) set.Add(n);
            return set;
        }

        void Give(GameLoop game, System.Random rnd, LootSummary s, LootKind kind)
        {
            var at = transform.position;
            switch (kind)
            {
                case LootKind.NightSeeds:
                    var seed = RandomNightSeed(game, rnd);
                    if (seed == null) goto case LootKind.Coins;
                    game.Seeds.Add(seed.id, 2);
                    s.lines.Add(Loc.Get("ui.loot.seeds", 2, Loc.Get("crop." + seed.id)));
                    break;
                case LootKind.Wood: Give(game, s, CurrencyType.Wood, rnd.Next(4, 9), at); break;
                case LootKind.Stone: Give(game, s, CurrencyType.Stone, rnd.Next(3, 7), at); break;
                case LootKind.Fertilizer:
                    game.AddFertilizer(2);
                    s.lines.Add(Loc.Get("ui.loot.fertilizer", 2));
                    break;
                case LootKind.Shard: Give(game, s, CurrencyType.StarShard, 1, at); break;
                case LootKind.Hybrid:
                    var hybrid = RandomHybrid(game, rnd);
                    if (hybrid == null) goto case LootKind.Shard;
                    game.Seeds.Add(hybrid.id, 1);
                    s.lines.Add(Loc.Get("ui.loot.seeds", 1, Loc.Get("crop." + hybrid.id)));
                    break;
                case LootKind.Coins:
                default: Give(game, s, CurrencyType.Coins, rnd.Next(40, 121), at); break;
            }
        }

        public static CropDefinition RandomHybrid(GameLoop game, System.Random rnd)
        {
            var list = new List<CropDefinition>();
            if (game.Crops != null)
                foreach (var c in game.Crops.crops)
                    if (c != null && c.hybrid) list.Add(c);
            return list.Count > 0 ? list[rnd.Next(list.Count)] : null;
        }

        static CropDefinition RandomNightSeed(GameLoop game, System.Random rnd)
        {
            var list = new List<CropDefinition>();
            if (game.Crops != null)
                foreach (var c in game.Crops.crops)
                    if (c != null && c.kind == CropKind.Night && !c.hybrid)
                        list.Add(c);
            return list.Count > 0 ? list[rnd.Next(list.Count)] : null;
        }

        static void Give(GameLoop game, LootSummary s, CurrencyType type, int amount, Vector3 at)
        {
            amount = Mathf.CeilToInt(amount * Upgrades.UpgradeEffects.ExpLoot);   // Bogaty ładunek
            game.Wallet.Add(type, amount);
            GameEvents.RaiseCurrencyGained(at, type, amount);
            s.lines.Add(Loc.Get("cost." + type.ToString().ToLowerInvariant(), amount));
        }

        /// <summary>Panel z listą łupu po rozładunku.</summary>
        class LootSummary : IContextActions
        {
            public Vector3 anchor;
            public string title;
            public readonly List<string> lines = new List<string>();

            public void FillContext(ContextInfo info)
            {
                info.title = title ?? Loc.Get("ui.loot.title");
                info.worldAnchor = anchor;
                info.lines.AddRange(lines);
            }
        }
    }
}
