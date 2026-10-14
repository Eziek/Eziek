using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Economy;
using Wyspa.Localization;

namespace Wyspa.UI
{
    /// <summary>
    /// Kolekcja Darów Mgły: kratka z ikonami w ramkach koloru rzadkości. Zdobyte dary w pełnym kolorze (kropki = poziom),
    /// niezdobyte wyszarzałe. Dymek: nazwa, rzadkość, efekt, poziom. Używa jej Księga (zakładka Rejs) i Przystań we mgle.
    /// </summary>
    public class GiftGrid
    {
        public const float Cell = 16f, Gap = 2f;
        static readonly Color Grey = new Color(0.32f, 0.3f, 0.34f, 0.75f);
        static readonly Color PipOn = new Color32(0xff, 0xe0, 0x8a, 0xff);
        static readonly Color PipOff = new Color32(0x33, 0x26, 0x3a, 0xff);
        static Sprite frame, white;

        public class Slot
        {
            public Prestige.Gift gift;
            public RectTransform rect;
            public Image icon, border, back;
            public readonly List<Image> pips = new List<Image>();
        }

        public readonly List<Slot> Slots = new List<Slot>();
        public RectTransform Root { get; private set; }

        /// <summary>Wysokość kratki dla danej szerokości (ile wierszy się zmieści).</summary>
        public static float HeightFor(float width)
        {
            int cols = Columns(width);
            int rows = (Prestige.Gifts.Length + cols - 1) / cols;
            return rows * (Cell + Gap) - Gap;
        }

        static int Columns(float width) => Mathf.Max(1, Mathf.FloorToInt((width + Gap) / (Cell + Gap)));

        public static GiftGrid Create(RectTransform parent, Vector2 topLeft, float width, TooltipView tooltip)
        {
            var grid = new GiftGrid();
            int cols = Columns(width);
            float used = cols * (Cell + Gap) - Gap;
            grid.Root = Ui.Place(Ui.Rect("Gifts", parent), Ui.TopLeft, Ui.TopLeft, topLeft + new Vector2(Mathf.Floor((width - used) / 2f), 0f),
                                 new Vector2(used, HeightFor(width)));
            for (int i = 0; i < Prestige.Gifts.Length; i++)
            {
                var g = Prestige.Gifts[i];
                var slot = BuildCell(grid.Root, g, new Vector2((i % cols) * (Cell + Gap), -(i / cols) * (Cell + Gap)));
                slot.back.raycastTarget = true;
                slot.back.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Describe(g));
                grid.Slots.Add(slot);
            }
            grid.Refresh();
            return grid;
        }

        /// <summary>Jedna komórka (tło, ikona, ramka rzadkości, kropki poziomów). Też klony do animacji losowania.</summary>
        public static Slot BuildCell(Transform parent, Prestige.Gift g, Vector2 topLeft)
        {
            var t = Ui.Theme;
            var slot = new Slot { gift = g };
            slot.back = Ui.Img(parent, "Gift_" + g.id, t.slot, true);
            slot.rect = Ui.Place(slot.back.rectTransform, Ui.TopLeft, Ui.TopLeft, topLeft, new Vector2(Cell, Cell));
            slot.icon = Ui.Img(slot.rect, "Icon", Icon(g), false);
            Ui.Place(slot.icon.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(12, 12));
            slot.border = Ui.Img(slot.rect, "Rarity", Frame(), false);
            Ui.Stretch(slot.border.rectTransform);
            if (g.MaxLevel > 1)
            {
                // kropki poziomów na dolnej krawędzi (po 1 px z przerwą)
                float x0 = Mathf.Floor((Cell - (g.MaxLevel * 2 - 1)) / 2f);
                for (int i = 0; i < g.MaxLevel; i++)
                {
                    var pip = Ui.Img(slot.rect, "Pip", White(), false);
                    Ui.Place(pip.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(x0 + i * 2, 1), new Vector2(1, 1));
                    slot.pips.Add(pip);
                }
            }
            return slot;
        }

        /// <summary>Własna ikona daru (tools/pixel-proto/gift_icons.py), a bez niej zwykła ikona UI.</summary>
        public static Sprite Icon(Prestige.Gift g)
        {
            var t = Ui.Theme;
            if (t == null)
                return null;
            var own = t.Icon(g.IconKey);
            return own != null ? own : t.Icon(g.icon);
        }

        public void Refresh()
        {
            foreach (var s in Slots)
                Paint(s, 1f);
        }

        /// <summary>Kolory komórki według stanu daru; alpha przygasza całość (np. na czas losowania).</summary>
        public static void Paint(Slot s, float alpha)
        {
            var g = s.gift;
            int lvl = Prestige.Level(g.id);
            bool owned = lvl > 0;
            var rc = Prestige.RarityColor(g.rarity);
            s.icon.color = owned ? new Color(1f, 1f, 1f, alpha) : new Color(Grey.r, Grey.g, Grey.b, Grey.a * alpha);
            s.border.color = new Color(rc.r, rc.g, rc.b, (owned ? 1f : 0.35f) * alpha);
            s.back.color = new Color(1f, 1f, 1f, alpha);
            for (int i = 0; i < s.pips.Count; i++)
            {
                var c = i < lvl ? PipOn : PipOff;
                s.pips[i].color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        /// <summary>Dymek daru: nazwa i rzadkość, efekt (obecny poziom albo pierwszy), poziom albo „jeszcze nie zdobyty”.</summary>
        public static string Describe(Prestige.Gift g)
        {
            int lvl = Prestige.Level(g.id);
            float v = g.values[Mathf.Clamp(lvl - 1, 0, g.values.Length - 1)];
            string head = Loc.Get(g.NameKey) + " - " + Loc.Get(Prestige.RarityKey(g.rarity));
            string effect = Loc.Get(g.DescKey, g.percent ? Mathf.Round(v * 1000f) / 10f : v);
            string state = lvl > 0 ? Loc.Get("ui.upgrade.level", lvl, g.MaxLevel) : Loc.Get("ui.gift.not_owned");
            return head + "\n" + effect + "\n" + state;
        }

        /// <summary>Ramka 16x16: obrys 1 px ze ściętymi rogami, biała (kolor nadaje Image).</summary>
        public static Sprite Frame()
        {
            if (frame != null)
                return frame;
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "GiftFrame" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    bool corner = (x == 0 || x == n - 1) && (y == 0 || y == n - 1);
                    px[y * n + x] = edge && !corner ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            frame = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            return frame;
        }

        public static Sprite White()
        {
            if (white != null)
                return white;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "White" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false);
            white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            return white;
        }
    }
}
