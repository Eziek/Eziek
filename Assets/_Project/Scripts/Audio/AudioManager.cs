using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;

namespace Wyspa.Audio
{
    /// <summary>
    /// Dźwięk (Etap 6c): tło dnia i nocy z płynnym przenikaniem, deszcz ponad tłem, efekty z puli źródeł.
    /// Klipy z Resources/Audio (placeholdery z tools/audio/gen_audio.py). Systemy gry wołają GameEvents.PlaySound("harvest").
    /// Głośność tła i efektów w Opcjach (PlayerPrefs), opcjonalna cisza, gdy okno nie jest aktywne.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int SfxVoices = 6;
        const float Crossfade = 2.5f;

        public static AudioManager Instance { get; private set; }

        // ---- opcje gracza
        public static float AmbientVolume
        {
            get => PlayerPrefs.GetFloat("volAmbient", 0.5f);
            set => PlayerPrefs.SetFloat("volAmbient", Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("volSfx", 0.7f);
            set => PlayerPrefs.SetFloat("volSfx", Mathf.Clamp01(value));
        }

        public static bool MuteInBackground
        {
            get => PlayerPrefs.GetInt("muteBg", 0) == 1;
            set => PlayerPrefs.SetInt("muteBg", value ? 1 : 0);
        }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource ambA, ambB, rain;
        bool aActive = true;
        string currentAmbient;
        float ambA_level, ambB_level, rainLevel;
        int nextVoice;

        void Awake()
        {
            Instance = this;
            ambA = NewSource("AmbientA", true);
            ambB = NewSource("AmbientB", true);
            rain = NewSource("Rain", true);
            for (int i = 0; i < SfxVoices; i++)
                voices.Add(NewSource("Sfx" + i, false));
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }
        }

        void OnEnable() => GameEvents.Sound += Play;
        void OnDisable() => GameEvents.Sound -= Play;
        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        AudioClip Clip(string id)
        {
            if (!clips.TryGetValue(id, out var c))
            {
                c = Resources.Load<AudioClip>("Audio/" + id);
                clips[id] = c;   // brak pliku = null, bez ponownego szukania
            }
            return c;
        }

        /// <summary>Efekt (np. „harvest”, „coin”): ten sam dźwięk najwyżej raz na 0.06 s (monety lecą seriami).</summary>
        void Play(string id, float volume)
        {
            var clip = Clip("sfx_" + id);
            if (clip == null)
                return;
            if (lastPlayed.TryGetValue(id, out float last) && Time.unscaledTime - last < 0.06f)
                return;
            lastPlayed[id] = Time.unscaledTime;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            v.pitch = Random.Range(0.95f, 1.05f);   // drobna zmiana, żeby powtórki nie brzmiały identycznie
            v.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxVolume);
        }

        void Update()
        {
            var game = GameLoop.Instance;
            bool night = game != null && game.Clock.IsNight;
            string want = night ? "amb_night" : "amb_day";
            if (want != currentAmbient)
            {
                currentAmbient = want;
                aActive = !aActive;
                var src = aActive ? ambA : ambB;
                src.clip = Clip(want);
                if (src.clip != null)
                    src.Play();
            }
            float dt = Time.unscaledDeltaTime / Crossfade;
            ambA_level = Mathf.MoveTowards(ambA_level, aActive ? 1f : 0f, dt);
            ambB_level = Mathf.MoveTowards(ambB_level, aActive ? 0f : 1f, dt);
            float amb = AmbientVolume;
            ambA.volume = ambA_level * amb;
            ambB.volume = ambB_level * amb;

            // deszcz ponad tłem (burza głośniej)
            float rainTarget = Weather.WeatherSystem.Current == Weather.WeatherKind.Storm ? 1f
                             : Weather.WeatherSystem.Current == Weather.WeatherKind.Rain ? 0.65f : 0f;
            rainLevel = Mathf.MoveTowards(rainLevel, rainTarget, dt);
            if (rainLevel > 0f && !rain.isPlaying)
            {
                rain.clip = Clip("amb_rain");
                if (rain.clip != null) rain.Play();
            }
            else if (rainLevel <= 0f && rain.isPlaying)
                rain.Stop();
            rain.volume = rainLevel * amb;

            AudioListener.volume = MuteInBackground && !Application.isFocused ? 0f : 1f;
        }
    }
}
