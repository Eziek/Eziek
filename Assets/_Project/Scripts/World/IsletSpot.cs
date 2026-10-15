using UnityEngine;
using Wyspa.Core;
using Wyspa.Input;
using Wyspa.Localization;

namespace Wyspa.World
{
    /// <summary>
    /// Kafelek odciętej wysepki (§13.10). Wysepki nie da się używać, dopóki gracz jej nie przyłączy: klik pokazuje tylko,
    /// ile kafelków wody dzieli ją od lądu, i otwiera rozbudowę. Tworzy i usuwa go Island (RefreshIslets).
    /// </summary>
    public class IsletSpot : MonoBehaviour, IClickable, IContextActions
    {
        Island island;
        int index;

        public void Init(Island owner, int islet)
        {
            island = owner;
            index = islet;
        }

        public void OnClick()
        {
            if (island != null)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get("ui.islet.title");
            info.worldAnchor = transform.position + new Vector3(0f, 1f, 0f);
            info.lines.Add(Loc.Get("ui.islet.desc"));
            info.lines.Add(Loc.Get("ui.islet.size", island.Map.IsletTiles(index).Count));
            info.lines.Add(Loc.Get("ui.islet.gap", island.Map.IsletGap(index)));
            var ui = UI.UiRoot.Instance;
            if (ui != null && ui.Expansion != null)
                info.Button(Loc.Get("ui.islet.expand"), () => ui.Expansion.Open());
        }
    }
}
