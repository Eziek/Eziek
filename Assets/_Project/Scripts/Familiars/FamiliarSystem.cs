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
    [Serializable]
    public class FamiliarSave
    {
        public List<string> arrived = new List<string>();
        public int nightHarvests;
        public long lastGiftCycle = -1;
    }

    /// <summary>
    /// Chowańce (wybór gracza po Etapie 6d): przychodzą same, gdy wyspa spełni warunek, i zostają na zawsze z małym bonusem.
    /// Kot (10 kupionych kafelków i skrzynia): mutacje x2, raz na cykl przynosi kilka monet. Kruk (dorosłe drzewo owocowe):
    /// drzewa rosną i owocują o 25% szybciej. Ropucha (20 zebranych roślin nocy): rośliny nocy +15%.
    /// Nietoperz (3 lampy): lampy palą o 20% mniej Esencji. Sprawdzane co kilka sekund; w menu głównym nic się nie dzieje.
    /// </summary>
    public class FamiliarSystem : MonoBehaviour
    {
        public const string Cat = "cat", Raven = "raven", Toad = "toad", Bat = "bat";
        public static readonly string[] Kinds = { Cat, Raven, Toad, Bat };

        public static FamiliarSystem Instance { get; private set; }

        readonly HashSet<string> arrived = new HashSet<string>();
        readonly Dictionary<string, Familiar> spawned = new Dictionary<string, Familiar>();
        int nightHarvests;
        long lastGiftCycle = -1;
        float timer = 3f;
        Transform root;

        public static bool Has(string kind) => Instance != null && Instance.arrived.Contains(kind);

        // ---- bonusy dla innych systemów
        public static float MutationMultiplier => Has(Cat) ? 2f : 1f;
        public static float TreeSpeed => Has(Raven) ? 1.25f : 1f;
        public static float NightGrowth => Has(Toad) ? 1.15f : 1f;
        public static float LampBurn => Has(Bat) ? 0.8f : 1f;

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
            if (def != null && def.kind == CropKind.Night)
                nightHarvests += count;
        }

        bool Condition(string kind, Island island)
        {
            switch (kind)
            {
                case Cat: return island.Map.Purchased >= 10 && HarvestCrate.All.Count > 0;
                case Raven: return FruitTree.All.Exists(t => t != null && t.Adult);
                case Toad: return nightHarvests >= 20;
                default: return Lamp.All.Count >= 3;
            }
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
            foreach (var kind in Kinds)
            {
                if (!arrived.Contains(kind) && Condition(kind, island))
                {
                    arrived.Add(kind);
                    var f = Spawn(kind);
                    if (f != null)
                        Visual.IslandFx.FindPopped(f.transform.position + new Vector3(0f, 0.4f, 0f));
                    GameEvents.RaiseToast("toast.familiar.arrived", Loc.Get("familiar." + kind), Loc.Get("familiar." + kind + ".bonus"));
                    Lore.Chronicle.Note("familiar:" + kind);
                }
                else if (arrived.Contains(kind) && !spawned.ContainsKey(kind))
                    Spawn(kind);   // po wczytaniu zapisu
            }
            CatGift(game);
        }

        /// <summary>Kot raz na cykl gry przynosi kilka monet („coś błyszczącego”).</summary>
        void CatGift(GameLoop game)
        {
            if (!arrived.Contains(Cat) || !spawned.TryGetValue(Cat, out var cat) || cat == null)
                return;
            long cycle = game.Clock.CycleNumber;
            if (lastGiftCycle < 0)
                lastGiftCycle = cycle;
            if (cycle <= lastGiftCycle)
                return;
            lastGiftCycle = cycle;
            int coins = UnityEngine.Random.Range(8, 21) + game.Island.Map.Purchased / 2;
            game.Wallet.Add(coins);
            GameEvents.RaiseCurrencyGained(cat.transform.position + new Vector3(0f, 0.4f, 0f), CurrencyType.Coins, coins);
            GameEvents.RaiseToast("toast.familiar.gift", coins);
        }

        Familiar Spawn(string kind)
        {
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (art == null)
                return null;
            Sprite[] frames, glows;
            switch (kind)
            {
                case Cat: frames = art.familiarCat; glows = art.familiarCatGlow; break;
                case Raven: frames = art.familiarRaven; glows = art.familiarRavenGlow; break;
                case Toad: frames = art.familiarToad; glows = null; break;
                default: frames = art.familiarBat; glows = art.familiarBatGlow; break;
            }
            if (frames == null || frames.Length == 0 || frames[0] == null)
                return null;   // grafika jeszcze nie wczytana (Wyspa → 2): chowaniec i tak daje bonus
            if (root == null)
            {
                root = new GameObject("Familiars").transform;
                root.SetParent(transform, false);
            }
            var go = new GameObject("Familiar_" + kind);
            go.transform.SetParent(root, false);
            var body = go.AddComponent<SpriteRenderer>();
            var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
            g.transform.SetParent(go.transform, false);
            if (art.glow != null)
                g.sharedMaterial = art.glow;
            var f = go.AddComponent<Familiar>();
            f.Setup(kind, body, g, frames, glows);
            spawned[kind] = f;
            return f;
        }

        // ---- zapis
        public FamiliarSave Snapshot() => new FamiliarSave { arrived = new List<string>(arrived), nightHarvests = nightHarvests, lastGiftCycle = lastGiftCycle };

        public void Restore(FamiliarSave save)
        {
            arrived.Clear();
            foreach (var f in spawned.Values)
                if (f != null) Destroy(f.gameObject);
            spawned.Clear();
            nightHarvests = 0;
            lastGiftCycle = -1;
            if (save == null)
                return;
            if (save.arrived != null)
                foreach (var k in save.arrived)
                    if (Array.IndexOf(Kinds, k) >= 0) arrived.Add(k);
            nightHarvests = save.nightHarvests;
            lastGiftCycle = save.lastGiftCycle;
        }
    }
}
