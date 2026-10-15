using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;
using Wyspa.World;

namespace Wyspa.Automation
{
    [Serializable]
    public class WispSave
    {
        public string id;
        public int kind;
        public int xp;
        public int name;
        public string hat;   // czapka (Economy.Hats), null = bez
        public float mood = -1f;   // nastrój 0-100 (-1: zapis sprzed nastroju)
    }

    /// <summary>
    /// Gniazdo ogników (rój ogników): budowla na kafelku, dom dla 3 ogników dowolnych zawodów. Klik → lista ogników
    /// (imię, zawód, poziom, doświadczenie), przywołanie nowego (monety + plony nocy, cena rośnie z liczbą ogników tego zawodu),
    /// wybór nasion dla siewcy. Ogniki z gniazda pracują w polu 7x7 wokół niego (latarnik i zbieracz na całej wyspie).
    /// </summary>
    public class WispNest : MonoBehaviour, IAreaEffect, IContextActions
    {
        /// <summary>Miejsca w gnieździe: 3, ulepszenie „Przestronne gniazdo” do 5.</summary>
        public static int Capacity => Upgrades.UpgradeEffects.NestCapacity;
        /// <summary>Tryb siewcy: „pamięć ziemi” (to samo, co rosło), „nic nie sadź” i „losowo” (roślina pasująca do pory dnia).</summary>
        public const string Memory = "", Off = "-", Random = "?";

        public static readonly List<WispNest> All = new List<WispNest>();

        SpriteRenderer body;
        Sprite[] frames;
        IslandArt art;
        string seed = Memory, seedNight = Memory;
        bool swap = true;
        readonly List<Wisp> wisps = new List<Wisp>();

        public int AreaRadius => Wisp.Radius;
        public IReadOnlyList<Wisp> Wisps => wisps;
        /// <summary>Nasiona siewcy w dzień (zapis: seed).</summary>
        public string Seed
        {
            get => seed;
            set { seed = value != null && Fits(value, false) ? value : Memory; lastProblem = null; }
        }
        /// <summary>Nasiona siewcy w nocy.</summary>
        public string SeedNight
        {
            get => seedNight;
            set { seedNight = value != null && Fits(value, true) ? value : Memory; lastProblem = null; }
        }
        /// <summary>Podmiana o świcie i zmierzchu: nocne, które w dzień stoją, na dzienne (i odwrotnie). Domyślnie włączona.</summary>
        public bool Swap => swap;
        public string SeedFor(bool night) => night ? seedNight : seed;

        /// <summary>Roślina pasuje do ustawienia: dzienna do dnia, nocna do nocy (tryby pamięć, losowo, nic pasują zawsze).</summary>
        static bool Fits(string mode, bool night)
        {
            var game = GameLoop.Instance;
            var c = game != null && game.Crops != null ? game.Crops.Find(mode) : null;
            return c == null || (c.kind == Farming.CropKind.Night) == night;
        }

        /// <summary>
        /// Dlaczego siewca nie posieje wybranej rośliny (tekst do panelu i toastu) albo null: poza porą roku, brak nasion
        /// hybrydy w worku, brak monet na nasiono.
        /// </summary>
        public string SowerProblem(bool night)
        {
            var game = GameLoop.Instance;
            var c = game != null && game.Crops != null ? game.Crops.Find(SeedFor(night)) : null;
            if (c == null)
                return null;
            string name = Loc.Get("crop." + c.id);
            if (!Farming.CropSeasons.Now(c))
                return Loc.Get("ui.nest.sower_season", name, Farming.CropSeasons.Text(c));
            if (game.Seeds.Count(c.id) > 0)
                return null;
            if (c.hybrid)
                return Loc.Get("ui.nest.sower_seeds", name);
            int price = Market.SeedPrice(c);
            return game.Wallet.Coins < price ? Loc.Get("ui.nest.sower_coins", name, price) : null;
        }

        /// <summary>Czy w zasięgu gniazda jest pusta grządka (siewca nie wyrywa roślin, które teraz rosną).</summary>
        public bool HasFreePlot()
        {
            var t = Tile;
            foreach (var p in Farming.FarmPlot.All)
                if (p != null && p.State.IsEmpty && Mathf.Abs(p.Cell.x - t.c) <= AreaRadius && Mathf.Abs(p.Cell.y - t.r) <= AreaRadius)
                    return true;
            return false;
        }

        string lastProblem;

        /// <summary>Siewca stoi przez problem z nasionami: toast raz, przy każdej zmianie powodu (null = sieje, zapomina).</summary>
        public void ReportSowerProblem(string problem)
        {
            if (problem == lastProblem)
                return;
            lastProblem = problem;
            if (problem != null)
                GameEvents.RaiseToast("toast.sower.waits", problem);
        }

