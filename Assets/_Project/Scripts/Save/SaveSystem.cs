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
        public int version = 5;
        public long savedAtUtcTicks;
        public int coins;                       // v1 (zostaje dla zgodności, v2 ma currencies)
        public float essence;
        public float essenceCapacity;           // v1 (v2 liczy pojemność z ulepszeń)
        public List<PlotSave> plots = new List<PlotSave>();
        public List<CurrencyAmount> currencies = new List<CurrencyAmount>();
        public List<UpgradeSave> upgrades = new List<UpgradeSave>();
        public long rhythmDayTicks;
        public float rhythmDailySeconds;
        public List<CrateEntry> crate = new List<CrateEntry>();
        public IslandSave island;               // v3: wyspa z kafelków (ziarno, kafelki, dekoracje, obiekty)
        public List<SeedSave> seeds = new List<SeedSave>();   // v4: worek nasion (hybrydy, kupiec, wyprawy)
        public int fertilizer;                  // v4: nawóz od kupca-widma
        public string merchantNight;            // v4: noc, w której kupiec-widmo już był
        public string monsterNight;             // v5: noc, w której potwór już się wynurzył
        public Lore.ChronicleSave chronicle;    // v5: cykl księżyca, punkty, rytuały, bestiariusz
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

    /// <summary>
    /// Zapis JSON + postęp offline: rośliny NOCY rosną za godziny nocy, które minęły od zamknięcia gry
    /// (lampy offline nie świecą, więc cała wyspa jest w ciemności). Rośliny DNIA offline nie rosną.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [SerializeField] float autosaveSeconds = 60f;
        [SerializeField] string fileName = "save.json";

        float timer;
        GameLoop owner;

        public string FilePath => Path.Combine(Application.persistentDataPath, fileName);

        void Update()
        {
            if (owner == null)
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
            if (owner != null)
                Save(owner);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && owner != null)
                Save(owner);
        }

        public void Save(GameLoop game)
        {
            owner = game;
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
                crate = game.Automation != null && game.Automation.Crate != null
                    ? game.Automation.Crate.Snapshot() : new List<CrateEntry>(),
                island = game.Island != null && game.Island.Ready ? game.Island.Snapshot() : null,
                seeds = game.Seeds.Snapshot(),
                fertilizer = game.Fertilizer,
                merchantNight = Sea.GhostMerchant.Instance != null ? Sea.GhostMerchant.Instance.VisitedNight : "",
                monsterNight = Sea.NightEvents.Instance != null ? Sea.NightEvents.Instance.MonsterNight : "",
                chronicle = Lore.Chronicle.Instance != null ? Lore.Chronicle.Instance.Snapshot() : null,
            };
            foreach (var plot in FarmPlot.All)
                data.plots.Add(new PlotSave { id = plot.PlotId, cropId = plot.State.cropId, growth = plot.State.growth,
                                              boost = plot.State.boostSeconds, fertilized = plot.State.fertilized });

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Nie udało się zapisać gry: {e.Message}");
            }
        }

        public void Load(GameLoop game)
        {
            owner = game;
            if (!File.Exists(FilePath))
                return;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wyspa] Uszkodzony zapis, start od zera: {e.Message}");
                return;
            }
            if (data == null)
                return;

            // najpierw wyspa: grządki (do wczytania niżej) i miejsca na obiekty powstają razem z kafelkami.
            // Zapis sprzed Etapu 3b nie ma wyspy - dostaje nowy świat (waluty i ulepszenia zostają).
            if (game.Island != null)
            {
                if (data.island != null && data.island.tiles != null && data.island.tiles.Count > 0)
                    game.Island.Restore(data.island);
                else
                    game.Island.NewWorld(GameLoop.NewSeed());
            }

            if (data.version >= 2 && data.currencies != null && data.currencies.Count > 0)
                game.Wallet.Restore(data.currencies);
            else
                game.Wallet.Set(data.coins);
            game.Upgrades.Restore(data.upgrades);
            game.ApplyUpgrades();                // pojemność słoja przed ustawieniem Esencji
            game.Essence.Set(data.essence);
            game.Rhythm.Restore(data.rhythmDayTicks, data.rhythmDailySeconds, DateTime.Now);
            if (game.Automation != null && game.Automation.Crate != null)
                game.Automation.Crate.Restore(data.crate);
            game.Seeds.Restore(data.seeds);
            game.Fertilizer = data.fertilizer;
            if (Sea.GhostMerchant.Instance != null)
                Sea.GhostMerchant.Instance.VisitedNight = data.merchantNight;
            if (Sea.NightEvents.Instance != null)
                Sea.NightEvents.Instance.MonsterNight = data.monsterNight;
            if (Lore.Chronicle.Instance != null)
                Lore.Chronicle.Instance.Restore(data.chronicle);

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
