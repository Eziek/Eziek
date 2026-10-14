using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>
    /// Gra wisi godzinami na pulpicie - ma mało kosztować, ale animacje mają być płynne.
    /// Opcje → Płynność: 0 oszczędna (30 / 12 kl./s), 1 płynna (60 / 30, domyślnie), 2 bardzo płynna (60 / 60).
    /// Pierwsza liczba: okno z fokusem, druga: w tle (gracz pracuje w innym programie).
    /// </summary>
    public class AppPerformance : MonoBehaviour
    {
        static readonly (int focused, int background)[] Profiles = { (30, 12), (60, 30), (60, 60) };

        public static AppPerformance Instance { get; private set; }

        int profile = 1;

        public int Profile
        {
            get => profile;
            set
            {
                profile = Mathf.Clamp(value, 0, Profiles.Length - 1);
                PlayerPrefs.SetInt("smoothness", profile);
                Apply(Application.isFocused);
            }
        }

        void Awake()
        {
            Instance = this;
            profile = Mathf.Clamp(PlayerPrefs.GetInt("smoothness", 1), 0, Profiles.Length - 1);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Apply(Application.isFocused);
            Application.focusChanged += Apply;
        }

        void OnDestroy()
        {
            Application.focusChanged -= Apply;
            if (Instance == this)
                Instance = null;
        }

        void Apply(bool focused) => Application.targetFrameRate = focused ? Profiles[profile].focused : Profiles[profile].background;
    }
}
