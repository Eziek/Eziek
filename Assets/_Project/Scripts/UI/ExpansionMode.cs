using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Tryb rozbudowy (GDD §13.5): kafelki przy lądzie mają przerywane ramki z ceną albo „?” (znalezisko),
    /// wolna trawa ma ramki „przekop na grządkę”. Najechanie = dymek z kosztem, LPM = kup, PPM lub Esc = wyjście.
    /// </summary>
    public class ExpansionMode : MonoBehaviour
    {
        const int FrameOrder = 4000;

        RectTransform canvasRect;
        RectTransform labelsRoot;
        GameObject panel;
        TextMeshProUGUI panelText;
        TooltipView tooltip;
        Transform worldRoot;
        readonly Dictionary<TilePos, TileFrame> frames = new Dictionary<TilePos, TileFrame>();
        readonly Dictionary<TilePos, TextMeshProUGUI> labels = new Dictionary<TilePos, TextMeshProUGUI>();
        TileFrame hovered;
        bool active;

        public bool IsActive => active;

        public static ExpansionMode Create(RectTransform canvasRoot, TooltipView tooltip)
        {
            var root = Ui.Stretch(Ui.Rect("Expansion", canvasRoot));
            var view = root.gameObject.AddComponent<ExpansionMode>();
            view.canvasRect = canvasRoot;
            view.tooltip = tooltip;
            view.labelsRoot = root;
            var img = Ui.Img(root, "Panel", Ui.Theme.tooltip, true);
            Ui.Place(img.rectTransform, Ui.BottomLeft, Ui.BottomLeft, new Vector2(4, 30), new Vector2(120, 16));
            view.panel = img.gameObject;
            view.panelText = Ui.Txt(img.transform, "Text", Ui.Theme.textLight);
            Ui.Place(view.panelText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -4), new Vector2(200, 10));
            view.panel.SetActive(false);
            return view;
        }

        public void Toggle()
        {
            if (active) Close();
            else Open();
        }

        public void Open()
        {
            var island = Island.Instance;
            if (island == null || !island.Ready)
                return;
            active = true;
            island.Changed += Rebuild;
            panel.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            if (!active)
                return;
            active = false;
            if (Island.Instance != null)
                Island.Instance.Changed -= Rebuild;
            ClearFrames();
            panel.SetActive(false);
            tooltip.Hide(this);
        }

        void OnDisable() => Close();

        void ClearFrames()
        {
            foreach (var f in frames.Values)
                if (f != null)
                {
                    f.gameObject.SetActive(false);
                    Destroy(f.gameObject);
                }
            frames.Clear();
            foreach (var l in labels.Values)
                if (l != null)
                    Destroy(l.gameObject);
            labels.Clear();
            hovered = null;
        }

        void Rebuild()
        {
            var island = Island.Instance;
            if (!active || island == null)
                return;
            ClearFrames();
            if (worldRoot == null)
                worldRoot = new GameObject("ExpansionFrames").transform;
            var art = island.Art;
            foreach (var p in island.Map.Candidates())
            {
                if (island.SeaBlocked(p))
                    continue;   // pod pomostem
                MakeFrame(p, true, art);
                var label = Ui.Txt(labelsRoot, "Price", Ui.Theme.textLight, TextAlignmentOptions.Center);
                Ui.Place(label.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(24, 10));
                labels[p] = label;
            }
            foreach (var p in island.FreeTiles())
                MakeFrame(p, false, art);
            panelText.text = Loc.Get("ui.expand.panel", island.Map.Count);
            ((RectTransform)panel.transform).sizeDelta = new Vector2(Ui.Width(panelText) + 10, 16);
            RefreshLabels();
        }

        void MakeFrame(TilePos p, bool buy, IslandArt art)
        {
            var go = new GameObject(buy ? $"Buy_{p}" : $"Dig_{p}");
            go.transform.SetParent(worldRoot, false);
            go.transform.position = Island.TileCenter(p);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art != null ? (buy ? art.frameBuy : art.frameDig) : null;
            sr.sortingOrder = FrameOrder;
            if (art != null && art.glow != null)
                sr.sharedMaterial = art.glow;   // ramki widać także w nocy
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            var frame = go.AddComponent<TileFrame>();
            frame.Init(this, p, buy, sr);
            frames[p] = frame;
        }

        void RefreshLabels()
        {
            var island = Island.Instance;
            var game = GameLoop.Instance;
            if (island == null)
                return;
            bool afford = game != null && island.CanAffordTile(game.Wallet, game.Essence);
            string price = island.NextTileCoins.ToString("N0", LocCore.Culture(Loc.Language));
            foreach (var kv in labels)
            {
                bool find = island.Map.Plan != null && island.Map.Plan.FindAt(kv.Key) != FindKind.None;
                kv.Value.text = find ? "?" : price;
                kv.Value.color = find ? Ui.Theme.textWisp : (afford ? Ui.Theme.textLight : Ui.Theme.textWarn);
            }
        }

        public void Click(TileFrame f)
        {
            var island = Island.Instance;
            var game = GameLoop.Instance;
            if (!active || island == null || game == null || f == null)
                return;
            if (f.IsBuy)
            {
                if (!island.TryBuy(f.Tile, game.Wallet, game.Essence))
                    GameEvents.RaiseToast("ui.expand.need", island.NextTileCoins, island.NextTileEssence);
            }
            else if (!island.TryDig(f.Tile, game.Wallet))
                GameEvents.RaiseToast("ui.dig.need", island.DigCoins);
        }

        void Update()
        {
            if (!active)
                return;
            var view = ViewController.Instance;
            if (UiInput.EscapePressed || (UiInput.RightReleased && (view == null || !view.LastRightWasDrag)))
            {
                Close();
                return;
            }
            UpdateHover();
        }

        void LateUpdate()
        {
            if (!active)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            foreach (var kv in labels)
            {
                Vector2 screen = cam.WorldToScreenPoint(Island.TileCenter(kv.Key));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                kv.Value.rectTransform.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y) - 1);
            }
            RefreshLabels();
        }

        void UpdateHover()
        {
            TileFrame now = null;
            var cam = Camera.main;
            var ui = UiRoot.Instance;
            if (cam != null && (ui == null || !ui.PointerOverUi))
            {
                Vector2 world = cam.ScreenToWorldPoint(UiInput.MousePosition);
                var hit = Wyspa.Input.ClickRouter.Pick(world);
                if (hit != null)
                    hit.TryGetComponent(out now);
            }
            if (now != hovered)
            {
                SetHover(hovered, false);
                SetHover(now, true);
                hovered = now;
            }
            if (hovered == null)
            {
                tooltip.Hide(this);
                return;
            }
            tooltip.Show(this, Describe(hovered));
        }

        void SetHover(TileFrame f, bool on)
        {
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (f == null || f.Renderer == null || art == null)
                return;
            f.Renderer.sprite = f.IsBuy ? (on ? art.frameBuyHover : art.frameBuy) : (on ? art.frameDigHover : art.frameDig);
        }

        string Describe(TileFrame f)
        {
            var island = Island.Instance;
            if (f.IsBuy)
            {
                string s = Loc.Get("ui.expand.title") + "\n" + Loc.Get("ui.expand.cost", island.NextTileCoins, island.NextTileEssence);
                if (island.Map.Plan != null && island.Map.Plan.FindAt(f.Tile) != FindKind.None)
                    s += "\n" + Loc.Get("ui.expand.find");
                return s + "\n" + Loc.Get("ui.expand.click");
            }
            return Loc.Get("ui.dig.title") + "\n" + Loc.Get("ui.dig.cost", island.DigCoins) + "\n" + Loc.Get("ui.dig.click");
        }
    }
}
