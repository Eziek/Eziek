using System;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>Esencja (Żar): płynie sama z czasu, gdy gra jest włączona (§9 p. 134), plus piec i kryształy; lampy w nocy ją spalają.</summary>
    public class EssenceStore : MonoBehaviour
    {
        [Tooltip("Pojemność słoja bez ulepszeń (nadmiar przepada).")]
        [SerializeField] float capacity = 60f;

        [Tooltip("Ile Esencji daje minuta aktywności.")]
        [UnityEngine.Serialization.FormerlySerializedAs("perActiveMinute")]
        [SerializeField] float perMinute = 1f;

        float amount;
        float currentCapacity = -1f;

        public event Action Changed;

        public float Amount => amount;
        public float BaseCapacity => capacity;
        /// <summary>Żar na minutę bez ulepszeń (Rozpałka, dary i mikstury mnożą go w GameLoop).</summary>
        public float PerMinute => perMinute;
        public float Capacity => currentCapacity > 0f ? currentCapacity : capacity;
        public float Normalized => Capacity > 0f ? amount / Capacity : 0f;

        /// <summary>Żar za sekundy czasu gry. Zwraca nadmiar, który nie zmieścił się w słoju („Żar w złoto”).</summary>
        public float AddSeconds(float seconds)
        {
            float add = seconds / 60f * perMinute;
            float overflow = Mathf.Max(0f, amount + add - Capacity);
            Set(amount + add);
            return overflow;
        }

        public bool TryConsume(float value)
        {
            if (value <= 0f)
                return true;
            if (amount < value)
                return false;
            Set(amount - value);
            return true;
        }

        public void SetCapacity(float value)
        {
            currentCapacity = Mathf.Max(1f, value);
            Set(amount);
        }

        public void Set(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, Capacity);
            if (Mathf.Approximately(clamped, amount))
                return;
            amount = clamped;
            Changed?.Invoke();
        }
    }
}
