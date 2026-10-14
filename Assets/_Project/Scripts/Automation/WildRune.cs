using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Automation
{
    /// <summary>
    /// Runa dziczy (rój ogników, zastępuje runę siewu - sianie przejął ognik-siewca): co kilka minut na wolnym kafelku trawy
    /// wokół runy (3x3 bez środka) wyrasta dzikie drzewo, krzak albo kamień. Razem z ognikiem-drwalem daje stałe drewno i kamień.
    /// </summary>
    public class WildRune : MonoBehaviour, IAreaEffect, IContextActions
    {
        public const int Radius = 1;
        /// <summary>Co ile sekund gry wyrasta coś nowego (do balansu).</summary>
        public const float Interval = 360f;

        public static readonly List<WildRune> All = new List<WildRune>();

        float timer;

        public int AreaRadius => Radius;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        TilePos Tile
        {
            get
            {
                var obj = GetComponent<TileObject>();
                return obj != null && obj.Placed ? obj.Tile : Island.WorldToTile(transform.position);
            }
        }

        public bool Covers(FarmPlot plot)
        {
            var t = Tile;
            return Mathf.Abs(plot.Cell.x - t.c) <= Radius && Mathf.Abs(plot.Cell.y - t.r) <= Radius;
        }

        /// <summary>Licznik rośnie z czasem gry; gdy wokół nie ma miejsca, czeka pełny (wyrośnie od razu po zwolnieniu kafelka).</summary>
        public void Tick(float dt, GameLoop game)
        {
            var island = Island.Instance;
            if (island == null || !island.Ready)
                return;
            timer = Mathf.Min(timer + dt, Interval);
            if (timer < Interval)
                return;
            var obj = GetComponent<TileObject>();
            if (obj != null && !obj.Placed)
                return;   // w trakcie przenoszenia
            if (island.GrowWild(Tile, Radius))
                timer = 0f;
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.rune.wild"));
            int left = Mathf.CeilToInt(Interval - timer);
            info.lines.Add(left <= 0 ? Loc.Get("ui.rune.full") : Loc.Get("ui.rune.next", left / 60 + ":" + (left % 60).ToString("00")));
        }
    }
}
