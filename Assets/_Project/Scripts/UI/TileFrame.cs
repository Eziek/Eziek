using UnityEngine;
using Wyspa.Input;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>Ramka kafelka w trybie rozbudowy: do kupienia (woda przy lądzie) albo do przekopania (wolna trawa).</summary>
    public class TileFrame : MonoBehaviour, IClickable
    {
        public TilePos Tile { get; private set; }
        public bool IsBuy { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        ExpansionMode owner;

        public void Init(ExpansionMode mode, TilePos tile, bool buy, SpriteRenderer sr)
        {
            owner = mode;
            Tile = tile;
            IsBuy = buy;
            Renderer = sr;
        }

        public void OnClick()
        {
            if (owner != null)
                owner.Click(this);
        }
    }
}
