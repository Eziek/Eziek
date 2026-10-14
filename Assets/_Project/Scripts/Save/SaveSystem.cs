using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Upgrades;
using Wyspa.Farming;
using Wyspa.World;

namespace Wyspa.Save
{
    [Serializable]
    class SaveData
    {
        public int version = 16;  // v16: przystań we mgle (stan między rejsami), v15: prestiż (perły, dary mgły) i zarobek rejsu, v14: siewca dzień/noc i podmiana, v13: cele wypraw i liczba wypraw, v12: rój ogników (gniazda, runy dziczy), v11: chowańce (v10: kocioł i mikstury, v9: zamówienia, v8: wprowadzenie)
        public long savedAtUtcTicks;
        public int coins;                       // v1 (zostaje dla zgodności, v2 ma currencies)
        public float essence;
        public float essenceCapacity;           // v1 (v2 liczy pojemność z ulepszeń)
        public List<PlotSave> plots = new List<PlotSave>();
        public List<CurrencyAmount> currencies = new List<CurrencyAmount>();
        public List<UpgradeSave> upgrades = new List<UpgradeSave>();
        public long rhythmDayTicks;
        public float rhythmDailySeconds;
        public List<CrateEntry> crate = new List<CrateEntry>();   // do v5: jedyna skrzynia (v6: zawartość przy obiekcie)
        public IslandSave island;               // v3: wyspa z kafelków (ziarno, kafelki, dekoracje, obiekty)
        public List<SeedSave> seeds = new List<SeedSave>();   // v4: worek nasion (hybrydy, kupiec, wyprawy)
        public int fertilizer;                  // v4: nawóz od kupca-widma
        public string merchantNight;            // v4: noc, w której kupiec-widmo już był
        public string monsterNight;             // v5: noc, w której potwór już się wynurzył
        public Lore.ChronicleSave chronicle;    // v5: cykl księżyca, punkty, rytuały, bestiariusz
        public List<string> unlockedSeeds = new List<string>();      // v6: nasiona odblokowane w sklepie
        public List<string> automationGranted = new List<string>();  // v6: darmowa pierwsza sztuka budowli już była
        public int intro;                       // v8: krok wprowadzenia (starsze zapisy: wprowadzenie skończone)
        public string slotName;                 // v8: nazwa zapisu w menu głównym (do 10 znaków)
        public Orders.OrderSave orders;         // v9: zamówienia z tablicy ogłoszeń
        public List<Alchemy.PotionSave> potions = new List<Alchemy.PotionSave>();   // v10: działające mikstury
        public Familiars.FamiliarSave familiars;    // v11: chowańce na wyspie
        public MarketSave market;               // v15: zarobek rejsu i renoma (Economy/Market)
        public PrestigeSave prestige;           // v15: perły mgły, dary, liczba rejsów (Economy/Prestige)
        public bool mist;                       // v16: slot czeka w Przystani we mgle (rejs skończony, nowej wyspy jeszcze nie ma)
        public List<string> mistUnlocks = new List<string>();   // v16: nasiona odblokowane w poprzednim rejsie (Pamięć nasion)
        public double clockOffset;              // v16: przesunięcie cyklu dnia i nocy (nowa wyspa zaczyna się rano, GameClock)
    }

    [Serializable]
    class PlotSave
    {
        public string id;
        public string cropId;
        public float growth;
        public float boost;        // v4: sekundy wzrostu w świetle ognika (jakość)
        public bool fertilized;
    }

    /// <summary>Nagłówek slotu do menu głównego: tylko pola potrzebne na liście (JsonUtility pomija resztę pliku).</summary>
    [Serializable]
    public class SlotInfo
    {
        public string slotName;
        public long savedAtUtcTicks;
        public IslandHeader island;
        public bool mist;   // slot w Przystani we mgle
        [NonSerialized] public int index;
        [NonSerialized] public bool exists;

        [Serializable]
        public class IslandHeader { public int purchased; }

