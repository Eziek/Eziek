using System.Collections.Generic;
using UnityEngine;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Save;

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

        public float DebugTimeScale
        {
            get => debugTimeScale;
            set => debugTimeScale = Mathf.Max(0f, value);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (SeedSelection.Current == null && crops != null && crops.crops.Count > 0)
                SeedSelection.Current = crops.crops[0];
            if (saveSystem != null)
                saveSystem.Load(this);
            foreach (var plot in FarmPlot.All)
                plot.RefreshVisual();
        }

        void OnDestroy()
        {
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

            if (activity.IsActive)
                essence.AddActiveSeconds(dt);

            // Najtańsze lampy dostają Esencję pierwsze - gdy jej braknie, gasną najdroższe.
            lampOrder.Clear();
            lampOrder.AddRange(Lamp.All);
            lampOrder.Sort((a, b) => Cost(a).CompareTo(Cost(b)));
            foreach (var lamp in lampOrder)
                lamp.Tick(dt, night, essence);

            foreach (var plot in FarmPlot.All)
            {
                Vector2 p = plot.LightProbe;
                var ctx = new GrowthContext
                {
                    playerActive = activity.IsActive,
                    night = night,
                    lit = Illumination.IsLit(p, night),
                    multiplier = night ? Illumination.GrowthMultiplier(p) : 1f,
                };
                plot.Tick(dt, ctx);
            }
        }

        static float Cost(Lamp lamp) => lamp.Definition != null ? lamp.Definition.essencePerMinute : 0f;
    }
}
