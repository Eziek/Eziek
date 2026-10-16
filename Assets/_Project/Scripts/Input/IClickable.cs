namespace Wyspa.Input
{
    public interface IClickable
    {
        void OnClick();
    }

    /// <summary>
    /// Klikalny obszar o niskim priorytecie (np. łowisko przy pomoście): wygrywa tylko, gdy pod kursorem nie ma nic innego.
    /// </summary>
    public interface ILowPriorityClick { }

    /// <summary>Klikalne przed innymi pod kursorem (chowańce: mały, ruchomy cel na grządce albo drzewie).</summary>
    public interface IPriorityClick { }
}

namespace Wyspa.Input
{
    /// <summary>
    /// Obiekt, który ścina się albo rozbija przytrzymaniem (GDD §9 p. 136): ClickRouter liczy czas i rysuje kółko nad obiektem.
    /// Krótki klik (puszczenie przed ClickRouter.TapTime) to zwykły IClickable.OnClick (np. zbiór owoców, panel).
    /// </summary>
    public interface IHoldAction
    {
        /// <summary>Czy teraz da się ściąć (np. jest dość Żaru). Nie: wciśnięcie działa jak zwykły klik.</summary>
        bool CanHold { get; }
        /// <summary>Ile sekund trzymać.</summary>
        float HoldSeconds { get; }
        /// <summary>Punkt w świecie nad obiektem, gdzie stoi kółko postępu.</summary>
        UnityEngine.Vector3 HoldAnchor { get; }
        /// <summary>Co klatkę w trakcie trzymania (progress 0..1): uderzenia, odpryski, dźwięk.</summary>
        void HoldTick(float progress);
        /// <summary>Kółko pełne: ścięcie albo rozbicie.</summary>
        void HoldComplete();
    }
}