        /// <summary>Jedno z dwóch ustawień siewcy (dzień albo noc) dla koła nasion.</summary>
        class SeedSlot : ISeedChooser
        {
            public WispNest nest;
            public bool night;
            public string Seed
            {
                get => night ? nest.seedNight : nest.seed;
                set { if (night) nest.SeedNight = value; else nest.Seed = value; }
            }
            public Vector3 PickerAnchor => nest.transform.position;
        }

        public TilePos Tile
        {
            get
            {
                var obj = GetComponent<TileObject>();
                return obj != null && obj.Placed ? obj.Tile : Island.WorldToTile(transform.position);
            }
        }

        public void Setup(SpriteRenderer bodyRenderer, Sprite[] nestFrames, IslandArt islandArt)
        {
            body = bodyRenderer;
            frames = nestFrames;
            art = islandArt;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Ognik zniknął (np. rozbiórka gniazda): zwolnij jego miejsce.</summary>
        public void Forget(Wisp w)
        {
            wisps.Remove(w);
            slots.Remove(w);
        }

        void Update()
        {
            if (body != null && frames != null && frames.Length > 1)
                body.sprite = frames[Mathf.FloorToInt(Time.unscaledTime * 1.5f) % frames.Length];   // migotanie dziupli
        }

        /// <summary>Gniazdo przeniesione: ogniki wracają do nowego domu.</summary>
        void OnPlaced()
        {
            foreach (var w in wisps)
                if (w != null) w.OnNestMoved();
        }

        static WispKindArt KindArt(IslandArt art, WispKind kind)
        {
            if (art == null || art.wispKinds == null)
                return null;
            foreach (var k in art.wispKinds)
                if (k != null && k.kind == kind)
                    return k;
            return null;
        }

        public static Sprite[] FramesFor(IslandArt art, WispKind kind, bool glow)
        {
            if (art == null || art.wispKinds == null)
                return null;
            foreach (var k in art.wispKinds)
                if (k != null && k.kind == kind)
                    return glow ? k.glow : k.frames;
            return null;
        }

        /// <summary>Nowy ognik w gnieździe (zakup, darmowy pierwszy, zapis). Zwraca null, gdy gniazdo pełne.</summary>
        /// <param name="restoring">Wczytanie zapisu: bez limitu miejsc (ulepszenia i dary, które go podnoszą, wczytują się
        /// później niż wyspa, więc czwarty i piąty ognik by przepadły).</param>
        public Wisp AddWisp(WispKind kind, string id = null, int xp = 0, int name = -1, bool restoring = false)
        {
            wisps.RemoveAll(w => w == null);
            if (!restoring && wisps.Count >= Capacity)
                return null;
            int slot = 0;
            while (wisps.Exists(w => SlotOf(w) == slot))
                slot++;
            var go = new GameObject("Wisp_" + kind);
            go.transform.SetParent(transform, false);
            // sprite'y na osobnym obiekcie: ognik może się delikatnie odsunąć od sąsiada, nie zmieniając trasy lotu
            var b = new GameObject("Body").AddComponent<SpriteRenderer>();
            b.transform.SetParent(go.transform, false);
            var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
            g.transform.SetParent(b.transform, false);
            if (art != null && art.glow != null)
                g.sharedMaterial = art.glow;
            var frames = FramesFor(art, kind, false);
            var glows = FramesFor(art, kind, true);
            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                frames = art != null ? art.wispFrames : null;   // brak grafiki zawodu (Wyspa → 2): zwykły ognik
                glows = art != null ? art.wispGlow : null;
            }
            var w = go.AddComponent<Wisp>();
            w.Setup(kind, this, slot, string.IsNullOrEmpty(id) ? "w" + Guid.NewGuid().ToString("N").Substring(0, 8) : id, xp,
                    name >= 0 ? name : UnityEngine.Random.Range(0, Wisp.NameCount), b, g, frames, glows);
            var ka = KindArt(art, kind);
            if (ka != null)
                w.SetTool(ka.tool, ka.toolGlow, art.glow);   // narzędzie zawodu w dłoni
            Visual.NightGlow.Add(go, new Vector2(0f, 0.6f), KindColor(kind), 1.4f, 0.55f);
            slots[w] = slot;
            wisps.Add(w);
            return w;
        }

        readonly Dictionary<Wisp, int> slots = new Dictionary<Wisp, int>();
        int SlotOf(Wisp w) => slots.TryGetValue(w, out int s) ? s : -1;

