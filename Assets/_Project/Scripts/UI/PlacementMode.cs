using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Tryb stawiania (GDD §12.5): półprzezroczysty „duch” obiektu przyciągany do kafelków, na czerwono gdy miejsce zajęte,
    /// zasięg światła lampy jako przerywany okrąg i licznik „Oświetli: N grządek”. LPM = postaw, PPM lub Esc = anuluj.
    /// Służy do stawiania lamp ze sklepu i przenoszenia obiektów.
    /// </summary>
    public class PlacementMode : MonoBehaviour
    {
        public static bool Active { get; private set; }
        /// <summary>Klatka, w której tryb zużył klik (ClickRouter go pomija).</summary>
        public static int ConsumedFrame { get; private set; } = -1;

        const float Ppu = 16f;
        const int GhostOrder = 3950;
        static readonly Color Ok = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color Bad = new Color(1f, 0.35f, 0.3f, 0.6f);
        static readonly Color RingColor = new Color32(0xd2, 0xff, 0xf0, 0xff);

        /// <summary>Co i jak stawiamy: duch, warunek miejsca, pozycja ducha, koszt, akcja po LPM.</summary>
        public class Request
        {
            public string nameKey;
            public Sprite sprite;
            public LampDefinition light;
            public List<CurrencyAmount> cost;                  // null = za darmo (przenoszenie)
            public Func<TilePos, bool> valid;
            public Func<TilePos, (Vector3 pos, bool flip)> ghostAt;
            public Func<TilePos, bool> confirm;                // true = postawione
            public int area;                                   // zasięg w kafelkach (runa 1 = 3x3, ognik 2 = 5x5), 0 = brak
        }

        TooltipView tooltip;
        SpriteRenderer ghost;
        SpriteRenderer ring;
        SpriteRenderer square;
        Request request;
        readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
        readonly Dictionary<int, Sprite> squares = new Dictionary<int, Sprite>();
        bool hasTile;
        TilePos tile;
        bool startedThisFrame;

        public static PlacementMode Create(Transform parent, TooltipView tooltip)
        {
            var view = parent.gameObject.AddComponent<PlacementMode>();
            view.tooltip = tooltip;
            var root = new GameObject("PlacementGhost").transform;
            view.ghost = NewRenderer("Ghost", root, GhostOrder);
            view.ring = NewRenderer("LightRange", root, GhostOrder - 1);
            view.square = NewRenderer("AreaRange", root, GhostOrder - 1);
            root.gameObject.SetActive(false);
            return view;
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        static (Vector3, bool) OnLand(TilePos t) => (Island.TileAnchor(t, 13, out _), false);

        /// <summary>Lampa ze sklepu: płaci się dopiero przy postawieniu.</summary>
        public void StartBuy(LampDefinition def)
        {
            var island = Island.Instance;
            if (def == null || island == null)
                return;
            Begin(new Request
            {
                nameKey = def.NameKey, sprite = def.sprite, light = def,
                cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, def.price) },
                valid = island.IsFree, ghostAt = OnLand,
                confirm = t =>
                {
                    if (island.SpawnLamp(def, t) == null)
                        return false;
                    GameEvents.RaiseToast("ui.toast.placed", Loc.Get(def.NameKey));
                    return true;
                },
            });
        }

        public void StartMove(TileObject obj)
        {
            var island = Island.Instance;
            if (obj == null || !obj.NeedsTile || island == null)
                return;
            var sr = obj.GetComponent<SpriteRenderer>();
            var lamp = obj.GetComponent<Lamp>();
            var areaEffect = obj.GetComponent<Automation.IAreaEffect>();
            Begin(new Request
            {
                nameKey = obj.NameKey, sprite = sr != null ? sr.sprite : null, light = lamp != null ? lamp.Definition : null,
                area = areaEffect != null ? areaEffect.AreaRadius : 0,
                valid = t => island.IsFree(t) || (obj.Placed && obj.Tile == t),
                ghostAt = OnLand,
                confirm = t => island.MoveObject(obj, t),
            });
        }

        public void Begin(Request r)
        {
            if (r == null || r.valid == null || r.confirm == null)
                return;
            request = r;
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (art != null && art.glow != null)
            {
                ghost.sharedMaterial = art.glow;   // duch i okrąg widać także w nocy
                ring.sharedMaterial = art.glow;
                square.sharedMaterial = art.glow;
            }
            ghost.sprite = r.sprite;
            ring.sprite = r.light != null ? Ring(Mathf.RoundToInt(r.light.radius * Ppu)) : null;
            ring.color = RingColor;
            square.sprite = r.area > 0 ? Square(r.area) : null;
            square.color = RingColor;
            ghost.transform.parent.gameObject.SetActive(true);
            Active = true;
            startedThisFrame = true;
            hasTile = false;
        }

        public void Cancel()
        {
            Active = false;
            request = null;
            if (ghost != null)
                ghost.transform.parent.gameObject.SetActive(false);
            tooltip.Hide(this);
        }

        void OnDisable() => Cancel();

        void Update()
        {
            if (!Active || request == null)
                return;
            var island = Island.Instance;
            var cam = Camera.main;
            if (island == null || cam == null)
            {
                Cancel();
                return;
            }
            var view = ViewController.Instance;
            if (UiInput.EscapePressed || (UiInput.RightReleased && (view == null || !view.LastRightWasDrag)))
            {
                Cancel();
                return;
            }

            Vector2 world = cam.ScreenToWorldPoint(UiInput.MousePosition);
            tile = Island.WorldToTile(world);
            hasTile = true;
            bool valid = request.valid(tile);
            var (at, flip) = (request.ghostAt ?? OnLand)(tile);
            ghost.transform.position = at;
            ghost.flipX = flip;
            ghost.color = valid ? Ok : Bad;

            var def = request.light;
            int lit = 0;
            if (def != null)
            {
                var center = (Vector2)at + def.lightOffset;
                ring.transform.position = new Vector3(center.x, center.y, 0f);
                foreach (var p in FarmPlot.All)
                    if ((p.LightProbe - center).sqrMagnitude <= def.radius * def.radius)
                        lit++;
            }
            ring.enabled = def != null;

            int covered = 0;
            if (request.area > 0)
            {
                square.transform.position = Island.TileCenter(tile);
                foreach (var p in FarmPlot.All)
                    if (Mathf.Abs(p.Cell.x - tile.c) <= request.area && Mathf.Abs(p.Cell.y - tile.r) <= request.area)
                        covered++;
            }
            square.enabled = request.area > 0;
            tooltip.Show(this, Describe(def, lit, valid) + (request.area > 0 ? "\n" + Loc.Get("ui.place.covers", covered) : ""));

            var ui = UiRoot.Instance;
            if (!startedThisFrame && UiInput.LeftPressed && (ui == null || !ui.PointerOverUi))
            {
                ConsumedFrame = Time.frameCount;
                Confirm(valid);
            }
            startedThisFrame = false;
        }

        string Describe(LampDefinition def, int lit, bool valid)
        {
            string s = string.IsNullOrEmpty(request.nameKey) ? "" : Loc.Get(request.nameKey);
            if (request.cost != null)
                s += "\n" + Loc.Get("ui.place.cost_any", CostText(request.cost));
            if (def != null)
                s += "\n" + Loc.Get("ui.place.lights", lit);
            if (!valid)
                s += "\n" + Loc.Get("ui.place.blocked");
            return s + "\n" + Loc.Get("ui.place.click");
        }

        /// <summary>Koszt w kilku walutach jako tekst: „30 monet, 10 drewna”.</summary>
        public static string CostText(List<CurrencyAmount> cost)
        {
            var parts = new List<string>();
            foreach (var c in cost)
                parts.Add(Loc.Get("cost." + c.type.ToString().ToLowerInvariant(), c.amount));
            return string.Join(", ", parts);
        }

        void Confirm(bool valid)
        {
            var game = GameLoop.Instance;
            if (!valid || game == null || !hasTile || request == null)
                return;
            if (request.cost != null && !game.Wallet.TrySpend(request.cost))
            {
                GameEvents.RaiseToast("ui.place.need", CostText(request.cost));
                return;
            }
            GameEvents.PlaySound("place");
            if (!request.confirm(tile))
            {
                if (request.cost != null)
                    foreach (var c in request.cost)
                        game.Wallet.Add(c.type, c.amount);
                return;
            }
            Cancel();
        }

        /// <summary>Przerywany kwadrat zasięgu budowli (kafelki wokół: promień 1 = 3x3), tworzony raz na promień.</summary>
        Sprite Square(int tiles)
        {
            if (squares.TryGetValue(tiles, out var cached))
                return cached;
            int size = (2 * tiles + 1) * (int)Ppu;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    if (!edge || ((x + y) / 2) % 3 == 2)
                        continue;   // obwód co 3 piksele: dwa pełne, jeden pusty
                    int i = (y * size + x) * 4;
                    px[i] = px[i + 1] = px[i + 2] = px[i + 3] = 255;
                }
            tex.LoadRawTextureData(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            squares[tiles] = sprite;
            return sprite;
        }

        /// <summary>Przerywany okrąg zasięgu światła (piksele co 3: dwa pełne, jeden pusty), tworzony raz na promień.</summary>
        Sprite Ring(int r)
        {
            if (r <= 0)
                return null;
            if (rings.TryGetValue(r, out var cached))
                return cached;
            int size = 2 * r + 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new byte[size * size * 4];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(d - r) > 0.5f)
                        continue;
                    // przerywanie wg kąta, żeby odcinki były równe na całym obwodzie
                    float ang = Mathf.Atan2(dy, dx) * r;
                    int step = Mathf.FloorToInt(ang / 2f);
                    if (((step % 3) + 3) % 3 == 2)
                        continue;
                    int i = (y * size + x) * 4;
                    px[i] = px[i + 1] = px[i + 2] = px[i + 3] = 255;
                }
            tex.LoadRawTextureData(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            rings[r] = sprite;
            return sprite;
        }
    }
}
