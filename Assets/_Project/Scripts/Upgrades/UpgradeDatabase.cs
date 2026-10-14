using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Upgrades
{
    [CreateAssetMenu(menuName = "Wyspa/Baza ulepszeń", fileName = "UpgradeDatabase")]
    public class UpgradeDatabase : ScriptableObject
    {
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

        public UpgradeDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var u in upgrades)
                if (u != null && u.id == id)
                    return u;
            return null;
        }
    }
}
