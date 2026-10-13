using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Farming
{
    [CreateAssetMenu(menuName = "Wyspa/Baza roślin", fileName = "CropDatabase")]
    public class CropDatabase : ScriptableObject
    {
        public List<CropDefinition> crops = new List<CropDefinition>();

        public CropDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var c in crops)
                if (c != null && c.id == id)
                    return c;
            return null;
        }
    }
}
