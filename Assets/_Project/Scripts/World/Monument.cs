using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.World
{
    [Serializable]
    public class MonumentSave
    {
        public int c, r;          // lewy górny kafelek monumentu (WorldPlan.monuments)
        public int phase;         // ukończone etapy 0-4
        public bool building;     // trwa budowa kolejnego etapu
        public float progress;    // sekundy budowy bieżącego etapu
        public float timer;       // gotowy monument: sekundy pracy do następnego daru
    }

    /// <summary>
    /// Monument na wysepce fabularnej (§13.11): jeden obiekt na prostokącie 3x2 kafelki. Odbudowa ma 4 etapy; każdy
    /// trzeba opłacić (drewno, kamień, monety), a potem budują go wolne ogniki: zlatują się na rusztowanie i stukają,
    /// etap trwa ok. 10 min pracy gracza (szybciej z 2-3 ognikami). Każdy etap zmienia wygląd (ruina, mur z rusztowaniem
    /// coraz wyżej, gotowa budowla) i odsłania zapisek historii. Gotowy monument przy pracy daje walutę historii.
    /// </summary>
    public class Monument : MonoBehaviour, IClickable, IContextActions
    {
        public const int Phases = 4;
        public const float PhaseSeconds = 600f;
        public const int MaxBuilders = 3;
        const float Ppu = 16f;
        // bez żadnego ognika na wyspie budowa idzie sama, wolniej (żeby nie utknąć przed pierwszym gniazdem)
        const float NoWispSpeed = 0.5f;

        public static readonly List<Monument> All = new List<Monument>();

        Island island;
        StoryArt art;
        SpriteRenderer body, glow;
        NightGlow light;
        readonly List<Automation.Wisp> builders = new List<Automation.Wisp>();
        float fxTimer;

        public StorySite Site { get; private set; }
        public int Islet { get; private set; }
        public TilePos Origin { get; private set; }
        public int Phase { get; private set; }
        public bool Building { get; private set; }
        public float Progress { get; private set; }
        public float Timer { get; private set; }
        public bool Done => Phase >= Phases;
        public bool NeedsBuilder => Building && isActiveAndEnabled && builders.Count < MaxBuilders;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(Island owner, StoryArt storyArt, SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer,
                         StorySite site, int islet, TilePos origin, MonumentSave state)
        {
            island = owner;
            art = storyArt;
            body = bodyRenderer;
            glow = glowRenderer;
            Site = site;
            Islet = islet;
            Origin = origin;
            if (state != null)
            {
                Phase = Mathf.Clamp(state.phase, 0, Phases);
                Building = state.building && !Done;
                Progress = Mathf.Clamp(state.progress, 0f, PhaseSeconds);
                Timer = Mathf.Clamp(state.timer, 0f, site.interval);
            }
            Refresh();
        }

        public MonumentSave Snapshot() => new MonumentSave
        {
            c = Origin.c, r = Origin.r, phase = Phase, building = Building, progress = Progress, timer = Timer,
        };

        void Refresh()
        {
            var phases = art != null ? art.phases : null;
            if (body != null && phases != null && phases.Length > 0)
                body.sprite = phases[Mathf.Clamp(Phase, 0, phases.Length - 1)];
            if (glow != null)
            {
                glow.sprite = art != null ? art.doneGlow : null;
                glow.enabled = Done && glow.sprite != null;
            }
            if (Done && light == null)
                light = NightGlow.Add(gameObject, new Vector2(0f, 2.2f), new Color(1f, 0.85f, 0.6f), 2.6f, 0.55f);
        }

        /// <summary>Koszt kolejnego etapu: podstawa historii, każdy etap o 40% droższy.</summary>
        public List<CurrencyAmount> PhaseCost()
        {
            float m = 1f + 0.4f * Phase;
            return new List<CurrencyAmount>
            {
                new CurrencyAmount(CurrencyType.Wood, Mathf.RoundToInt(Site.wood * m)),
                new CurrencyAmount(CurrencyType.Stone, Mathf.RoundToInt(Site.stone * m)),
                new CurrencyAmount(CurrencyType.Coins, Mathf.RoundToInt(Site.coins * m)),
            };
        }

        public bool TryStartPhase(Wallet wallet)
        {
            if (Done || Building || wallet == null)
                return false;
            var cost = PhaseCost();
            if (!wallet.TrySpend(cost))
            {
                GameEvents.RaiseToast("toast.ruin.need", cost[0].amount, cost[1].amount, cost[2].amount);
                return false;
            }
            Building = true;
            Progress = 0f;
            GameEvents.PlaySound("dig");
            GameEvents.RaiseToast("toast.monument.started", Loc.Get(Site.NameKey));
            IslandFx.TileClaimed(transform.position + new Vector3(0f, 0.5f, 0f), island != null && island.Art != null ? island.Art.foam : null);
            return true;
        }

        // ------------------------------------------------------------ ogniki-budowniczowie

        /// <summary>Monument, który czeka na budowniczego (najbliższy podanego miejsca).</summary>
        public static Monument WantingBuilder(Vector3 near)
        {
            Monument best = null;
            float bestD = float.MaxValue;
            foreach (var m in All)
            {
                if (m == null || !m.NeedsBuilder)
                    continue;
                float d = (m.transform.position - near).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = m;
                }
            }
            return best;
        }

        /// <summary>Miejsce na rusztowaniu: lewy bok, prawy bok, góra; coraz wyżej z każdym etapem.</summary>
        Vector3 Spot(int i)
        {
            float top = (0.9f + 0.7f * Phase) ;
            var off = i == 0 ? new Vector3(-1.45f, top * 0.7f, 0f) : i == 1 ? new Vector3(1.45f, top * 0.85f, 0f) : new Vector3(0.2f, top + 0.6f, 0f);
            return transform.position + off;
        }

        public bool TryJoin(Automation.Wisp wisp, out Vector3 spot)
        {
            spot = default;
            Prune();
            if (wisp == null || !NeedsBuilder || builders.Contains(wisp))
                return false;
            builders.Add(wisp);
            spot = Spot(builders.Count - 1);
            return true;
        }

        public void Leave(Automation.Wisp wisp) => builders.Remove(wisp);

        void Prune() => builders.RemoveAll(b => b == null || !b.isActiveAndEnabled || b.BuildSite != this);

        /// <summary>Ogniki, które już doleciały i stukają.</summary>
        int Working()
        {
            Prune();
            int n = 0;
            foreach (var b in builders)
                if (b.AtBuildSpot)
                    n++;
            return n;
        }

        // ------------------------------------------------------------ budowa i dary

        /// <summary>Tick z AutomationSystem: budowa idzie przy pracy gracza i z ognikami na rusztowaniu; gotowy daje walutę.</summary>
        public void Tick(float dt, GameLoop game)
        {
            if (game == null || game.Activity == null || !game.Activity.IsActive)
                return;
            float tempo = game.Activity.TempoMultiplier;
            if (Building)
            {
                int n = Working();
                float speed = n > 0 ? 1f + 0.3f * (n - 1) : (Automation.Wisp.All.Count == 0 ? NoWispSpeed : 0f);
                Progress += dt * tempo * speed;
                if (Progress >= PhaseSeconds)
                    CompletePhase();
                return;
            }
            if (!Done)
                return;
            Timer += dt * tempo;
            if (Timer < Site.interval)
                return;
            Timer -= Site.interval;
            game.Wallet.Add(Site.product, Site.amount);
            GameEvents.RaiseCurrencyGained(transform.position + new Vector3(0f, 2.5f, 0f), Site.product, Site.amount);
            GameEvents.PlaySound("coin", 0.4f);
        }

        void CompletePhase()
        {
            Phase++;
            Building = false;
            Progress = 0f;
            builders.Clear();   // ogniki same odlatują, gdy Building zgaśnie
            Refresh();
            string lore = Site.Lore(Mathf.Clamp(Phase, 1, StorySite.LoreLines));
            Lore.Chronicle.Note("lore:" + lore);
            GameEvents.PlaySound("tile");
            var fx = PixelParticles.Instance;
            var dust = island != null && island.Art != null ? island.Art.dust : null;
            if (fx != null && dust != null)
                for (int i = 0; i < 8; i++)
                    fx.Spawn(transform.position + new Vector3((i - 3.5f) * 6f / Ppu, 2f / Ppu, 0f), dust,
                             new Vector2((i - 3.5f) * 4f, 10f), 0f, 0.5f, body != null ? body.sortingOrder + 2 : 0, false, 8f);
            if (Done)
            {
                foreach (var key in Site.AllLore())
                    Lore.Chronicle.Note("lore:" + key);
                GameEvents.RaiseToast("toast.story.built", Loc.Get(Site.BuildingKey));
                GameEvents.RaiseToast("toast.find.lore", Loc.Get(Site.EndKey));
                Lore.Chronicle.Count("story");
            }
            else
                GameEvents.RaiseToast("toast.monument.phase", Loc.Get(lore), Phase, Phases);
            island?.NotifyChanged();
        }

        /// <summary>F1: kolejny etap od razu (rozpoczęty za darmo albo dokończony).</summary>
        public void DebugAdvance()
        {
            if (Done)
                return;
            if (!Building)
            {
                Building = true;
                Progress = 0f;
                return;
            }
            CompletePhase();
        }

        void Update()
        {
            if (!Building || body == null)
                return;
            fxTimer -= Time.unscaledDeltaTime;
            if (fxTimer > 0f)
                return;
            fxTimer = UnityEngine.Random.Range(0.35f, 0.8f);
            if (Working() == 0)
                return;
            // budowa: wióry i odpryski z miejsca, w którym stukają ogniki, czasem kłąb pyłu u podstawy
            var fx = PixelParticles.Instance;
            var b = builders[UnityEngine.Random.Range(0, builders.Count)];
            if (fx == null || b == null)
                return;
            var chips = island != null && island.Art != null ? island.Art.Deco(UnityEngine.Random.value < 0.5f ? DecoKind.Rock : DecoKind.TreeRound) : null;
            if (chips != null && chips.chips != null)
                fx.Burst(b.transform.position + new Vector3(0f, -2f / Ppu, 0f), chips.chips, 2, 26f, 140f, 0.45f, body.sortingOrder + 3);
            if (UnityEngine.Random.value < 0.25f)
                GameEvents.PlaySound("dig", 0.15f);
        }

        // ------------------------------------------------------------ panel

        public void OnClick() => GameEvents.RequestContext(this);

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get(Done ? Site.BuildingKey : Site.NameKey);
            info.worldAnchor = transform.position + new Vector3(0f, 4.2f, 0f);
            int minutes = Mathf.RoundToInt(Site.interval / 60f);
            if (Done)
            {
                info.lines.Add(Loc.Get(Site.DescKey, Site.amount, minutes));
                info.lines.Add(Loc.Get("ui.story.next", Mathf.Max(1, Mathf.CeilToInt((Site.interval - Timer) / 60f))));
                info.lines.Add(Loc.Get(Site.EndKey));
                return;
            }
            info.lines.Add(Loc.Get("ui.monument.desc", Loc.Get(Site.BuildingKey)));
            info.lines.Add(Loc.Get(Site.DescKey, Site.amount, minutes));
            info.lines.Add(Loc.Get("ui.monument.phase", Phase, Phases));
            if (Building)
            {
                int n = Working();
                info.lines.Add(Loc.Get("ui.monument.building", Mathf.FloorToInt(Progress / PhaseSeconds * 100f), n, MaxBuilders));
                if (n == 0)
                    info.lines.Add(Loc.Get(Automation.Wisp.All.Count == 0 ? "ui.monument.no_wisps" : "ui.monument.waiting"));
                return;
            }
            var cost = PhaseCost();
            info.lines.Add(Loc.Get("ui.monument.cost", cost[0].amount, cost[1].amount, cost[2].amount));
            var game = GameLoop.Instance;
            bool afford = game != null && game.Wallet.CanAfford(cost);
            info.Button(Loc.Get("act.rebuild"), () => TryStartPhase(game != null ? game.Wallet : null), afford);
        }
    }
}
