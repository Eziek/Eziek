using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>
    /// Gra wisi godzinami na pulpicie - ma prawie nic nie kosztować.
    /// Animacje i tak są skokowe (~10 kl./s), więc w tle wystarczy mało klatek.
    /// </summary>
    public class AppPerformance : MonoBehaviour
    {
        [SerializeField] int focusedFps = 30;
        [SerializeField] int backgroundFps = 12;

        void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Apply(Application.isFocused);
            Application.focusChanged += Apply;
        }

        void OnDestroy() => Application.focusChanged -= Apply;

        void Apply(bool focused) => Application.targetFrameRate = focused ? focusedFps : backgroundFps;
    }
}
