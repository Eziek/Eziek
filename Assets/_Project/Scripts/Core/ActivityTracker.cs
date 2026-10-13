using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>Czy gracz pracuje przy komputerze (ostatni input w systemie krócej niż próg).</summary>
    public class ActivityTracker : MonoBehaviour
    {
        [Tooltip("Po ilu sekundach bez inputu gracz przestaje być aktywny.")]
        [SerializeField] float idleThresholdSeconds = 60f;

        [Tooltip("Do testów: gracz zawsze aktywny.")]
        [SerializeField] bool forceActive;

        public bool IsActive { get; private set; }
        public float SecondsSinceInput { get; private set; }

        public bool ForceActive
        {
            get => forceActive;
            set => forceActive = value;
        }

        /// <summary>Wywoływane co tick przez GameLoop.</summary>
        public void Refresh()
        {
            if (forceActive)
            {
                SecondsSinceInput = 0f;
                IsActive = true;
                return;
            }

            if (SystemIdle.IsSupported)
            {
                SecondsSinceInput = SystemIdle.SecondsSinceLastInput();
                IsActive = SecondsSinceInput < idleThresholdSeconds;
            }
            else
            {
                // Poza Windows na razie przybliżenie: aktywny, gdy okno gry ma fokus.
                SecondsSinceInput = 0f;
                IsActive = Application.isFocused;
            }
        }
    }
}
