using UnityEngine;
using Wyspa.Core;
using Wyspa.Input;
using Wyspa.Localization;

namespace Wyspa.World
{
    /// <summary>
    /// Szczątki na kafelku przyłączonej wysepki fabularnej (§13.11). Klik: panel z kosztem odbudowy części i postępem.
    /// Odbudowana część stoi w rusztowaniu, dopóki nie stanie cała budowla. Tworzy i usuwa je Island.
    /// </summary>
    public class RuinPiece : MonoBehaviour, IClickable, IContextActions
    {
        Island island;
        SpriteRenderer sr;

        public int Islet { get; private set; }
        public TilePos Tile { get; private set; }
        public bool Fixed { get; private set; }

        public void Init(Island owner, int islet, TilePos tile, bool isFixed, SpriteRenderer renderer)
        {
            island = owner;
            Islet = islet;
            Tile = tile;
            Fixed = isFixed;
            sr = renderer;
        }

        public void SetFixed(Sprite scaffold)
        {
            Fixed = true;
            if (sr != null && scaffold != null)
                sr.sprite = scaffold;
        }

        public void OnClick()
        {
            if (island != null)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            var site = island.StoryOf(Islet);
            info.title = Loc.Get(site.NameKey);
            info.worldAnchor = transform.position + new Vector3(0f, 1.25f, 0f);
            info.lines.Add(Loc.Get("ui.ruin.desc", Loc.Get(site.BuildingKey)));
            info.lines.Add(Loc.Get(site.DescKey, site.amount, Mathf.RoundToInt(site.interval / 60f)));
            info.lines.Add(Loc.Get("ui.ruin.progress", island.RuinsFixedOn(Islet), island.Map.IsletTiles(Islet).Count));
            if (Fixed)
            {
                info.lines.Add(Loc.Get("ui.ruin.fixed"));
                return;
            }
            info.lines.Add(Loc.Get("ui.ruin.cost", site.wood, site.stone, site.coins));
            var game = GameLoop.Instance;
            bool afford = game != null && game.Wallet.CanAfford(site.RepairCost());
            info.Button(Loc.Get("act.rebuild"), () => island.TryRepair(this, game != null ? game.Wallet : null), afford);
        }
    }
}
