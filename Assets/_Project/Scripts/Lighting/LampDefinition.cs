using UnityEngine;

namespace Wyspa.Lighting
{
    [CreateAssetMenu(menuName = "Wyspa/Lampa", fileName = "Lamp_")]
    public class LampDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public int price = 30;

        [Tooltip("Ile Esencji spala minuta świecenia (0 = darmowa).")]
        public float essencePerMinute = 0.1f;

        [Tooltip("Zasięg światła w jednostkach świata (16 px = 1).")]
        public float radius = 2f;

        public Color color = new Color(1f, 0.7f, 0.4f);
        public float intensity = 1f;

        [Tooltip("Mnożnik wzrostu roślin w zasięgu (np. klatka z ognikiem > 1).")]
        public float growthMultiplier = 1f;

        [Tooltip("Lekkie migotanie (płomienie).")]
        public bool flicker = true;
    }
}
