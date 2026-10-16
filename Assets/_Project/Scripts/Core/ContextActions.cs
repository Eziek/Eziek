using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>Przycisk w panelu kontekstowym (tekst już przetłumaczony).</summary>
    public class ContextButton
    {
        public string label;
        public Action action;
        public bool enabled = true;
        /// <summary>Po kliknięciu panel zostaje otwarty i odświeża się (przełączniki, czapki, przywołanie).</summary>
        public bool keepOpen;
    }

    /// <summary>Zawartość panelu kontekstowego przy obiekcie (GDD §12.3): tytuł, linie opisu, przyciski.</summary>
    public class ContextInfo
    {
        public string title;
        public Vector3 worldAnchor;
        /// <summary>Przyciski jeden pod drugim, na całą szerokość (np. oferta kupca).</summary>
        public bool verticalButtons;
        /// <summary>Panel odświeża się sam (co pół sekundy), gdy treść się zmieni: postęp wzrostu, Żar na usunięcie.</summary>
        public bool live;
        public readonly List<string> lines = new List<string>();
        public readonly List<ContextButton> buttons = new List<ContextButton>();

        public void Button(string label, Action action, bool enabled = true, bool keepOpen = false) =>
            buttons.Add(new ContextButton { label = label, action = action, enabled = enabled, keepOpen = keepOpen });

        /// <summary>Zakładki panelu (rząd pod tytułem); tab = wybrana, ustawia ją panel przed FillContext.</summary>
        public readonly List<string> tabs = new List<string>();
        public int tab;

        /// <summary>Dodaje zakładkę; true = to jest wybrana zakładka, więc jej linie i przyciski trzeba teraz dopisać.</summary>
        public bool Tab(string label)
        {
            tabs.Add(label);
            return tabs.Count - 1 == tab;
        }
    }

    /// <summary>Obiekt na wyspie, który po kliknięciu pokazuje panel z akcjami. Komponenty na tym samym obiekcie mogą dopisywać akcje.</summary>
    public interface IContextActions
    {
        void FillContext(ContextInfo info);
    }
}