        public DateTime SavedLocal => new DateTime(savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
        public int Tiles => island != null ? island.purchased : 0;
    }

    /// <summary>
    /// Zapis JSON + postęp offline: rośliny NOCY rosną za godziny nocy, które minęły od zamknięcia gry
    /// (lampy offline nie świecą, więc cała wyspa jest w ciemności). Rośliny DNIA offline nie rosną.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [SerializeField] float autosaveSeconds = 60f;

        // ------------------------------------------------------------ sloty

        /// <summary>Ile slotów zapisu ma menu główne.</summary>
        public const int Slots = 3;
        /// <summary>Najdłuższa nazwa zapisu.</summary>
        public const int MaxNameLength = 10;
        const string LegacyFile = "save.json";

        /// <summary>Wybrany slot (0..2) albo -1 = menu główne (gra bez zapisu, wyspa w tle). Przeżywa przeładowanie sceny.</summary>
        public static int CurrentSlot = -1;
        /// <summary>Nazwa bieżącego zapisu (z pliku albo wpisana przy nowej grze).</summary>
        public static string CurrentName = "";
        public static bool InMenu => CurrentSlot < 0;
        /// <summary>Slot w Przystani we mgle (między rejsami) albo -1. Przystań działa jak menu: wyspa w tle bez zapisu.</summary>
        public static int MistSlot = -1;
        public static bool InMist => MistSlot >= 0;

        /// <summary>Każde uruchomienie gry (także Play w edytorze bez przeładowania domeny) zaczyna się od menu głównego.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CurrentSlot = -1;
            CurrentName = "";
            MistSlot = -1;
        }

