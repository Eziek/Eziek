using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Economy;

namespace Wyspa.Upgrades
{
    public enum UpgradeCategory
    {
        Essence,
        Automation,
        Rhythm,
        Island,
        Lamps,
        Sea,
        Rituals,
        Trade,      // handel i dochody (Economy/Market)
    }

    [Serializable]
    public class UpgradeLevel
    {
        public List<CurrencyAmount> cost = new List<CurrencyAmount>();

        [Tooltip("Wartość efektu na tym poziomie (np. pojemność słoja, odstęp zbioru w sekundach).")]
        public float value;
    }

    [Serializable]
    public class UpgradeRequirement
    {
        public UpgradeDefinition upgrade;
        public int minLevel = 1;
    }

    /// <summary>Ulepszenie z GDD §11. Teksty tylko jako klucze lokalizacji (GDD §16).</summary>
    [CreateAssetMenu(menuName = "Wyspa/Ulepszenie", fileName = "Upgrade_")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string id;
        public string nameKey;
        public string descriptionKey;

        [Tooltip("Tylko dla HUD-a debugowego, nie trafia do gry.")]
        public string debugName;

        public UpgradeCategory category;
        public Sprite icon;
        public List<UpgradeLevel> levels = new List<UpgradeLevel>();
        public List<UpgradeRequirement> requires = new List<UpgradeRequirement>();

        public int MaxLevel => levels.Count;
    }
}
