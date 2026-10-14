namespace Wyspa.Lore
{
    /// <summary>
    /// Zakończenie historii „Ostatnia latarnia”. Po przeczytaniu ostatniego zapisku z Serca mgły na ołtarzu pojawia się
    /// napis: w noc pełni gracz zapala tam „Światło Opiekunów”. Oko wynurza się po raz ostatni jako przyjaciel, epilog to
    /// trzy zapiski, a nagroda jest stała: lampy palą mniej Żaru, rośliny nocy rosną szybciej, mgła jest rzadsza.
    /// Stan w kronice („story:finale”), więc zapisuje się razem z bestiariuszem.
    /// </summary>
    public static class Story
    {
        public const string FinaleId = "story:finale";
        public const string LastFragment = "lore.heart.5";
        /// <summary>Epilog (strona Zapiski w bestiariuszu, panel oka w finale).</summary>
        public static readonly string[] Ending = { "lore.end.1", "lore.end.2", "lore.end.3" };

        /// <summary>Koszt zapalenia światła na ołtarzu: plony nocy i odłamki gwiazd.</summary>
        public const int NightCost = 10, ShardCost = 3;

        /// <summary>Historia zakończona (ustawiane przy wczytaniu kroniki i w finale).</summary>
        public static bool Finished { get; private set; }

        /// <summary>Ostatni zapisek przeczytany, finał jeszcze przed graczem.</summary>
        public static bool Ready => !Finished && LoreBook.Found(LastFragment);

        public static float LampBurn => Finished ? 0.8f : 1f;
        public static float NightGrowth => Finished ? 1.1f : 1f;
        public static float Fog => Finished ? 0.7f : 1f;

        public static void Refresh(Chronicle c) => Finished = c != null && c.Found(FinaleId);

        public static void Finish()
        {
            Chronicle.Note(FinaleId);
            foreach (var k in Ending)
                Chronicle.Note("lore:" + k);
            Finished = true;
        }
    }
}
