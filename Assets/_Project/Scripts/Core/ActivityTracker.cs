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

        [Tooltip("Do testów: gracz zawsze nieaktywny (symulacja przerwy).")]
        [SerializeField] bool forceIdle;

        public bool IsActive { get; private set; }
        public float SecondsSinceInput { get; private set; }

        /// <summary>Próg bezczynności (opcje gracza, PlayerPrefs „idleSeconds”).</summary>
        public float IdleThresholdSeconds
        {
            get => idleThresholdSeconds;
            set { idleThresholdSeconds = Mathf.Clamp(value, 15f, 900f); PlayerPrefs.SetFloat("idleSeconds", idleThresholdSeconds); }
        }

        void Awake() => idleThresholdSeconds = PlayerPrefs.GetFloat("idleSeconds", idleThresholdSeconds);

        public bool ForceActive
        {
            get => forceActive;
            set { forceActive = value; if (value) forceIdle = false; }
        }

        public bool ForceIdle
        {
            get => forceIdle;
            set { forceIdle = value; if (value) forceActive = false; }
        }

        /// <summary>Wywoływane co tick przez GameLoop.</summary>
        public void Refresh()
        {
            if (forceIdle)
            {
                SecondsSinceInput = idleThresholdSeconds;
                IsActive = false;
                return;
            }

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
