using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Visual;

namespace Wyspa.World
{
    [Serializable] public class TileSave { public int c, r; public TileKind kind; }
    [Serializable] public class DecoSave { public int c, r; public DecoKind kind; }
    [Serializable] public class ObjectSave { public string id; public int c, r; public string lamp; }

    [Serializable]
    public class IslandSave
    {
        public int seed;
        public int purchased;
        public List<TileSave> tiles = new List<TileSave>();
        public List<DecoSave> decorations = new List<DecoSave>();
        public List<ObjectSave> objects = new List<ObjectSave>();
    }

    /// <summary>
    /// Wyspa z kafelków (GDD §13): mapa + obraz malowany w locie (ląd, woda, mgła), grządki na kafelkach ziemi,
    /// dekoracje, rozmieszczanie obiektów, zakup kafelków i znaleziska. Współrzędne świata: kafelek 16 px, środek świata w (0,0).
    /// </summary>
    public class Island : MonoBehaviour
    {
        public static Island Instance { get; private set; }

        const float Ppu = 16f;
        const int Half = IslandPainter.Size / 2;   // 320 px: środek tekstury = (0,0) w świecie
        const string StarterLamp = "lantern";
        const float NewTileDecoChance = 0.25f;   // §14.1: dekoracje na nowo odkrytych kafelkach

        [SerializeField] IslandArt art;
        [SerializeField] SpriteRenderer islandRenderer;
        [SerializeField] SpriteRenderer waterRenderer;
        [SerializeField] SpriteRenderer fogRenderer;
        [SerializeField] WaterAnimator water;
        [SerializeField] List<TileObject> objects = new List<TileObject>();

        readonly IslandMap map = new IslandMap();
        readonly Dictionary<TilePos, FarmPlot> plots = new Dictionary<TilePos, FarmPlot>();
        readonly Dictionary<TilePos, Decoration> decorations = new Dictionary<TilePos, Decoration>();
        readonly List<TileObject> spawned = new List<TileObject>();   // lampy postawione w trakcie gry
        int lampCounter;
        bool claiming;
        readonly Dictionary<TilePos, TileObject> occupied = new Dictionary<TilePos, TileObject>();
        readonly Dictionary<string, TilePos> savedObjectTiles = new Dictionary<string, TilePos>();
        Texture2D islandTex, waterTex, dataTex;
        Transform plotsRoot, decoRoot;
        IslandPicture picture;

        public IslandMap Map => map;
        public IslandArt Art => art;
        public bool Ready => map.Count > 0;
        /// <summary>Po każdym przemalowaniu wyspy (zakup kafelka, nowy świat, wczytanie).</summary>
        public event Action Rebuilt;
        /// <summary>Po każdej zmianie kafelków lub tego, co na nich stoi (ramki trybu rozbudowy).</summary>
        public event Action Changed;

        public void Setup(IslandArt islandArt, SpriteRenderer islandSr, SpriteRenderer waterSr, SpriteRenderer fogSr,
                          WaterAnimator waterAnimator, List<TileObject> tileObjects)
        {
            art = islandArt;
            islandRenderer = islandSr;
            waterRenderer = waterSr;
            fogRenderer = fogSr;
            water = waterAnimator;
            objects = tileObjects;
        }

