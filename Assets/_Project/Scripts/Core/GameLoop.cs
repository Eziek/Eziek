using System.Collections.Generic;
using UnityEngine;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Save;
using Wyspa.Upgrades;

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

        [SerializeField] float tickInterval = 1f;

        [Tooltip("Do testów: ile sekund gry mija na 1 s (np. 60 = minuta na sekundę).")]
        [SerializeField] float debugTimeScale = 1f;

        readonly List<Lamp> lampOrder = new List<Lamp>();
        float accumulator;

        public ActivityTracker Activity => activity;
        public GameClock Clock => clock;
        public EssenceStore Essence => essence;
        public Wallet Wallet => wallet;
        public CropDatabase Crops => crops;
        public WorkRhythm Rhythm => rhythm;
        public UpgradeState Upgrades => upgrades;

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
        }

        void Start()
        {
            if (SeedSelection.Current == null && crops != null && crops.crops.Count > 0)
                SeedSelection.Current = crops.crops[0];
            ApplyUpgrades();
            if (saveSystem != null)
                saveSystem.Load(this);
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
        }

        /// <summary>Warunki wzrostu dla grządki (światło, noc, aktywność) - używane też przez HUD.</summary>
        public GrowthContext ContextFor(FarmPlot plot, bool night)
        {
            Vector2 p = plot.LightProbe;
            return new GrowthContext
            {
                playerActive = activity.IsActive,
                night = night,
                lit = Illumination.IsLit(p, night),
                multiplier = night ? Illumination.GrowthMultiplier(p) : 1f,
            };
        }

        static float Cost(Lamp lamp) => lamp.Definition != null ? lamp.Definition.essencePerMinute : 0f;
    }
}
