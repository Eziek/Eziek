using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
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

        TooltipView tooltip;
        SpriteRenderer ghost;
        SpriteRenderer ring;
        LampDefinition buying;
        TileObject moving;
        readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
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

        /// <summary>Lampa ze sklepu: płaci się dopiero przy postawieniu.</summary>
        public void StartBuy(LampDefinition def)
        {
            if (def == null)
                return;
            Begin(def, null, def.sprite);
        }

        public void StartMove(TileObject obj)
        {
            if (obj == null || !obj.NeedsTile)
                return;
            var sr = obj.GetComponent<SpriteRenderer>();
            var lamp = obj.GetComponent<Lamp>();
            Begin(lamp != null ? lamp.Definition : null, obj, sr != null ? sr.sprite : null);
        }

        void Begin(LampDefinition lamp, TileObject move, Sprite sprite)
        {
            buying = move == null ? lamp : null;
            moving = move;
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (art != null && art.glow != null)
            {
                ghost.sharedMaterial = art.glow;   // duch i okrąg widać także w nocy
                ring.sharedMaterial = art.glow;
            }
            ghost.sprite = sprite;
            var def = lamp;
            ring.sprite = def != null ? Ring(Mathf.RoundToInt(def.radius * Ppu)) : null;
            ring.color = RingColor;
            ghost.transform.parent.gameObject.SetActive(true);
            Active = true;
            startedThisFrame = true;
            hasTile = false;
        }

        public void Cancel()
        {
            Active = false;
            buying = null;
            moving = null;
            if (ghost != null)
                ghost.transform.parent.gameObject.SetActive(false);
            tooltip.Hide(this);
        }

        void OnDisable() => Cancel();

        LampDefinition Lamp => buying != null ? buying : (moving != null && moving.GetComponent<Lamp>() != null ? moving.GetComponent<Lamp>().Definition : null);

        void Update()
        {
            if (!Active)
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
            bool valid = island.IsFree(tile) || (moving != null && moving.Placed && moving.Tile == tile);
            var at = Island.TileAnchor(tile, 13, out _);
            ghost.transform.position = at;
            ghost.color = valid ? Ok : Bad;

            var def = Lamp;
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
            tooltip.Show(this, Describe(def, lit, valid));

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
            string name = buying != null ? Loc.Get(buying.NameKey) : (moving != null ? Loc.Get(moving.NameKey) : "");
            string s = name;
            if (buying != null)
                s += "\n" + Loc.Get("ui.place.cost", buying.price);
            if (def != null)
                s += "\n" + Loc.Get("ui.place.lights", lit);
            if (!valid)
                s += "\n" + Loc.Get("ui.place.blocked");
            return s + "\n" + Loc.Get("ui.place.click");
        }

        void Confirm(bool valid)
        {
            var island = Island.Instance;
            var game = GameLoop.Instance;
            if (!valid || island == null || game == null || !hasTile)
                return;
            if (buying != null)
            {
                if (!game.Wallet.TrySpend(buying.price))
                {
                    GameEvents.RaiseToast("ui.dig.need", buying.price);
                    return;
                }
                if (island.SpawnLamp(buying, tile) == null)
                {
                    game.Wallet.Add(buying.price);
                    return;
                }
                GameEvents.RaiseToast("ui.toast.placed", Loc.Get(buying.NameKey));
            }
            else if (moving != null)
                island.MoveObject(moving, tile);
            Cancel();
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
