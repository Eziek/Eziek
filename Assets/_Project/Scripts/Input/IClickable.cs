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
}
