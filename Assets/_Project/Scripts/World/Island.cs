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
    [Serializable] public class ObjectSave
    {
        public string id;
        public int c, r;
        public string lamp;
        public string building;
        public List<Automation.CrateEntry> crate;   // v6: zawartość skrzyni na plony
        public string seed;                        // v6: tryb runy siewu ("" pamięć, "-" nic, id rośliny)
        public string tree;                        // v7: gatunek drzewa owocowego
        public float growth, fruit;                // v7: wzrost drzewa i odrastanie owoców (sekundy)
        public int fallen;                         // v8: owoce strącone przez ognika, leżące pod drzewem
        public string merchant;                    // v8: id duszka-handlarza przy tej skrzyni
        public List<Automation.WispSave> wisps;    // v12: ogniki w gnieździe (zawód, doświadczenie, imię)
        public string brewAuto;                    // v12: kocioł - przepis, który warzy ognik-alchemik
        public string seedNight;                   // v14: gniazdo - nasiona siewcy w nocy (seed = w dzień)
        public bool noSwap;                        // v14: gniazdo - siewca nie podmienia roślin o świcie/zmierzchu
        public int storeReserve;                   // v14: skład - rezerwa drewna i kamienia
        public bool storeOff;                      // v14: skład - automatyczna sprzedaż wyłączona
        public string brew;                        // v10: kocioł - przepis w kotle
        public float brewLeft;                     // v10: sekundy do końca warzenia
        public bool brewReady;                     // v10: mikstura gotowa do wypicia
    }

    [Serializable]
    public class IslandSave
    {
        public int seed;
        public int purchased;
        public List<TileSave> tiles = new List<TileSave>();
        public List<DecoSave> decorations = new List<DecoSave>();
        public List<ObjectSave> objects = new List<ObjectSave>();
        // morze
        public Sea.ExpeditionSave expedition;
        public List<int> zonesFound = new List<int>();   // odkryte strefy (toast tylko raz)
        public bool pier;
        public int pierC, pierR;
        public int pierDir;   // +1 deski w prawo, -1 w lewo (0 w starszych zapisach = z lądu)
        public bool nets;
        public bool boat;
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
        Sea.Pier pier;
        GameObject nets;
        readonly HashSet<ZoneKind> zonesFound = new HashSet<ZoneKind>();
        readonly List<GameObject> critters = new List<GameObject>();
        const float ZoneDecoChance = 0.55f;
        Sea.Expedition boat;
        readonly Dictionary<TilePos, TileObject> occupied = new Dictionary<TilePos, TileObject>();
        readonly Dictionary<string, TilePos> savedObjectTiles = new Dictionary<string, TilePos>();
        Texture2D islandTex, waterTex, dataTex;
        Transform plotsRoot, decoRoot;
        IslandPicture picture;
        Texture2D landMaskTex;
        SpriteMask landMask;

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

        void Start()
        {
            EnsureLightShafts();
            // ptaki: tworzone w locie (bez przebudowy sceny), sprite'y z IslandArt (Wyspa → 2)
            if (art != null && art.birdCrow != null && art.birdCrow.Length >= 4 && FindAnyObjectByType<AmbientBirds>() == null)
            {
                var birds = new GameObject("Birds");
                birds.transform.SetParent(transform.parent, false);
                birds.AddComponent<AmbientBirds>().Setup(art.birdCrow, art.birdFinch, FindAnyObjectByType<GameClock>());
            }
        }

        /// <summary>
        /// Snopy światła (LightShafts, Light 2D): scena może go nie mieć (bez Wyspa → 3), więc tworzymy obiekt sami.
        /// </summary>
        void EnsureLightShafts()
        {
            if (FindAnyObjectByType<LightShafts>() != null)
                return;
            var go = new GameObject("LightShafts");
            go.transform.SetParent(transform.parent, false);
            go.AddComponent<LightShafts>().Setup(null, null, FindAnyObjectByType<GameClock>());
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

        /// <summary>Odwrotność PixelToWorld: piksele obrazu wyspy (y w dół), np. dla shaderów liczących z mapy danych.</summary>
        public static Vector2 WorldToPixel(Vector2 world) => new Vector2(world.x * Ppu + Half, Half - world.y * Ppu);

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

        public void NewWorld(int seed, bool withBoard = true)
        {
            Clear();
            map.NewWorld(seed);
            var plan = map.Plan;
            Rebuild();
            SyncPlots();
            // nowa gra: na czwartym kafelku startu zamiast dekoracji stoi tablica z karteczkami wprowadzenia
            if (withBoard)
                SpawnBoard(plan.startDecoTile);
            else
                AddDecoration(plan.startDecoTile, plan.startDeco);
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
            var legacyMerchants = new List<(TilePos tile, string id)>();
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
            if (save.zonesFound != null)
                foreach (var z in save.zonesFound)
                    zonesFound.Add((ZoneKind)z);
            RefreshCritters();
            var pierTile = new TilePos(save.pierC, save.pierR);
            int savedDir = save.pierDir != 0 ? save.pierDir : (map.Owns(new TilePos(pierTile.c - 1, pierTile.r)) ? 1 : -1);
            if (save.pier && map.Owns(new TilePos(pierTile.c + savedDir, pierTile.r)))
            {
                // zapis sprzed poprawki: ląd zabudował deski pomostu - rozbieramy go i oddajemy koszt (też sieci i łodzi)
                refund.Add(new CurrencyAmount(CurrencyType.Coins, 300));
                refund.Add(new CurrencyAmount(CurrencyType.Wood, 10));
                if (save.nets) { refund.Add(new CurrencyAmount(CurrencyType.Coins, 150)); refund.Add(new CurrencyAmount(CurrencyType.Wood, 5)); }
                if (save.boat) { refund.Add(new CurrencyAmount(CurrencyType.Coins, 600)); refund.Add(new CurrencyAmount(CurrencyType.Wood, 15)); }
                save.pier = false;
            }
            if (save.pier)
            {
                RestorePier(pierTile, save.pierDir);
                if (save.nets)
                    BuildNets();
                if (save.boat && BuildBoat())
                    boat.Restore(save.expedition);
            }
            foreach (var o in save.objects)
            {
                // lampy: kupione (lamp = id definicji) albo startowa latarnia z zapisu 3b (id lamp_lantern)
                string lampId = !string.IsNullOrEmpty(o.lamp) ? o.lamp : (o.id == "lamp_lantern" ? StarterLamp : null);
                var def = lampId != null && art != null ? art.Lamp(lampId) : null;
                var tile = new TilePos(o.c, o.r);
                if (def != null && IsFree(tile))
                    SpawnLamp(def, tile, o.id);
                else if (o.building == "altar" && IsFree(tile))
                    SpawnAltar(tile, o.id);
                else if (o.building == BoardBuilding && IsFree(tile))
                    SpawnBoard(tile, o.id);
                else if (o.building == TreeBuilding && IsFree(tile))
                    SpawnTree(Farming.TreeSpecies.Find(o.tree), tile, o.id, o.growth, o.fruit, o.fallen);
                else if (o.building == Automation.AutomationSystem.Wisp && IsFree(tile))
                {
                    // zapis sprzed roju: ognik-ogrodnik stał na własnym kafelku - teraz gniazdo z tym ognikiem
                    var n = SpawnBuilding(Automation.AutomationSystem.Nest, tile, o.id);
                    if (n != null && n.TryGetComponent(out Automation.WispNest nest))
                        nest.AddWisp(Automation.WispKind.Gardener);
                }
                else if (LegacyBuilding(o) == Automation.AutomationSystem.Merchant)
                    legacyMerchants.Add((tile, o.id));   // zapis sprzed v8: duszek stał na własnym kafelku
                else if (LegacyBuilding(o) is string kind && IsFree(tile))
                {
                    // budowle automatyki; zapisy sprzed 6b miały jedną skrzynię, skrzynię kupca i kamień runiczny ze sceny
                    var b = SpawnBuilding(kind, tile, o.id);
                    if (b != null && o.crate != null && b.TryGetComponent(out Automation.HarvestCrate crate))
                        crate.Restore(o.crate);
                    if (b != null && b.TryGetComponent(out Automation.WispNest nest))
                        nest.Restore(o.wisps, o.seed, o.seedNight, o.noSwap);
                    if (b != null && !string.IsNullOrEmpty(o.merchant))
                        SpawnMerchant(b, o.merchant);
                    if (b != null && b.TryGetComponent(out Automation.Storehouse store))
                        store.Restore(o.storeReserve, o.storeOff);
                    if (b != null && b.TryGetComponent(out Alchemy.Cauldron cauldron))
                    {
                        cauldron.AutoRecipe = o.brewAuto;
                        if (!string.IsNullOrEmpty(o.brew))
                            cauldron.Restore(o.brew, o.brewLeft, o.brewReady);
                    }
                }
            }
            // duszki ze starych zapisów: do skrzyni bez duszka, a gdy takiej nie ma - skrzynia na ich dawnym kafelku
            foreach (var (tile, id) in legacyMerchants)
            {
                var crate = CrateWithoutMerchant(TileCenter(tile));
                if (crate == null && IsFree(tile))
                    crate = SpawnBuilding(Automation.AutomationSystem.Crate, tile);
                if (crate != null)
                    SpawnMerchant(crate, id);
            }
            PlacePending();   // obiekty chciane wcześniej (np. skrzynia po wczytaniu ulepszeń)
            Changed?.Invoke();
        }

        public IslandSave Snapshot()
        {
            var s = new IslandSave { seed = map.Seed, purchased = map.Purchased };
            if (pier != null)
            {
                s.pier = true;
                s.pierC = pier.Tile.c;
                s.pierR = pier.Tile.r;
                s.pierDir = pier.Dir;
                s.nets = nets != null;
            }
            s.boat = boat != null;
            foreach (var z in zonesFound)
                s.zonesFound.Add((int)z);
            if (boat != null)
                s.expedition = boat.Snapshot();
            foreach (var kv in map.Tiles)
                s.tiles.Add(new TileSave { c = kv.Key.c, r = kv.Key.r, kind = kv.Value });
            foreach (var kv in decorations)
                if (kv.Value != null)
                    s.decorations.Add(new DecoSave { c = kv.Key.c, r = kv.Key.r, kind = kv.Value.Kind });
            foreach (var o in AllObjects())
            {
                if (!o.Placed || !o.NeedsTile)
                    continue;
                var os = new ObjectSave { id = o.Id, c = o.Tile.c, r = o.Tile.r, lamp = o.LampId, building = o.BuildingId };
                if (o.TryGetComponent(out Automation.HarvestCrate crate))
                {
                    os.crate = crate.Snapshot();
                    var m = MerchantOf(o);
                    if (m != null)
                        os.merchant = m.Id;
                }
                if (o.TryGetComponent(out Automation.WispNest nest))
                {
                    os.wisps = nest.Snapshot();
                    os.seed = nest.Seed;
                    os.seedNight = nest.SeedNight;
                    os.noSwap = !nest.Swap;
                }
                if (o.TryGetComponent(out Alchemy.Cauldron cauldron))
                    os.brewAuto = cauldron.AutoRecipe;
                if (o.TryGetComponent(out Automation.Storehouse store))
                {
                    os.storeReserve = store.Reserve;
                    os.storeOff = !store.Auto;
                }
                if (cauldron != null && cauldron.Brewing != null)
                {
                    os.brew = cauldron.Brewing;
                    os.brewLeft = cauldron.Left;
                    os.brewReady = cauldron.Ready;
                }
                if (o.TryGetComponent(out Farming.FruitTree tree) && tree.Species != null)
                {
                    os.tree = tree.Species.id;
                    os.growth = tree.Growth;
                    os.fruit = tree.FruitProgress;
                    os.fallen = tree.FallenCount;
                }
                s.objects.Add(os);
            }
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
            if (pier != null) { pier.gameObject.SetActive(false); Destroy(pier.gameObject); }
            pier = null;
            if (nets != null) { nets.SetActive(false); Destroy(nets); }
            nets = null;
            if (boat != null) { boat.gameObject.SetActive(false); Destroy(boat.gameObject); }
            boat = null;
            zonesFound.Clear();
            foreach (var c in critters)
                if (c != null) Destroy(c);
            critters.Clear();
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

        public int NextTileCoins => Mathf.Max(1, Mathf.RoundToInt(IslandEconomy.TileCoins(map.Purchased) * Economy.Prestige.TileCostMultiplier));   // Tania ziemia
        /// <summary>Kupione kafelki (Dzierżawa).</summary>
        public int PurchasedTiles => map != null ? map.Purchased : 0;
        public int NextTileEssence => IslandEconomy.TileEssence(map.Purchased);
        public int DigCoins => IslandEconomy.DigCoins(SoilCount);

        public bool CanAffordTile(Wallet wallet, EssenceStore essence) =>
            wallet != null && essence != null && wallet.Coins >= NextTileCoins && essence.Amount >= NextTileEssence;

        /// <summary>Kupno kafelka: monety + Esencja (GDD §13.2). Po zakupie mgła się rozstępuje i wypada znalezisko.</summary>
        public bool TryBuy(TilePos p, Wallet wallet, EssenceStore essence)
        {
            if (claiming || SeaBlocked(p) || !map.CanBuy(p) || !CanAffordTile(wallet, essence))
                return false;
            int coins = NextTileCoins, ess = NextTileEssence;
            if (!wallet.TrySpend(coins))
                return false;
            if (!essence.TryConsume(ess))
            {
                wallet.Add(coins);
                return false;
            }
            GameEvents.PlaySound("tile");
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
            Lore.Chronicle.Count("tile");
            var zone = ZoneAt(p);
            if (zone != ZoneKind.None)
            {
                if (zonesFound.Add(zone))
                    GameEvents.RaiseToast("toast.zone.found", Localization.Loc.Get(ZoneKey(zone)));
                var landmark = Landmark(p, zone);
                if (landmark.HasValue)
                    AddDecoration(p, landmark.Value);
                else if (WHash.Value(p.c, p.r, map.Seed + 913) < ZoneDecoChance)
                    AddDecoration(p, ZoneDeco(p, zone));
                RefreshCritters();
            }
            else if (WHash.Value(p.c, p.r, map.Seed + 911) < NewTileDecoChance)
                AddDecoration(p, NewTileDeco(p));
            PlacePending();
            GrantFind(p, find, wallet);
            Changed?.Invoke();
        }

        // ================================================================ strefy

        public ZoneKind ZoneAt(TilePos p) => map.Owns(p) && map.Plan != null ? map.Plan.ZoneAt(p) : ZoneKind.None;

        public static string ZoneKey(ZoneKind z) =>
            z == ZoneKind.Cemetery ? "zone.graveyard" : z == ZoneKind.Swamp ? "zone.swamp" : z == ZoneKind.Ruins ? "zone.ruins" : "";

        /// <summary>Bonus podłoża dla grządki (§11.3 A): cmentarz - rośliny nocy x1.5, bagno - trupi grzyb x1.5.</summary>
        public float ZoneBonus(FarmPlot plot)
        {
            if (plot == null || plot.State.crop == null)
                return 1f;
            var zone = ZoneAt(new TilePos(plot.Cell.x, plot.Cell.y));
            var crop = plot.State.crop;
            if (zone == ZoneKind.Cemetery && crop.kind == CropKind.Night) return 1.5f;
            if (zone == ZoneKind.Swamp && crop.id == "corpseshroom") return 1.5f;
            return 1f;
        }

        /// <summary>Zabytki stoją w środku strefy (krypta, posąg); kamienne schody obok posągu.</summary>
        DecoKind? Landmark(TilePos p, ZoneKind zone)
        {
            foreach (var zc in map.Plan.zoneCenters)
            {
                if (zc.kind != zone)
                    continue;
                if (zc.center == p)
                    return zone == ZoneKind.Cemetery ? DecoKind.Crypt : zone == ZoneKind.Ruins ? DecoKind.Statue : DecoKind.Mangrove;
                if (zone == ZoneKind.Ruins && p.r == zc.center.r + 1 && p.c == zc.center.c)
                    return DecoKind.StoneStairs;
            }
            return null;
        }

        static readonly (DecoKind kind, int weight)[] CemeteryPool =
        {
            (DecoKind.Gravestone0, 12), (DecoKind.Gravestone1, 10), (DecoKind.Gravestone2, 8), (DecoKind.WoodenCross, 12),
            (DecoKind.Obelisk, 6), (DecoKind.OpenGrave, 8), (DecoKind.SkullPile, 8), (DecoKind.Candles, 10),
            (DecoKind.IronGate, 5), (DecoKind.Thorns, 10), (DecoKind.DeadTree, 9),
        };
        static readonly (DecoKind kind, int weight)[] SwampPool =
        {
            (DecoKind.Mangrove, 12), (DecoKind.LilyPads, 12), (DecoKind.RottenLog, 14), (DecoKind.Cattails, 18),
            (DecoKind.Reeds, 16), (DecoKind.SwampPool, 12), (DecoKind.DeadTree, 8),
        };
        static readonly (DecoKind kind, int weight)[] RuinsPool =
        {
            (DecoKind.BrokenWall, 14), (DecoKind.FallenColumn, 14), (DecoKind.RuinsRubble, 16), (DecoKind.Brazier, 8),
            (DecoKind.RuneTablet, 8), (DecoKind.VinePillar, 14), (DecoKind.BrokenPillar, 12), (DecoKind.RuinedArch, 8),
        };

        DecoKind ZoneDeco(TilePos p, ZoneKind zone)
        {
            var pool = zone == ZoneKind.Cemetery ? CemeteryPool : zone == ZoneKind.Swamp ? SwampPool : RuinsPool;
            int total = 0;
            foreach (var e in pool) total += e.weight;
            double v = WHash.Value(p.c, p.r, map.Seed + 914) * total;
            foreach (var e in pool)
            {
                if (v < e.weight) return e.kind;
                v -= e.weight;
            }
            return pool[0].kind;
        }

        /// <summary>Stworzenia tła (nie postacie): wrona na cmentarzu, żaba i bąble na bagnie. Ok. 1 na 6 kafelków strefy.</summary>
        void RefreshCritters()
        {
            foreach (var c in critters)
                if (c != null) Destroy(c);
            critters.Clear();
            if (art == null || map.Plan == null)
                return;
            var cemetery = new List<TilePos>();
            var swamp = new List<TilePos>();
            foreach (var p in map.Tiles.Keys)
            {
                var z = ZoneAt(p);
                if (z == ZoneKind.Cemetery) cemetery.Add(p);
                else if (z == ZoneKind.Swamp && !decorations.ContainsKey(p) && !plots.ContainsKey(p)) swamp.Add(p);
            }
            cemetery.Sort((a, b) => WHash.Hash(a.c, a.r, 7).CompareTo(WHash.Hash(b.c, b.r, 7)));
            swamp.Sort((a, b) => WHash.Hash(a.c, a.r, 8).CompareTo(WHash.Hash(b.c, b.r, 8)));
            for (int i = 0; i < cemetery.Count && i < (cemetery.Count + 5) / 6; i++)
                SpawnCritter(cemetery[i], art.crow, art.crowGlow, false, 5);
            int frogs = (swamp.Count + 5) / 6;
            for (int i = 0; i < swamp.Count && i < frogs * 2; i++)
            {
                bool frog = i % 2 == 0;
                SpawnCritter(swamp[i], frog ? art.frog : art.bubbles, frog ? art.frogGlow : null, !frog, frog ? -3 : 2);
            }
        }

        void SpawnCritter(TilePos p, Sprite[] frames, Sprite[] glowFrames, bool loop, int dx)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                return;
            var at = TileAnchor(p, 11, out int order) + new Vector3(dx / Ppu, 0f, 0f);
            var sr = NewSprite("Critter", decoRoot, at, order + 3);
            sr.sprite = frames[0];
            SpriteRenderer glowSr = null;
            if (glowFrames != null && glowFrames.Length > 0 && glowFrames[0] != null)
            {
                glowSr = NewSprite("Glow", sr.transform, at, order + 4);
                glowSr.sprite = glowFrames[0];
                if (art.glow != null)
                    glowSr.sharedMaterial = art.glow;
            }
            sr.gameObject.AddComponent<Visual.ZoneCritter>().Setup(sr, glowSr, frames, glowFrames, loop, WHash.Hash(p.c, p.r, 9));
            critters.Add(sr.gameObject);
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
            GameEvents.PlaySound("dig");
            SyncPlots();
            if (plots.TryGetValue(p, out var plot))
                IslandFx.Planted(plot.CropPosition, 0);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Zasypanie grządki (pierścień akcji): z powrotem trawa, roślina przepada, koszt przekopania nie wraca.</summary>
        public bool TryFill(FarmPlot plot)
        {
            if (plot == null)
                return false;
            var p = new TilePos(plot.Cell.x, plot.Cell.y);
            if (!plots.TryGetValue(p, out var found) || found != plot)
                return false;
            plots.Remove(p);
            map.SetKind(p, TileKind.Grass);
            IslandFx.Planted(plot.CropPosition, 0);
            plot.gameObject.SetActive(false);   // od razu znika z FarmPlot.All
            Destroy(plot.gameObject);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Rozbiórka budowli kupionej w grze (lampa, skrzynia, ognik, duszek, runa, ołtarz): wraca połowa monet z ceny.
        /// Skrzynia najpierw sprzedaje zawartość po pełnej cenie.
        /// </summary>
        public bool Dismantle(TileObject obj, Wallet wallet, CropDatabase crops)
        {
            if (obj == null || !spawned.Contains(obj))
                return false;
            int refund = 0;
            if (!string.IsNullOrEmpty(obj.LampId) && art != null && art.Lamp(obj.LampId) is LampDefinition lamp)
                refund = lamp.price / 2;
            else if (Array.IndexOf(Automation.AutomationSystem.Kinds, obj.BuildingId) >= 0)
            {
                var cost = Automation.AutomationSystem.Cost(obj.BuildingId, Mathf.Max(0, CountBuilding(obj.BuildingId) - 1));
                refund = cost[0].amount / 2;
            }
            else if (obj.BuildingId == "altar")
                refund = 250;
            else if (obj.BuildingId == BoardBuilding && wallet != null)
            {
                wallet.Add(CurrencyType.Wood, BoardWood);   // tablica rozebrana na deski
                GameEvents.RaiseCurrencyGained(obj.transform.position, CurrencyType.Wood, BoardWood);
            }
            var attached = MerchantOf(obj);
            if (attached != null)
            {
                // duszek-handlarz odchodzi razem ze skrzynią (zwrot połowy jego ceny)
                var mc = Automation.AutomationSystem.Cost(Automation.AutomationSystem.Merchant, Mathf.Max(0, CountBuilding(Automation.AutomationSystem.Merchant) - 1));
                refund += mc[0].amount / 2;
                RemoveObject(attached);
            }
            if (obj.TryGetComponent(out Automation.HarvestCrate crate) && wallet != null)
            {
                int sold = crate.SellAll(wallet, crops, Economy.SaleChannel.Crate);
                if (sold > 0)
                    GameEvents.RaiseCurrencyGained(obj.transform.position, CurrencyType.Coins, sold);
            }
            if (refund > 0 && wallet != null)
            {
                wallet.Refund(refund);   // zwrot nie jest zarobkiem rejsu
                GameEvents.RaiseCurrencyGained(obj.transform.position, CurrencyType.Coins, refund);
            }
            IslandFx.Planted(obj.transform.position, 0);
            RemoveObject(obj);
            return true;
        }

        /// <summary>Usunięcie obiektu kupionego w grze (rozbiórka, ścięte drzewo): kafelek wolny, obiekt znika.</summary>
        public void RemoveObject(TileObject obj)
        {
            if (obj == null)
                return;
            if (obj.Placed && occupied.TryGetValue(obj.Tile, out var o) && o == obj)
                occupied.Remove(obj.Tile);
            savedObjectTiles.Remove(obj.Id);
            spawned.Remove(obj);
            obj.gameObject.SetActive(false);
            Destroy(obj.gameObject);
            Changed?.Invoke();
        }

        /// <summary>Czy obiekt można rozebrać (kupiony w grze, nie ze sceny). Drzewa się ścina, nie rozbiera.</summary>
        public bool CanDismantle(TileObject obj) => obj != null && spawned.Contains(obj) && obj.NeedsTile && obj.BuildingId != TreeBuilding;

        // ================================================================ drzewa owocowe (GDD §15.1)

        public const string TreeBuilding = "tree";

        /// <summary>Drzewo owocowe na kafelku (sklep, tryb stawiania, zapis): sprite fazy, świecenie owoców, kołysanie.</summary>
        public TileObject SpawnTree(Farming.TreeSpecies species, TilePos tile, string id = null, float growth = 0f, float fruit = 0f,
                                    int fallen = 0)
        {
            if (species == null || art == null || !IsFree(tile))
                return null;
            var treeArt = art.Tree(species.id);
            var go = new GameObject("Tree_" + species.id);
            go.transform.SetParent(decoRoot, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = treeArt != null && treeArt.stages != null && treeArt.stages.Length > 0 ? treeArt.stages[0] : null;
            UseFx(body, art.swayTree);
            var glow = NewSprite("Glow", go.transform, Vector3.zero, 1);
            glow.transform.localPosition = Vector3.zero;
            // świecące owoce kołyszą się razem z drzewem (ten sam rytm, materiał bez oświetlenia)
            if (art.swayTreeGlow != null)
                UseFx(glow, art.swayTreeGlow);
            else if (art.glow != null)
                glow.sharedMaterial = art.glow;
            glow.enabled = false;
            go.AddComponent<SpriteFx>().Setup(0.05f, body, glow);   // każde drzewo we własnej fazie kołysania
            var tree = go.AddComponent<Farming.FruitTree>();
            AddTileCollider(go);
            var obj = go.AddComponent<TileObject>();
            if (string.IsNullOrEmpty(id) || BuildingIdTaken(id))
                id = NewBuildingId(TreeBuilding);
            obj.Setup(id, species.NameKey);
            obj.BuildingId = TreeBuilding;
            obj.Wanted = true;
            spawned.Add(obj);
            PutOnTile(obj, tile);
            tree.Setup(species, treeArt, body, glow, growth, fruit);
            tree.RestoreFallen(fallen);
            Changed?.Invoke();
            return obj;
        }

        /// <summary>Strefa kafelka pod drzewem (krwawa jarzębina rośnie lepiej na cmentarzu).</summary>
        public float TreeZoneBonus(Farming.FruitTree tree)
        {
            if (tree == null || tree.Species == null || !tree.Species.cemeteryBonus)
                return 1f;
            var obj = tree.GetComponent<TileObject>();
            return obj != null && obj.Placed && ZoneAt(obj.Tile) == ZoneKind.Cemetery ? 1.5f : 1f;
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
                    string loreKey = Lore.LoreBook.TakeKeeper();   // zapiski Opiekuna po kolei
                    GameEvents.RaiseToast("toast.find.lore", Localization.Loc.Get(loreKey));
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
                // maska lądu: grządki na brzegu nie wystają poza zaokrąglony, nierówny brzeg wyspy
                landMaskTex = NewTexture(s, false, "IslandLandMask");
                if (islandRenderer != null)
                {
                    var go = new GameObject("LandMask");
                    go.transform.SetParent(islandRenderer.transform, false);
                    landMask = go.AddComponent<SpriteMask>();
                    landMask.sprite = Sprite.Create(landMaskTex, rect, pivot, Ppu, 0, SpriteMeshType.FullRect);
                    landMask.alphaCutoff = 0.5f;
                }
            }
            landMaskTex.LoadRawTextureData(picture.landMask);
            landMaskTex.Apply(false);
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
            sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // przycięta do lądu (maska LandMask)
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
            if (a != null && a.glowSprite != null)
            {
                var g = NewSprite("Glow", sr.transform, sr.transform.position, order + 1);
                g.sprite = a.glowSprite;
                if (art.glow != null)
                    g.sharedMaterial = art.glow;
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
            GameEvents.PlaySound("chop");
            var a = d.Art;
            game.Wallet.Add(a.reward, a.rewardAmount);
            GameEvents.RaiseCurrencyGained(at, a.reward, a.rewardAmount);
            if (a.bonusLore && UnityEngine.Random.value < a.bonusChance)
            {
                string loreKey = Lore.LoreBook.TakeKeeper();
                GameEvents.RaiseToast("toast.find.lore", Localization.Loc.Get(loreKey));
            }
            else if (a.bonusAmount > 0 && UnityEngine.Random.value < a.bonusChance)
            {
                game.Wallet.Add(a.bonus, a.bonusAmount);
                GameEvents.RaiseCurrencyGained(at, a.bonus, a.bonusAmount);
                if (!string.IsNullOrEmpty(a.bonusToast))
                    GameEvents.RaiseToast(a.bonusToast, a.bonusAmount);
            }
            d.PlayRemoval(() => FinishRemoval(d));
            return true;
        }

        // ---------------------------------------------------------------- dzicz: drwal i runy dziczy (rój ogników)

        static readonly HashSet<DecoKind> Wild = new HashSet<DecoKind>
        {
            DecoKind.TreeRound, DecoKind.Rock, DecoKind.Bush, DecoKind.DeadTree, DecoKind.Spruce, DecoKind.Stump,
            DecoKind.StumpSpruce, DecoKind.StumpDead, DecoKind.Rubble, DecoKind.RottenLog, DecoKind.Thorns,
        };

        /// <summary>Dzikie drzewa, krzaki, kamienie i pnie, które może rozebrać ognik-drwal (bez zabytków i stref).</summary>
        public IEnumerable<Decoration> WildDecorations()
        {
            foreach (var d in decorations.Values)
                if (d != null && !d.Removing && d.Art != null && d.Art.removable && !d.Art.landmark && Wild.Contains(d.Kind))
                    yield return d;
        }

        /// <summary>
        /// Rozbiórka przez ognika-drwala: bez Esencji, nagroda (drewno / kamień) idzie z ognikiem do gniazda, bonusy od razu.
        /// Po drzewie zostaje pień, jak przy usuwaniu ręcznym.
        /// </summary>
        public bool WispRemove(Decoration d, out CurrencyType reward, out int amount)
        {
            reward = CurrencyType.Wood;
            amount = 0;
            var game = GameLoop.Instance;
            if (d == null || d.Removing || d.Art == null || !d.Art.removable || game == null)
                return false;
            var a = d.Art;
            reward = a.reward;
            amount = a.rewardAmount;
            if (a.bonusAmount > 0 && !a.bonusLore && UnityEngine.Random.value < a.bonusChance)
            {
                var at = d.transform.position + new Vector3(0f, 8f / Ppu, 0f);
                game.Wallet.Add(a.bonus, a.bonusAmount);
                GameEvents.RaiseCurrencyGained(at, a.bonus, a.bonusAmount);
            }
            GameEvents.PlaySound("chop", 0.6f);
            d.PlayRemoval(() => FinishRemoval(d));
            return true;
        }

        static readonly DecoKind[] WildGrowth = { DecoKind.TreeRound, DecoKind.TreeRound, DecoKind.Spruce, DecoKind.Bush, DecoKind.Rock, DecoKind.Rock };

        /// <summary>Runa dziczy: na wolnym kafelku trawy wokół (bez środka) wyrasta dzikie drzewo, krzak albo kamień.</summary>
        public bool GrowWild(TilePos center, int radius)
        {
            var free = new List<TilePos>();
            for (int dc = -radius; dc <= radius; dc++)
                for (int dr = -radius; dr <= radius; dr++)
                {
                    var p = new TilePos(center.c + dc, center.r + dr);
                    if ((dc != 0 || dr != 0) && IsFree(p) && !plots.ContainsKey(p))
                        free.Add(p);
                }
            if (free.Count == 0)
                return false;
            var tile = free[UnityEngine.Random.Range(0, free.Count)];
            AddDecoration(tile, WildGrowth[UnityEngine.Random.Range(0, WildGrowth.Length)]);
            IslandFx.Grew(TileAnchor(tile, 12, out int order), order);
            IslandFx.FindPopped(TileCenter(tile));
            GameEvents.PlaySound("plant", 0.6f);
            Changed?.Invoke();
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

        // ================================================================ morze: pomost i sieci

        public bool HasPier => pier != null;
        public Sea.Pier PierObject => pier;
        public bool HasNets => nets != null;
        public bool HasBoat => boat != null;
        public Sea.Expedition Boat => boat;

        /// <summary>Łódź przy końcu pomostu.</summary>
        public bool BuildBoat()
        {
            if (pier == null || boat != null)
                return false;
            bool landLeft = pier.Dir > 0;
            var at = (Vector3)pier.Tip + new Vector3((landLeft ? -4f : 4f) / Ppu, -10f / Ppu, 0f);
            var go = new GameObject("Boat");
            go.transform.SetParent(decoRoot, false);
            go.transform.position = at;
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = art != null ? art.boat : null;
            body.flipX = !landLeft;
            body.sortingOrder = pier.GetComponent<SpriteRenderer>().sortingOrder + 2;
            var glowSr = NewSprite("Glow", go.transform, at, body.sortingOrder + 1);
            glowSr.sprite = art != null ? art.boatGlow : null;
            glowSr.flipX = !landLeft;
            if (art != null && art.glow != null)
                glowSr.sharedMaterial = art.glow;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(30f / Ppu, 16f / Ppu);
            col.offset = new Vector2(0f, 8f / Ppu);
            boat = go.AddComponent<Sea.Expedition>();
            boat.Init(body, glowSr, col);
            Visual.Floating.Attach(body, 5f, art != null ? art.floatMat : null);   // kadłub w wodzie, piana, kręgi
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Woda zajęta przez pomost i to, co przy nim stoi (deski na 2 kafelki, łódź i sieci przy końcu):
        /// 3 kafelki w stronę morza, w rzędzie pomostu i pod nim. Tych kafelków nie da się kupić, żeby ląd nie zabudował pomostu.
        /// </summary>
        public bool SeaBlocked(TilePos p)
        {
            if (pier == null)
                return false;
            int dir = PierDir(pier.Tile);
            for (int k = 0; k < 3; k++)
                if ((p.c == pier.Tile.c + k * dir) && (p.r == pier.Tile.r || p.r == pier.Tile.r + 1))
                    return true;
            return false;
        }

        /// <summary>Kierunek desek: od lądu w stronę morza (+1 w prawo, -1 w lewo).</summary>
        int PierDir(TilePos p) => pierDirOverride != 0 ? pierDirOverride
            : pier != null && pier.Tile == p ? pier.Dir : (map.Owns(new TilePos(p.c - 1, p.r)) ? 1 : -1);

        /// <summary>Pomost: kafelek wody z lądem z lewej albo z prawej; dwa kafelki desek muszą leżeć na wodzie.</summary>
        public bool PierValid(TilePos p) => pier == null && PierSpot(p);

        /// <summary>Przeniesienie pomostu: to samo miejsce albo nowe, które spełnia warunki budowy.</summary>
        public bool PierMoveValid(TilePos p) => pier != null && (p == pier.Tile || PierSpot(p));

        bool PierSpot(TilePos p)
        {
            if (!WorldGenerator.InWorld(p) || map.Owns(p))
                return false;
            bool left = map.Owns(new TilePos(p.c - 1, p.r)), right = map.Owns(new TilePos(p.c + 1, p.r));
            if (!left && !right)
                return false;
            var next = new TilePos(p.c + (left ? 1 : -1), p.r);
            return WorldGenerator.InWorld(next) && !map.Owns(next);
        }

        /// <summary>Podstawa pomostu przy brzegu; sprite odwrócony, gdy ląd jest po prawej. Zwraca też koniec pomostu.</summary>
        public (Vector3 pos, bool flip) PierPlacement(TilePos p, out Vector3 tipWorld, out int order)
        {
            var (x0, y0) = IslandPainter.TileOrigin(p);
            bool landLeft = PierDir(p) > 0;
            int baseX = landLeft ? x0 - 1 : x0 + IslandPainter.Tile;
            int tipX = landLeft ? baseX + 33 : baseX - 33;
            order = y0 + 9;
            tipWorld = AnchorWorld(tipX, y0 + 6);
            return (AnchorWorld(baseX, y0 + 9), !landLeft);
        }

        /// <summary>Pomost z zapisu: bez sprawdzania miejsca (ląd mógł urosnąć obok w starszych wersjach).</summary>
        void RestorePier(TilePos p, int dir)
        {
            if (dir == 0)
                dir = map.Owns(new TilePos(p.c - 1, p.r)) ? 1 : -1;
            pierDirOverride = dir;
            CreatePier(p);
            pierDirOverride = 0;
        }

        int pierDirOverride;
        readonly List<CurrencyAmount> refund = new List<CurrencyAmount>();

        /// <summary>Zwrot za pomost rozebrany przy wczytaniu (woła GameLoop po przywróceniu portfela).</summary>
        public void FlushRefunds(Wallet wallet)
        {
            if (refund.Count == 0 || wallet == null)
                return;
            foreach (var c in refund)
                wallet.Add(c.type, c.amount);
            refund.Clear();
            GameEvents.RaiseToast("toast.pier.refund");
        }

        public bool BuildPier(TilePos p)
        {
            if (!PierValid(p))
                return false;
            CreatePier(p);
            return true;
        }

        void CreatePier(TilePos p)
        {
            var (pos, flip) = PierPlacement(p, out var tip, out int order);
            var sr = NewSprite("Pier", decoRoot, pos, order);
            sr.sprite = art != null ? art.pier : null;
            sr.flipX = flip;
            var area = sr.gameObject.AddComponent<BoxCollider2D>();
            area.size = new Vector2(10f, 8f);   // łowisko (Pier.Range = 5 kafelków)
            area.offset = (Vector2)(tip - pos);
            int dir = PierDir(p);
            pier = sr.gameObject.AddComponent<Sea.Pier>();
            pier.Init(this, water, p, tip, dir);
            Changed?.Invoke();
        }

        /// <summary>
        /// Pomost w nowe miejsce: sieci i łódź płyną razem z nim (łódź zachowuje wyprawę, sieci odliczanie połowu).
        /// </summary>
        public bool MovePier(TilePos p)
        {
            if (!PierMoveValid(p))
                return false;
            if (p == pier.Tile)
                return true;
            bool hadNets = nets != null, hadBoat = boat != null;
            float netsTimer = hadNets && nets.TryGetComponent(out Sea.FishingNets n) ? n.Elapsed : 0f;
            var expedition = hadBoat ? boat.Snapshot() : null;
            pier.gameObject.SetActive(false); Destroy(pier.gameObject);
            pier = null;
            if (nets != null) { nets.SetActive(false); Destroy(nets); }
            nets = null;
            if (boat != null) { boat.gameObject.SetActive(false); Destroy(boat.gameObject); }
            boat = null;
            CreatePier(p);
            if (hadNets && BuildNets() && nets.TryGetComponent(out Sea.FishingNets moved))
                moved.Elapsed = netsTimer;
            if (hadBoat && BuildBoat())
                boat.Restore(expedition);
            return true;
        }

        public bool BuildNets()
        {
            if (pier == null || nets != null)
                return false;
            bool landLeft = pier.Dir > 0;
            var at = (Vector3)pier.Tip + new Vector3((landLeft ? 14f : -14f) / Ppu, -8f / Ppu, 0f);
            var sr = NewSprite("FishingNets", decoRoot, at, 2000);
            sr.sprite = art != null ? art.netBuoy : null;
            sr.sortingOrder = pier.GetComponent<SpriteRenderer>().sortingOrder + 1;
            var area = sr.gameObject.AddComponent<BoxCollider2D>();   // klik w boję: panel sieci
            area.size = new Vector2(12f / Ppu, 14f / Ppu);
            area.offset = new Vector2(0f, 7f / Ppu);
            sr.gameObject.AddComponent<Sea.FishingNets>().Init(this);
            Visual.Floating.Attach(sr, 5f, art != null ? art.floatMat : null);   // boja w wodzie, kręgi
            nets = sr.gameObject;
            Changed?.Invoke();
            return true;
        }

        // ================================================================ budowle na brzegu

        /// <summary>Wolny kafelek lądu przy wodzie (ołtarz, latarnia morska stoją na brzegu).</summary>
        public bool IsShoreFree(TilePos p)
        {
            if (!IsFree(p))
                return false;
            foreach (var n in IslandMap.Neighbours(p))
                if (!map.Owns(n))
                    return true;
            return false;
        }

        public bool HasBuilding(string id)
        {
            foreach (var o in spawned)
                if (o != null && (o.BuildingId == id || o.LampId == id))
                    return true;
            return false;
        }

        public TileObject SpawnAltar(TilePos tile, string id = null)
        {
            if (!IsFree(tile) || HasBuilding("altar"))
                return null;
            var sr = NewSprite("Altar", decoRoot, Vector3.zero, 0);
            sr.sprite = art != null ? art.altar : null;
            if (art != null && art.altarGlow != null)
            {
                var g = NewSprite("Glow", sr.transform, Vector3.zero, 1);
                g.sprite = art.altarGlow;
                if (art.glow != null)
                    g.sharedMaterial = art.glow;
            }
            sr.gameObject.AddComponent<Sea.Altar>();
            AddTileCollider(sr.gameObject);
            var obj = sr.gameObject.AddComponent<TileObject>();
            obj.Setup(string.IsNullOrEmpty(id) ? "altar" : id, "bld.altar");
            obj.BuildingId = "altar";
            obj.Wanted = true;
            spawned.Add(obj);
            PutOnTile(obj, tile);
            Changed?.Invoke();
            return obj;
        }

        // ================================================================ tablica wprowadzenia (GDD §12.8)

        public const string BoardBuilding = "board";
        const int BoardWood = 3;

        /// <summary>Tablica z karteczkami (jest tylko na nowej wyspie, aż gracz ją rozbierze).</summary>
        public TileObject Board
        {
            get
            {
                foreach (var o in spawned)
                    if (o != null && o.BuildingId == BoardBuilding)
                        return o;
                return null;
            }
        }

        /// <summary>Lampy na wyspie (kupione i startowa), np. dla kroku wprowadzenia „kup lampę”.</summary>
        public int CountLamps()
        {
            int n = 0;
            foreach (var o in AllObjects())
                if (o != null && o.Placed && !string.IsNullOrEmpty(o.LampId))
                    n++;
            return n;
        }

        public TileObject SpawnBoard(TilePos tile, string id = null)
        {
            if (art == null || art.noticeBoard == null || !IsFree(tile) || Board != null)
                return null;
            var sr = NewSprite("NoticeBoard", decoRoot, Vector3.zero, 0);
            sr.sprite = art.noticeBoard;
            sr.gameObject.AddComponent<UI.NoticeBoard>().Setup(sr, art.noticeBoard, art.noticeBoardEmpty);
            AddTileCollider(sr.gameObject);
            var obj = sr.gameObject.AddComponent<TileObject>();
            obj.Setup(string.IsNullOrEmpty(id) ? BoardBuilding : id, "bld.board");
            obj.BuildingId = BoardBuilding;
            obj.Wanted = true;
            spawned.Add(obj);
            PutOnTile(obj, tile);
            Changed?.Invoke();
            return obj;
        }

        // ================================================================ budowle automatyki

        static string LegacyBuilding(ObjectSave o)
        {
            if (!string.IsNullOrEmpty(o.building))
                return Array.IndexOf(Automation.AutomationSystem.Kinds, o.building) >= 0 ? o.building : null;
            switch (o.id)
            {
                case "crate": return Automation.AutomationSystem.Crate;
                case "merchant_chest": return Automation.AutomationSystem.Merchant;
                case "rune": return Automation.AutomationSystem.Rune;
                default: return null;
            }
        }

        public int CountBuilding(string kind)
        {
            int n = 0;
            foreach (var o in spawned)
                if (o != null && o.BuildingId == kind)
                    n++;
            return n;
        }

        /// <summary>Pierwsza (darmowa) sztuka po zakupie ulepszenia: na wolnym kafelku najbliżej startu.</summary>
        /// <param name="expand">Pierwsza, darmowa sztuka (z ulepszenia albo daru mgły): gdy nie ma wolnego kafelka, mgła
        /// odsłania obok darmowy kafelek trawy, żeby budowla nie przepadła (np. „Wierny ognik” na pełnej wyspie 2x2).</param>
        public TileObject SpawnBuildingNear(string kind, bool expand = false)
        {
            if (kind == Automation.AutomationSystem.Merchant)
            {
                var crate = CrateWithoutMerchant(TileCenter(new TilePos(15, 15)));
                if (crate == null)
                {
                    GameEvents.RaiseToast("toast.merchant.need_crate");
                    return null;
                }
                return SpawnMerchant(crate);
            }
            if (!NearestFree(out var tile) && !(expand && ClaimFreeTile(out tile)))
            {
                GameEvents.RaiseToast("toast.no_space", Localization.Loc.Get(Automation.AutomationSystem.NameKey(kind)));
                return null;
            }
            return SpawnBuilding(kind, tile);
        }

        /// <summary>
        /// Darmowy kafelek trawy przy lądzie, najbliżej środka wyspy (nie na wodzie pomostu). Bez dekoracji (miejsce na
        /// budowlę) i bez podnoszenia ceny kolejnych kafelków; znalezisko spod kafelka wypada jak przy zakupie.
        /// False = nie ma gdzie (koniec świata).
        /// </summary>
        bool ClaimFreeTile(out TilePos tile)
        {
            tile = default;
            if (!Ready)
                return false;
            var candidates = map.Candidates();
            candidates.RemoveAll(SeaBlocked);
            float bestD = float.MaxValue;
            foreach (var p in candidates)
            {
                float d = (p.c - 15.5f) * (p.c - 15.5f) + (p.r - 15.5f) * (p.r - 15.5f);
                if (d < bestD)
                {
                    bestD = d;
                    tile = p;
                }
            }
            if (bestD == float.MaxValue || !map.AddFree(tile))
                return false;
            Rebuild();
            IslandFx.TileClaimed(TileCenter(tile), art != null ? art.foam : null);
            GameEvents.PlaySound("tile");
            var game = GameLoop.Instance;
            if (game != null && map.Plan != null)
                GrantFind(tile, map.Plan.FindAt(tile), game.Wallet);   // znaleziska (też zapiski Opiekuna) nie przepadają
            Changed?.Invoke();
            return IsFree(tile);
        }

        /// <summary>Skrzynia, ognik, duszek-handlarz albo runa na kafelku (sklep, tryb stawiania, zapis).</summary>
        public TileObject SpawnBuilding(string kind, TilePos tile, string id = null)
        {
            if (art == null || !IsFree(tile))
                return null;
            var go = new GameObject("Building_" + kind);
            go.transform.SetParent(decoRoot, false);
            var body = go.AddComponent<SpriteRenderer>();
            SpriteRenderer Glow(Sprite sprite)
            {
                var g = NewSprite("Glow", go.transform, Vector3.zero, 1);
                g.transform.localPosition = Vector3.zero;
                g.sprite = sprite;
                if (art.glow != null)
                    g.sharedMaterial = art.glow;
                return g;
            }
            switch (kind)
            {
                case Automation.AutomationSystem.Crate:
                    body.sprite = art.crateFill != null && art.crateFill.Length > 0 ? art.crateFill[0] : null;
                    go.AddComponent<Automation.HarvestCrate>().Setup(art.crateFill, body);
                    break;
                case Automation.AutomationSystem.Nest:
                    // gniazdo ogników: pień z dziuplami; ogniki to dzieci gniazda (rój ogników)
                    body.sprite = First(art.wispNest);
                    go.AddComponent<Automation.WispNest>().Setup(body, art.wispNest, art);
                    break;
                case Automation.AutomationSystem.Cauldron:
                    body.sprite = art.cauldronIdle;
                    go.AddComponent<Alchemy.Cauldron>().Setup(body, Glow(null), art.cauldronIdle, art.cauldronBrew, art.cauldronBrewGlow,
                                                                 art.cauldronReady, art.cauldronReadyGlow);
                    break;
                case Automation.AutomationSystem.Store:
                    body.sprite = art.storehouse;
                    go.AddComponent<Automation.Storehouse>();
                    break;
                case Automation.AutomationSystem.Rune:
                    body.sprite = art.rune;
                    if (art.runeGlow != null)
                        Glow(art.runeGlow);
                    go.AddComponent<Automation.WildRune>();
                    NightGlow.Add(go, new Vector2(0f, 0.6f), new Color(0.37f, 0.81f, 0.72f), 1.1f, 0.45f);
                    break;
                default:
                    Destroy(go);
                    return null;
            }
            AddTileCollider(go);
            var obj = go.AddComponent<TileObject>();
            if (string.IsNullOrEmpty(id) || BuildingIdTaken(id))
                id = NewBuildingId(kind);
            obj.Setup(id, Automation.AutomationSystem.NameKey(kind));
            obj.BuildingId = kind;
            obj.Wanted = true;
            spawned.Add(obj);
            PutOnTile(obj, tile);
            Changed?.Invoke();
            return obj;
        }

        // ---------------------------------------------------------------- duszek-handlarz przy skrzyni

        static readonly Vector2Int MerchantOffset = new Vector2Int(9, 5);   // piksele od podstawy skrzyni: obok, lekko wyżej

        /// <summary>Duszek-handlarz przypięty do skrzyni (null = skrzynia bez duszka).</summary>
        public TileObject MerchantOf(TileObject crate)
        {
            if (crate == null)
                return null;
            foreach (var o in spawned)
                if (o != null && o.BuildingId == Automation.AutomationSystem.Merchant && o.Anchor == crate)
                    return o;
            return null;
        }

        /// <summary>Skrzynia na kafelku, przy której nie ma jeszcze duszka (tryb stawiania duszka).</summary>
        public TileObject FreeCrateAt(TilePos t) =>
            occupied.TryGetValue(t, out var o) && o != null && o.BuildingId == Automation.AutomationSystem.Crate && MerchantOf(o) == null ? o : null;

        /// <summary>Najbliższa skrzynia bez duszka.</summary>
        public TileObject CrateWithoutMerchant(Vector3 near)
        {
            TileObject best = null;
            foreach (var o in spawned)
                if (o != null && o.Placed && o.BuildingId == Automation.AutomationSystem.Crate && MerchantOf(o) == null
                    && (best == null || (o.transform.position - near).sqrMagnitude < (best.transform.position - near).sqrMagnitude))
                    best = o;
            return best;
        }

        /// <summary>Gdzie unosi się duszek przy skrzyni na kafelku t (podgląd w trybie stawiania).</summary>
        public static Vector3 MerchantSpot(TilePos t) => TileAnchor(t, 13, out _) + new Vector3(MerchantOffset.x / Ppu, MerchantOffset.y / Ppu, 0f);

        /// <summary>
        /// Duszek-handlarz bez własnego kafelka: unosi się obok skrzyni i sprzedaje ją, gdy tylko się zapełni.
        /// Przenoszenie skrzyni zabiera duszka ze sobą, rozbiórka skrzyni - też.
        /// </summary>
        public TileObject SpawnMerchant(TileObject crate, string id = null)
        {
            if (art == null || crate == null || crate.BuildingId != Automation.AutomationSystem.Crate || MerchantOf(crate) != null)
                return null;
            var go = new GameObject("Building_merchant");
            go.transform.SetParent(decoRoot, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = First(art.merchantFrames);
            var g = NewSprite("Glow", go.transform, Vector3.zero, 1);
            g.transform.localPosition = Vector3.zero;
            g.sprite = First(art.merchantGlow);
            if (art.glow != null)
                g.sharedMaterial = art.glow;
            go.AddComponent<Automation.MerchantSpirit>().Setup(body, g, art.merchantFrames, art.merchantGlow);
            NightGlow.Add(go, new Vector2(0f, 0.7f), new Color(0.95f, 0.75f, 0.4f), 1.6f, 0.7f);
            var obj = go.AddComponent<TileObject>();
            if (string.IsNullOrEmpty(id) || BuildingIdTaken(id))
                id = NewBuildingId(Automation.AutomationSystem.Merchant);
            obj.Setup(id, Automation.AutomationSystem.NameKey(Automation.AutomationSystem.Merchant), crate, MerchantOffset);
            obj.BuildingId = Automation.AutomationSystem.Merchant;
            obj.Wanted = true;
            spawned.Add(obj);
            TryPlace(obj, false);
            Changed?.Invoke();
            return obj;
        }

        static Sprite First(Sprite[] sprites) => sprites != null && sprites.Length > 0 ? sprites[0] : null;

        bool BuildingIdTaken(string id)
        {
            foreach (var o in AllObjects())
                if (o.Id == id)
                    return true;
            return false;
        }

        string NewBuildingId(string kind)
        {
            for (int i = 1; ; i++)
                if (!BuildingIdTaken($"{kind}_{i}"))
                    return $"{kind}_{i}";
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

        /// <summary>Czy obiekt ma stać na wyspie (np. skrzynia po kupnie ognika). Do.</summary>
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
                c.RemoveAll(SeaBlocked);
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
