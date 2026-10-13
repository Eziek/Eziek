using System;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>Esencja: energia z pracy. Aktywność ją ładuje, lampy w nocy ją spalają.</summary>
    public class EssenceStore : MonoBehaviour
    {
        [Tooltip("Pojemność słoja (nadmiar przepada).")]
        [SerializeField] float capacity = 60f;

        [Tooltip("Ile Esencji daje minuta aktywności.")]
        [SerializeField] float perActiveMinute = 1f;

        float amount;

        public event Action Changed;

        public float Amount => amount;
        public float Capacity => capacity;
        public float Normalized => capacity > 0f ? amount / capacity : 0f;

        public void AddActiveSeconds(float seconds) => Set(amount + seconds / 60f * perActiveMinute);

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
            capacity = Mathf.Max(1f, value);
            Set(amount);
        }

        public void Set(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, capacity);
            if (Mathf.Approximately(clamped, amount))
                return;
            amount = clamped;
            Changed?.Invoke();
        }
    }
}
