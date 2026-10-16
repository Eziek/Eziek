using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Panel informacji o grządce (GDD §9 p. 136: prawy klik albo przytrzymanie palca): to samo co dymek (co rośnie, ile,
    /// czego potrzebuje) i akcje: posadź / zbierz, wykop roślinę, zasyp grządkę, obsiej puste grządki losowo.
    /// Odświeża się na żywo (postęp wzrostu).
    /// </summary>
    public class PlotInfo : IContextActions, IContextAlive
    {
        readonly FarmPlot plot;

        public PlotInfo(FarmPlot plot) => this.plot = plot;

        public bool Alive => plot != null && plot.isActiveAndEnabled;

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            if (!Alive || game == null)
                return;
            info.live = true;
            info.title = Loc.Get("obj.plot");
            info.worldAnchor = plot.CropPosition + new Vector3(0f, 1f, 0f);
            foreach (var line in UiRoot.Describe(plot, game).Split('\n'))
                info.lines.Add(line);
            var s = plot.State;
            var p = plot;
            if (s.IsEmpty)
            {
                info.Button(Loc.Get("act.plant"), () => { if (p != null) p.OnClick(); });
                info.Button(Loc.Get("act.plant_random"), UiRoot.PlantRandom);
            }
            else if (s.IsRipe)
                info.Button(Loc.Get("act.harvest"), () => { if (p != null) p.OnClick(); });
            else
                info.Button(Loc.Get("act.dig_up"), () => { if (p != null) p.DigUp(); });
            if (Island.Instance != null)
            {
                string fill = Loc.Get("act.fill");
                if (!s.IsEmpty)
                    fill += " (" + Loc.Get("ui.fill.lose", Loc.Get("crop." + s.crop.id)) + ")";
                info.Button(fill, () => { if (p != null && Island.Instance != null) Island.Instance.TryFill(p); });
            }
            info.verticalButtons = true;
        }
    }
}
