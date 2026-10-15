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
    /// Serce symulacji: co tick (domyślnie 1 s) ładuje Esencję (Żar płynie sam z czasu), zasila lampy i podlicza wzrost roślin.
    /// Symulacja w tickach, nie w każdym Update; działa tylko przy włączonej grze (offline rosną tylko rośliny nocy, §9 p. 134).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameLoop : MonoBehaviour
    {
        public static GameLoop Instance { get; private set; }

        [SerializeField] GameClock clock;
        [SerializeField] EssenceStore essence;
        [SerializeField] Wallet wallet;
        [SerializeField] CropDatabase crops;
        [SerializeField] SaveSystem saveSystem;
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
        long lastCycle = -1;   // doba gry z poprzedniego ticku (świt = nowa doba)

        /// <summary>Szansa na nasiono hybrydy przy zbiorze, gdy sąsiad (4 kierunki) ma roślinę-partnera (GDD §11.3 D).</summary>
        /// <summary>Szansa mutacji przy zbiorze (3%, ulepszenie „Krzyżowanie” więcej).</summary>
        public static float MutationChance => UpgradeEffects.MutationChance;
        /// <summary>Do testów (HUD debugowy): każda mutacja się udaje.</summary>
        public static bool DebugAlwaysMutate;

        /// <summary>Rytuał „Zmierzch”: rośliny nocy rosną też w ostatniej godzinie przed nocą.</summary>
        /// <summary>Rytuał „Zmierzchowe ziarno”: o świcie i zmierzchu rośliny rosną 1,5x szybciej.</summary>
        public static bool TwilightBoost;
        /// <summary>Rytuał „Głębokie korzenie”: rośliny dnia rosną szybciej.</summary>
        public static float DayGrowthMultiplier = 1f;
        float accumulator;

        public GameClock Clock => clock;
        public EssenceStore Essence => essence;
        public Wallet Wallet => wallet;
        public CropDatabase Crops => crops;
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

        // ---- nasiona do odblokowania

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

        /// <summary>
        /// Losowe nasiono na porę dnia: w nocy roślina nocy, w dzień roślina dnia. Spośród odblokowanych
        /// (na które stać) i tych z worka; krwawe rośliny pełni tylko w pełnię. Null, gdy nie ma z czego losować.
        /// </summary>
        public CropDefinition RandomSeed(bool night, CropDefinition except = null)
        {
            if (crops == null)
                return null;
            var pool = new List<CropDefinition>();
            foreach (var c in crops.crops)
            {
                if (c == null || c == except || (c.kind == CropKind.Night) != night || (c.requiresFullMoon && !clock.IsFullMoon)
                    || !CropSeasons.Now(c))
                    continue;   // poza porą roku nie sieje się (rośliny sezonowe)
                if (seeds.Count(c.id) > 0 || (IsUnlocked(c) && wallet.Coins >= Economy.Market.SeedPrice(c)))
                    pool.Add(c);
            }
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        /// <summary>Pierścień akcji: każda pusta grządka dostaje losowe nasiono na porę dnia (worek, potem monety). Zwraca ile.</summary>
        public int PlantRandomEverywhere()
        {
            int planted = 0;
            bool night = clock.IsNight;
            foreach (var plot in FarmPlot.All.ToArray())
            {
                if (plot == null || !plot.State.IsEmpty)
                    continue;
                var crop = RandomSeed(night);
                if (crop == null)
                    break;   // nie stać na żadne nasiono
                if (!seeds.TryTake(crop.id) && (crop.hybrid || !wallet.TrySpend(Economy.Market.SeedPrice(crop))))
                    continue;
                plot.Replant(crop);
                if (TakeFertilizer())
                    plot.State.fertilized = true;
                planted++;
            }
            return planted;   // dźwięk i efekt daje każde Replant
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
            // starsze sceny nie mają tych komponentów - dokładamy je w locie
            // (bez operatora ??, bo „pusty” obiekt Unity nie jest prawdziwym null)
            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades == null) upgrades = gameObject.AddComponent<UpgradeState>();
            Alchemy.Potions.Restore(null);   // statyczne: bez tego mikstury z poprzedniego slotu działałyby dalej
            Economy.Market.Restore(null);    // zarobek rejsu i renoma (wczytuje SaveSystem)
            Economy.Prestige.Restore(null);  // perły i dary mgły (wczytuje SaveSystem)
            GameClock.SaveOffsetSeconds = 0;  // menu główne: zwykły cykl; zapis ustawia własne przesunięcie (nowa wyspa: rano)
            GameClock.SeasonStart = 0;
            Seasons.Reset();
            Lighting.Lamp.ResetScenes();      // łańcuchy lamp ze scenek poprzedniej sceny
            if (GetComponent<Orders.OrderBook>() == null) gameObject.AddComponent<Orders.OrderBook>();   // zamówienia z tablicy
            if (GetComponent<Familiars.FamiliarSystem>() == null) gameObject.AddComponent<Familiars.FamiliarSystem>();   // chowańce
            if (GetComponent<Weather.WeatherSystem>() == null) gameObject.AddComponent<Weather.WeatherSystem>();   // pogoda
            if (GetComponent<Visual.DayTransitions>() == null) gameObject.AddComponent<Visual.DayTransitions>();   // scenki świtu i zmierzchu
            if (GetComponent<Audio.AudioManager>() == null) gameObject.AddComponent<Audio.AudioManager>();   // dźwięk
            if (GetComponent<Visual.AmbientLife>() == null) gameObject.AddComponent<Visual.AmbientLife>();   // ćmy, skaczące ryby
            if (GetComponent<Platform.TrayIcon>() == null) gameObject.AddComponent<Platform.TrayIcon>();   // ikona w zasobniku (build Windows)
            upgrades.Changed += ApplyUpgrades;
        }

        /// <summary>Pojemność słoja: ulepszenie „Słój Żaru” i kryształy Żaru (+20 każdy).</summary>
        public void RefreshEssenceCapacity()
        {
            if (essence != null && upgrades != null)
                essence.SetCapacity(upgrades.Value(UpgradeIds.EssenceJar, essence.BaseCapacity) * Economy.Prestige.JarMultiplier   // Niezmordowany
                                    + Wyspa.Automation.EmberCrystal.TotalCapacityBonus);
        }

        /// <summary>Przelicza efekty ulepszeń na systemy (wołane po zakupie i po wczytaniu gry).</summary>
        public void ApplyUpgrades()
        {
            RefreshEssenceCapacity();
            UpgradeEffects.Apply(upgrades);
            Economy.Prestige.ApplyToEffects();   // dary mgły dokładają się do drzewka
            if (automation != null)
                automation.ApplyUpgrades(upgrades);
        }

        /// <summary>Po każdym zbiorze (ręcznym i przez ognika): bestiariusz, liczniki cyklu, mutacje. Ponowne sadzenie robią runy siewu.</summary>
        /// <summary>Ziarno wyspy w tle menu głównego.</summary>
        const int MenuSeed = 20261014;

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
            if (crop != null && UpgradeEffects.SeedReturn > 0f && Random.value < UpgradeEffects.SeedReturn)
            {
                seeds.Add(crop.id);   // Zbieracz nasion
                Visual.IslandFx.FindPopped(plot != null ? plot.CropPosition : Vector3.zero);
            }
            if (crop != null)
                GameEvents.RaiseHarvested("crop:" + crop.id, 1);
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
                    if (!DebugAlwaysMutate && Random.value >= MutationChance * Familiars.FamiliarSystem.MutationMultiplier)   // kot: x2
                        return;   // jedna próba na zbiór
                    // pierwsze odkrycie (§9 p. 130): wpis w Bestiariuszu (Mutacje) i 2 nasiona zamiast 1
                    var chronicle = Lore.Chronicle.Instance;
                    bool first = chronicle != null && !chronicle.Found("mutation:" + h.id);
                    seeds.Add(h.id, first ? 2 : 1);
                    Lore.Chronicle.Note("mutation:" + h.id);
                    if (first)
                        GameEvents.RaiseToast("toast.mutation.new", Localization.Loc.Get("crop." + h.id), 2);
                    else
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
            // pierwszy start slotu: wyspa 2x2 z losowego ziarna; menu główne: stała wyspa w tle, bez tablicy wprowadzenia
            if (island != null && !island.Ready)
                island.NewWorld(Save.SaveSystem.InMenu ? MenuSeed : NewSeed(), !Save.SaveSystem.InMenu);
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
            if (!Save.SaveSystem.InMenu)
                Seasons.Update(clock);   // pory roku: zdarzenie zmiany dla wyglądu i roślin sezonowych
            float frame = Time.unscaledDeltaTime;
            if (frame > 60f)
            {
                // uśpiony komputer albo długie zawieszenie: rośliny nocy dostają przespaną noc jak offline,
                // reszta nie przelicza godzin w jednej klatce (bez spalania Żaru i nagłego dojrzewania)
                CatchUpNight(frame);
                frame = tickInterval;
            }
            accumulator = Mathf.Min(accumulator + frame, 5f);
            while (accumulator >= tickInterval)
            {
                accumulator -= tickInterval;
                Tick(tickInterval * debugTimeScale);
            }
        }

        /// <summary>Wzrost roślin nocy za przerwę w działaniu gry (jak przy wczytaniu zapisu: noc, świt, zmierzch, pełnia x2).</summary>
        void CatchUpNight(float gapSeconds)
        {
            var now = System.DateTime.Now;
            float night = clock.NightCropSecondsBetween(now.AddSeconds(-gapSeconds), now);
            if (night <= 0f)
                return;
            bool fullMoon = clock.IsFullMoon;
            foreach (var plot in FarmPlot.All)
            {
                var c = plot != null ? plot.State.crop : null;
                if (c == null || c.kind != CropKind.Night || (c.requiresFullMoon && !fullMoon))
                    continue;
                plot.State.growth = Mathf.Min(c.growSeconds, plot.State.growth + night * (fullMoon ? 2f : 1f));
                plot.RefreshVisual();
            }
        }

        void Tick(float dt)
        {
            bool night = clock.IsNight;
            // pełnia: komunikat raz na noc (rośliny nocy x2, krwawa róża rośnie)
            if (night && clock.IsFullMoon && !fullMoonAnnounced)
            {
                fullMoonAnnounced = true;
                GameEvents.RaiseToast("toast.fullmoon");
                Lore.Chronicle.Note("event:fullmoon");
            }
            else if (!night)
                fullMoonAnnounced = false;
            // Żar płynie sam z czasu (§9 p. 134): Rozpałka, napar żaru, Żar Opiekunów
            float overflow = essence.AddSeconds(dt * Alchemy.Potions.Essence * Economy.Prestige.EmberMultiplier
                                                * (UpgradeEffects.EssenceRate + (night ? UpgradeEffects.NightEssence : 0f)));   // Nocny żar
            // Żar w złoto (nadmiar ponad pełny słój) i Dniówka (monety za minutę gry)
            Economy.Market.AddIncome(wallet, overflow * UpgradeEffects.EmberCoins + dt / 60f * UpgradeEffects.WorkWage);
            // świt nowej doby gry: odsetki, dzierżawa, popyt dnia (bez wypłaty przy wczytaniu)
            long cycle = clock.CycleNumber;
            if (lastCycle >= 0 && cycle != lastCycle)
                Economy.Market.OnNewDay(this);
            lastCycle = cycle;

            // Najtańsze lampy dostają Esencję pierwsze - gdy jej braknie, gasną najdroższe.
            lampOrder.Clear();
            lampOrder.AddRange(Lamp.All);
            lampOrder.Sort((a, b) => Cost(a).CompareTo(Cost(b)));
            foreach (var lamp in lampOrder)
                lamp.Tick(dt, night, essence);

            foreach (var plot in FarmPlot.All)
                plot.Tick(dt, ContextFor(plot, night));

            // drzewa owocowe: sadzonki rosną cały czas, owoce wg gatunku (dzień / noc / pełnia)
            for (int i = FruitTree.All.Count - 1; i >= 0; i--)
            {
                var tree = FruitTree.All[i];
                float mult = (island != null ? island.TreeZoneBonus(tree) : 1f) * Familiars.FamiliarSystem.TreeSpeed   // kruk
                           * UpgradeEffects.FruitSpeed;   // Dojrzałe owoce
                tree.Tick(dt, night, clock.IsFullMoon, mult);
            }

            if (automation != null)
            {
                autoHarvested.Clear();
                automation.Tick(dt, this, autoHarvested);
                foreach (var (plot, crop) in autoHarvested)
                    AfterHarvest(plot, crop);
                if (autoHarvested.Count > 0 && UpgradeEffects.WispTip > 0)
                    wallet.Add(UpgradeEffects.WispTip * autoHarvested.Count);   // Napiwki ogników
            }
        }

        /// <summary>Warunki wzrostu dla grządki (światło, noc) - używane też przez HUD.</summary>
        public GrowthContext ContextFor(FarmPlot plot, bool night)
        {
            Vector2 p = plot.LightProbe;
            var crop = plot.State.crop;
            bool twilight = !night && clock.IsTwilight;
            if (twilight && crop != null && crop.kind == CropKind.Night)
                night = true;   // świt i zmierzch liczą się roślinom nocy jak noc (rośliny dnia rosną wtedy jak w dzień)
            // rośliny dnia: rytuał „Głębokie korzenie”, wywar wzrostu, pogoda, ulepszenia
            float dayBonus = crop != null && crop.kind == CropKind.Day
                ? DayGrowthMultiplier * Alchemy.Potions.DayGrowth * Weather.WeatherSystem.DayGrowth * UpgradeEffects.DayGrowth   // deszcz +20%
                  * (Weather.WeatherSystem.Raining ? Familiars.FamiliarSystem.RainGrowth : 1f) : 1f;   // ślimak: w deszczu jeszcze szybciej
            // mikstury z kotła: wywar wzrostu (rośliny dnia), eliksir nocy (rośliny nocy)
            if (crop != null && crop.kind == CropKind.Night)
                dayBonus = Alchemy.Potions.NightGrowth * Familiars.FamiliarSystem.NightGrowth * UpgradeEffects.NightGrowth * Lore.Story.NightGrowth;   // + Światło Opiekunów   // eliksir nocy, ropucha
            if (twilight && TwilightBoost)
                dayBonus *= 1.5f;   // Zmierzchowe ziarno
            return new GrowthContext
            {
                night = night,
                lit = Illumination.IsLit(p, night),
                multiplier = (night ? Illumination.GrowthMultiplier(p) : 1f) * (island != null ? island.ZoneBonus(plot) : 1f) * dayBonus
                           * Economy.Prestige.GrowthMultiplier,   // Zielone ręce (dar mgły)
                fullMoon = clock.IsFullMoon,
                lightBoost = night ? Illumination.GrowthMultiplier(p) : 1f,   // tylko to podnosi jakość
            };
        }

        static float Cost(Lamp lamp) => lamp.Definition != null ? lamp.Definition.essencePerMinute : 0f;
    }
}
