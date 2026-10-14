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
    /// Klik w deski (bez ryby pod kursorem) otwiera panel z przeniesieniem; sieci i łódź płyną razem z pomostem.
    /// </summary>
    public class Pier : MonoBehaviour, IClickable, ILowPriorityClick, IContextActions
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

        /// <summary>Kursor nad deskami pomostu (nie nad łowiskiem wokół).</summary>
        bool OnPlanks(Vector2 world)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
                return false;
            var b = sr.bounds;
            return world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y;
        }

        public string HoverText(Vector2 world)
        {
            if (water == null)
                return null;
            int i = water.FishAt(world, tip, Range);
            if (i < 0)
                return OnPlanks(world) ? Loc.Get("bld.pier") + "\n" + Loc.Get("ui.object.hint") : null;
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
            {
                if (OnPlanks(world))
                    GameEvents.RequestContext(this);
                return;
            }
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

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get("bld.pier");
            info.worldAnchor = transform.position + new Vector3(0f, 1.2f, 0f);
            info.lines.Add(Loc.Get("ui.pier.info"));
            if (island != null && (island.HasNets || island.HasBoat))
                info.lines.Add(Loc.Get("ui.pier.moves_all"));
            info.Button(Loc.Get("act.move"), () => StartMove(island));
        }

        /// <summary>Tryb stawiania dla pomostu (za darmo): duch desek na wodzie przy brzegu, sieci i łódź płyną za nim.</summary>
        public static void StartMove(Island island)
        {
            if (island == null || !island.HasPier || UiRoot.Instance == null)
                return;
            UiRoot.Instance.StartPlacement(new PlacementMode.Request
            {
                nameKey = "bld.pier",
                sprite = island.Art != null ? island.Art.pier : null,
                valid = island.PierMoveValid,
                ghostAt = t => island.PierPlacement(t, out _, out _),
                confirm = island.MovePier,
            });
        }
    }
}