        void Awake()
        {
            Instance = this;
            plotsRoot = new GameObject("Plots").transform;
            plotsRoot.SetParent(transform, false);
            decoRoot = new GameObject("Decorations").transform;
            decoRoot.SetParent(transform, false);
            foreach (var o in objects)
                if (o != null)
                    o.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ================================================================ współrzędne

        public static Vector3 PixelToWorld(float px, float py) => new Vector3((px - Half) / Ppu, (Half - py) / Ppu, 0f);

        /// <summary>Punkt zaczepienia sprite'a: środek kolumny px, dolna krawędź wiersza py (jak Anchor w WyspaSetup).</summary>
        public static Vector3 AnchorWorld(int px, int py) => new Vector3((px + 0.5f - Half) / Ppu, (Half - (py + 1)) / Ppu, 0f);

        public static Vector3 TileCenter(TilePos p)
        {
            var (x, y) = IslandPainter.TileOrigin(p);
            return PixelToWorld(x + 8, y + 8);
        }

        /// <summary>Podstawa obiektu na kafelku: środek kafelka w poziomie, wiersz row od górnej krawędzi.</summary>
        public static Vector3 TileAnchor(TilePos p, int row, out int sortingOrder)
        {
            var (x, y) = IslandPainter.TileOrigin(p);
            sortingOrder = y + row;
            return AnchorWorld(x + 8, y + row);
        }

        public static TilePos WorldToTile(Vector2 world)
        {
            float px = world.x * Ppu + Half, py = Half - world.y * Ppu;
            return new TilePos(Mathf.FloorToInt((px - IslandPainter.Margin) / IslandPainter.Tile),
                               Mathf.FloorToInt((py - IslandPainter.Margin) / IslandPainter.Tile));
        }

        /// <summary>Prostokąt lądu (z klifem) w jednostkach świata.</summary>
        public Rect LandRect
        {
            get
            {
                if (picture == null)
                    return new Rect(-2f, -2f, 4f, 4f);
                var a = PixelToWorld(picture.minX, picture.maxY + 1);
                var b = PixelToWorld(picture.maxX + 1, picture.minY);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
        }

        /// <summary>Miejsce na wodzie pod wyspą (łódź kupca-widma): środek lądu w poziomie, kawałek pod klifem.</summary>
        public Vector3 WaterBelow(int pixelsBelow)
        {
            if (picture == null)
                return new Vector3(0f, -3f, 0f);
            int cx = (picture.minX + picture.maxX) / 2;
            return AnchorWorld(cx, picture.maxY + pixelsBelow);
        }

        public IEnumerable<Transform> Trees
        {
            get
            {
                foreach (var d in decorations.Values)
                    if (d != null && !d.Removing && (d.Kind == DecoKind.TreeRound || d.Kind == DecoKind.Spruce))
                        yield return d.transform;
            }
        }

        public int SoilCount
        {
            get
            {
                int n = 0;
                foreach (var k in map.Tiles.Values)
                    if (k == TileKind.Soil) n++;
                return n;
            }
        }

        public bool Claiming => claiming;

        /// <summary>Grządki na sąsiednich kafelkach (4 kierunki) - do mutacji.</summary>
        public IEnumerable<FarmPlot> NeighbourPlots(FarmPlot plot)
        {
            if (plot == null)
                yield break;
            var t = new TilePos(plot.Cell.x, plot.Cell.y);
            foreach (var n in IslandMap.Neighbours(t))
                if (plots.TryGetValue(n, out var other) && other != null)
                    yield return other;
        }

        public bool IsFree(TilePos p) => map.Owns(p) && map.KindAt(p) == TileKind.Grass
                                         && !decorations.ContainsKey(p) && !occupied.ContainsKey(p);

        public List<TilePos> FreeTiles()
        {
            var list = new List<TilePos>();
            foreach (var p in map.Tiles.Keys)
                if (IsFree(p))
                    list.Add(p);
            return list;
        }

        // ================================================================ świat

        public void NewWorld(int seed)
        {
            Clear();
            map.NewWorld(seed);
            var plan = map.Plan;
            AddDecoration(plan.startDecoTile, plan.startDeco);
            Rebuild();
            SyncPlots();
            // lampa startowa na wolnym kafelku startu (GDD §13.1: 2 grządki, obiekt, latarnia)
            var lantern = art != null ? art.Lamp(StarterLamp) : null;
            if (lantern != null && NearestFree(out var lampTile))
                SpawnLamp(lantern, lampTile);
            PlacePending();   // obiekty chciane wcześniej (np. skrzynia po wczytaniu ulepszeń)
            Changed?.Invoke();
        }

        public void Restore(IslandSave save)
        {
            Clear();
            var tiles = new List<(TilePos, TileKind)>();
            foreach (var t in save.tiles)
                tiles.Add((new TilePos(t.c, t.r), t.kind));
            map.Restore(save.seed, tiles, save.purchased);
            foreach (var d in save.decorations)
                AddDecoration(new TilePos(d.c, d.r), d.kind);
            foreach (var o in save.objects)
                if (!string.IsNullOrEmpty(o.id))
                    savedObjectTiles[o.id] = new TilePos(o.c, o.r);
            Rebuild();
            SyncPlots();
            foreach (var o in save.objects)
            {
                // lampy: kupione (lamp = id definicji) albo startowa latarnia z zapisu 3b (id lamp_lantern)
                string lampId = !string.IsNullOrEmpty(o.lamp) ? o.lamp : (o.id == "lamp_lantern" ? StarterLamp : null);
                var def = lampId != null && art != null ? art.Lamp(lampId) : null;
                var tile = new TilePos(o.c, o.r);
                if (def != null && IsFree(tile))
                    SpawnLamp(def, tile, o.id);
            }
            PlacePending();   // obiekty chciane wcześniej (np. skrzynia po wczytaniu ulepszeń)
            Changed?.Invoke();
        }

        public IslandSave Snapshot()
        {
            var s = new IslandSave { seed = map.Seed, purchased = map.Purchased };
            foreach (var kv in map.Tiles)
                s.tiles.Add(new TileSave { c = kv.Key.c, r = kv.Key.r, kind = kv.Value });
            foreach (var kv in decorations)
                if (kv.Value != null)
                    s.decorations.Add(new DecoSave { c = kv.Key.c, r = kv.Key.r, kind = kv.Value.Kind });
            foreach (var o in AllObjects())
                if (o.Placed && o.NeedsTile)
                    s.objects.Add(new ObjectSave { id = o.Id, c = o.Tile.c, r = o.Tile.r, lamp = o.LampId });
            return s;
        }

        void Clear()
        {
            // SetActive(false) od razu wypisuje grządki z FarmPlot.All (Destroy działa dopiero na końcu klatki)
            foreach (var p in plots.Values)
                if (p != null) { p.gameObject.SetActive(false); Destroy(p.gameObject); }
            plots.Clear();
            foreach (var d in decorations.Values)
                if (d != null) { d.gameObject.SetActive(false); Destroy(d.gameObject); }
            decorations.Clear();
            foreach (var o in spawned)
                if (o != null) { o.gameObject.SetActive(false); Destroy(o.gameObject); }
            spawned.Clear();
            lampCounter = 0;
            claiming = false;
            StopAllCoroutines();
            occupied.Clear();
            savedObjectTiles.Clear();
            foreach (var o in objects)
                if (o != null)
                {
                    o.Placed = false;
                    o.gameObject.SetActive(false);
                }
        }

        IEnumerable<TileObject> AllObjects()
        {
            foreach (var o in objects)
                if (o != null) yield return o;
            foreach (var o in spawned)
                if (o != null) yield return o;
        }

        // ================================================================ zakup i przekopywanie

        public int NextTileCoins => IslandEconomy.TileCoins(map.Purchased);
        public int NextTileEssence => IslandEconomy.TileEssence(map.Purchased);
        public int DigCoins => IslandEconomy.DigCoins(SoilCount);

        public bool CanAffordTile(Wallet wallet, EssenceStore essence) =>
            wallet != null && essence != null && wallet.Coins >= NextTileCoins && essence.Amount >= NextTileEssence;

        /// <summary>Kupno kafelka: monety + Esencja (GDD §13.2). Po zakupie mgła się rozstępuje i wypada znalezisko.</summary>
        public bool TryBuy(TilePos p, Wallet wallet, EssenceStore essence)
        {
            if (claiming || !map.CanBuy(p) || !CanAffordTile(wallet, essence))
                return false;
            int coins = NextTileCoins, ess = NextTileEssence;
            if (!wallet.TrySpend(coins))
                return false;
            if (!essence.TryConsume(ess))
            {
                wallet.Add(coins);
                return false;
            }
            var find = map.Buy(p);
            StartCoroutine(Claim(p, find, wallet));
            return true;
        }

        /// <summary>Ląd wynurza się z wody w 3 skokowych klatkach, potem piana, znalezisko i (czasem) dekoracja (§13.5).</summary>
        IEnumerator Claim(TilePos p, FindKind find, Wallet wallet)
        {
            claiming = true;
            IslandFx.TileClaimed(TileCenter(p), art != null ? art.foam : null);
            var wait = new WaitForSecondsRealtime(0.1f);
            foreach (int grow in new[] { 6, 11 })
            {
                Rebuild(p, grow);
                yield return wait;
            }
            Rebuild();
            claiming = false;
            if (WHash.Value(p.c, p.r, map.Seed + 911) < NewTileDecoChance)
                AddDecoration(p, NewTileDeco(p));
            PlacePending();
            GrantFind(p, find, wallet);
            Changed?.Invoke();
        }

        /// <summary>Dekoracja nowego kafelka: drzewa i krzaki częściej, martwe drzewo rzadko.</summary>
        DecoKind NewTileDeco(TilePos p)
        {
            double v = WHash.Value(p.c, p.r, map.Seed + 912);
            if (v < 0.30) return DecoKind.TreeRound;
            if (v < 0.50) return DecoKind.Spruce;
            if (v < 0.75) return DecoKind.Bush;
            if (v < 0.95) return DecoKind.Rock;
            return DecoKind.DeadTree;
        }

        /// <summary>Przekopanie wolnego kafelka trawy na grządkę (tanio, §13.3).</summary>
        public bool TryDig(TilePos p, Wallet wallet)
        {
            if (!IsFree(p) || wallet == null || !wallet.TrySpend(DigCoins))
                return false;
            map.SetKind(p, TileKind.Soil);
            SyncPlots();
            if (plots.TryGetValue(p, out var plot))
                IslandFx.Planted(plot.CropPosition, 0);
            Changed?.Invoke();
            return true;
        }

        void GrantFind(TilePos p, FindKind find, Wallet wallet)
        {
            if (find == FindKind.None)
                return;
            var at = TileCenter(p);
            uint h = WHash.Hash(p.c, p.r, map.Seed + 77);
            switch (find)
            {
                case FindKind.Coins:
                    int coins = 15 + (int)(h % 21) + map.Purchased * 2;
                    wallet.Add(coins);
                    GameEvents.RaiseCurrencyGained(at, CurrencyType.Coins, coins);
                    GameEvents.RaiseToast("toast.find.coins", coins);
                    break;
                case FindKind.NightSeed:
                    wallet.Add(CurrencyType.NightHarvest, 2);
                    GameEvents.RaiseCurrencyGained(at, CurrencyType.NightHarvest, 2);
                    GameEvents.RaiseToast("toast.find.nightseed", 2);
                    break;
                case FindKind.Shard:
                    wallet.Add(CurrencyType.StarShard, 1);
                    GameEvents.RaiseCurrencyGained(at, CurrencyType.StarShard, 1);
                    GameEvents.RaiseToast("toast.find.shard");
                    break;
                case FindKind.Lore:
                    GameEvents.RaiseToast("toast.find.lore", Localization.Loc.Get("lore." + (1 + h % 6)));
                    break;
                case FindKind.Spring:
                    GameEvents.RaiseToast("toast.find.spring");
                    break;
                case FindKind.Monolith:
                    GameEvents.RaiseToast("toast.find.monolith");
                    break;
            }
            IslandFx.FindPopped(at);
        }

        // ================================================================ obraz wyspy

        void Rebuild(TilePos? rising = null, int grow = IslandPainter.Tile)
        {
            picture = IslandPainter.Paint(map, rising, grow);
            int s = picture.width;
            if (islandTex == null)
            {
                islandTex = NewTexture(s, false, "IslandLand");
                waterTex = NewTexture(s, false, "IslandWater");
                dataTex = NewTexture(s, true, "IslandWaterData");
                var rect = new Rect(0, 0, s, s);
                var pivot = new Vector2(0.5f, 0.5f);
                if (islandRenderer != null) islandRenderer.sprite = Sprite.Create(islandTex, rect, pivot, Ppu, 0, SpriteMeshType.FullRect);
                if (waterRenderer != null) waterRenderer.sprite = Sprite.Create(waterTex, rect, pivot, Ppu, 0, SpriteMeshType.FullRect);
                if (fogRenderer != null) fogRenderer.sprite = Sprite.Create(dataTex, rect, pivot, Ppu, 0, SpriteMeshType.FullRect);
            }
            islandTex.LoadRawTextureData(picture.island);
            islandTex.Apply(false);
            waterTex.LoadRawTextureData(picture.waterBase);
            waterTex.Apply(false);
            dataTex.LoadRawTextureData(picture.data);
            dataTex.Apply(false);
            if (water != null)
                water.SetData(dataTex, picture);
            Rebuilt?.Invoke();
        }

        static Texture2D NewTexture(int size, bool linear, string name)
        {
            // linear = true: dane (odległość, klasa) bez korekcji gamma
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false, linear)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            return t;
        }

        // ================================================================ grządki i dekoracje

        void SyncPlots()
        {
            foreach (var kv in map.Tiles)
                if (kv.Value == TileKind.Soil && !plots.ContainsKey(kv.Key))
                    plots[kv.Key] = CreatePlot(kv.Key);
        }

        FarmPlot CreatePlot(TilePos p)
        {
            var go = new GameObject($"Plot_{p.c}_{p.r}");
            go.transform.SetParent(plotsRoot, false);
            go.transform.position = TileCenter(p);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art != null ? art.plotTile : null;
            sr.sortingOrder = -1500;
            var plot = go.AddComponent<FarmPlot>();   // dokłada BoxCollider2D (RequireComponent)
            var col = go.GetComponent<BoxCollider2D>();
            if (col != null)
                col.size = new Vector2(1f, 1f);

            var crop = NewSprite("Crop", go.transform, TileAnchor(p, 12, out int order), order);
            var glow = NewSprite("Glow", crop.transform, crop.transform.position, order + 1);
            if (art != null && art.glow != null)
                glow.sharedMaterial = art.glow;
            UseFx(crop, art != null ? art.swayCrop : null);
            UseFx(glow, art != null ? art.swayCropGlow : null);
            var fx = crop.gameObject.AddComponent<SpriteFx>();
            fx.Setup(0.15f, crop, glow);
            plot.Setup($"plot_{p.c}_{p.r}", crop, glow, fx);
            plot.Cell = new Vector2Int(p.c, p.r);
            plot.RefreshVisual();
            return plot;
        }

        void AddDecoration(TilePos p, DecoKind kind)
        {
            var a = art != null ? art.Deco(kind) : null;
            var sr = NewSprite("Deco_" + kind, decoRoot, TileAnchor(p, 13, out int order), order);
            sr.sprite = a != null ? a.sprite : null;
            if (a != null && a.sway)
            {
                UseFx(sr, art.swayTree);
                sr.gameObject.AddComponent<SpriteFx>().Setup(0.05f, sr);
            }
            AddTileCollider(sr.gameObject);
            var deco = sr.gameObject.AddComponent<Decoration>();
            deco.Init(this, p, kind, a, sr);
            decorations[p] = deco;
        }

        /// <summary>Klikalny obszar = kafelek pod obiektem (podstawa stoi 6 px pod środkiem kafelka).</summary>
        static void AddTileCollider(GameObject go)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null)
                col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.offset = new Vector2(-0.5f / Ppu, 6f / Ppu);
        }