        public static Color KindColor(WispKind k)
        {
            switch (k)
            {
                case WispKind.Sower: return new Color(0.56f, 0.82f, 0.42f);
                case WispKind.Woodcutter: return new Color(0.94f, 0.54f, 0.23f);
                case WispKind.Alchemist: return new Color(0.83f, 0.6f, 0.94f);
                case WispKind.Lamplighter: return new Color(1f, 0.88f, 0.54f);
                case WispKind.Gatherer: return new Color(0.87f, 0.91f, 1f);
                default: return new Color(0.37f, 0.81f, 0.72f);
            }
        }

        // ------------------------------------------------------------ zakup ognika

        /// <summary>Czy zawód jest dostępny: siewca wymaga ulepszenia „Runa siewu”, alchemik - kotła na wyspie.</summary>
        /// <summary>Jest miejsce na kolejnego ognika (limit z ulepszeń i darów).</summary>
        public bool HasRoom
        {
            get
            {
                wisps.RemoveAll(w => w == null);
                return wisps.Count < Capacity;
            }
        }

        public static bool KindUnlocked(WispKind k, GameLoop game)
        {
            if (game == null || !game.Upgrades.Has(Upgrades.UpgradeIds.WispGardener))
                return false;
            switch (k)
            {
                case WispKind.Sower: return game.Upgrades.Has(Upgrades.UpgradeIds.SoilMemory);
                case WispKind.Alchemist: return Alchemy.Cauldron.All.Count > 0;
                default: return true;
            }
        }

