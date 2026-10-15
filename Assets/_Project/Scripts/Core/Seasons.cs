using System;

namespace Wyspa.Core
{
    /// <summary>
    /// Pory roku w grze (GDD §9 p. 127): bieżąca pora z zegara gry (GameClock.Season, 6 cykli na porę) i zdarzenie zmiany,
    /// na które reagują wygląd (sprite'y, teren, woda, pogoda, cząsteczki) i rośliny sezonowe. Przy zmianie w trakcie gry
    /// toast; przy wczytaniu tylko ustawienie bez toastu.
    /// </summary>
    public static class Seasons
    {
        static bool known;

        public static Season Current { get; private set; } = Season.Spring;

        /// <summary>Nowa pora roku (także pierwsze ustawienie po wczytaniu).</summary>
        public static event Action<Season> Changed;

        /// <summary>Klucz nazwy pory (lokalizacja): season.spring, season.summer, season.autumn, season.winter.</summary>
        public static string NameKey(Season s) => "season." + s.ToString().ToLowerInvariant();

        /// <summary>Woła GameLoop co klatkę (tanie: porównanie liczby).</summary>
        public static void Update(GameClock clock)
        {
            if (clock == null)
                return;
            var s = clock.Season;
            if (known && s == Current)
                return;
            bool announce = known;
            known = true;
            Current = s;
            if (announce)
                GameEvents.RaiseToast("toast.season", UnityLocalized(s));
            Changed?.Invoke(s);
        }

        /// <summary>Nowa scena (menu, inny zapis): następne Update ustawi porę bez toastu.</summary>
        public static void Reset() => known = false;

        static string UnityLocalized(Season s) => Localization.Loc.Get(NameKey(s));

        /// <summary>F1: kolejna pora roku od razu (wymuszona do końca sesji albo do „z kalendarza gry”).</summary>
        public static void DebugNext(GameClock clock)
        {
            var next = (Season)(((int)(GameClock.DebugSeason ?? clock.Season) + 1) % 4);
            GameClock.DebugSeason = next;
        }
    }
}
