using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Automation
{
    /// <summary>
    /// Runa siewu (Etap 6b, dawna „pamięć ziemi”): sadzi na pustych grządkach w 8 kafelkach wokół siebie (3x3).
    /// Klik → wybór nasion: „pamięć” (to samo, co rosło), konkretna roślina albo nic. Nasiono najpierw z worka,
    /// potem za monety (hybryd nie da się kupić). Run może być wiele.
    /// </summary>
    public class SeedRune : MonoBehaviour, IAreaEffect, IContextActions
    {
        public const int Radius = 1;
        /// <summary>Tryb „pamięć ziemi” (puste id) i „nic nie sadź”.</summary>
        public const string Memory = "", Off = "-";
        const float Interval = 2f;

        public static readonly List<SeedRune> All = new List<SeedRune>();

        string seed = Memory;
        float timer;
        bool warnedCoins;

        public int AreaRadius => Radius;
        public string Seed
        {
            get => seed;
            set { seed = value ?? Memory; warnedCoins = false; }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        TilePos Tile
        {
            get
            {
                var obj = GetComponent<TileObject>();
                return obj != null ? obj.Tile : Island.WorldToTile(transform.position);
            }
        }

        public bool Covers(FarmPlot plot)
        {
            var t = Tile;
            return Mathf.Abs(plot.Cell.x - t.c) <= Radius && Mathf.Abs(plot.Cell.y - t.r) <= Radius;
        }

        /// <summary>Co 2 s: każda pusta grządka w zasięgu dostaje nasiono (wg trybu). Zwraca liczbę posadzonych.</summary>
        public int Tick(float dt, GameLoop game)
        {
            timer += dt;
            if (timer < Interval || game == null || seed == Off)
                return 0;
            timer = 0f;
            var chosen = seed == Memory || game.Crops == null ? null : game.Crops.Find(seed);
            if (seed != Memory && chosen == null)
                return 0;
            int planted = 0;
            foreach (var plot in FarmPlot.All)
            {
                if (!plot.State.IsEmpty || !Covers(plot))
                    continue;
                var crop = chosen != null ? chosen : plot.LastCrop;
                if (crop == null)
                    continue;
                if (!game.Seeds.TryTake(crop.id) && (crop.hybrid || !game.Wallet.TrySpend(crop.seedPrice)))
                {
                    if (!warnedCoins)
                    {
                        warnedCoins = true;
                        GameEvents.RaiseToast("toast.rune.no_seeds", Loc.Get("crop." + crop.id));
                    }
                    continue;
                }
                warnedCoins = false;
                plot.Replant(crop);
                planted++;
            }
            return planted;
        }

        public void FillContext(ContextInfo info)
        {
            string what = seed == Memory ? Loc.Get("ui.rune.memory")
                        : seed == Off ? Loc.Get("ui.rune.off")
                        : Loc.Get("crop." + seed);
            info.lines.Add(Loc.Get("ui.rune.sows", what));
            info.lines.Add(Loc.Get("ui.rune.range"));
            info.Button(Loc.Get("ui.rune.choose"), () =>
            {
                var ui = UI.UiRoot.Instance;
                if (ui != null)
                    ui.Wheel.OpenPicker(this);
            });
        }
    }
}
