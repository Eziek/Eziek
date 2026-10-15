using UnityEngine;
using Wyspa.Input;
using Wyspa.UI;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Własny kursor (GDD §9 p. 115): pikselowa strzałka z żarzącym się czubkiem, rysowana z map znaków poniżej
    /// (16x16, powiększona x2 do 32x32), więc nie potrzebuje importu tekstur. Stany: zwykły, nad czymś klikalnym
    /// (czubek płonie, 2 klatki iskier), nad wodą (turkusowy czubek), przeciąganie widoku (strzałki w 4 strony).
    /// Przy szybkim ruchu nad wyspą zostawia za sobą kilka iskierek.
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        const int Scale = 2;

        // . przezroczysty, o kontur, d ciemny kamień, b kamień, l jasny brzeg, e żar, f płomień, t turkus, w biel ognika
        static readonly string[] Arrow =
        {
            "oo..............",
            "o#o.............",
            "o##o............",
            "ol#bo...........",
            "olbbbo..........",
            "olbbbbo.........",
            "olbbbbbo........",
            "olbbbbbbo.......",
            "olbbbbbddo......",
            "olbbdoooooo.....",
            "olbdoob.........",
            "oldo..obo.......",
            "odo...obo.......",
            "oo.....oo.......",
            "................",
            "................",
        };
        static readonly string[] Move =
        {
            ".......o........",
            "......olo.......",
            ".....olblo......",
            "....ooobooo.....",
            ".......b........",
            "...o...b...o....",
            "..oo...b...oo...",
            ".olbbbb#bbbblo..",
            "..oo...b...oo...",
            "...o...b...o....",
            ".......b........",
            "....ooobooo.....",
            ".....olblo......",
            "......olo.......",
            ".......o........",
            "................",
        };

        enum State { Normal, Hover, Water, Drag }

        Texture2D normal, hover0, hover1, water, drag;
        State state = (State)(-1);
        int frame = -1;
        float animTimer, trailTimer;
        Vector2 lastMouse;

        void Start()
        {
            normal = Make(Arrow, 'e', false);
            hover0 = Make(Arrow, 'f', true, 0);
            hover1 = Make(Arrow, 'f', true, 1);
            water = Make(Arrow, 't', false);
            drag = Make(Move, 'e', false);
        }

        void OnDisable() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        static Color32 Col(char c, char tip)
        {
            if (c == '#') c = tip;
            switch (c)
            {
                case 'o': return new Color32(15, 10, 16, 255);
                case 'd': return new Color32(31, 48, 68, 255);
                case 'b': return new Color32(48, 72, 95, 255);
                case 'l': return new Color32(132, 142, 155, 255);
                case 'e': return new Color32(240, 138, 58, 255);
                case 'f': return new Color32(255, 224, 138, 255);
                case 't': return new Color32(95, 207, 184, 255);
                case 'w': return new Color32(210, 255, 240, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }

        /// <summary>Tekstura kursora z mapy znaków; sparkle = iskry obok czubka (dwie klatki).</summary>
        static Texture2D Make(string[] map, char tip, bool sparkle, int f = 0)
        {
            int n = 16 * Scale;
            var px = new Color32[n * n];
            void Put(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= 16 || y >= 16) return;
                for (int sy = 0; sy < Scale; sy++)
                    for (int sx = 0; sx < Scale; sx++)
                        px[(n - 1 - (y * Scale + sy)) * n + x * Scale + sx] = c;   // wiersz 0 tekstury = dół
            }
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    Put(x, y, Col(map[y][x], tip));
            if (sparkle)
            {
                var fl = Col('f', tip);
                var w = Col('w', tip);
                if (f == 0) { Put(4, 1, fl); Put(6, 3, w); Put(3, 0, w); }
                else { Put(5, 0, w); Put(5, 2, fl); Put(7, 2, fl); }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        void Update()
        {
            if (normal == null)
                return;
            var mouse = UiInput.MousePosition;
            var next = Decide(mouse);
            animTimer -= Time.unscaledDeltaTime;
            int f = frame;
            if (next == State.Hover && animTimer <= 0f)
            {
                animTimer = 0.22f;
                f = 1 - Mathf.Max(0, frame);
            }
            if (next != state || (next == State.Hover && f != frame))
            {
                state = next;
                frame = f;
                var tex = next == State.Hover ? (frame == 0 ? hover0 : hover1) : next == State.Water ? water : next == State.Drag ? drag : normal;
                var hotspot = next == State.Drag ? new Vector2(7.5f * Scale, 7.5f * Scale) : Vector2.zero;
                Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
            }
            Trail(mouse);
        }

        State Decide(Vector2 mouse)
        {
            var view = ViewController.Instance;
            if (view != null && view.Dragging)
                return State.Drag;
            var ui = UiRoot.Instance;
            var cam = Camera.main;
            if (cam == null || (ui != null && ui.PointerOverUi))
                return State.Normal;
            Vector2 world = cam.ScreenToWorldPoint(mouse);
            if (ClickRouter.Pick(world) != null)
                return State.Hover;
            return CursorPlay.IsWater(world) ? State.Water : State.Normal;
        }

        /// <summary>Szybki ruch nad wyspą: iskierki za kursorem (rzadko, żeby nie zaśmiecać).</summary>
        void Trail(Vector2 mouse)
        {
            float speed = (mouse - lastMouse).magnitude / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            lastMouse = mouse;
            trailTimer -= Time.unscaledDeltaTime;
            var cam = Camera.main;
            var ui = UiRoot.Instance;
            if (trailTimer > 0f || speed < 900f || cam == null || (ui != null && ui.PointerOverUi))
                return;
            trailTimer = 0.07f;
            IslandFx.WispTrail(cam.ScreenToWorldPoint(mouse));
        }
    }
}
