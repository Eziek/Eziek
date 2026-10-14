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
    }

    /// <summary>Zawartość panelu kontekstowego przy obiekcie (GDD §12.3): tytuł, linie opisu, przyciski.</summary>
    public class ContextInfo
    {
        public string title;
        public Vector3 worldAnchor;
        public readonly List<string> lines = new List<string>();
        public readonly List<ContextButton> buttons = new List<ContextButton>();

        public void Button(string label, Action action, bool enabled = true) =>
            buttons.Add(new ContextButton { label = label, action = action, enabled = enabled });
    }

    /// <summary>Obiekt na wyspie, który po kliknięciu pokazuje panel z akcjami. Komponenty na tym samym obiekcie mogą dopisywać akcje.</summary>
    public interface IContextActions
    {
        void FillContext(ContextInfo info);
    }
}
