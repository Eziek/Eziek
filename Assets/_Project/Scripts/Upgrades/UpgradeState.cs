using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Economy;

namespace Wyspa.Upgrades
{
    [Serializable]
    public struct UpgradeSave
    {
        public string id;
        public int level;
    }

    /// <summary>Posiadane poziomy ulepszeń. Systemy pytają o Level/Value, a zakup idzie przez TryBuy.</summary>
    public class UpgradeState : MonoBehaviour
    {
        [SerializeField] UpgradeDatabase database;

        readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public event Action Changed;

        public UpgradeDatabase Database => database;

        public int Level(string id) => id != null && levels.TryGetValue(id, out var l) ? l : 0;

        public bool Has(string id) => Level(id) > 0;

        /// <summary>Wartość efektu na obecnym poziomie albo fallback, gdy ulepszenia nie ma.</summary>
        public float Value(string id, float fallback)
        {
            var def = database != null ? database.Find(id) : null;
            int level = Level(id);
            if (def == null || level <= 0 || def.levels.Count == 0)
                return fallback;
            return def.levels[Mathf.Min(level, def.levels.Count) - 1].value;
        }

        public UpgradeLevel NextLevel(UpgradeDefinition def)
        {
            if (def == null)
                return null;
            int level = Level(def.id);
            return level < def.levels.Count ? def.levels[level] : null;
        }

        /// <summary>Koszt poziomu po zniżce z darów mgły („Stare znajomości”: mniej monet).</summary>
        public List<CurrencyAmount> CostOf(UpgradeLevel level)
        {
            var list = new List<CurrencyAmount>();
            if (level == null)
                return list;
            float m = Prestige.UpgradeCostMultiplier;
            foreach (var c in level.cost)
                list.Add(c.type == CurrencyType.Coins && m < 1f ? new CurrencyAmount(c.type, Mathf.Max(1, Mathf.RoundToInt(c.amount * m))) : c);
            return list;
        }

        public bool RequirementsMet(UpgradeDefinition def)
        {
            if (def == null)
                return false;
            foreach (var r in def.requires)
                if (r != null && r.upgrade != null && Level(r.upgrade.id) < r.minLevel)
                    return false;
            return true;
        }

        public bool CanBuy(UpgradeDefinition def, Wallet wallet)
        {
            var next = NextLevel(def);
            return next != null && RequirementsMet(def) && wallet.CanAfford(CostOf(next));
        }

        public bool TryBuy(UpgradeDefinition def, Wallet wallet)
        {
            if (!CanBuy(def, wallet))
                return false;
            var next = NextLevel(def);
            if (!wallet.TrySpend(CostOf(next)))
                return false;
            levels[def.id] = Level(def.id) + 1;
            Changed?.Invoke();
            return true;
        }

        public List<UpgradeSave> Snapshot()
        {
            var list = new List<UpgradeSave>();
            foreach (var kv in levels)
                list.Add(new UpgradeSave { id = kv.Key, level = kv.Value });
            return list;
        }

        public void Restore(IEnumerable<UpgradeSave> list)
        {
            levels.Clear();
            if (list != null)
                foreach (var u in list)
                    if (!string.IsNullOrEmpty(u.id) && u.level > 0)
                        levels[u.id] = u.level;
            Changed?.Invoke();
        }
    }
}
