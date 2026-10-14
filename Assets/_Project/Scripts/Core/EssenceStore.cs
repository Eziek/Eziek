using System;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>Esencja: energia z pracy. Aktywność ją ładuje, lampy w nocy ją spalają.</summary>
    public class EssenceStore : MonoBehaviour
    {
        [Tooltip("Pojemność słoja bez ulepszeń (nadmiar przepada).")]
        [SerializeField] float capacity = 60f;

        [Tooltip("Ile Esencji daje minuta aktywności.")]
        [SerializeField] float perActiveMinute = 1f;

        float amount;
        float currentCapacity = -1f;

        public event Action Changed;

        public float Amount => amount;
        public float BaseCapacity => capacity;
        public float Capacity => currentCapacity > 0f ? currentCapacity : capacity;
        public float Normalized => Capacity > 0f ? amount / Capacity : 0f;

        /// <summary>Żar za sekundy pracy. Zwraca nadmiar, który nie zmieścił się w słoju („Żar w złoto”).</summary>
        public float AddActiveSeconds(float seconds)
        {
            float add = seconds / 60f * perActiveMinute;
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
