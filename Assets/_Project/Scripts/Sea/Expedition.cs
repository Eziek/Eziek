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
        public bool longTrip;
    }

    /// <summary>
    /// Łódź i wyprawy (GDD §11.3 F): łódź płynie w mgłę na N godzin AKTYWNEJ pracy i wraca z łupem
    /// (nasiona, drewno, kamień, monety, czasem odłamek gwiazdy, fragment lore albo nasiono hybrydy). Jedna wyprawa naraz.
    /// </summary>
    public class Expedition : MonoBehaviour, IClickable, IContextActions
    {
        public const float ShortHours = 2f, LongHours = 4f;
        const float Ppu = 16f;

        enum State { Docked, Away, Returned }

        SpriteRenderer body;
        SpriteRenderer glow;
        Collider2D clickArea;
        Vector3 dock;
        State state;
        float progress, need;
        bool longTrip;
        bool animating;

        public void Init(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Collider2D area)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            clickArea = area;
            dock = transform.position;
            Show(true);
        }

        public ExpeditionSave Snapshot() => new ExpeditionSave { state = (int)state, progress = progress, need = need, longTrip = longTrip };

        public void Restore(ExpeditionSave s)
        {
            if (s == null)
                return;
            state = (State)Mathf.Clamp(s.state, 0, 2);
            progress = s.progress;
            need = s.need;
            longTrip = s.longTrip;
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
            var game = GameLoop.Instance;
            if (game == null || !game.Activity.IsActive)
                return;
            progress += Time.unscaledDeltaTime * Mathf.Max(0f, game.DebugTimeScale);
            if (progress >= need && !animating)
                StartCoroutine(Return());
        }

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
                    info.Button(Loc.Get("ui.boat.short", ShortHours), () => Send(false));
                    info.Button(Loc.Get("ui.boat.long", LongHours), () => Send(true));
                    break;
                case State.Away:
                    info.lines.Add(Loc.Get("ui.boat.away", Mathf.Max(0f, (need - progress) / 3600f)));
                    break;
                case State.Returned:
                    info.lines.Add(Loc.Get("ui.boat.returned"));
                    info.Button(Loc.Get("ui.boat.unload"), Unload);
                    break;
            }
        }

        void Send(bool isLong)
        {
            if (state != State.Docked || animating)
                return;
            longTrip = isLong;
            need = (isLong ? LongHours : ShortHours) * 3600f;
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

        /// <summary>Łódź odpływa w mgłę (albo z niej wraca) skokowo, migając przy końcu jak cząsteczki.</summary>
        IEnumerator Sail(bool away)
        {
            animating = true;
            var wait = new WaitForSecondsRealtime(0.15f);
            var dir = new Vector3(0f, -1f, 0f);
            if (body != null) body.enabled = true;
            if (glow != null) glow.enabled = true;
            if (clickArea != null) clickArea.enabled = false;
            for (int i = 0; i <= 8; i++)
            {
                int k = away ? i : 8 - i;
                transform.position = dock + dir * (k * 3f / Ppu);
                float a = 1f - k / 8f;
                if (body != null) body.color = new Color(1f, 1f, 1f, a);
                yield return wait;
            }
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
            int rolls = longTrip ? rnd.Next(5, 7) : rnd.Next(2, 4);
            for (int i = 0; i < rolls; i++)
                Roll(game, rnd, summary, false);
            if (longTrip)
                Roll(game, rnd, summary, true);   // długa wyprawa: zawsze coś rzadkiego
            state = State.Docked;
            progress = 0f;
            need = 0f;
            IslandFx.FindPopped(transform.position);
            GameEvents.RaiseToast("toast.boat.unloaded");
            GameEvents.RequestContext(summary);
        }

        void Roll(GameLoop game, System.Random rnd, LootSummary s, bool rare)
        {
            double v = rare ? 0.75 + rnd.NextDouble() * 0.25 : rnd.NextDouble();
            var at = transform.position;
            if (v < 0.22)
            {
                var seed = RandomNightSeed(game, rnd);
                if (seed != null)
                {
                    game.Seeds.Add(seed.id, 2);
                    s.lines.Add(Loc.Get("ui.loot.seeds", 2, Loc.Get("crop." + seed.id)));
                    return;
                }
            }
            if (v < 0.40) { Give(game, s, CurrencyType.Wood, rnd.Next(4, 9), at); return; }
            if (v < 0.55) { Give(game, s, CurrencyType.Stone, rnd.Next(3, 7), at); return; }
            if (v < 0.70) { Give(game, s, CurrencyType.Coins, rnd.Next(40, 121), at); return; }
            if (v < 0.78)
            {
                game.AddFertilizer(2);
                s.lines.Add(Loc.Get("ui.loot.fertilizer", 2));
                return;
            }
            if (v < 0.88) { Give(game, s, CurrencyType.StarShard, 1, at); return; }
            if (v < 0.95)
            {
                s.lines.Add(Loc.Get("lore." + rnd.Next(1, 7)));
                return;
            }
            CropDefinition hybrid = null;
            if (game.Crops != null)
                foreach (var c in game.Crops.crops)
                    if (c != null && c.hybrid) hybrid = c;
            if (hybrid != null)
            {
                game.Seeds.Add(hybrid.id, 1);
                s.lines.Add(Loc.Get("ui.loot.seeds", 1, Loc.Get("crop." + hybrid.id)));
            }
            else
                Give(game, s, CurrencyType.StarShard, 1, at);
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
            game.Wallet.Add(type, amount);
            GameEvents.RaiseCurrencyGained(at, type, amount);
            s.lines.Add(Loc.Get("cost." + type.ToString().ToLowerInvariant(), amount));
        }

        /// <summary>Panel z listą łupu po rozładunku.</summary>
        class LootSummary : IContextActions
        {
            public Vector3 anchor;
            public readonly List<string> lines = new List<string>();

            public void FillContext(ContextInfo info)
            {
                info.title = Loc.Get("ui.loot.title");
                info.worldAnchor = anchor;
                info.lines.AddRange(lines);
            }
        }
    }
}