        // ================================================================ usuwanie dekoracji (§14)

        public bool TryRemove(Decoration d)
        {
            var game = GameLoop.Instance;
            if (d == null || d.Removing || d.Art == null || !d.Art.removable || game == null)
                return false;
            if (!game.Essence.TryConsume(d.Art.essenceCost))
            {
                GameEvents.RaiseToast("ui.deco.need", d.Art.essenceCost);
                return false;
            }
            var at = d.transform.position + new Vector3(0f, 8f / Ppu, 0f);
            var a = d.Art;
            game.Wallet.Add(a.reward, a.rewardAmount);
            GameEvents.RaiseCurrencyGained(at, a.reward, a.rewardAmount);
            if (a.bonusAmount > 0 && UnityEngine.Random.value < a.bonusChance)
            {
                game.Wallet.Add(a.bonus, a.bonusAmount);
                GameEvents.RaiseCurrencyGained(at, a.bonus, a.bonusAmount);
                if (!string.IsNullOrEmpty(a.bonusToast))
                    GameEvents.RaiseToast(a.bonusToast, a.bonusAmount);
            }
            d.PlayRemoval(() => FinishRemoval(d));
            return true;
        }

        void FinishRemoval(Decoration d)
        {
            if (d == null)
                return;
            var tile = d.Tile;
            var a = d.Art;
            if (decorations.TryGetValue(tile, out var cur) && cur == d)
                decorations.Remove(tile);
            d.gameObject.SetActive(false);
            Destroy(d.gameObject);
            if (a != null && a.hasRemains)
                AddDecoration(tile, a.remains);
            PlacePending();
            Changed?.Invoke();
        }