        public static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot + 1}.json");

        /// <summary>Stan slotów do menu głównego (puste sloty: exists = false). Stary save.json trafia najpierw do slotu 1.</summary>
        public static List<SlotInfo> ListSlots()
        {
            MigrateLegacy();
            var list = new List<SlotInfo>();
            for (int i = 0; i < Slots; i++)
            {
                SlotInfo info = null;
                try
                {
                    string path = SlotPath(i);
                    if (File.Exists(path))
                        info = JsonUtility.FromJson<SlotInfo>(File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Wyspa] Nie udało się odczytać slotu {i + 1}: {e.Message}");
                }
                // plik jest, ale się nie czyta (uszkodzony): slot dalej zajęty, żeby „Nowa gra” go nie nadpisała
                bool fileThere = File.Exists(SlotPath(i));
                if (info == null)
                    info = new SlotInfo();
                info.exists = fileThere;
                info.index = i;
                list.Add(info);
            }
            return list;
        }

        // folder zapisów sprzed nadania nazwy gry (Application.persistentDataPath = LocalLow/<firma>/<produkt>)
        static readonly string[] OldDataDirs = { Path.Combine("DefaultCompany", "My project (5)") };
        static bool oldDirChecked;

        /// <summary>
        /// Po nadaniu nazwy (Misty Packland) Unity zapisuje gdzie indziej. Gdy w nowym folderze nie ma zapisów,
        /// kopiujemy pliki JSON ze starego (stary folder zostaje jako kopia zapasowa). Raz na uruchomienie.
        /// </summary>
        static void MigrateOldDataDir()
        {
            if (oldDirChecked)
                return;
            oldDirChecked = true;
            try
            {
                string dir = Application.persistentDataPath;
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "save*.json").Length > 0)
                    return;
                var localLow = Directory.GetParent(dir)?.Parent;
                if (localLow == null)
                    return;
                foreach (var rel in OldDataDirs)
                {
                    string old = Path.Combine(localLow.FullName, rel);
                    if (old == dir || !Directory.Exists(old) || Directory.GetFiles(old, "save*.json").Length == 0)
                        continue;
                    Directory.CreateDirectory(dir);
                    foreach (var f in Directory.GetFiles(old, "*.json"))
                        File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), false);
                    Debug.Log($"[Wyspa] Zapisy przeniesione z {old}");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się przenieść zapisów ze starego folderu: {e.Message}");
            }
        }

        static void MigrateLegacy()
        {
            MigrateOldDataDir();
            try
            {
                string legacy = Path.Combine(Application.persistentDataPath, LegacyFile);
                if (File.Exists(legacy) && !File.Exists(SlotPath(0)))
                    File.Move(legacy, SlotPath(0));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się przenieść starego zapisu do slotu 1: {e.Message}");
            }
        }

        /// <summary>Gra na slocie (wczytanie albo nowa wyspa, gdy plik nie istnieje): scena ładuje się od nowa.</summary>
        public static void Play(int slot, string newName = null)
        {
            slot = Mathf.Clamp(slot, 0, Slots - 1);
            CurrentName = CleanName(newName, slot);
            var info = ReadSlot(slot);
            if (info != null && info.mist)
            {
                // rejs skończony, ale gracz nie wypłynął jeszcze na nową wyspę: najpierw Przystań we mgle
                if (!string.IsNullOrEmpty(info.slotName))
                    CurrentName = info.slotName;
                CurrentSlot = -1;
                MistSlot = slot;
            }
            else
            {
                MistSlot = -1;
                CurrentSlot = slot;
            }
            ReloadScene();
        }

        static SlotInfo ReadSlot(int slot)
        {
            try
            {
                string path = SlotPath(slot);
                return File.Exists(path) ? JsonUtility.FromJson<SlotInfo>(File.ReadAllText(path)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void DeleteSlot(int slot)
        {
            try
            {
                string path = SlotPath(slot);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się usunąć slotu {slot + 1}: {e.Message}");
            }
        }

        /// <summary>Nazwa zapisu: bez znaków nowej linii, najwyżej 10 znaków; pusta = domyślna „Wyspa N”.</summary>
        public static string CleanName(string name, int slot)
        {
            name = (name ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
            if (name.Length > MaxNameLength)
                name = name.Substring(0, MaxNameLength);
            return name.Length > 0 ? name : Localization.Loc.Get("ui.menu.default_name", slot + 1);
        }

        /// <summary>Powrót do menu głównego: zapis bieżącej gry, potem scena od nowa bez slotu.</summary>
        public void BackToMenu()
        {
            if (owner != null)
                Save(owner);
            owner = null;
            CurrentSlot = -1;
            MistSlot = -1;
            ReloadScene();
        }

        static void ReloadScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        float timer;
        GameLoop owner;

        public string FilePath => SlotPath(Mathf.Max(0, CurrentSlot));

        void Update()
        {
            if (owner == null || InMenu)
                return;
            timer += Time.unscaledDeltaTime;
            if (timer >= autosaveSeconds)
            {
                timer = 0f;
                Save(owner);
            }
        }

        void OnApplicationQuit()
        {
            if (owner != null && !InMenu)
                Save(owner);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && owner != null && !InMenu)
                Save(owner);
        }

        /// <summary>Reset zapisu (Opcje, z potwierdzeniem): plik bieżącego slotu znika, scena ładuje się od nowa z nową wyspą
        /// (ta sama nazwa). Opcje gracza zostają.</summary>
        public void ResetAll()
        {
            if (InMenu)
                return;
            var keep = owner;
            owner = null;   // bez autozapisu w tej klatce
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się usunąć zapisu: {e.Message}");
                owner = keep;   // reset nieudany: autozapis działa dalej
                return;
            }
            ReloadScene();
        }

        public void Save(GameLoop game)
        {
            owner = game;
            if (InMenu)
                return;   // menu główne: wyspa w tle nie jest zapisywana
            var data = new SaveData
            {
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                coins = game.Wallet.Coins,
                essence = game.Essence.Amount,
                essenceCapacity = game.Essence.Capacity,
                currencies = game.Wallet.Snapshot(),
                upgrades = game.Upgrades.Snapshot(),
                rhythmDayTicks = game.Rhythm.SaveDayTicks,
                rhythmDailySeconds = game.Rhythm.SaveDailySeconds,
                unlockedSeeds = game.SnapshotUnlocks(),
                automationGranted = game.Automation != null ? game.Automation.SnapshotGranted() : new List<string>(),
                island = game.Island != null && game.Island.Ready ? game.Island.Snapshot() : null,
                seeds = game.Seeds.Snapshot(),
                fertilizer = game.Fertilizer,
                merchantNight = Sea.GhostMerchant.Instance != null ? Sea.GhostMerchant.Instance.VisitedNight : "",
                monsterNight = Sea.NightEvents.Instance != null ? Sea.NightEvents.Instance.MonsterNight : "",
                chronicle = Lore.Chronicle.Instance != null ? Lore.Chronicle.Instance.Snapshot() : null,
                intro = UI.IntroNotes.Instance != null ? UI.IntroNotes.Instance.Step : UI.IntroNotes.Done,
                slotName = CurrentName,
                orders = Orders.OrderBook.Instance != null ? Orders.OrderBook.Instance.Snapshot() : null,
                potions = Alchemy.Potions.Snapshot(),
                familiars = Familiars.FamiliarSystem.Instance != null ? Familiars.FamiliarSystem.Instance.Snapshot() : null,
                market = Market.Snapshot(),
                prestige = Prestige.Snapshot(),
                clockOffset = GameClock.SaveOffsetSeconds,
            };
            foreach (var plot in FarmPlot.All)
                data.plots.Add(new PlotSave { id = plot.PlotId, cropId = plot.State.cropId, growth = plot.State.growth,
                                              boost = plot.State.boostSeconds, fertilized = plot.State.fertilized });

            Write(data);
        }

        /// <summary>
        /// Rejs we mgłę: perły z zarobku rejsu, potem slot przechodzi w stan „we mgle” (stara wyspa znika) i scena ładuje się
        /// od nowa jako Przystań we mgle (UI/MistHarborView). Zostają perły, dary, kronika (bestiariusz, historia, rytuały),
        /// nazwa wyspy i lista odblokowanych nasion (dla Pamięci nasion). False = za mało pereł.
        /// </summary>
        public bool SailIntoMist(GameLoop game)
        {
            if (InMenu || game == null || Prestige.CollectVoyage() <= 0)
                return false;
            var data = new SaveData
            {
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                chronicle = Lore.Chronicle.Instance != null ? Lore.Chronicle.Instance.Snapshot() : null,
                intro = UI.IntroNotes.Done,
                slotName = CurrentName,
                prestige = Prestige.Snapshot(),
                mist = true,
                mistUnlocks = game.SnapshotUnlocks(),
            };
            owner = null;   // bez autozapisu starej wyspy w tej klatce
            WriteTo(FilePath, data);
            MistSlot = CurrentSlot;
            CurrentSlot = -1;
            ReloadScene();
            return true;
        }

        static SaveData ReadMist()
        {
            if (!InMist)
                return null;
            try
            {
                string path = SlotPath(MistSlot);
                var data = File.Exists(path) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(path)) : null;
                return data != null && data.mist ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się odczytać zapisu we mgle: {e.Message}");
                return null;
            }
        }

        /// <summary>Przystań: perły i dary z zapisu slotu (wołane przy otwarciu ekranu). False = slot nie jest we mgle.</summary>
        public static bool LoadMist()
        {
            var data = ReadMist();
            if (data == null)
                return false;
            Prestige.Restore(data.prestige);
            if (!string.IsNullOrEmpty(data.slotName))
                CurrentName = data.slotName;
            return true;
        }

        /// <summary>Po każdym losowaniu w Przystani: perły i dary od razu do pliku (zamknięcie gry nic nie cofa).</summary>
        public static void SaveMist()
        {
            var data = ReadMist();
            if (data == null)
                return;
            data.prestige = Prestige.Snapshot();
            data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
            WriteTo(SlotPath(MistSlot), data);
        }

        /// <summary>
        /// Wypłynięcie z Przystani: nowa wyspa w tym samym slocie z darami (monety i surowce na start, ognik-ogrodnik,
        /// odblokowane nasiona, nawóz, pełny słój Żaru). Scena ładuje się od nowa już z grą.
        /// </summary>
        public static void LeaveMist()
        {
            var old = ReadMist();
            if (old == null)
                return;
            var upgrades = new List<UpgradeSave>();
            if (Prestige.Level(Prestige.FaithfulWisp) > 0)
                upgrades.Add(new UpgradeSave { id = UpgradeIds.WispGardener, level = 1 });
            var data = new SaveData
            {
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                currencies = Prestige.StartCurrencies(),
                upgrades = upgrades,
                unlockedSeeds = Prestige.Level(Prestige.SeedMemory) > 0 && old.mistUnlocks != null ? old.mistUnlocks : new List<string>(),
                chronicle = old.chronicle,
                intro = UI.IntroNotes.Done,
                slotName = old.slotName,
                market = new MarketSave(),
                prestige = Prestige.Snapshot(),
                fertilizer = Mathf.RoundToInt(Prestige.Value(Prestige.MistCompost)),
                essence = Prestige.Level(Prestige.EmberReserve) > 0 ? 1e6f : 0f,   // przycięte do pojemności słoja
                clockOffset = GameClock.MorningOffset(),   // nowa wyspa zaczyna się rano
            };
            data.coins = Prestige.StartCurrencies()[0].amount;
            int slot = MistSlot;
            WriteTo(SlotPath(slot), data);
            MistSlot = -1;
            CurrentSlot = slot;
            CurrentName = CleanName(old.slotName, slot);
            ReloadScene();
        }

        /// <summary>Z Przystani do menu głównego (slot zostaje we mgle: „Graj” wraca do Przystani).</summary>
        public static void MistToMenu()
        {
            MistSlot = -1;
            CurrentSlot = -1;
            ReloadScene();
        }

        void Write(SaveData data) => WriteTo(FilePath, data);

        static void WriteTo(string path, SaveData data)
        {
            try
            {
                // najpierw plik tymczasowy, potem podmiana: przerwany zapis (ubity proces) nie zostawi uciętego JSON-a
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (!File.Exists(path))
                    File.Move(tmp, path);
                else
                {
                    try { File.Replace(tmp, path, null); }
                    catch (IOException) { File.Copy(tmp, path, true); File.Delete(tmp); }   // dysk bez obsługi podmiany
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się zapisać gry: {e.Message}");
            }
        }

        /// <summary>Nieczytelny zapis: kopia obok (save_N.corrupt.json), zanim autozapis go nadpisze.</summary>
        void BackupCorrupt()
        {
            try
            {
                File.Copy(FilePath, Path.ChangeExtension(FilePath, ".corrupt.json"), true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się skopiować uszkodzonego zapisu: {e.Message}");
            }
        }

        public void Load(GameLoop game)
        {
            owner = game;
            if (InMenu)
                return;
            MigrateLegacy();
            if (!File.Exists(FilePath))
            {
                GameClock.SaveOffsetSeconds = GameClock.MorningOffset();   // nowa gra albo reset: zawsze od rana
                return;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Uszkodzony zapis, start od zera (kopia: *.corrupt.json): {e.Message}");
                BackupCorrupt();
                return;
            }
            if (data == null)
            {
                BackupCorrupt();
                return;
            }
            if (!string.IsNullOrEmpty(data.slotName))
                CurrentName = data.slotName;
            GameClock.SaveOffsetSeconds = data.clockOffset;
            if (data.mist)
            {
                // slot we mgle (np. wczytany z pominięciem menu): najpierw Przystań
                owner = null;
                MistSlot = CurrentSlot;
                CurrentSlot = -1;
                ReloadScene();
                return;
            }

            // najpierw wyspa: grządki (do wczytania niżej) i miejsca na obiekty powstają razem z kafelkami.
            // Bardzo stary zapis bez wyspy dostaje nowy świat (waluty i ulepszenia zostają).
            if (game.Island != null)
            {
                if (data.island != null && data.island.tiles != null && data.island.tiles.Count > 0)
                    game.Island.Restore(data.island);
                else
                    game.Island.NewWorld(GameLoop.NewSeed());
            }

            game.RestoreUnlocks(data.unlockedSeeds);
            if (game.Automation != null)
                game.Automation.RestoreGranted(data.automationGranted);   // przed ApplyUpgrades: darmowa sztuka tylko raz
            if (data.version >= 2 && data.currencies != null && data.currencies.Count > 0)
                game.Wallet.Restore(data.currencies);
            else
                game.Wallet.Set(data.coins);
            Market.Restore(data.market);
            Prestige.Restore(data.prestige);
            game.Upgrades.Restore(data.upgrades);
            game.ApplyUpgrades();                // pojemność słoja przed ustawieniem Esencji
            game.Essence.Set(data.essence);
            game.Rhythm.Restore(data.rhythmDayTicks, data.rhythmDailySeconds, DateTime.Now);
            if (data.version < 6 && data.crate != null && data.crate.Count > 0 && HarvestCrate.All.Count > 0)
                HarvestCrate.All[0].Restore(data.crate);   // zapis sprzed 6b: zawartość jedynej skrzyni
            game.Seeds.Restore(data.seeds);
            game.Fertilizer = data.fertilizer;
            if (Sea.GhostMerchant.Instance != null)
                Sea.GhostMerchant.Instance.VisitedNight = data.merchantNight;
            if (Sea.NightEvents.Instance != null)
                Sea.NightEvents.Instance.MonsterNight = data.monsterNight;
            if (Lore.Chronicle.Instance != null)
                Lore.Chronicle.Instance.Restore(data.chronicle);
            if (Orders.OrderBook.Instance != null)
                Orders.OrderBook.Instance.Restore(data.orders);
            Alchemy.Potions.Restore(data.potions);
            if (Familiars.FamiliarSystem.Instance != null)
                Familiars.FamiliarSystem.Instance.Restore(data.familiars);
            if (UI.IntroNotes.Instance != null)
                UI.IntroNotes.Instance.Step = data.version >= 8 ? Mathf.Clamp(data.intro, 0, UI.IntroNotes.Done) : UI.IntroNotes.Done;

            var savedAt = new DateTime(data.savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
            float offlineNight = game.Clock.NightSecondsBetween(savedAt, DateTime.Now);

            var byId = new Dictionary<string, PlotSave>();
            foreach (var p in data.plots)
                if (!string.IsNullOrEmpty(p.id))
                    byId[p.id] = p;

            foreach (var plot in FarmPlot.All)
            {
                if (!byId.TryGetValue(plot.PlotId, out var saved))
                    continue;
                var def = game.Crops != null ? game.Crops.Find(saved.cropId) : null;
                if (def == null)
                {
                    plot.State.Clear();
                    continue;
                }
                plot.State.Plant(def);
                plot.State.growth = saved.growth;
                plot.State.boostSeconds = saved.boost;
                plot.State.fertilized = saved.fertilized;
                if (def.kind == CropKind.Night)
                {
                    // pełnia (przybliżenie: stan teraz): rośliny nocy x2, krwawa róża rośnie tylko wtedy
                    bool fullMoon = game.Clock.IsFullMoon;
                    float gained = def.requiresFullMoon && !fullMoon ? 0f : offlineNight * (fullMoon ? 2f : 1f);
                    plot.State.growth = Mathf.Min(def.growSeconds, plot.State.growth + gained);
                }
            }

            if (offlineNight > 0f)
                Debug.Log($"[Wyspa] Postęp offline: {offlineNight / 3600f:0.0} h nocy.");
        }
    }
}
