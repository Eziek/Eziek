using System;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>
    /// Pora dnia z cyklu gry (prośba gracza, zastępuje zegar komputera): pełna doba trwa 1 h, z czego 40 min to dzień
    /// (06:00-20:00 na zegarze gry), a 20 min to noc (20:00-06:00). Cykl liczy się od stałej daty w czasie UTC, więc idzie dalej,
    /// także gdy gra jest wyłączona. Każdy cykl to osobna „doba gry” (Now ma datę i godzinę na zegarze gry), dzięki czemu
    /// zdarzenia „raz na noc” (kupiec, potwór, ołtarz) działają raz na cykl. Księżyc: pełny cykl faz trwa 8 nocy, pełnia to jedna noc.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public const float CycleSeconds = 3600f;
        public const float DaySeconds = 2400f;
        public const float NightSeconds = CycleSeconds - DaySeconds;
        /// <summary>Godziny na zegarze gry: noc od 20:00 do 06:00.</summary>
        public const float NightStartsAt = 20f, NightEndsAt = 6f;
        /// <summary>Ile nocy trwa cykl księżyca (od nowiu do nowiu).</summary>
        public const int MoonCycleNights = 8;

        static readonly DateTime EpochUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly DateTime VirtualEpoch = new DateTime(2000, 1, 1);

        [Header("Debug")]
        [SerializeField] bool useDebugHour;
        [Range(0f, 24f)] [SerializeField] float debugHour = 12f;

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

        // ------------------------------------------------------------ czas cyklu

        /// <summary>Czas od początku epoki w cyklach (część całkowita = numer doby gry).</summary>
        static double Cycles(DateTime utc) => ((utc - EpochUtc).TotalSeconds + DebugOffsetSeconds) / CycleSeconds;

        /// <summary>Do testów (HUD debugowy): przesunięcie cyklu gry, np. +1 cykl bez czekania godziny.</summary>
        public static double DebugOffsetSeconds;

        /// <summary>Sekunda cyklu (0..3600): 0 = 06:00, 2400 = 20:00 (początek nocy).</summary>
        static float SecondOfCycle(double cycles) => (float)((cycles - Math.Floor(cycles)) * CycleSeconds);

        /// <summary>Godzina zegara gry dla sekundy cyklu (dzień 14 h w 40 min, noc 10 h w 20 min).</summary>
        public static float HourAt(float second) =>
            second < DaySeconds
                ? NightEndsAt + 14f * second / DaySeconds
                : Mathf.Repeat(NightStartsAt + 10f * (second - DaySeconds) / NightSeconds, 24f);

        /// <summary>Odwrotnie: sekunda cyklu dla godziny zegara gry (do HUD debugowego).</summary>
        public static float SecondAt(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (hour >= NightEndsAt && hour < NightStartsAt)
                return (hour - NightEndsAt) / 14f * DaySeconds;
            float intoNight = Mathf.Repeat(hour - NightStartsAt, 24f);
            return DaySeconds + intoNight / 10f * NightSeconds;
        }

        double CyclesNow
        {
            get
            {
                double c = Cycles(DateTime.UtcNow);
                return useDebugHour ? Math.Floor(c) + SecondAt(debugHour) / CycleSeconds : c;
            }
        }

        /// <summary>Sekunda bieżącego cyklu (0 = świt 06:00, 2400 = zmrok 20:00).</summary>
        public float Second => SecondOfCycle(CyclesNow);
        /// <summary>Numer bieżącej doby gry (cyklu).</summary>
        public long CycleNumber => (long)Math.Floor(CyclesNow);
        public float Hour => HourAt(Second);
        public bool IsNight => Second >= DaySeconds;
        /// <summary>Ile sekund trwa już bieżąca noc (0, gdy jest dzień).</summary>
        public float SecondsIntoNight => Mathf.Max(0f, Second - DaySeconds);

        /// <summary>Data i godzina na zegarze gry: jedna doba gry = jeden cykl (do HUD i zdarzeń „raz na noc”).</summary>
        public DateTime Now
        {
            get
            {
                double c = CyclesNow;
                float h = HourAt(SecondOfCycle(c));
                // noc po północy (00:00-06:00) należy jeszcze do tej samej doby cyklu, ale na zegarze to już następny dzień
                long day = (long)Math.Floor(c) + (h < NightEndsAt ? 1 : 0);
                return VirtualEpoch.AddDays(day).AddHours(h);
            }
        }

        /// <summary>Klucz bieżącej (albo ostatniej) nocy: numer cyklu. Zdarzenia „raz na noc” porównują ten klucz.</summary>
        public string NightKey => "c" + CycleNumber;

        /// <summary>Zmierzch: ostatnia godzina zegara gry przed nocą (ok. 3 min).</summary>
        public bool IsDusk
        {
            get
            {
                float h = Hour;
                return h >= NightStartsAt - 1f && h < NightStartsAt;
            }
        }

        public static bool IsNightAt(DateTime utc) => SecondOfCycle(Cycles(utc)) >= DaySeconds;

        /// <summary>Ile sekund nocy minęło między dwoma momentami (postęp offline). Liczone dokładnie, cykl po cyklu.</summary>
        public float NightSecondsBetween(DateTime fromLocal, DateTime toLocal, double maxHours = 24 * 7)
        {
            if (toLocal <= fromLocal)
                return 0f;
            DateTime end = toLocal - fromLocal > TimeSpan.FromHours(maxHours) ? fromLocal.AddHours(maxHours) : toLocal;
            double a = Cycles(fromLocal.ToUniversalTime()), b = Cycles(end.ToUniversalTime());
            return (float)(NightUpTo(b) - NightUpTo(a));
        }

        /// <summary>Sekundy nocy od epoki do chwili c (w cyklach).</summary>
        static double NightUpTo(double c)
        {
            double whole = Math.Floor(c);
            double inCycle = (c - whole) * CycleSeconds;
            return whole * NightSeconds + Math.Max(0.0, inCycle - DaySeconds);
        }

        // ------------------------------------------------------------ księżyc (8 nocy na cykl)

        /// <summary>Czas księżyca w nocach: liczba całkowita = środek nocy danego cyklu (sekunda 3000).</summary>
        static double MoonTime(double cycles) => cycles - (DaySeconds + NightSeconds / 2f) / CycleSeconds;

        static float PhaseOf(double cycles)
        {
            double p = MoonTime(cycles) / MoonCycleNights;
            return (float)(p - Math.Floor(p));
        }

        /// <summary>0 = nów, 0.5 = pełnia (środek co ósmej nocy).</summary>
        public float MoonPhase => PhaseOf(CyclesNow);
        public bool IsFullMoon => DebugFullMoon || Mathf.Abs(MoonPhase - 0.5f) < FullMoonWindow;

        /// <summary>Połowa okna pełni w fazie: 1/16 = jeden cykl wokół środka nocy pełni (rytuał „Księżycowa rosa” podwaja).</summary>
        public static float FullMoonWindow = 1f / 16f;

        /// <summary>Numer cyklu księżyca (od nowiu do nowiu) - do punktów Księgi rytuałów (GDD §11.3 G).</summary>
        public static int CycleIndex(DateTime utc) => (int)Math.Floor(MoonTime(Cycles(utc)) / MoonCycleNights);

        /// <summary>Ile nocy do najbliższego nowiu (końca cyklu księżyca).</summary>
        public static int NightsToNewMoon(DateTime utc) =>
            Mathf.Max(1, Mathf.CeilToInt((1f - PhaseOf(Cycles(utc))) * MoonCycleNights));

        /// <summary>Do testów (HUD debugowy): pełnia niezależnie od fazy księżyca.</summary>
        public static bool DebugFullMoon;

        public static float HourOf(DateTime t) => (float)t.TimeOfDay.TotalHours;
    }
}
