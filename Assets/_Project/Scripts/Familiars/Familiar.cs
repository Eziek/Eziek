using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Input;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.UI;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Familiars
{
    /// <summary>
    /// Chowaniec na wyspie (nie zajmuje kafelka). Każdy żyje po swojemu (GDD §9 p. 102):
    /// kot spaceruje, siada, myje się, w nocy śpi przy gnieździe, poluje na nisko lecące ogniki (skok, ognik podskakuje
    /// z „!”) i odwiedza inne chowańce; kruk siada na drzewach i lampach, kracze, skacze po ziemi przy grządkach i dziobie,
    /// czasem krąży nad wyspą; ropucha skacze do grządek, w deszczu i w nocy rechocze, w nocy łapie językiem świetliki;
    /// nietoperz w nocy krąży wokół lamp (czasem goni ćmę), w dzień wisi pod koroną drzewa i się kołysze; ćma w nocy
    /// trzepocze przy lampach i świecących roślinach (sama świeci), w dzień odpoczywa na drzewie; sowa w dzień drzemie
    /// na drzewie, w nocy rozgląda się za ognikami, pohukuje i bezszelestnie przelatuje; ślimak pełznie powoli do grządek,
    /// zostawia świecący ślad, chowa się w skorupce, gdy kursor jest blisko; lis-widmo pojawia się o zmierzchu, biega
    /// po wyspie, siada na brzegu patrząc w morze, w nocy zwija się w kłębek, o świcie znika.
    /// Klik: panel z bonusem, więzią, „Pogłaszcz” i „Nakarm”. Kursor obok: dymek z bonusem.
    /// </summary>
    public class Familiar : MonoBehaviour, IClickable, IContextActions, IPriorityClick
    {
        public enum Mood { Happy, Fed, Petted, Startled }

        const float Ppu = 16f;
        public static readonly List<Familiar> All = new List<Familiar>();

        FamiliarDef def;
        SpriteRenderer body, glow;
        Sprite[] frames, glows;
        Transform visual;          // sprite'y (podskok przy reakcji, bez ruszania toru)
        BoxCollider2D hit;
        WispBubble bubble;
        Light2D ownLight;          // ćma świeci w nocy
        bool flying;
        bool hovered;
        float hoverReact;
        string forcedPose;
        float forcedUntil;
        float hopUntil;
        float alpha = 1f;          // lis-widmo: widoczny tylko w nocy
        float seed;
        readonly object tipOwner = new object();

        public string Kind => def != null ? def.id : null;
        public bool Flying => flying;
        /// <summary>Widać go (lis-widmo w dzień znika).</summary>
        public bool Visible => alpha > 0.3f;
        /// <summary>Punkt na ziemi pod chowańcem (cień): latające mają go niżej.</summary>
        public Vector3 GroundPoint => transform.position - new Vector3(0f, flying ? 0.55f : 0f, 0f);
        public float Alpha => alpha;

        public void Setup(FamiliarDef familiarDef, SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowFrames)
        {
            def = familiarDef;
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glows = glowFrames;
            visual = body.transform != transform ? body.transform : null;
            seed = Random.value * 10f;
            hit = gameObject.AddComponent<BoxCollider2D>();
            hit.size = new Vector2(0.9f, 0.75f);
            hit.offset = new Vector2(0f, 0.38f);
            bubble = WispBubble.Create(transform.parent, visual != null ? visual : transform, 1.0f);
            if (def.id == FamiliarSystem.Moth)
            {
                var lg = new GameObject("MothLight");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                ownLight = lg.AddComponent<Light2D>();
                ownLight.lightType = Light2D.LightType.Point;
                ownLight.pointLightOuterRadius = 1.1f;
                ownLight.pointLightInnerRadius = 0.1f;
                ownLight.color = new Color(0.85f, 0.7f, 1f);
                ownLight.intensity = 0f;
            }
            if (def.id == FamiliarSystem.Fox && GameLoop.Instance != null && !GameLoop.Instance.Clock.IsNight && !GameLoop.Instance.Clock.IsDusk)
                alpha = 0f;   // lis-widmo w dzień od razu niewidoczny (bez znikania po wczytaniu)
            Show(def.poses[0]);
            StartCoroutine(Life());
        }

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            if (hovered && UiRoot.Instance != null)
                UiRoot.Instance.Tooltip.Hide(tipOwner);
            hovered = false;
        }

        void OnDestroy()
        {
            if (bubble != null)
                Destroy(bubble.gameObject);
        }

        // ------------------------------------------------------------ klatki

        /// <summary>Poza po nazwie (reakcja może ją na chwilę nadpisać).</summary>
        void Show(string pose)
        {
            if (forcedPose != null && Time.unscaledTime < forcedUntil)
                pose = forcedPose;
            int i = def.Pose(pose);
            if (i < 0 || frames == null || i >= frames.Length || frames[i] == null)
                i = 0;
            if (frames == null || frames.Length == 0)
                return;
            body.sprite = frames[i];
            if (glow != null)
            {
                var g = glows != null && i < glows.Length ? glows[i] : null;
                glow.sprite = g;
                glow.enabled = g != null;
            }
        }

        /// <summary>Klatka z cyklu póz (np. chód: walk0..walk3) w tempie fps.</summary>
        void Cycle(float fps, params string[] poses) => Show(poses[Mathf.FloorToInt(Time.unscaledTime * fps + seed) % poses.Length]);

        void Face(float dx)
        {
            if (Mathf.Abs(dx) > 0.01f)
                body.flipX = dx < 0f;
            if (glow != null)
                glow.flipX = body.flipX;
        }

        void Say(int glyph)
        {
            if (bubble != null && !bubble.Showing)
                bubble.Say(new[] { glyph }, new Color(0.95f, 0.85f, 1f), 0.8f, OnScreen() ? 0.25f : 0f);
        }

        bool OnScreen()
        {
            var cam = Camera.main;
            if (cam == null)
                return false;
            var v = cam.WorldToViewportPoint(transform.position);
            return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        // ------------------------------------------------------------ co klatkę

        void LateUpdate()
        {
            // kolejność rysowania z wysokości na ekranie (jak obiekty na kafelkach); w locie ponad wszystkim
            int order = flying ? 4600 : Mathf.RoundToInt(Island.WorldToPixel(transform.position).y) + 4;
            var home = Home;
            if (!flying && home != null && (transform.position - home.Spot).sqrMagnitude < 0.02f)
                order = home.FrontOrder;   // w domku (kosz, dziupla, grzęda): przed budowlą
            else if (!flying && perchOrder != 0 && (transform.position - perchAt).sqrMagnitude < 0.02f)
                order = Mathf.Max(order, perchOrder);   // na koronie drzewa, lampie, tablicy: przed nimi
            body.sortingOrder = order;
            if (glow != null) glow.sortingOrder = order + 1;
            // podskok przy reakcji (tylko sprite)
            if (visual != null)
            {
                float k = hopUntil - Time.unscaledTime;
                visual.localPosition = k > 0f ? new Vector3(0f, Mathf.Round(Mathf.Sin(k / 0.4f * Mathf.PI) * 3f) / Ppu, 0f) : Vector3.zero;
            }
            UpdateGhost();
            if (ownLight != null)
                ownLight.intensity = Mathf.MoveTowards(ownLight.intensity, Night ? 0.55f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f + seed) : 0f, Time.unscaledDeltaTime);
            hit.enabled = Visible && !Save.SaveSystem.InMenu;
            UpdateHover();
        }

        /// <summary>Lis-widmo: pojawia się o zmierzchu, znika o świcie, w nocy lekko migocze.</summary>
        void UpdateGhost()
        {
            if (def.id != FamiliarSystem.Fox)
                return;
            var clock = GameLoop.Instance != null ? GameLoop.Instance.Clock : null;
            bool out_ = clock != null && (clock.IsNight || clock.IsDusk);
            float target = out_ ? 0.72f + 0.08f * Mathf.Sin(Time.unscaledTime * 2.3f + seed) : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 0.4f);
            body.color = new Color(1f, 1f, 1f, alpha);
            if (glow != null)
                glow.color = new Color(1f, 1f, 1f, alpha);
        }

        void UpdateHover()
        {
            var cam = Camera.main;
            var ui = UiRoot.Instance;
            if (cam == null || ui == null || Save.SaveSystem.InMenu)
                return;
            Vector2 mouse = cam.ScreenToWorldPoint(UiInput.MousePosition);
            bool over = Visible && !ui.PointerOverUi && ((Vector2)transform.position + new Vector2(0f, 0.35f) - mouse).sqrMagnitude < 0.5f * 0.5f;
            if (over && !hovered)
            {
                ui.Tooltip.Show(tipOwner, Loc.Get("familiar." + Kind) + "\n" + FamiliarSystem.BonusText(Kind)
                                          + "\n" + Loc.Get("ui.familiar.bond", FamiliarSystem.Bond(Kind), FamiliarSystem.MaxBond));
                if (Time.unscaledTime > hoverReact)
                {
                    hoverReact = Time.unscaledTime + 3f;
                    if (def.id == FamiliarSystem.Snail)
                        React(Mood.Startled);   // ślimak chowa się w skorupce
                    else
                        IslandFx.FindPopped(transform.position + new Vector3(0f, 0.4f, 0f));
                }
            }
            else if (!over && hovered)
                ui.Tooltip.Hide(tipOwner);
            hovered = over;
        }

        // ------------------------------------------------------------ klik i reakcje

        public void OnClick()
        {
            if (Visible && !Save.SaveSystem.InMenu)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            string k = Kind;
            info.title = Loc.Get("familiar." + k);
            info.worldAnchor = transform.position + new Vector3(0f, 0.8f, 0f);
            info.lines.Add(FamiliarSystem.BonusText(k));
            int bond = FamiliarSystem.Bond(k);
            info.lines.Add(Loc.Get("ui.familiar.bond", bond, FamiliarSystem.MaxBond));
            int next = FamiliarSystem.NextBondFeeds(k);
            if (next > 0)
                info.lines.Add(Loc.Get("ui.familiar.next", next - FamiliarSystem.Feeds(k)));
            info.Button(Loc.Get("ui.familiar.pet"), () => React(Mood.Petted), true, true);
            if (FamiliarSystem.FedToday(k))
                info.lines.Add(Loc.Get("ui.familiar.full"));
            else
            {
                var cost = FamiliarSystem.TreatCost(k);
                var game = GameLoop.Instance;
                bool can = game != null && game.Wallet.CanAfford(cost);
                info.Button(Loc.Get("ui.familiar.feed", Loc.Get("familiar." + k + ".treat"), PlacementMode.CostText(cost)),
                            () => { if (FamiliarSystem.Instance != null) FamiliarSystem.Instance.Feed(k); }, can, true);
            }
        }

        /// <summary>Reakcja: dymek, iskry, podskok i poza charakterystyczna dla zwierzęcia.</summary>
        public void React(Mood mood)
        {
            hopUntil = Time.unscaledTime + 0.4f;
            switch (mood)
            {
                case Mood.Startled:
                    Force(def.id == FamiliarSystem.Snail ? "hide" : def.poses[0], 2.5f);
                    Say(RuneGlyphs.Exclaim);
                    return;
                case Mood.Fed:
                    Say(RuneGlyphs.Heart);
                    IslandFx.FindPopped(transform.position + new Vector3(0f, 0.5f, 0f));
                    break;
                case Mood.Happy:
                    Say(RuneGlyphs.Star);
                    break;
                default:
                    Say(def.id == FamiliarSystem.Cat || def.id == FamiliarSystem.Fox ? RuneGlyphs.Heart : RuneGlyphs.Note);
                    if (OnScreen())
                        GameEvents.PlaySound("babble_hum", 0.3f);
                    break;
            }
            // poza „zadowolenia”
            switch (def.id)
            {
                case FamiliarSystem.Cat: Force("blink", 1.6f); break;          // mruży oczy (mruczenie)
                case FamiliarSystem.Raven: Force("caw", 0.6f); break;
                case FamiliarSystem.Toad: Force("croak", 1.2f); break;
                case FamiliarSystem.Owl: Force("hoot", 1.0f); break;
                case FamiliarSystem.Snail: Force("hide", 1.5f); break;
                case FamiliarSystem.Fox: Force("sit1", 1.2f); break;
            }
        }

        void Force(string pose, float seconds)
        {
            if (def.Pose(pose) < 0)
                return;
            forcedPose = pose;
            forcedUntil = Time.unscaledTime + seconds;
            Show(pose);
        }

        // ------------------------------------------------------------ zachowanie: wspólne

        IEnumerator Life()
        {
            switch (def.id)
            {
                case FamiliarSystem.Cat: return CatLife();
                case FamiliarSystem.Raven: return RavenLife();
                case FamiliarSystem.Toad: return ToadLife();
                case FamiliarSystem.Bat: return BatLife();
                case FamiliarSystem.Moth: return MothLife();
                case FamiliarSystem.Owl: return OwlLife();
                case FamiliarSystem.Snail: return SnailLife();
                default: return FoxLife();
            }
        }

        static bool Night => GameLoop.Instance != null && GameLoop.Instance.Clock.IsNight;
        static bool Raining => Weather.WeatherSystem.Raining;

        /// <summary>Losowe miejsce na trawie (podstawa kafelka, z przesunięciem w poziomie); nigdy na wodzie ani klifie.</summary>
        static Vector3 RandomLand()
        {
            var island = Island.Instance;
            if (island == null || !island.Ready || island.Map.Count == 0)
                return Vector3.zero;
            var tiles = new List<TilePos>(island.Map.Tiles.Keys);
            for (int i = 0; i < 10; i++)
            {
                var t = tiles[Random.Range(0, tiles.Count)];
                var p = Island.TileAnchor(t, Random.Range(9, 15), out _) + new Vector3(Random.Range(-5, 6) / Ppu, 0f, 0f);
                if (Walkable(p))
                    return p;
            }
            return ToLand(Island.TileAnchor(tiles[Random.Range(0, tiles.Count)], 12, out _));
        }

        // ---- chodzenie tylko po lądzie (GDD §9 p. 125): kot, ropucha, ślimak i lis (oraz kruk, gdy ląduje) nie wchodzą
        // na wodę ani klif. Cel przesuwamy na najbliższą trawę, a gdy prosta droga przecina wodę, trasa omija ją po trawie
        // (World/LandGrid: BFS po pikselach, wygładzenie). Siatka liczona od nowa po każdej przebudowie obrazu wyspy.

        static LandGrid grid;

        static LandGrid Grid()
        {
            var island = Island.Instance;
            var pic = island != null ? island.Picture : null;
            if (pic == null || pic.landMask == null)
                return null;
            if (grid == null || grid.Picture != pic)
                grid = new LandGrid(pic, island.Map);
            return grid;
        }

        static Vector3 ToWorld((double x, double y) p) => Snap(Island.PixelToWorld((float)p.x, (float)p.y));

        /// <summary>Trawa na kafelku gracza (nie woda, klif, pas 2 px przy brzegu ani nieprzyłączona wysepka).</summary>
        static bool Walkable(Vector3 p)
        {
            var g = Grid();
            if (g == null)
                return true;
            var px = Island.WorldToPixel(p);
            return g.Walkable(px.x, px.y);
        }

        /// <summary>Najbliższy punkt na trawie (bez trawy w pobliżu: bez zmian).</summary>
        static Vector3 ToLand(Vector3 p)
        {
            var g = Grid();
            if (g == null)
                return p;
            var px = Island.WorldToPixel(p);
            return g.Nearest(px.x, px.y, out double x, out double y) ? ToWorld((x, y)) : p;
        }

        /// <summary>Czy prosty odcinek idzie cały po trawie.</summary>
        static bool LineOnLand(Vector3 a, Vector3 b)
        {
            var g = Grid();
            if (g == null)
                return true;
            var pa = Island.WorldToPixel(a);
            var pb = Island.WorldToPixel(b);
            return g.Line(pa.x, pa.y, pb.x, pb.y);
        }

        /// <summary>Punkty drogi po trawie z from do to (pusta: nie ma dokąd iść, chowaniec zostaje).</summary>
        static List<Vector3> Route(Vector3 from, Vector3 to)
        {
            var g = Grid();
            var route = new List<Vector3>();
            if (g == null)
            {
                route.Add(to);
                return route;
            }
            var pf = Island.WorldToPixel(from);
            var pt = Island.WorldToPixel(to);
            foreach (var p in g.Route(pf.x, pf.y, pt.x, pt.y))
                route.Add(ToWorld(p));
            return route;
        }

        // ---- domek (FamiliarHome): chowaniec kręci się wokół niego, wraca i w nim śpi

        FamiliarHome Home => FamiliarHome.Of(Kind);
        bool HomeChance(float p) => Home != null && Random.value < p;
        /// <summary>Spacer: zwykle w pobliżu domku, czasem dalej po wyspie.</summary>
        Vector3 Wander() => HomeChance(0.6f) ? Home.Around() : RandomLand();
        Vector3 PlotOrHome() => HomeChance(0.35f) ? Home.Spot : NearPlot();
        Vector3 PerchOrHome(float below = 0f)
        {
            if (!HomeChance(0.4f))
                return RandomPerch(below);
            hasPerch = false;   // do domku: grzęda wolna dla innych
            return Home.Spot;
        }
        Vector3 SleepSpot() => Home != null ? Home.Spot : CozySpot();

        /// <summary>Miejsce obok grządki (ropucha, kruk, ślimak).</summary>
        static Vector3 NearPlot()
        {
            var plots = FarmPlot.All;
            return plots.Count > 0 ? plots[Random.Range(0, plots.Count)].transform.position + new Vector3(Random.Range(-6, 7) / Ppu, -6f / Ppu, 0f)
                                   : RandomLand();
        }

        /// <summary>Ciepłe miejsce na noc: przy gnieździe ogników albo skrzyni.</summary>
        static Vector3 CozySpot()
        {
            if (WispNest.All.Count > 0)
                return WispNest.All[Random.Range(0, WispNest.All.Count)].transform.position + new Vector3(Random.Range(-10, 11) / Ppu, -2f / Ppu, 0f);
            if (HarvestCrate.All.Count > 0)
                return HarvestCrate.All[0].transform.position + new Vector3(0.6f, -2f / Ppu, 0f);
            return RandomLand();
        }

        /// <summary>
        /// Gdzie może usiąść ptak: korony drzew (owocowych i dzikich), tablica, lampy; z kolejnością rysowania budowli, na której
        /// się siedzi (żeby ptak był przed koroną, nie za nią). hang: można pod nią wisieć (nietoperz), tylko korony drzew.
        /// </summary>
        static List<(Vector3 at, int order, bool hang)> Perches()
        {
            var list = new List<(Vector3, int, bool)>();
            foreach (var t in FruitTree.All)
            {
                var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
                if (t != null && t.Adult && sr != null)
                    list.Add((new Vector3(sr.bounds.center.x + Random.Range(-0.3f, 0.3f), sr.bounds.max.y - 0.45f, 0f), sr.sortingOrder + 3, true));
            }
            var island = Island.Instance;
            if (island != null)
            {
                foreach (var tr in island.Trees)
                {
                    var sr = tr != null ? tr.GetComponent<SpriteRenderer>() : null;
                    if (sr != null)
                        list.Add((new Vector3(sr.bounds.center.x, sr.bounds.max.y - 0.5f, 0f), sr.sortingOrder + 3, true));
                }
                var board = island.Board;
                if (board != null)
                    list.Add((board.transform.position + new Vector3(0f, 1.1f, 0f), Order(board.gameObject), false));
            }
            foreach (var l in Lamp.All)
                if (l != null)
                    list.Add((l.transform.position + new Vector3(0f, 1.4f, 0f), Order(l.gameObject), false));
            return list;
        }

        static int Order(GameObject go) => go.TryGetComponent(out SpriteRenderer sr) ? sr.sortingOrder + 3 : 0;

        Vector3 perchAt, perchBase;
        int perchOrder;
        bool hasPerch;

        /// <summary>Grzęda zajęta przez innego chowańca: siedzi na niej albo do niej leci.</summary>
        bool Taken(Vector3 at)
        {
            foreach (var o in All)
            {
                if (o == null || o == this || !o.hasPerch)
                    continue;
                var d = o.perchBase - at;
                if (Mathf.Abs(d.x) > 0.6f || Mathf.Abs(d.y) > 0.9f)
                    continue;
                if (o.flying || (o.transform.position - o.perchAt).sqrMagnitude < 0.05f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Losowa wolna grzęda; zapamiętuje, przed czym ma się rysować, gdy na niej siedzi. below > 0: wisi pod nią
        /// (nietoperz), więc tylko korony drzew (pod tablicą czy lampą wisiałby na środku deski albo słupa).
        /// </summary>
        Vector3 RandomPerch(float below = 0f)
        {
            var all = Perches();
            var free = all.FindAll(p => (below <= 0f || p.hang) && !Taken(p.at));
            if (free.Count == 0 && Home != null)
            {
                hasPerch = false;   // wszystkie zajęte: do domku zamiast na cudzą grzędę
                return Home.Spot;
            }
            if (free.Count == 0)
                free = all.FindAll(p => below <= 0f || p.hang);   // bez domku: jak dawniej, byle nie na desce
            if (free.Count == 0)
                free = all;
            if (free.Count == 0)
            {
                hasPerch = false;
                return RandomLand() + new Vector3(0f, 0.8f, 0f);
            }
            var (at, order, _) = free[Random.Range(0, free.Count)];
            perchBase = at;
            perchAt = at - new Vector3(0f, below, 0f);
            perchOrder = order;
            hasPerch = true;
            return perchAt;
        }


        /// <summary>Spacer po lądzie: odcinkami po trasie z Route (omija wodę).</summary>
        IEnumerator Walk(Vector3 to, float speed, float fps, params string[] poses)
        {
            foreach (var leg in Route(transform.position, to))
                yield return WalkLeg(leg, speed, fps, poses);
        }

        IEnumerator WalkLeg(Vector3 to, float speed, float fps, string[] poses)
        {
            var from = transform.position;
            float dist = Vector3.Distance(from, to);
            Face(to.x - from.x);
            float dur = Mathf.Max(0.2f, dist / speed);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                transform.position = Vector3.Lerp(from, to, t / dur);
                Cycle(fps, poses);
                yield return null;
            }
            transform.position = Snap(to);
        }

        IEnumerator Fly(Vector3 to, float speed, float arc, float fps, params string[] poses)
        {
            flying = true;
            var from = transform.position;
            Face(to.x - from.x);
            float dur = Mathf.Max(0.4f, Vector3.Distance(from, to) / speed);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float u = t / dur, e = u * u * (3f - 2f * u);
                transform.position = Vector3.Lerp(from, to, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * arc, 0f);
                Cycle(fps, poses);
                yield return null;
            }
            transform.position = Snap(to);
            flying = false;
        }

        /// <summary>Czeka, pokazując pozę z funkcji (np. siedzenie z mruganiem).</summary>
        IEnumerator Idle(float seconds, System.Func<float, string> pose)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Show(pose(t));
                yield return null;
            }
        }

        /// <summary>Siedzenie: zwykle poza 0, co jakiś czas mrugnięcie albo machnięcie ogonem.</summary>
        string Sitting(float t, string sit, string alt, string blink)
        {
            float c = (t + seed) % 4f;
            if (blink != null && c > 3.8f) return blink;
            if (alt != null && Mathf.FloorToInt((t + seed) * 1.5f) % 5 == 0) return alt;
            return sit;
        }

        // ------------------------------------------------------------ kot

        IEnumerator CatLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                float r = Random.value;
                if (Night && r < 0.45f)
                {
                    // drzemka w ciepłym miejscu, oddech, „Z”, po przebudzeniu przeciąganie
                    yield return Walk(SleepSpot(), 1.1f, 8f, "walk0", "walk1", "walk2", "walk3");
                    float nap = Random.Range(15f, 30f), z = 2f;
                    for (float t = 0f; t < nap; t += Time.unscaledDeltaTime)
                    {
                        Cycle(0.7f, "sleep0", "sleep1");
                        z -= Time.unscaledDeltaTime;
                        if (z <= 0f) { z = Random.Range(4f, 7f); Say(RuneGlyphs.Sleep); }
                        yield return null;
                    }
                    yield return Idle(1.2f, _ => "stretch");
                    continue;
                }
                var wisp = r < 0.3f ? NearWisp(3.5f) : null;
                if (wisp != null)
                {
                    yield return Hunt(wisp);
                    continue;
                }
                if (r < 0.45f && VisitFriend(out var friend))
                {
                    var spot = friend.transform.position + new Vector3(friend.transform.position.x > transform.position.x ? -0.7f : 0.7f, 0f, 0f);
                    yield return Walk(spot, 1.1f, 8f, "walk0", "walk1", "walk2", "walk3");
                    Face(friend.transform.position.x - transform.position.x);
                    if (friend.Kind == FamiliarSystem.Raven && !friend.Flying)
                        friend.Scare();   // kruk nie daje się podejść
                    else
                        Say(RuneGlyphs.Heart);
                    yield return Idle(Random.Range(4f, 8f), t => Sitting(t, "sit0", "sit1", "blink"));
                    continue;
                }
                if (r < 0.6f)
                {
                    yield return Idle(Random.Range(2.5f, 4.5f), t => Mathf.FloorToInt(t * 3f) % 2 == 0 ? "groom" : "sit0");   // mycie łapki
                    continue;
                }
                if (HomeChance(0.25f))
                {
                    // w dzień też lubi swój kosz: siada i mruży oczy, czasem drzemie
                    yield return Walk(Home.Spot, 1.1f, 8f, "walk0", "walk1", "walk2", "walk3");
                    yield return Idle(Random.Range(6f, 14f), t => t > 5f ? (Mathf.FloorToInt(t * 0.7f) % 2 == 0 ? "sleep0" : "sleep1")
                                                                         : Sitting(t, "sit0", "sit1", "blink"));
                    continue;
                }
                yield return Walk(Wander(), 1.1f, 8f, "walk0", "walk1", "walk2", "walk3");
                yield return Idle(Random.Range(4f, 12f), t => Sitting(t, "sit0", "sit1", "blink"));
            }
        }

        Wisp NearWisp(float range)
        {
            Wisp best = null;
            float bestD = range * range;
            foreach (var w in Wisp.All)
            {
                if (w == null)
                    continue;
                float d = (w.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        bool VisitFriend(out Familiar friend)
        {
            friend = null;
            var others = All.FindAll(f => f != this && f != null && f.Visible && !f.Flying);
            if (others.Count == 0)
                return false;
            friend = others[Random.Range(0, others.Count)];
            return true;
        }

        /// <summary>Polowanie na ognika: podkradanie się, skok, ognik podskakuje z „!”, kot siada zadowolony.</summary>
        IEnumerator Hunt(Wisp wisp)
        {
            var under = new Vector3(wisp.transform.position.x, wisp.transform.position.y - 0.9f, 0f);
            var near = under + new Vector3(under.x > transform.position.x ? -0.8f : 0.8f, 0f, 0f);
            if (!Walkable(under) || !Walkable(near) || !LineOnLand(near, under))
                yield break;   // ognik nad wodą albo za klifem: kot nie skacze do wody
            yield return Walk(near, 0.6f, 4f, "walk0", "walk1", "walk2", "walk3");   // skradanie
            yield return Idle(0.6f, _ => "stretch");                                  // przyczajenie
            if (wisp == null)
                yield break;
            var from = transform.position;
            var to = new Vector3(wisp.transform.position.x, under.y, 0f);
            Face(to.x - from.x);
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float u = t / 0.45f;
                transform.position = Vector3.Lerp(from, to, u) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.5f, 0f);
                Show("pounce");
                yield return null;
            }
            transform.position = Snap(to);
            if (wisp != null)
                wisp.Startle();
            yield return Idle(Random.Range(2f, 4f), t => Sitting(t, "sit0", "sit1", "blink"));
        }

        // ------------------------------------------------------------ kruk

        bool scared;

        /// <summary>Ktoś się zbliża (kot): kruk zrywa się z ziemi i odlatuje na grzędę.</summary>
        public void Scare()
        {
            if (def.id != FamiliarSystem.Raven || flying)
                return;
            scared = true;
            Force("caw", 0.4f);
            Say(RuneGlyphs.Exclaim);
        }

        IEnumerator RavenLife()
        {
            transform.position = RandomPerch();
            while (true)
            {
                float r = Random.value;
                if (r < 0.5f)
                {
                    yield return Fly(PerchOrHome(), 3f, 0.6f, 10f, "fly0", "fly1", "fly2", "fly1");
                    float stay = Random.Range(6f, 15f);
                    for (float t = 0f; t < stay; t += Time.unscaledDeltaTime)
                    {
                        float c = (t + seed) % 5f;
                        Show(c < 0.25f ? "peck" : c > 4.4f && c < 4.9f && Mathf.FloorToInt(seed) % 2 == 0 ? "caw" : Sitting(t, "sit0", "sit1", null));
                        yield return null;
                    }
                }
                else if (r < 0.82f)
                {
                    // na ziemi przy grządkach: podskoki i dziobanie
                    scared = false;
                    yield return Fly(ToLand(NearPlot()), 3f, 0.8f, 10f, "fly0", "fly1", "fly2", "fly1");   // ląduje na trawie, nie w wodzie
                    for (int i = 0; i < Random.Range(3, 7) && !scared; i++)
                    {
                        var from = transform.position;
                        var to = from + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.1f, 0.1f), 0f);
                        if (!Walkable(to))
                            to = from;   // podskok w miejscu zamiast do wody
                        Face(to.x - from.x);
                        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
                        {
                            transform.position = Vector3.Lerp(from, to, t / 0.2f) + new Vector3(0f, Mathf.Sin(t / 0.2f * Mathf.PI) * 0.1f, 0f);
                            Show("sit1");
                            yield return null;
                        }
                        transform.position = Snap(to);
                        yield return Idle(Random.Range(0.6f, 1.4f), t => Mathf.FloorToInt(t * 4f) % 3 == 0 ? "peck" : "sit0");
                    }
                }
                else
                {
                    // krąg nad wyspą
                    var center = RandomLand() + new Vector3(0f, 1.6f, 0f);
                    yield return Fly(center + new Vector3(1.2f, 0f, 0f), 3f, 0.4f, 10f, "fly0", "fly1", "fly2", "fly1");
                    flying = true;
                    for (float t = 0f; t < 5f; t += Time.unscaledDeltaTime)
                    {
                        float a = t * 1.3f;
                        var p = center + new Vector3(Mathf.Cos(a) * 1.2f, Mathf.Sin(a) * 0.45f, 0f);
                        Face(p.x - transform.position.x);
                        transform.position = p;
                        Cycle(6f, "fly0", "fly1", "fly2", "fly1");
                        yield return null;
                    }
                    flying = false;
                }
            }
        }

        // ------------------------------------------------------------ ropucha

        IEnumerator ToadLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                int hops = 0;
                foreach (var to in Route(transform.position, PlotOrHome()))
                while ((to - transform.position).sqrMagnitude > 0.05f && hops++ < 24)
                {
                    var from = transform.position;
                    var step = Vector3.ClampMagnitude(to - from, 0.35f);
                    Face(step.x);
                    yield return Idle(0.15f, _ => "crouch");
                    for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                    {
                        float u = t / 0.3f;
                        transform.position = from + step * u + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.18f, 0f);
                        Show("jump");
                        yield return null;
                    }
                    transform.position = Snap(from + step);
                    yield return Idle(Random.Range(0.4f, 1.2f), t => Sitting(t, "sit", null, "blink"));
                }
                // na miejscu: w deszczu i w nocy rechot, w nocy łowi świetliki
                float stay = Random.Range(4f, 10f);
                float next = 1.5f;
                for (float t = 0f; t < stay; t += Time.unscaledDeltaTime)
                {
                    next -= Time.unscaledDeltaTime;
                    if (next <= 0f)
                    {
                        next = Random.Range(1.5f, 3.5f);
                        if (Night && Random.value < 0.5f)
                        {
                            Face(Random.value < 0.5f ? -1f : 1f);
                            Force("tongue", 0.25f);
                            IslandFx.FindPopped(transform.position + new Vector3(body.flipX ? -0.45f : 0.45f, 0.4f, 0f));
                        }
                        else if (Night || Raining)
                        {
                            Force("croak", 0.8f);
                            if (OnScreen() && Random.value < 0.4f)
                                Say(RuneGlyphs.Note);
                        }
                    }
                    Show(Sitting(t, "sit", null, "blink"));
                    yield return null;
                }
            }
        }

        // ------------------------------------------------------------ nietoperz

        IEnumerator BatLife()
        {
            transform.position = RandomPerch(0.55f);
            while (true)
            {
                if (Night && Lamp.All.Count > 0)
                {
                    var moth = FamiliarSystem.Find(FamiliarSystem.Moth);
                    bool chase = moth != null && moth.Flying && Random.value < 0.3f;
                    var lamp = Lamp.All[Random.Range(0, Lamp.All.Count)];
                    var center = lamp != null ? lamp.transform.position + new Vector3(0f, 1.3f, 0f) : transform.position;
                    flying = true;
                    float phase = Random.value * 6.28f;
                    float time = chase ? Random.Range(4f, 7f) : Random.Range(10f, 20f);
                    for (float t = 0f; t < time && Night; t += Time.unscaledDeltaTime)
                    {
                        Vector3 target;
                        if (chase && moth != null)
                            target = moth.transform.position + new Vector3(Mathf.Sin(t * 5f) * 0.3f, 0.2f, 0f);   // goni ćmę
                        else
                        {
                            float a = phase + t * 2.2f;
                            target = center + new Vector3(Mathf.Cos(a) * 1.1f, Mathf.Sin(a * 2f) * 0.35f, 0f);
                        }
                        Face(target.x - transform.position.x);
                        transform.position = Vector3.Lerp(transform.position, target, Mathf.Min(1f, Time.unscaledDeltaTime * 4f));
                        Cycle(12f, "fly0", "fly1", "fly2", "fly1");
                        yield return null;
                    }
                    continue;
                }
                // dzień: wisi pod koroną drzewa (albo pod tablicą / lampą) i lekko się kołysze
                var spot = PerchOrHome(0.55f);
                yield return Fly(spot, 2.5f, 0.3f, 12f, "fly0", "fly1", "fly2", "fly1");
                while (!Night || Lamp.All.Count == 0)
                {
                    Cycle(0.6f, "hang0", "hang1");
                    yield return null;
                }
            }
        }

        // ------------------------------------------------------------ ćma księżycowa

        IEnumerator MothLife()
        {
            transform.position = RandomPerch();
            while (true)
            {
                if (Night)
                {
                    // trzepot wokół światła: lampa albo świecąca roślina
                    var center = LightSpot();
                    flying = true;
                    float time = Random.Range(8f, 16f);
                    var bat = FamiliarSystem.Find(FamiliarSystem.Bat);
                    for (float t = 0f; t < time && Night; t += Time.unscaledDeltaTime)
                    {
                        var target = center + new Vector3(Mathf.Sin(t * 1.7f + seed) * 0.8f + Mathf.Sin(t * 5.3f) * 0.15f,
                                                          Mathf.Sin(t * 2.3f) * 0.4f + Mathf.Sin(t * 7.1f) * 0.08f, 0f);
                        if (bat != null && bat.Flying && (bat.transform.position - transform.position).sqrMagnitude < 0.8f)
                            target += (transform.position - bat.transform.position).normalized * 0.8f;   // ucieka przed nietoperzem
                        Face(target.x - transform.position.x);
                        transform.position = Vector3.Lerp(transform.position, target, Mathf.Min(1f, Time.unscaledDeltaTime * 3f));
                        Cycle(14f, "fly0", "fly1", "fly2", "fly1");
                        yield return null;
                    }
                    continue;
                }
                yield return Fly(PerchOrHome(), 1.5f, 0.3f, 14f, "fly0", "fly1", "fly2", "fly1");
                while (!Night)
                {
                    Show("rest");
                    yield return new WaitForSecondsRealtime(1f);
                }
            }
        }

        static Vector3 LightSpot()
        {
            var spots = new List<Vector3>();
            foreach (var l in Lamp.All)
                if (l != null && l.IsOn) spots.Add((Vector3)l.LightCenter + new Vector3(0f, -0.2f, 0f));
            foreach (var p in FarmPlot.All)
                if (p != null && p.State.crop != null && p.State.crop.kind == CropKind.Night && p.State.IsRipe)
                    spots.Add(p.CropPosition + new Vector3(0f, 0.6f, 0f));
            return spots.Count > 0 ? spots[Random.Range(0, spots.Count)] : RandomLand() + new Vector3(0f, 1f, 0f);
        }

        // ------------------------------------------------------------ sowa

        IEnumerator OwlLife()
        {
            transform.position = RandomPerch();
            while (true)
            {
                if (!Night)
                {
                    // dzień: drzemka na drzewie, oczy prawie zawsze zamknięte
                    float t0 = Random.Range(20f, 40f);
                    yield return Idle(t0, t => (t + seed) % 6f < 5.4f ? "blink" : "sit");
                    if (Random.value < 0.3f)
                        yield return Fly(PerchOrHome(), 2f, 0.4f, 4f, "fly0", "fly1");
                    continue;
                }
                // noc: rozgląda się za ognikami, pohukuje, przelatuje bezszelestnie
                float stay = Random.Range(10f, 25f), hoot = Random.Range(3f, 8f);
                for (float t = 0f; t < stay && Night; t += Time.unscaledDeltaTime)
                {
                    var w = NearWisp(5f);
                    if (w != null)
                        Face(w.transform.position.x - transform.position.x);
                    hoot -= Time.unscaledDeltaTime;
                    if (hoot <= 0f)
                    {
                        hoot = Random.Range(6f, 12f);
                        Force("hoot", 0.9f);
                        if (OnScreen())
                            Say(RuneGlyphs.Note);
                    }
                    Show(Sitting(t, "sit", "look", "blink"));
                    yield return null;
                }
                if (Random.value < 0.4f)
                {
                    var center = RandomLand() + new Vector3(0f, 1.8f, 0f);
                    yield return Fly(center, 2.2f, 0.3f, 4f, "fly0", "fly1");
                    flying = true;
                    for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
                    {
                        float a = t * 1f;
                        var p = center + new Vector3(Mathf.Sin(a) * 1.4f, (Mathf.Cos(a) - 1f) * 0.4f, 0f);
                        Face(p.x - transform.position.x);
                        transform.position = p;
                        Cycle(3f, "fly0", "fly1");
                        yield return null;
                    }
                    flying = false;
                }
                yield return Fly(PerchOrHome(), 2.2f, 0.4f, 4f, "fly0", "fly1");
            }
        }

        // ------------------------------------------------------------ ślimak

        IEnumerator SnailLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                float trail = 0f;
                foreach (var to in Route(transform.position, PlotOrHome()))
                {
                Face(to.x - transform.position.x);
                while ((to - transform.position).sqrMagnitude > 0.01f)
                {
                    bool hidden = forcedPose == "hide" && Time.unscaledTime < forcedUntil;
                    if (!hidden)
                    {
                        float speed = Raining ? 0.22f : 0.12f;
                        transform.position = Vector3.MoveTowards(transform.position, to, speed * Time.unscaledDeltaTime);
                        trail -= Time.unscaledDeltaTime;
                        if (trail <= 0f)
                        {
                            trail = 0.6f;
                            IslandFx.SlimeTrail(transform.position + new Vector3(body.flipX ? 0.3f : -0.3f, 0.05f, 0f));
                        }
                    }
                    Cycle(1.6f, "crawl0", "crawl1");
                    yield return null;
                }
                }
                yield return Idle(Random.Range(5f, 12f), t => "crawl0");
            }
        }

        // ------------------------------------------------------------ lis-widmo

        IEnumerator FoxLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                var clock = GameLoop.Instance != null ? GameLoop.Instance.Clock : null;
                if (alpha < 0.05f)
                {
                    // dzień: niewidoczny, czeka na zmierzch w nowym miejscu
                    transform.position = RandomLand();
                    yield return new WaitForSecondsRealtime(2f);
                    continue;
                }
                float r = Random.value;
                if (clock != null && clock.IsNight && clock.SecondsIntoNight > 600f && r < 0.4f)
                {
                    yield return Walk(SleepSpot(), 1.6f, 9f, "walk0", "walk1", "walk2", "walk3");
                    yield return Idle(Random.Range(15f, 25f), _ => "curl");
                    continue;
                }
                if (r < 0.5f)
                {
                    // na brzeg: siada i patrzy w morze
                    var shore = ShoreSpot();
                    yield return Walk(shore, 1.6f, 9f, "walk0", "walk1", "walk2", "walk3");
                    var island = Island.Instance;
                    if (island != null && island.Ready)
                        Face(shore.x - island.LandRect.center.x);
                    yield return Idle(Random.Range(6f, 12f), t => Sitting(t, "sit0", "sit1", null));
                    continue;
                }
                yield return Walk(Wander(), 1.8f, 10f, "walk0", "walk1", "walk2", "walk3");
                yield return Idle(Random.Range(3f, 6f), t => Sitting(t, "sit0", "sit1", null));
            }
        }

        /// <summary>Kafelek na skraju lądu (bez sąsiada z jednej strony).</summary>
        static Vector3 ShoreSpot()
        {
            var island = Island.Instance;
            if (island == null || !island.Ready)
                return Vector3.zero;
            var edge = new List<TilePos>();
            foreach (var t in island.Map.Tiles.Keys)
                if (!island.Map.Owns(new TilePos(t.c - 1, t.r)) || !island.Map.Owns(new TilePos(t.c + 1, t.r))
                    || !island.Map.Owns(new TilePos(t.c, t.r + 1)))
                    edge.Add(t);
            if (edge.Count == 0)
                return RandomLand();
            return Island.TileAnchor(edge[Random.Range(0, edge.Count)], 12, out _);
        }

        static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
    }
}
