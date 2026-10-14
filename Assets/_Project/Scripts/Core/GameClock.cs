using System;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>Pora dnia z zegara komputera (+ nadpisanie do testów) i faza księżyca.</summary>
    public class GameClock : MonoBehaviour
    {
        [Header("Noc wg zegara komputera (godziny)")]
        [Range(0f, 24f)] [SerializeField] float nightStartsAt = 20f;
        [Range(0f, 24f)] [SerializeField] float nightEndsAt = 6f;

        [Header("Debug")]
        [SerializeField] bool useDebugHour;
        [Range(0f, 24f)] [SerializeField] float debugHour = 12f;

        const double SynodicMonthDays = 29.530588853;
        static readonly DateTime KnownNewMoonUtc = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);

        /// <summary>Godzina początku nocy (opcje gracza, PlayerPrefs „nightStart”).</summary>
        public float NightStartsAt
        {
            get => nightStartsAt;
            set { nightStartsAt = Mathf.Repeat(value, 24f); PlayerPrefs.SetFloat("nightStart", nightStartsAt); }
        }

        /// <summary>Godzina końca nocy (PlayerPrefs „nightEnd”).</summary>
        public float NightEndsAt
        {
            get => nightEndsAt;
            set { nightEndsAt = Mathf.Repeat(value, 24f); PlayerPrefs.SetFloat("nightEnd", nightEndsAt); }
        }

        void Awake()
        {
            nightStartsAt = PlayerPrefs.GetFloat("nightStart", nightStartsAt);
            nightEndsAt = PlayerPrefs.GetFloat("nightEnd", nightEndsAt);
        }

        public bool UseDebugHour
        {
            get => useDebugHour;
            set => useDebugHour = value;
        }

        public float DebugHour
        {
            get => debugHour;
            set => debugHour = Mathf.Repeat(value, 24f);
        }

        public DateTime Now => useDebugHour ? DateTime.Today.AddHours(debugHour) : DateTime.Now;
        public float Hour => HourOf(Now);
        public bool IsNight => IsNightAt(Now);

        /// <summary>0 = nów, 0.5 = pełnia.</summary>
        public float MoonPhase => MoonPhaseAt(DateTime.UtcNow);
        public bool IsFullMoon => DebugFullMoon || Mathf.Abs(MoonPhase - 0.5f) < FullMoonWindow;

        /// <summary>Połowa okna pełni w fazie (0.034 = ok. ±1 doba; rytuał „Księżycowa rosa” podwaja).</summary>
        public static float FullMoonWindow = 0.034f;

        /// <summary>Zmierzch: ostatnia godzina przed nocą (rytuał „Zmierzch”: rośliny nocy rosną też wtedy).</summary>
        public bool IsDusk
        {
            get
            {
                float h = Hour, start = nightStartsAt, from = Mathf.Repeat(start - 1f, 24f);
                return from < start ? h >= from && h < start : h >= from || h < start;
            }
        }

        /// <summary>Numer cyklu księżyca (od nowiu do nowiu) - do punktów Księgi rytuałów (GDD §11.3 G).</summary>
        public static int CycleIndex(DateTime utc) => (int)Math.Floor((utc - KnownNewMoonUtc).TotalDays / SynodicMonthDays);

        /// <summary>Ile dni do najbliższego nowiu (końca cyklu).</summary>
        public static float DaysToNewMoon(DateTime utc) => (float)((1.0 - MoonPhaseAt(utc)) * SynodicMonthDays);

        /// <summary>Do testów (HUD debugowy): pełnia niezależnie od prawdziwej fazy księżyca.</summary>
        public static bool DebugFullMoon;

        public static float HourOf(DateTime t) => (float)t.TimeOfDay.TotalHours;

        public bool IsNightAt(DateTime t)
        {
            float h = HourOf(t);
            return nightStartsAt > nightEndsAt
                ? h >= nightStartsAt || h < nightEndsAt
                : h >= nightStartsAt && h < nightEndsAt;
        }

        /// <summary>Ile sekund nocy minęło między dwoma momentami (do postępu offline). Krok: 1 minuta.</summary>
        public float NightSecondsBetween(DateTime fromLocal, DateTime toLocal, double maxHours = 24 * 7)
        {
            if (toLocal <= fromLocal)
                return 0f;
            DateTime end = toLocal - fromLocal > TimeSpan.FromHours(maxHours) ? fromLocal.AddHours(maxHours) : toLocal;
            double seconds = 0;
            for (DateTime t = fromLocal; t < end; t = t.AddMinutes(1))
            {
                double step = Math.Min(60.0, (end - t).TotalSeconds);
                if (IsNightAt(t))
                    seconds += step;
            }
            return (float)seconds;
        }

        public static float MoonPhaseAt(DateTime utc)
        {
            double days = (utc - KnownNewMoonUtc).TotalDays;
            double phase = days / SynodicMonthDays;
            return (float)(phase - Math.Floor(phase));
        }
    }
}