        public static List<CurrencyAmount> Cost(WispKind k)
        {
            int baseCoins;
            switch (k)
            {
                case WispKind.Woodcutter: case WispKind.Lamplighter: baseCoins = 250; break;
                case WispKind.Alchemist: baseCoins = 350; break;
                default: baseCoins = 200; break;
            }
            int coins = Mathf.RoundToInt(baseCoins * Mathf.Pow(1.5f, Wisp.CountOfKind(k)) / 5f) * 5;
            return new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, coins), new CurrencyAmount(CurrencyType.NightHarvest, 3) };
        }

        void Summon(WispKind k)
        {
            var game = GameLoop.Instance;
            if (game == null || wisps.Count >= Capacity || !KindUnlocked(k, game))
                return;
            var cost = Cost(k);
            if (!game.Wallet.TrySpend(cost))
                return;
            var w = AddWisp(k);
            if (w == null)
            {
                foreach (var c in cost) game.Wallet.Refund(c.type, c.amount);
                return;
            }
            Visual.IslandFx.FindPopped(transform.position + new Vector3(0f, 1f, 0f));
            GameEvents.PlaySound("magic", 0.7f);
            GameEvents.RaiseToast("toast.wisp.summoned", Loc.Get(w.NameKey), Loc.Get(Wisp.KindKey(k)));
        }

        // ------------------------------------------------------------ panel

        /// <summary>
        /// Panel gniazda w zakładkach (zamiast ściany tekstu): Ogniki (lista z poziomami), Czapki (gdy jakąś masz),
        /// Siewca (gdy jest siewca), Przywołaj. Przełączniki i przywołanie nie zamykają panelu.
        /// </summary>
        public void FillContext(ContextInfo info)
        {
            info.verticalButtons = true;
            wisps.RemoveAll(w => w == null);
            var game = GameLoop.Instance;

            if (info.Tab(Loc.Get("ui.nest.tab_wisps")))
            {
                info.lines.Add(Loc.Get("ui.nest.range", Wisp.Radius * 2 + 1));
                float tired = 0f;
                foreach (var w in wisps)
                {
                    int next = w.NextLevelXp;
                    string line = next < 0
                        ? Loc.Get("ui.nest.wisp_max", Loc.Get(w.NameKey), Loc.Get(Wisp.KindKey(w.Kind)), w.Level)
                        : Loc.Get("ui.nest.wisp", Loc.Get(w.NameKey), Loc.Get(Wisp.KindKey(w.Kind)), w.Level, w.Xp, next);
                    // nastrój: ile procent pracy (poniżej 60 wolniej), śpiący odpoczywa
                    line += "  " + (w.Resting ? Loc.Get("ui.nest.resting") : Loc.Get("ui.nest.mood", Mathf.RoundToInt(w.Mood), Mathf.RoundToInt(w.MoodFactor * 100f)));
                    info.lines.Add(line);
                    if (w.Mood < Wisp.Content) tired++;
                }
                if (tired > 0 && WispDreamHouse.All.Count == 0 && WispBonfire.All.Count == 0)
                    info.lines.Add(Loc.Get("ui.nest.need_rest"));
                if (wisps.Count == 0)
                    info.lines.Add(Loc.Get("ui.nest.empty"));
            }

            // czapki: klik = następna posiadana czapka (albo bez); panel zostaje otwarty, można klikać dalej
            if (Economy.Hats.AnyOwned && wisps.Count > 0 && info.Tab(Loc.Get("ui.nest.tab_hats")))
                foreach (var w in wisps)
                {
                    var wisp = w;
                    string hatName = wisp.Hat != null ? Loc.Get("hat." + wisp.Hat) : Loc.Get("ui.nest.no_hat");
                    info.Button(Loc.Get("ui.nest.hat", Loc.Get(wisp.NameKey), hatName), () =>
                    {
                        wisp.SetHat(Economy.Hats.Next(wisp.Hat));
                        GameEvents.PlaySound("click", 0.5f);
                    }, true, true);
                }

            if (wisps.Exists(w => w.Kind == WispKind.Sower) && info.Tab(Loc.Get("ui.nest.tab_sower")))
            {
                foreach (bool night in new[] { false, true })
                {
                    var slot = new SeedSlot { nest = this, night = night };
                    string s = slot.Seed;
                    string what = s == Memory ? Loc.Get("ui.rune.memory") : s == Off ? Loc.Get("ui.rune.off")
                                : s == Random ? Loc.Get("ui.rune.random") : Loc.Get("crop." + s);
                    string problem = SowerProblem(night);
                    if (problem != null)
                        info.lines.Add(problem);
                    info.Button(Loc.Get(night ? "ui.nest.sower_night" : "ui.nest.sower_day", what), () =>
                    {
                        var ui = UI.UiRoot.Instance;
                        if (ui != null)
                            ui.Wheel.OpenPicker(slot, slot.night);   // koło nasion zamiast panelu
                    });
                }
                if (!HasFreePlot())
                    info.lines.Add(Loc.Get("ui.nest.sower_no_plot"));
                info.Button(Loc.Get("ui.nest.swap", Loc.Get(swap ? "ui.on" : "ui.off")), () => swap = !swap, true, true);
            }

            if (info.Tab(Loc.Get("ui.nest.tab_summon")))
            {
                if (wisps.Count >= Capacity)
                    info.lines.Add(Loc.Get("ui.nest.full", Capacity));
                else
                    foreach (WispKind k in Enum.GetValues(typeof(WispKind)))
                    {
                        if (!KindUnlocked(k, game))
                            continue;
                        var kind = k;
                        var cost = Cost(k);
                        info.Button(Loc.Get("ui.nest.summon", Loc.Get(Wisp.KindKey(k)), UI.PlacementMode.CostText(cost)),
                                    () => Summon(kind), game != null && game.Wallet.CanAfford(cost), true);
                    }
            }
        }

        // ------------------------------------------------------------ zapis

        public List<WispSave> Snapshot()
        {
            var list = new List<WispSave>();
            foreach (var w in wisps)
                if (w != null)
                    list.Add(new WispSave { id = w.Id, kind = (int)w.Kind, xp = w.Xp, name = w.NameIndex, hat = w.Hat, mood = w.Mood });
            return list;
        }

        public void Restore(List<WispSave> list, string seedMode, string seedNightMode, bool noSwap)
        {
            seed = seedMode ?? Memory;
            seedNight = seedNightMode ?? Memory;
            swap = !noSwap;
            // zapis sprzed v14 (jedno ustawienie): nasiono nocne przechodzi do ustawienia nocy
            var game = GameLoop.Instance;
            if (seedNightMode == null && game != null && game.Crops != null && game.Crops.Find(seed) is Farming.CropDefinition c
                && c.kind == Farming.CropKind.Night)
            {
                seedNight = seed;
                seed = Memory;
            }
            // roślina w złym ustawieniu (dawne koło siewcy pozwalało przełączyć dzień/noc): przenosi ją albo czyści,
            // inaczej siewca nigdy by jej nie posiał (np. dynia w ustawieniu nocy)
            if (!Fits(seedNight, true) && seed == Memory && Fits(seedNight, false))
            {
                seed = seedNight;
                seedNight = Memory;
            }
            if (!Fits(seed, false) && seedNight == Memory && Fits(seed, true))
            {
                seedNight = seed;
                seed = Memory;
            }
            if (!Fits(seed, false)) seed = Memory;
            if (!Fits(seedNight, true)) seedNight = Memory;
            if (list == null)
                return;
            foreach (var s in list)
                if (s != null && Enum.IsDefined(typeof(WispKind), s.kind))
                {
                    var w = AddWisp((WispKind)s.kind, s.id, s.xp, s.name, true);
                    if (w != null && !string.IsNullOrEmpty(s.hat))
                        w.SetHat(s.hat);
                    if (w != null)
                        w.SetMood(s.mood);
                }
        }
    }
}
