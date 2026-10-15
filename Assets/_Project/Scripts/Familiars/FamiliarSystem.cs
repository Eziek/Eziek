using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Familiars
{
    /// <summary>Rodzaj chowańca: pozy grafiki (kolejność jak w tools/pixel-proto/familiars.py) i przysmak do karmienia.</summary>
    public class FamiliarDef
    {
        public string id;
        public string[] poses;
        public CurrencyType treat;
        public int treatCost;   // za poziom więzi 0; potem x (poziom + 1)

        public int Pose(string name) => Array.IndexOf(poses, name);
    }

    [Serializable]
    public class FamiliarSave
    {
        public List<string> arrived = new List<string>();
        public int nightHarvests;
        public long lastGiftCycle = -1;
        // więź (karmienie): równoległe listy id / liczba karmień / doba ostatniego karmienia
        public List<string> bondIds = new List<string>();
        public List<int> bondFeeds = new List<int>();
        public List<long> bondFed = new List<long>();
        public int moonHarvests, rainHarvests;
        public long ravenGiftCycle = -1, batGiftCycle = -1;
    }

    /// <summary>
    /// Chowańce (GDD §9 p. 38 i 102): przychodzą same, gdy wyspa spełni warunek, i zostają na zawsze z bonusem.
    /// Kot (10 kafelków i skrzynia): mutacje częściej, raz na dobę monety. Kruk (dorosłe drzewo owocowe): drzewa szybciej,
    /// raz na dobę nasiona. Ropucha (20 roślin nocy): rośliny nocy szybciej. Nietoperz (3 lampy): lampy palą mniej, o świcie
    /// nawóz. Ćma księżycowa (10 księżycowych kwiatów lub maków): rośliny nocy drożej. Sowa (5 ogników): ogniki więcej
    /// doświadczenia. Ślimak (15 roślin zebranych w deszczu): rośliny dnia w deszczu szybciej. Lis-widmo (powrót z rejsu
    /// we mgłę): kupiec-widmo taniej.
    /// Więź: klik w chowańca = panel; raz na dobę gry można go nakarmić (przysmak kosztuje więcej z każdym poziomem),
    /// karmienia podnoszą poziom więzi (0-4), a każdy poziom wzmacnia bonus.
    /// </summary>
    public class FamiliarSystem : MonoBehaviour
    {
        public const string Cat = "cat", Raven = "raven", Toad = "toad", Bat = "bat",
                            Moth = "moth", Owl = "owl", Snail = "snail", Fox = "fox";
        public const int MaxBond = 4;
        static readonly int[] BondFeeds = { 0, 1, 3, 6, 10 };   // karmień do poziomu 0..4

        public static readonly FamiliarDef[] Defs =
        {
            new FamiliarDef { id = Cat, poses = new[] { "sit0", "sit1", "blink", "walk0", "walk1", "walk2", "walk3", "sleep0", "sleep1", "groom", "stretch", "pounce" }, treat = CurrencyType.Coins, treatCost = 15 },
            new FamiliarDef { id = Raven, poses = new[] { "sit0", "sit1", "peck", "caw", "fly0", "fly1", "fly2" }, treat = CurrencyType.Coins, treatCost = 20 },
            new FamiliarDef { id = Toad, poses = new[] { "sit", "blink", "croak", "crouch", "jump", "tongue" }, treat = CurrencyType.NightHarvest, treatCost = 2 },
            new FamiliarDef { id = Bat, poses = new[] { "fly0", "fly1", "fly2", "hang0", "hang1" }, treat = CurrencyType.NightHarvest, treatCost = 2 },
            new FamiliarDef { id = Moth, poses = new[] { "fly0", "fly1", "fly2", "rest" }, treat = CurrencyType.NightHarvest, treatCost = 3 },
            new FamiliarDef { id = Owl, poses = new[] { "sit", "blink", "look", "hoot", "fly0", "fly1" }, treat = CurrencyType.Coins, treatCost = 25 },
            new FamiliarDef { id = Snail, poses = new[] { "crawl0", "crawl1", "hide" }, treat = CurrencyType.Coins, treatCost = 10 },
            new FamiliarDef { id = Fox, poses = new[] { "sit0", "sit1", "walk0", "walk1", "walk2", "walk3", "curl" }, treat = CurrencyType.NightHarvest, treatCost = 4 },
        };

        public static FamiliarDef Def(string id)
        {
            foreach (var d in Defs)
                if (d.id == id) return d;
            return null;
        }

        public static FamiliarSystem Instance { get; private set; }

        readonly HashSet<string> arrived = new HashSet<string>();
        readonly Dictionary<string, Familiar> spawned = new Dictionary<string, Familiar>();
        readonly Dictionary<string, int> feeds = new Dictionary<string, int>();
        readonly Dictionary<string, long> fedCycle = new Dictionary<string, long>();
        int nightHarvests, moonHarvests, rainHarvests;
        long lastGiftCycle = -1, ravenGiftCycle = -1, batGiftCycle = -1;
        float timer = 3f;
        Transform root;

        public static bool Has(string kind) => Instance != null && Instance.arrived.Contains(kind);

        /// <summary>Poziom więzi 0..4 (0 = jeszcze nie karmiony).</summary>
        public static int Bond(string kind)
        {
            if (Instance == null || !Instance.feeds.TryGetValue(kind, out int n))
                return 0;
            int level = 0;
            for (int i = 1; i < BondFeeds.Length; i++)
                if (n >= BondFeeds[i]) level = i;
            return level;
        }

        /// <summary>Siła bonusu: poziom więzi, a domek dokłada jeszcze jeden.</summary>
        static float Level(string kind) => Has(kind) ? Bond(kind) + (FamiliarHome.Of(kind) != null ? 1 : 0) : 0f;

        // ---- bonusy dla innych systemów (każdy poziom więzi wzmacnia bonus)
        public static float MutationMultiplier => Has(Cat) ? 2f + 0.25f * Level(Cat) : 1f;
        public static float TreeSpeed => Has(Raven) ? 1.25f + 0.05f * Level(Raven) : 1f;
        public static float NightGrowth => Has(Toad) ? 1.15f + 0.03f * Level(Toad) : 1f;
        public static float LampBurn => Has(Bat) ? 0.8f - 0.03f * Level(Bat) : 1f;
        public static float NightSale => Has(Moth) ? 1.15f + 0.05f * Level(Moth) : 1f;
        public static float WispXp => Has(Owl) ? 1.25f + 0.05f * Level(Owl) : 1f;
        public static float RainGrowth => Has(Snail) ? 1.15f + 0.03f * Level(Snail) : 1f;
        public static float MerchantPrice => Has(Fox) ? 0.9f - 0.03f * Level(Fox) : 1f;

        /// <summary>Bonus w procentach do tekstu (familiar.*.bonus, {0}).</summary>
        public static int BonusPercent(string kind)
        {
            float level = Bond(kind) + (FamiliarHome.Of(kind) != null ? 1 : 0);
            switch (kind)
            {
                case Cat: return Mathf.RoundToInt((1f + 0.25f * level) * 100f);
                case Raven: return Mathf.RoundToInt((0.25f + 0.05f * level) * 100f);
                case Toad: return Mathf.RoundToInt((0.15f + 0.03f * level) * 100f);
                case Bat: return Mathf.RoundToInt((0.2f + 0.03f * level) * 100f);
                case Moth: return Mathf.RoundToInt((0.15f + 0.05f * level) * 100f);
                case Owl: return Mathf.RoundToInt((0.25f + 0.05f * level) * 100f);
                case Snail: return Mathf.RoundToInt((0.15f + 0.03f * level) * 100f);
                default: return Mathf.RoundToInt((0.1f + 0.03f * level) * 100f);
            }
        }

        public static string BonusText(string kind) => Loc.Get("familiar." + kind + ".bonus", BonusPercent(kind));

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnEnable() => GameEvents.Harvested += OnHarvested;
        void OnDisable() => GameEvents.Harvested -= OnHarvested;

        void OnHarvested(string id, int count)
        {
            var game = GameLoop.Instance;
            if (game == null || game.Crops == null || id == null || !id.StartsWith("crop:"))
                return;
            var def = game.Crops.Find(id.Substring(5));
            if (def == null)
                return;
            if (def.kind == CropKind.Night)
                nightHarvests += count;
            if (def.id == "moonflower" || def.id == "moonpoppy")
                moonHarvests += count;
            if (Weather.WeatherSystem.Raining)
                rainHarvests += count;
        }

        /// <summary>Postęp warunku przybycia (do podpowiedzi w Bestiariuszu): (ile jest, ile trzeba).</summary>
        public static (int have, int need) Progress(string kind)
        {
            var s = Instance;
            var island = Island.Instance;
            switch (kind)
            {
                case Cat: return (island != null && island.Ready ? island.Map.Purchased : 0, 10);
                case Raven: return (FruitTree.All.Exists(t => t != null && t.Adult) ? 1 : 0, 1);
                case Toad: return (s != null ? s.nightHarvests : 0, 20);
                case Bat: return (Lamp.All.Count, 3);
                case Moth: return (s != null ? s.moonHarvests : 0, 10);
                case Owl: return (Wisp.All.Count, 5);
                case Snail: return (s != null ? s.rainHarvests : 0, 15);
                default: return (Prestige.Voyages, 1);
            }
        }

        bool Condition(string kind, Island island)
        {
            var (have, need) = Progress(kind);
            if (kind == Cat)
                return have >= need && HarvestCrate.All.Count > 0;
            return have >= need;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 5f;
            var game = GameLoop.Instance;
            var island = Island.Instance;
            if (game == null || island == null || !island.Ready || Save.SaveSystem.InMenu)
                return;
            foreach (var d in Defs)
            {
                var kind = d.id;
                if (!arrived.Contains(kind) && Condition(kind, island))
                {
                    arrived.Add(kind);
                    var f = Spawn(kind);
                    if (f != null)
                        Visual.IslandFx.FindPopped(f.transform.position + new Vector3(0f, 0.4f, 0f));
                    GameEvents.PlaySound("magic");
                    GameEvents.RaiseToast("toast.familiar.arrived", Loc.Get("familiar." + kind), BonusText(kind));
                    Lore.Chronicle.Note("familiar:" + kind);
                }
                else if (arrived.Contains(kind) && !spawned.ContainsKey(kind))
                    Spawn(kind);   // po wczytaniu zapisu
            }
            Gifts(game);
        }

        // ------------------------------------------------------------ prezenty

        void Gifts(GameLoop game)
        {
            long cycle = game.Clock.CycleNumber;
            // kot: raz na dobę coś błyszczącego (monety)
            if (Ready(Cat, ref lastGiftCycle, cycle, out var cat))
            {
                int coins = Mathf.RoundToInt((UnityEngine.Random.Range(8, 21) + game.Island.Map.Purchased / 2) * (1f + 0.25f * Bond(Cat)));
                game.Wallet.Add(coins);
                GameEvents.RaiseCurrencyGained(cat.transform.position + new Vector3(0f, 0.4f, 0f), CurrencyType.Coins, coins);
                GameEvents.RaiseToast("toast.familiar.gift", coins);
                cat.React(Familiar.Mood.Happy);
            }
            // kruk: raz na dobę garść nasion (odblokowana roślina, nie hybryda)
            if (Ready(Raven, ref ravenGiftCycle, cycle, out var raven) && game.Crops != null)
            {
                var pool = game.Crops.crops.FindAll(c => c != null && game.IsUnlocked(c) && !c.requiresFullMoon);
                if (pool.Count > 0)
                {
                    var c = pool[UnityEngine.Random.Range(0, pool.Count)];
                    int n = 2 + Bond(Raven);
                    game.Seeds.Add(c.id, n);
                    GameEvents.RaiseToast("toast.familiar.raven_gift", n, Loc.Get("crop." + c.id));
                    raven.React(Familiar.Mood.Happy);
                }
            }
            // nietoperz: o świcie nawóz (po nocy łowów)
            if (game.Clock.IsDawn && Ready(Bat, ref batGiftCycle, cycle, out var bat))
            {
                int n = 1 + (Bond(Bat) >= 3 ? 1 : 0);
                game.AddFertilizer(n);
                GameEvents.RaiseToast("toast.familiar.bat_gift", n);
                bat.React(Familiar.Mood.Happy);
            }
        }

        /// <summary>Prezent chowańca należy się w tej dobie (pierwsze sprawdzenie tylko zapamiętuje dobę).</summary>
        bool Ready(string kind, ref long last, long cycle, out Familiar f)
        {
            f = null;
            if (!arrived.Contains(kind) || !spawned.TryGetValue(kind, out f) || f == null)
                return false;
            if (last < 0)
                last = cycle;
            if (cycle <= last)
                return false;
            last = cycle;
            return true;
        }

        // ------------------------------------------------------------ karmienie

        public static List<CurrencyAmount> TreatCost(string kind)
        {
            var d = Def(kind);
            int amount = d != null ? d.treatCost * (Bond(kind) + 1) : 0;
            return new List<CurrencyAmount> { new CurrencyAmount(d != null ? d.treat : CurrencyType.Coins, amount) };
        }

        /// <summary>Nakarmiony już w tej dobie gry (raz na dobę).</summary>
        public static bool FedToday(string kind)
        {
            var s = Instance;
            var game = GameLoop.Instance;
            return s != null && game != null && s.fedCycle.TryGetValue(kind, out long c) && c == game.Clock.CycleNumber;
        }

        public static int Feeds(string kind) => Instance != null && Instance.feeds.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>Karmień do następnego poziomu więzi (-1 = najwyższy).</summary>
        public static int NextBondFeeds(string kind)
        {
            int level = Bond(kind);
            return level >= MaxBond ? -1 : BondFeeds[level + 1];
        }

        public bool Feed(string kind)
        {
            var game = GameLoop.Instance;
            if (game == null || !arrived.Contains(kind) || FedToday(kind))
                return false;
            var cost = TreatCost(kind);
            if (!game.Wallet.TrySpend(cost))
            {
                GameEvents.RaiseToast("ui.place.need", UI.PlacementMode.CostText(cost));
                return false;
            }
            int before = Bond(kind);
            feeds[kind] = Feeds(kind) + 1;
            fedCycle[kind] = game.Clock.CycleNumber;
            if (spawned.TryGetValue(kind, out var f) && f != null)
                f.React(Familiar.Mood.Fed);
            GameEvents.PlaySound("magic", 0.5f);
            int after = Bond(kind);
            if (after > before)
                GameEvents.RaiseToast("toast.familiar.bond", Loc.Get("familiar." + kind), after, BonusText(kind));
            else
                GameEvents.RaiseToast("toast.familiar.fed", Loc.Get("familiar." + kind));
            return true;
        }

        // ------------------------------------------------------------ na wyspie

        Familiar Spawn(string kind)
        {
            var art = Island.Instance != null ? Island.Instance.Art : null;
            var fa = art != null ? art.Familiar(kind) : null;
            var def = Def(kind);
            if (fa == null || fa.frames == null || fa.frames.Length == 0 || fa.frames[0] == null || def == null)
                return null;   // grafika jeszcze nie wczytana (Wyspa → 2): chowaniec i tak daje bonus
            if (root == null)
            {
                root = new GameObject("Familiars").transform;
                root.SetParent(transform, false);
            }
            var go = new GameObject("Familiar_" + kind);
            go.transform.SetParent(root, false);
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();   // osobno: podskok przy reakcji nie rusza toru
            body.transform.SetParent(go.transform, false);
            var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
            g.transform.SetParent(body.transform, false);
            if (art.glow != null)
                g.sharedMaterial = art.glow;
            var f = go.AddComponent<Familiar>();
            f.Setup(def, body, g, fa.frames, fa.glows);
            spawned[kind] = f;
            return f;
        }

        /// <summary>Do testów (HUD debugowy): wszystkie chowańce od razu.</summary>
        public void DebugArriveAll()
        {
            foreach (var d in Defs)
                if (arrived.Add(d.id))
                    Spawn(d.id);
        }

        /// <summary>Do testów (HUD debugowy): karmienie bez kosztu i bez limitu doby (więź +1).</summary>
        public void DebugBondAll()
        {
            foreach (var d in Defs)
                if (arrived.Contains(d.id))
                    feeds[d.id] = Feeds(d.id) + 1;
        }

        // ---- zapis
        public FamiliarSave Snapshot()
        {
            var save = new FamiliarSave
            {
                arrived = new List<string>(arrived), nightHarvests = nightHarvests, lastGiftCycle = lastGiftCycle,
                moonHarvests = moonHarvests, rainHarvests = rainHarvests, ravenGiftCycle = ravenGiftCycle, batGiftCycle = batGiftCycle,
            };
            foreach (var kv in feeds)
            {
                save.bondIds.Add(kv.Key);
                save.bondFeeds.Add(kv.Value);
                save.bondFed.Add(fedCycle.TryGetValue(kv.Key, out long c) ? c : -1);
            }
            return save;
        }

        public void Restore(FamiliarSave save)
        {
            arrived.Clear();
            foreach (var f in spawned.Values)
                if (f != null) Destroy(f.gameObject);
            spawned.Clear();
            feeds.Clear();
            fedCycle.Clear();
            nightHarvests = moonHarvests = rainHarvests = 0;
            lastGiftCycle = ravenGiftCycle = batGiftCycle = -1;
            if (save == null)
                return;
            if (save.arrived != null)
                foreach (var k in save.arrived)
                    if (Def(k) != null) arrived.Add(k);
            nightHarvests = save.nightHarvests;
            moonHarvests = save.moonHarvests;
            rainHarvests = save.rainHarvests;
            lastGiftCycle = save.lastGiftCycle;
            ravenGiftCycle = save.ravenGiftCycle;
            batGiftCycle = save.batGiftCycle;
            if (save.bondIds != null && save.bondFeeds != null)
                for (int i = 0; i < save.bondIds.Count && i < save.bondFeeds.Count; i++)
                {
                    feeds[save.bondIds[i]] = save.bondFeeds[i];
                    if (save.bondFed != null && i < save.bondFed.Count && save.bondFed[i] >= 0)
                        fedCycle[save.bondIds[i]] = save.bondFed[i];
                }
        }

        /// <summary>Chowaniec danego rodzaju na wyspie (zabawy ogników, nietoperz goni ćmę).</summary>
        public static Familiar Find(string kind) =>
            Instance != null && Instance.spawned.TryGetValue(kind, out var f) ? f : null;
    }
}
