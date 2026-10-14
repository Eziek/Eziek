using System.Collections.Generic;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Save;
using Wyspa.Upgrades;
using Wyspa.World;

namespace Wyspa.Core
{
    /// <summary>
    /// Serce symulacji: co tick (domyślnie 1 s) sprawdza aktywność, ładuje Esencję,
    /// zasila lampy i podlicza wzrost roślin. Symulacja w tickach, nie w każdym Update.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameLoop : MonoBehaviour
    {
        public static GameLoop Instance { get; private set; }

        [SerializeField] ActivityTracker activity;
        [SerializeField] GameClock clock;
        [SerializeField] EssenceStore essence;
        [SerializeField] Wallet wallet;
        [SerializeField] CropDatabase crops;
        [SerializeField] SaveSystem saveSystem;
        [SerializeField] WorkRhythm rhythm;
        [SerializeField] UpgradeState upgrades;
        [SerializeField] AutomationSystem automation;
        [SerializeField] Island island;

        [SerializeField] float tickInterval = 1f;

        [Tooltip("Do testów: ile sekund gry mija na 1 s (np. 60 = minuta na sekundę).")]
        [SerializeField] float debugTimeScale = 1f;

        readonly List<Lamp> lampOrder = new List<Lamp>();
        readonly List<(FarmPlot plot, CropDefinition crop)> autoHarvested = new List<(FarmPlot, CropDefinition)>();
        readonly HashSet<string> unlockedSeeds = new HashSet<string>();
        readonly SeedBag seeds = new SeedBag();
        bool fullMoonAnnounced;

        /// <summary>Szansa na nasiono hybrydy przy zbiorze, gdy sąsiad (4 kierunki) ma roślinę-partnera (GDD §11.3 D).</summary>
        public const float MutationChance = 0.03f;
        /// <summary>Do testów (HUD debugowy): każda mutacja się udaje.</summary>
        public static bool DebugAlwaysMutate;

        /// <summary>Rytuał „Zmierzch”: rośliny nocy rosną też w ostatniej godzinie przed nocą.</summary>
        public static bool DuskGrowth;
        /// <summary>Rytuał „Głębokie korzenie”: rośliny dnia rosną szybciej.</summary>
        public static float DayGrowthMultiplier = 1f;
        float accumulator;

        public ActivityTracker Activity => activity;
        public GameClock Clock => clock;
        public EssenceStore Essence => essence;
        public Wallet Wallet => wallet;
        public CropDatabase Crops => crops;
        public WorkRhythm Rhythm => rhythm;
        public UpgradeState Upgrades => upgrades;
        public AutomationSystem Automation => automation;
        public Island Island => island;
        public SeedBag Seeds => seeds;

        int fertilizer;
        /// <summary>Nawóz (od kupca-widma): przy sadzeniu daje +1 do jakości (GDD §11.3 D).</summary>
        public int Fertilizer { get => fertilizer; set => fertilizer = Mathf.Max(0, value); }
        public void AddFertilizer(int n) => Fertilizer += n;

        public bool TakeFertilizer()
        {
            if (fertilizer <= 0)
                return false;
            fertilizer--;
            return true;
        }

        // ---- nasiona do odblokowania (Etap 6b, Sklep → Nasiona)

        /// <summary>Czy roślinę da się kupić w kole nasion (unlockPrice 0 = od początku).</summary>
        public bool IsUnlocked(CropDefinition crop) => crop != null && !crop.hybrid && (crop.unlockPrice <= 0 || unlockedSeeds.Contains(crop.id));

        public bool TryUnlock(CropDefinition crop)
        {
            if (crop == null || IsUnlocked(crop) || crop.hybrid || !wallet.TrySpend(crop.unlockPrice))
                return false;
            unlockedSeeds.Add(crop.id);
            GameEvents.RaiseToast("toast.seed_unlocked", Localization.Loc.Get("crop." + crop.id));
            return true;
        }

        /// <summary>Do testów (HUD debugowy): wszystkie nasiona odblokowane.</summary>
        public void DebugUnlockAll()
        {
            if (crops != null)
                foreach (var c in crops.crops)
                    if (c != null && !c.hybrid) unlockedSeeds.Add(c.id);
        }

        public List<string> SnapshotUnlocks() => new List<string>(unlockedSeeds);

        public void RestoreUnlocks(IEnumerable<string> ids)
        {
            unlockedSeeds.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) unlockedSeeds.Add(id);
        }

        /// <summary>Ziarno nowego świata (każda gra inna, GDD §13.4).</summary>
        public static int NewSeed() => new System.Random().Next(1, int.MaxValue);

        public float DebugTimeScale
        {
            get => debugTimeScale;
            set => debugTimeScale = Mathf.Max(0f, value);
        }