        // ================================================================ lampy (sklep, tryb stawiania)

        /// <summary>Lampa postawiona w trakcie gry: sprite, piksele emisyjne, światło 2D i komponent Lamp.</summary>
        public TileObject SpawnLamp(LampDefinition def, TilePos tile, string id = null)
        {
            if (def == null || !IsFree(tile))
                return null;
            var go = new GameObject("Lamp_" + def.id);
            go.transform.SetParent(decoRoot, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = def.sprite;
            var glow = NewSprite("Glow", go.transform, Vector3.zero, 1);
            glow.transform.localPosition = Vector3.zero;
            glow.sprite = def.glowSprite;
            if (art != null && art.glow != null)
                glow.sharedMaterial = art.glow;
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(def.lightOffset.x, def.lightOffset.y, 0f);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = def.radius;
            light.pointLightInnerRadius = def.radius * 0.25f;
            light.color = def.color;
            light.intensity = def.intensity;
            light.falloffIntensity = 0.6f;
            var lamp = go.AddComponent<Lamp>();
            lamp.Setup(def, light, glow);
            AddTileCollider(go);
            var obj = go.AddComponent<TileObject>();
            if (string.IsNullOrEmpty(id))
                id = $"lamp_{def.id}_{++lampCounter}";
            else
                lampCounter++;
            obj.Setup(id, def.NameKey);
            obj.LampId = def.id;
            obj.Wanted = true;
            spawned.Add(obj);
            PutOnTile(obj, tile);
            Changed?.Invoke();
            return obj;
        }

        /// <summary>Przeniesienie obiektu na wolny kafelek (tryb stawiania). Obiekty zakotwiczone idą za nim.</summary>
        public bool MoveObject(TileObject obj, TilePos tile)
        {
            if (obj == null || !obj.NeedsTile || !(IsFree(tile) || (obj.Placed && obj.Tile == tile)))
                return false;
            if (obj.Placed && occupied.TryGetValue(obj.Tile, out var o) && o == obj)
                occupied.Remove(obj.Tile);
            PutOnTile(obj, tile);
            foreach (var f in AllObjects())
                if (f.Anchor == obj && f.Placed)
                {
                    f.Placed = false;
                    TryPlace(f, false);
                }
            PlacePending();
            Changed?.Invoke();
            return true;
        }

        void PutOnTile(TileObject obj, TilePos tile)
        {
            occupied[tile] = obj;
            obj.Tile = tile;
            savedObjectTiles[obj.Id] = tile;
            var at = TileAnchor(tile, obj.BaseRow, out int order);
            Activate(obj, at, order);
        }

        static SpriteRenderer NewSprite(string name, Transform parent, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        static void UseFx(SpriteRenderer sr, Material fx)
        {
            var fallback = sr.sharedMaterial;
            if (fx != null)
                sr.sharedMaterial = fx;
            sr.gameObject.AddComponent<FxMaterial>().Setup(sr, fx, fallback);
        }

        // ================================================================ obiekty na kafelkach

        /// <summary>Czy obiekt ma stać na wyspie (np. skrzynia po kupnie ognika). Do Etapu 3c stawiany samoczynnie.</summary>
        public void Want(TileObject obj, bool wanted)
        {
            if (obj == null)
                return;
            obj.Wanted = wanted;
            if (!wanted)
            {
                if (obj.Placed && obj.NeedsTile && occupied.TryGetValue(obj.Tile, out var o) && o == obj)
                    occupied.Remove(obj.Tile);
                obj.Placed = false;
                obj.gameObject.SetActive(false);
                PlacePending();
                Changed?.Invoke();
                return;
            }
            if (!obj.Placed)
                TryPlace(obj, true);
        }

        void PlacePending()
        {
            if (!Ready)
                return;
            // najpierw obiekty na własnych kafelkach, potem te przy kotwicach
            foreach (var o in objects)
                if (o != null && o.Wanted && !o.Placed && o.NeedsTile)
                    TryPlace(o, false);
            foreach (var o in objects)
                if (o != null && o.Wanted && !o.Placed && !o.NeedsTile)
                    TryPlace(o, false);
        }

        void TryPlace(TileObject obj, bool announce)
        {
            if (!Ready)
                return;
            if (!obj.NeedsTile)
            {
                var a = obj.Anchor;
                if (a == null || !a.Placed)
                    return;
                var pos = a.transform.position + new Vector3(obj.AnchorOffset.x / Ppu, obj.AnchorOffset.y / Ppu, 0f);
                Activate(obj, pos, SortingOf(a) + 2);
                return;
            }
            TilePos tile;
            if (savedObjectTiles.TryGetValue(obj.Id, out var saved) && IsFree(saved))
                tile = saved;
            else if (!NearestFree(out tile))
            {
                if (announce)
                    GameEvents.RaiseToast("toast.no_space", Localization.Loc.Get(obj.NameKey));
                return;
            }
            PutOnTile(obj, tile);
            // obiekty zakotwiczone przy tym (np. ognik przy skrzyni)
            foreach (var o in objects)
                if (o != null && o.Wanted && !o.Placed && o.Anchor == obj)
                    TryPlace(o, false);
            Changed?.Invoke();
        }

        static int SortingOf(TileObject o)
        {
            var sr = o.GetComponent<SpriteRenderer>();
            return sr != null ? sr.sortingOrder : 0;
        }

        static void Activate(TileObject obj, Vector3 position, int order)
        {
            obj.transform.position = position;
            var main = obj.GetComponent<SpriteRenderer>();
            foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = sr == main ? order : order + 1;
            obj.Placed = true;
            obj.gameObject.SetActive(true);
            obj.SendMessage("OnPlaced", SendMessageOptions.DontRequireReceiver);
        }

        /// <summary>Wolny kafelek najbliżej środka startu (tam, gdzie gracz zaczynał).</summary>
        bool NearestFree(out TilePos best)
        {
            best = default;
            float bestD = float.MaxValue;
            foreach (var p in map.Tiles.Keys)
            {
                if (!IsFree(p))
                    continue;
                float d = (p.c - 15.5f) * (p.c - 15.5f) + (p.r - 15.5f) * (p.r - 15.5f);
                if (d < bestD || (Mathf.Approximately(d, bestD) && (p.r < best.r || (p.r == best.r && p.c < best.c))))
                {
                    bestD = d;
                    best = p;
                }
            }
            return bestD < float.MaxValue;
        }

        // ================================================================ testy (HUD debugowy)

        public void DebugBuyRandom(int count)
        {
            var rnd = new System.Random();
            for (int i = 0; i < count; i++)
            {
                var c = map.Candidates();
                if (c.Count == 0)
                    break;
                map.Buy(c[rnd.Next(c.Count)]);
            }
            Rebuild();
            PlacePending();
            Changed?.Invoke();
        }
    }
}
