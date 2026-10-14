using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;
using Wyspa.Localization;
using Wyspa.UI;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Sea
{
    /// <summary>
    /// Pomost (GDD §11.3 F): stoi na kafelku wody przy brzegu. Łowisko wokół końca pomostu: klik w rybę (z shadera wody)
    /// daje łuski i monety; świecąca ryba w nocy - więcej łusek. Złowiona ryba wraca po chwili.
    /// </summary>
    public class Pier : MonoBehaviour, IClickable, ILowPriorityClick
    {
        const float Range = 5f;            // zasięg łowiska od końca pomostu (kafelki)
        const float RespawnSeconds = 90f;

        Island island;
        WaterAnimator water;
        Vector2 tip;

        public TilePos Tile { get; private set; }
        /// <summary>Kierunek desek od lądu (+1 w prawo, -1 w lewo), ustalony przy budowie.</summary>
        public int Dir { get; private set; } = 1;
        public Vector2 Tip => tip;

        public void Init(Island owner, WaterAnimator waterAnimator, TilePos tile, Vector2 tipWorld, int dir)
        {
            Dir = dir >= 0 ? 1 : -1;
            island = owner;
            water = waterAnimator;
            Tile = tile;
            tip = tipWorld;
        }

        public string HoverText(Vector2 world)
        {
            if (water == null)
                return null;
            int i = water.FishAt(world, tip, Range);
            if (i < 0)
                return null;
            var game = GameLoop.Instance;
            bool glow = water.IsGlowing(i) && game != null && game.Clock.IsNight;
            return Loc.Get(glow ? "ui.fish.glow" : "ui.fish.normal") + "\n" + Loc.Get("ui.fish.click");
        }

        public void OnClick()
        {
            var game = GameLoop.Instance;
            var cam = Camera.main;
            if (water == null || game == null || cam == null)
                return;
            Vector2 world = cam.ScreenToWorldPoint(UiInput.MousePosition);
            int i = water.FishAt(world, tip, Range);
            if (i < 0)
                return;
            var at = water.FishPosition(i);
            bool glow = water.IsGlowing(i) && game.Clock.IsNight;
            water.Hide(i, RespawnSeconds);
            Lore.Chronicle.Note(glow ? "fish:glow" : "fish:normal");
            Lore.Chronicle.Count("fish");
            int scales = glow ? 3 : 1;
            int coins = Random.Range(2, 6);
            game.Wallet.Add(CurrencyType.Scale, scales);
            game.Wallet.Add(CurrencyType.Coins, coins);
            GameEvents.RaiseCurrencyGained(at, CurrencyType.Scale, scales);
            GameEvents.RaiseCurrencyGained(at, CurrencyType.Coins, coins);
            GameEvents.RaiseToast(glow ? "toast.fish.glow" : "toast.fish.caught", scales);
            IslandFx.TileClaimed(at, island != null && island.Art != null ? island.Art.foam : null);
        }
    }
}