        void Awake()
        {
            Instance = this;
            // starsze sceny (sprzed Etapu 2a) nie mają tych komponentów - dokładamy je w locie
            // (bez operatora ??, bo „pusty” obiekt Unity nie jest prawdziwym null)
            if (rhythm == null) rhythm = GetComponent<WorkRhythm>();
            if (rhythm == null) rhythm = gameObject.AddComponent<WorkRhythm>();
            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades == null) upgrades = gameObject.AddComponent<UpgradeState>();
            upgrades.Changed += ApplyUpgrades;
        }

        /// <summary>Przelicza efekty ulepszeń na systemy (wołane po zakupie i po wczytaniu gry).</summary>
        public void ApplyUpgrades()
        {
            essence.SetCapacity(upgrades.Value(UpgradeIds.EssenceJar, essence.BaseCapacity));
            if (automation != null)
                automation.ApplyUpgrades(upgrades);
        }

        /// <summary>Po każdym zbiorze (ręcznym i przez ognika): bestiariusz, liczniki cyklu, mutacje. Ponowne sadzenie robią runy siewu.</summary>
        public void AfterHarvest(FarmPlot plot, CropDefinition crop)
        {
            if (crop != null)
            {
                Lore.Chronicle.Note("crop:" + crop.id);
                Lore.Chronicle.Count("harvest");
                if (crop.kind == CropKind.Night)
                    Lore.Chronicle.Count("night");
            }
            TryMutate(plot, crop);
        }

        /// <summary>
        /// Mutacja (GDD §11.3 D): sąsiednia grządka z rośliną-partnerem daje szansę na nasiono hybrydy do worka.
        /// Pary rodziców są w danych hybryd (CropDefinition.parentA/B).
        /// </summary>
        void TryMutate(FarmPlot plot, CropDefinition crop)
        {
            if (plot == null || crop == null || crops == null || island == null)
                return;
            foreach (var n in island.NeighbourPlots(plot))
            {
                var other = n.State.crop;
                if (other == null || other == crop)
                    continue;
                foreach (var h in crops.crops)
                {
                    if (h == null || !h.IsParentPair(crop.id, other.id))
                        continue;
                    if (!DebugAlwaysMutate && Random.value >= MutationChance)
                        return;   // jedna próba na zbiór
                    seeds.Add(h.id);
                    GameEvents.RaiseToast("toast.mutation", Localization.Loc.Get("crop." + h.id));
                    Visual.IslandFx.FindPopped(plot.CropPosition);
                    return;
                }
            }
        }

        void Start()
        {
            if (SeedSelection.Current == null && crops != null && crops.crops.Count > 0)
                SeedSelection.Current = crops.crops[0];
            ApplyUpgrades();
            if (saveSystem != null)
                saveSystem.Load(this);
            if (island != null && !island.Ready)
                island.NewWorld(NewSeed());   // pierwszy start: wyspa 2x2 z losowego ziarna
            if (island != null)
                island.FlushRefunds(wallet);
            foreach (var plot in FarmPlot.All)
                plot.RefreshVisual();
        }

        void OnDestroy()
        {
            if (upgrades != null)
                upgrades.Changed -= ApplyUpgrades;
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            accumulator += Time.unscaledDeltaTime;
            while (accumulator >= tickInterval)
            {
                accumulator -= tickInterval;
                Tick(tickInterval * debugTimeScale);
            }
        }

        void Tick(float dt)
        {
            activity.Refresh();
            bool night = clock.IsNight;

            rhythm.Tick(activity.IsActive, dt, System.DateTime.Now);
            // pełnia: komunikat raz na noc (rośliny nocy x2, krwawa róża rośnie)
            if (night && clock.IsFullMoon && !fullMoonAnnounced)
            {
                fullMoonAnnounced = true;
                GameEvents.RaiseToast("toast.fullmoon");
                Lore.Chronicle.Note("event:fullmoon");
            }
            else if (!night)
                fullMoonAnnounced = false;
            if (activity.IsActive)
                essence.AddActiveSeconds(dt * rhythm.EssenceMultiplier);

            // Najtańsze lampy dostają Esencję pierwsze - gdy jej braknie, gasną najdroższe.
            lampOrder.Clear();
            lampOrder.AddRange(Lamp.All);
            lampOrder.Sort((a, b) => Cost(a).CompareTo(Cost(b)));
            foreach (var lamp in lampOrder)
                lamp.Tick(dt, night, essence);

            foreach (var plot in FarmPlot.All)
                plot.Tick(dt, ContextFor(plot, night));

            // drzewa owocowe: rosną od pracy (z tempem pracy), owoce wg gatunku (praca / noc / pełnia)
            for (int i = FruitTree.All.Count - 1; i >= 0; i--)
            {
                var tree = FruitTree.All[i];
                float mult = activity.TempoMultiplier * (island != null ? island.TreeZoneBonus(tree) : 1f);
                tree.Tick(dt, activity.IsActive, night, clock.IsFullMoon, mult);
            }

            if (automation != null)
            {
                autoHarvested.Clear();
                automation.Tick(dt, this, autoHarvested);
                foreach (var (plot, crop) in autoHarvested)
                    AfterHarvest(plot, crop);
            }
        }

        /// <summary>Warunki wzrostu dla grządki (światło, noc, aktywność) - używane też przez HUD.</summary>
        public GrowthContext ContextFor(FarmPlot plot, bool night)
        {
            Vector2 p = plot.LightProbe;
            var crop = plot.State.crop;
            if (!night && DuskGrowth && crop != null && crop.kind == CropKind.Night && clock.IsDusk)
                night = true;   // zmierzch liczy się roślinom nocy jak noc
            // rośliny dnia: rytuał „Głębokie korzenie” i tempo pracy (pisanie, mysz)
            float dayBonus = crop != null && crop.kind == CropKind.Day ? DayGrowthMultiplier * activity.TempoMultiplier : 1f;
            return new GrowthContext
            {
                playerActive = activity.IsActive,
                night = night,
                lit = Illumination.IsLit(p, night),
                multiplier = (night ? Illumination.GrowthMultiplier(p) : 1f) * (island != null ? island.ZoneBonus(plot) : 1f) * dayBonus,
                fullMoon = clock.IsFullMoon,
            };
        }

        static float Cost(Lamp lamp) => lamp.Definition != null ? lamp.Definition.essencePerMinute : 0f;
    }
}
