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

        // ---- tempo pracy: pisanie i ruch myszy przyspieszają rośliny dnia
        const float SampleSeconds = 0.2f;
        const int Samples = 75;                     // okno 15 s
        const float KeyboardWeight = 1f, MouseWeight = 0.35f;
        /// <summary>Średnia waga próbek, przy której tempo = 1 (ciągłe pisanie trafia w ok. połowę próbek).</summary>
        const float FullTempo = 0.5f;
        /// <summary>Największy mnożnik wzrostu roślin dnia przy pełnym tempie.</summary>
        public const float MaxTempoBonus = 0.5f;

        readonly float[] samples = new float[Samples];
        int sampleIndex;
        float sampleSum;
        float sampleTimer;
        uint lastStamp;
        int lastCursorX, lastCursorY;

        /// <summary>Kiedy (Time.unscaledTime) ostatnio wykryto pisanie / ruch myszy - wskaźnik w pasku HUD.</summary>
        public float LastKeyboardTime { get; private set; } = -10f;
        public float LastMouseTime { get; private set; } = -10f;

        /// <summary>Do testów (HUD debugowy): stałe tempo 0..1, ujemne = mierzone naprawdę.</summary>
        public float DebugTempo { get; set; } = -1f;

        /// <summary>
        /// Tempo pracy 0..1 z ostatnich 15 s: jak często był input (pisanie liczy się w pełni, sam ruch myszy w ok. 1/3).
        /// Bez globalnych hooków: co 0.2 s porównujemy znacznik ostatniego inputu i pozycję kursora (GDD §4.1).
        /// </summary>
        public float Tempo => DebugTempo >= 0f ? Mathf.Clamp01(DebugTempo) : Mathf.Clamp01(sampleSum / Samples / FullTempo);

        /// <summary>Mnożnik wzrostu roślin dnia: 1 + 0.5 x tempo (tylko gdy gracz jest aktywny).</summary>
        public float TempoMultiplier => IsActive ? 1f + MaxTempoBonus * Tempo : 1f;

        void Update()
        {
            if (!SystemIdle.IsSupported)
                return;
            sampleTimer += Time.unscaledDeltaTime;
            while (sampleTimer >= SampleSeconds)
            {
                sampleTimer -= SampleSeconds;
                Sample();
            }
        }

        void Sample()
        {
            uint stamp = SystemIdle.LastInputStamp();
            SystemIdle.CursorPosition(out int cx, out int cy);
            float w = 0f;
            if (stamp != lastStamp && lastStamp != 0u)
            {
                bool mouse = cx != lastCursorX || cy != lastCursorY;   // input bez ruchu kursora = klawiatura
                w = mouse ? MouseWeight : KeyboardWeight;
                if (mouse) LastMouseTime = Time.unscaledTime;
                else LastKeyboardTime = Time.unscaledTime;
            }
            lastStamp = stamp;
            lastCursorX = cx;
            lastCursorY = cy;
            sampleSum += w - samples[sampleIndex];
            samples[sampleIndex] = w;
            sampleIndex = (sampleIndex + 1) % Samples;
        }

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
