using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Alchemy;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.Sea;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Automation
{
    /// <summary>Budowla działająca na kafelkach wokół siebie (gniazdo, runa): zasięg do podglądu w trybie stawiania.</summary>
    public interface IAreaEffect
    {
        /// <summary>Promień w kafelkach (1 = 3x3, 3 = 7x7).</summary>
        int AreaRadius { get; }
    }

    /// <summary>Coś, co sieje wybrane nasiona (gniazdo z siewcą): koło nasion ustawia tryb.</summary>
    public interface ISeedChooser
    {
        string Seed { get; set; }
        /// <summary>Gdzie otworzyć koło nasion.</summary>
        Vector3 PickerAnchor { get; }
    }

    /// <summary>Płynny lot duszków: łuk w górę między punktami, wygładzony start i lądowanie.</summary>
    public static class Flight
    {
        /// <summary>Lot z a do b z prędkością kafelki/s; łuk wznosi się o arc (jednostki świata) w połowie drogi.</summary>
        public static IEnumerator Arc(Transform t, Vector3 a, Vector3 b, float tilesPerSecond, float arc = 0.35f)
        {
            float duration = Mathf.Max(0.25f, Vector3.Distance(a, b) / Mathf.Max(0.1f, tilesPerSecond));
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                float u = time / duration;
                float e = u * u * (3f - 2f * u);   // smoothstep: łagodny start i lądowanie
                t.position = Vector3.Lerp(a, b, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * arc, 0f);
                yield return null;
            }
            t.position = b;
        }

        /// <summary>Delikatne unoszenie w miejscu (zamiast skaczących klatek sprite'a).</summary>
        public static Vector3 Hover(Vector3 home, float seed) =>
            home + new Vector3(Mathf.Sin(Time.unscaledTime * 0.7f + seed) * 0.04f, Mathf.Sin(Time.unscaledTime * 1.6f + seed) * 0.07f, 0f);
    }

    public enum WispKind { Gardener, Sower, Woodcutter, Alchemist, Lamplighter, Gatherer }

    /// <summary>
    /// Ognik z gniazda. Każdy ma zawód:
    /// ogrodnik zbiera plony i owoce do skrzyń, siewca sadzi, drwal rozbiera dzikie drzewa i kamienie, alchemik obsługuje kocioł,
    /// latarnik nocą „dolewa światła” lampom, zbieracz łapie spadające gwiazdy, owoce z ziemi i błyskotki w trawie.
    /// Bez pracy krąży wokół gniazda, drzew i lamp, a czasem zlatuje się z innym wolnym ognikiem na pogaduszki
    /// (dymki z runami i szczebiot, GDD §11.3 E1). Ogniki nie wchodzą w siebie: przy mijaniu sprite'y delikatnie się odsuwają.
    /// Za pracę zdobywa doświadczenie: wyższy poziom = szybszy lot, więcej niesie,
    /// jaśniejszy blask. Ma imię (dymek gniazda). Ogrodnik, siewca, drwal i alchemik pracują w polu 7x7 wokół gniazda,
    /// latarnik i zbieracz na całej wyspie.
    /// </summary>
    public class Wisp : MonoBehaviour
    {
        public const int Radius = 3;
        public const int NameCount = 12;
        const float Personal = 0.6f;       // bliżej niż tyle ogniki się rozpychają (ognik ma ok. 10 px szerokości)
        const float MaxNudge = 0.3f;       // najdalej o tyle sprite odsuwa się od toru lotu
        const float ChatRange = 3.5f;      // jak daleko szuka rozmówcy
        const float ChatGap = 0.38f;       // pół odstępu między rozmówcami
        const float HeadHeight = 1.05f;    // dymek nad głową (od punktu zaczepienia sprite'a)
        const int MaxChats = 2;            // tyle rozmów naraz na całej wyspie
        static int activeChats;
        static readonly int[] LevelXp = { 0, 10, 30, 70, 150, 300 };   // poziomy 1..6

        public static readonly List<Wisp> All = new List<Wisp>();
        /// <summary>Cele, po które już leci jakiś ognik (grządki, drzewa, dekoracje, lampy, gwiazdy, kotły).</summary>
        static readonly HashSet<object> reserved = new HashSet<object>();

        struct Carried
        {
            public CropDefinition crop;
            public int quality;
            public TreeSpecies fruit;
            public Sprite sprite;
        }

        WispKind kind;
        WispNest nest;
        string id;
        int xp, nameIndex, slot;
        SpriteRenderer body, glow;
        Sprite[] frames, glowFrames;
        float seed;
        bool flying;
        float trailTimer;
        float shinyTimer;
        bool warnedFull;
        Transform visual;           // sprite'y ognika (odsuwane od sąsiadów)
        SpriteRenderer tool, toolFront, toolGlow;   // narzędzie zawodu (koszyk: tył + przód, rzeczy między nimi)
        SpriteRenderer hatSr, hatGlow;              // czapka (sklep z czapkami)
        string hat;
        const float HatY = 14f / 16f;               // rondo na czubku głowy
        Sprite[] toolFrames, toolGlowFrames;
        int toolPose;
        float toolPoseUntil;
        const float HandX = 5f / 16f, HandY = 9.5f / 16f;   // chwyt: prawy bok tułowia (sprite ognika 16x20, pivot u dołu)
        Vector3 nudge;
        WispBubble bubble;
        float chatCooldown;
        bool idling;                // krąży bez pracy: można go zagadać
        bool chatReady;             // rozmówca doleciał na miejsce
        bool chatHost;              // ten ognik zaczął rozmowę (liczy się do MaxChats)
        Wisp chatWith;
        readonly List<object> myReservations = new List<object>();
        readonly List<Carried> carried = new List<Carried>();
        readonly List<SpriteRenderer> carriedSprites = new List<SpriteRenderer>();
        CurrencyType loadType;
        int loadAmount;

        public WispKind Kind => kind;
        public string Id => id;
        public int Xp => xp;
        public int NameIndex => nameIndex;
        public WispNest Nest => nest;
        public string NameKey => "wisp.name." + nameIndex;
        public static string KindKey(WispKind k) => "wisp.kind." + k.ToString().ToLowerInvariant();

        public int Level
        {
            get
            {
                int l = 1;
                for (int i = 1; i < LevelXp.Length; i++)
                    if (xp >= LevelXp[i]) l = i + 1;
                return l;
            }
        }

        /// <summary>Doświadczenie do następnego poziomu (-1 = najwyższy).</summary>
        public int NextLevelXp => Level < LevelXp.Length ? LevelXp[Level] : -1;
        float Speed => 3f * (1f + 0.08f * (Level - 1)) * Upgrades.UpgradeEffects.WispSpeed;
        int Carry => 3 + (Level - 1) / 2;

        public static bool AnyOfKind(WispKind k)
        {
            foreach (var w in All)
                if (w.kind == k) return true;
            return false;
        }

        public static int CountOfKind(WispKind k)
        {
            int n = 0;
            foreach (var w in All)
                if (w.kind == k) n++;
            return n;
        }

        public void Setup(WispKind wispKind, WispNest owner, int nestSlot, string wispId, int experience, int name,
                          SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites)
        {
            kind = wispKind;
            nest = owner;
            slot = nestSlot;
            id = wispId;
            xp = Mathf.Max(0, experience);
            nameIndex = Mathf.Clamp(name, 0, NameCount - 1);
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
            seed = Random.value * 10f;
            shinyTimer = Random.Range(120f, 300f);
            chatCooldown = Random.Range(10f, 45f);
            visual = body != null && body.transform != transform ? body.transform : null;
            if (bubble == null)
                bubble = WispBubble.Create(transform, visual, HeadHeight);
            transform.position = Home;
            ApplyLevelLook();
        }

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            Release();
            LeaveChat();
            if (nest != null)
                nest.Forget(this);
            if (carried.Count > 0)
                Deliver(HarvestCrate.NearestWithSpace(transform.position));
            if (loadAmount > 0)
                DepositLoad();
        }

        void Start() => StartCoroutine(Brain());

        /// <summary>Miejsce przy gnieździe (trzy ogniki obok siebie nad pniem).</summary>
        Vector3 Home
        {
            get
            {
                var n = nest != null ? nest.transform.position : transform.position;
                return n + new Vector3((slot - 1) * 0.45f, 1.15f + (slot == 1 ? 0.25f : 0f), 0f);
            }
        }

        /// <summary>Gniazdo przeniesione: ognik wraca do nowego domu przy następnym kursie.</summary>
        public void OnNestMoved()
        {
            StopAllCoroutines();
            Release();
            LeaveChat();
            flying = false;
            if (carried.Count > 0)
                Deliver(HarvestCrate.NearestWithSpace(transform.position));
            if (loadAmount > 0)
                DepositLoad();
            transform.position = Home;
            StartCoroutine(Brain());
        }

        // ------------------------------------------------------------ narzędzie zawodu

        /// <summary>Narzędzie zawodu (nazwa plików tool_*, tools/pixel-proto/wisp_tools.py).</summary>
        public static string ToolName(WispKind k)
        {
            switch (k)
            {
                case WispKind.Gardener: return "sickle";
                case WispKind.Sower: return "seedbag";
                case WispKind.Woodcutter: return "axepick";
                case WispKind.Alchemist: return "flask";
                case WispKind.Lamplighter: return "lantern";
                default: return "basket";
            }
        }

        bool HasBasket => kind == WispKind.Gatherer && toolFront != null;

        // ------------------------------------------------------------ czapka

        /// <summary>Czapka ognika (id z Economy.Hats) albo null.</summary>
        public string Hat => hat;

        /// <summary>Zakłada czapkę (null = bez). Grafika z IslandArt (Wyspa → 2); bez niej ognik jest bez czapki.</summary>
        public void SetHat(string id)
        {
            var art = Island.Instance != null ? Island.Instance.Art : null;
            hat = string.IsNullOrEmpty(id) || Economy.Hats.Find(id) == null ? null : id;
            var sprite = art != null ? art.Hat(hat, false) : null;
            var glowSprite = art != null ? art.Hat(hat, true) : null;
            var parent = visual != null ? visual : transform;
            if (hatSr == null)
                hatSr = NewPart("Hat", parent);
            hatSr.sprite = sprite;
            hatSr.enabled = sprite != null;
            if (glowSprite != null && hatGlow == null)
            {
                hatGlow = NewPart("HatGlow", parent);
                if (art.glow != null)
                    hatGlow.sharedMaterial = art.glow;   // świeca, aureola, klejnoty świecą w nocy
            }
            if (hatGlow != null)
            {
                hatGlow.sprite = glowSprite;
                hatGlow.enabled = glowSprite != null;
            }
            if (bubble != null)
                bubble.HeadHeight = HeadHeight + (sprite != null ? sprite.rect.height / 16f - 0.15f : 0f);   // dymek nad czapką
        }

        void UpdateHat()
        {
            if (hatSr == null)
                return;
            bool flip = body != null && body.flipX;
            var at = new Vector3(flip ? -0.5f / 16f : 0.5f / 16f, HatY, 0f);
            hatSr.flipX = flip;
            hatSr.transform.localPosition = at;
            hatSr.sortingOrder = 4406;
            if (hatGlow != null)
            {
                hatGlow.flipX = flip;
                hatGlow.transform.localPosition = at;
                hatGlow.sortingOrder = 4407;
            }
        }

        /// <summary>
        /// Narzędzie w dłoni: drwal siekiero-kilof, ogrodnik sierp, siewca worek nasion, alchemik fiolka, latarnik latarenka,
        /// zbieracz koszyk (zebrane rzeczy leżą w nim). Bez grafiki (przed Wyspa → 2) ognik jest bez narzędzia.
        /// </summary>
        public void SetTool(Sprite[] frames, Sprite[] glowFrames, Material glowMaterial)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                return;
            toolFrames = frames;
            toolGlowFrames = glowFrames;
            var parent = visual != null ? visual : transform;
            tool = NewPart("Tool", parent);
            tool.sprite = frames[0];
            if (kind == WispKind.Gatherer && frames.Length > 1 && frames[1] != null)
            {
                toolFront = NewPart("ToolFront", parent);
                toolFront.sprite = frames[1];
            }
            if (glowFrames != null && glowFrames.Length > 0 && glowFrames[0] != null)
            {
                toolGlow = NewPart("ToolGlow", parent);
                if (glowMaterial != null)
                    toolGlow.sharedMaterial = glowMaterial;   // płomyk latarenki i wywar świecą też w nocy
            }
            if (carried.Count > 0)
                ShowCarried();
        }

        static SpriteRenderer NewPart(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(HandX, HandY, 0f);
            return go.AddComponent<SpriteRenderer>();
        }

        /// <summary>Poza narzędzia na chwilę (uderzenie, zamach, sypanie nasion), potem wraca klatka 0.</summary>
        void Pose(int frame, float seconds)
        {
            toolPose = frame;
            toolPoseUntil = Time.unscaledTime + seconds;
        }

        void UpdateTool()
        {
            if (tool == null)
                return;
            bool flip = body != null && body.flipX;
            int f;
            if (kind == WispKind.Alchemist || kind == WispKind.Lamplighter)
                f = Mathf.FloorToInt(Time.unscaledTime * (kind == WispKind.Lamplighter ? 6f : 3f) + seed) % 2;   // bulgotanie, migotanie
            else if (kind == WispKind.Gatherer)
                f = 0;
            else
                f = Time.unscaledTime < toolPoseUntil ? toolPose : 0;
            if (!HasBasket && f < toolFrames.Length && toolFrames[f] != null)
                tool.sprite = toolFrames[f];
            var at = new Vector3(flip ? -HandX : HandX, HandY, 0f);
            foreach (var part in new[] { tool, toolFront, toolGlow })
            {
                if (part == null)
                    continue;
                part.flipX = flip;
                part.transform.localPosition = at;
            }
            if (HasBasket)
            {
                // rzeczy w koszyku idą za koszykiem, gdy ognik się obraca
                int n = 0, count = 0;
                foreach (var c in carriedSprites)
                    if (c != null && c.enabled) count++;
                foreach (var c in carriedSprites)
                    if (c != null && c.enabled)
                        c.transform.localPosition = new Vector3(at.x + (n++ - (count - 1) / 2f) * 2.5f / 16f, HandY - 4.5f / 16f, 0f);
            }
            tool.sortingOrder = 4402;
            if (toolFront != null)
                toolFront.sortingOrder = 4404;
            if (toolGlow != null)
            {
                toolGlow.sortingOrder = 4405;
                toolGlow.sprite = toolGlowFrames != null && f < toolGlowFrames.Length ? toolGlowFrames[f] : null;
            }
        }

        /// <summary>Świt (scenka): senny dymek z wielokropkiem i mruczenie (gdy akurat nic nie mówi).</summary>
        public void Yawn()
        {
            if (bubble == null || bubble.Showing || chatWith != null)
                return;
            bubble.Say(new[] { RuneGlyphs.Dots }, RuneColor, VoicePitch * 0.9f, OnScreen() ? 0.25f : 0f);
        }

        /// <summary>Scenka odpłynięcia: ognik odkłada to, co niesie, wraca do gniazda i już tylko unosi się nad nim.</summary>
        public void RecallHome()
        {
            StopAllCoroutines();
            Release();
            LeaveChat();
            flying = false;
            if (carried.Count > 0)
                Deliver(HarvestCrate.NearestWithSpace(transform.position));
            if (loadAmount > 0)
                DepositLoad();
            StartCoroutine(RestAtHome());
        }

        IEnumerator RestAtHome()
        {
            yield return new WaitForSecondsRealtime(Random.Range(0f, 0.6f));
            yield return FlyTo(Home, 0.3f);
            while (true)
                yield return HoverFor(5f);
        }

        // ------------------------------------------------------------ wygląd

        void ApplyLevelLook()
        {
            // wyższy poziom: trochę większy i jaśniejszy blask
            float s = 1f + 0.04f * (Level - 1);
            transform.localScale = new Vector3(s, s, 1f);
            if (glow != null)
            {
                var c = glow.color;
                c.a = Mathf.Clamp01(0.75f + 0.05f * Level);
                glow.color = c;
            }
        }

        void Update()
        {
            if (frames != null && frames.Length > 0)
            {
                int f = Mathf.FloorToInt(Time.unscaledTime * 8f + seed) % frames.Length;
                if (body != null) body.sprite = frames[f];
                if (glow != null && glowFrames != null && f < glowFrames.Length) glow.sprite = glowFrames[f];
            }
            if (body != null) body.sortingOrder = 4400;   // ogniki latają nad wyspą
            if (glow != null) glow.sortingOrder = 4401;
            foreach (var c in carriedSprites)
                if (c != null) c.sortingOrder = HasBasket ? 4403 : 4399;   // w koszyku: między tyłem a przodem
            UpdateTool();
            UpdateHat();
            chatCooldown -= Time.unscaledDeltaTime;
            if (flying)
            {
                trailTimer -= Time.unscaledDeltaTime;
                if (trailTimer <= 0f)
                {
                    trailTimer = 0.12f;
                    IslandFx.WispTrail(transform.position + new Vector3(0f, 0.55f, 0f));
                }
            }
        }

        /// <summary>
        /// Ogniki nie wchodzą w siebie: gdy tory lotu się zbliżą, sprite'y odsuwają się od siebie (każdy o połowę zachodzenia,
        /// w pionie słabiej), płynnie i najwyżej o MaxNudge. Tor lotu (transform) zostaje bez zmian, więc praca idzie dalej.
        /// </summary>
        void LateUpdate()
        {
            if (visual == null)
                return;
            var me = transform.position;
            var push = Vector3.zero;
            foreach (var o in All)
            {
                if (o == this || o == null)
                    continue;
                var d = me - o.transform.position;
                d.z = 0f;
                float dist = d.magnitude;
                if (dist >= Personal)
                    continue;
                // w tym samym punkcie: stały kierunek dla pary (po ziarnie), żeby nie migotały
                var dir = dist > 0.001f ? d / dist : new Vector3(seed > o.seed ? 1f : -1f, 0f, 0f);
                push += dir * ((Personal - dist) * 0.5f);
            }
            push.y *= 0.6f;
            push = Vector3.ClampMagnitude(push, MaxNudge);
            nudge = Vector3.Lerp(nudge, push, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
            float s = Mathf.Max(0.01f, transform.localScale.x);
            visual.localPosition = nudge / s;
        }

        // ------------------------------------------------------------ doświadczenie

        /// <summary>Do testów (HUD debugowy): doświadczenie od razu, z awansem jak w grze.</summary>
        public void DebugAddXp(int n) => AddXp(n);

        void AddXp(int n)
        {
            int before = Level;
            xp += Mathf.Max(1, Mathf.RoundToInt(n * Upgrades.UpgradeEffects.WispXp));
            if (Level > before)
            {
                ApplyLevelLook();
                IslandFx.FindPopped(transform.position + new Vector3(0f, 0.6f, 0f));
                GameEvents.PlaySound("magic", 0.6f);
                GameEvents.RaiseToast("toast.wisp.level", Loc.Get(NameKey), Loc.Get(KindKey(kind)), Level);
            }
        }

        // ------------------------------------------------------------ rezerwacje i zasięg

        bool Reserve(object target)
        {
            if (target == null || reserved.Contains(target))
                return false;
            reserved.Add(target);
            myReservations.Add(target);
            return true;
        }

        void Release()
        {
            foreach (var r in myReservations)
                reserved.Remove(r);
            myReservations.Clear();
        }

        TilePos NestTile => nest != null ? nest.Tile : Island.WorldToTile(transform.position);

        bool InRange(int c, int r)
        {
            var t = NestTile;
            return Mathf.Abs(c - t.c) <= Radius && Mathf.Abs(r - t.r) <= Radius;
        }

        bool InRange(Vector3 world)
        {
            var t = Island.WorldToTile(world);
            return InRange(t.c, t.r);
        }

        IEnumerator FlyTo(Vector3 target, float arc = 0.35f)
        {
            flying = true;
            if (body != null) body.flipX = target.x < transform.position.x;
            yield return Flight.Arc(transform, transform.position, target, Speed, arc);
            flying = false;
        }

        IEnumerator HoverFor(float seconds)
        {
            var at = transform.position;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                transform.position = Flight.Hover(at, seed);
                yield return null;
            }
            transform.position = at;
        }

        // ------------------------------------------------------------ mózg: praca albo krążenie

        IEnumerator Brain()
        {
            yield return new WaitForSecondsRealtime(Random.Range(0.3f, 1.5f));
            while (true)
            {
                idling = false;
                if (nest == null)
                    yield break;
                var job = Island.Instance != null && Island.Instance.Ready && !Save.SaveSystem.InMenu ? FindJob() : null;
                if (job != null)
                {
                    yield return job;
                    Release();
                    yield return HoverFor(Random.Range(0.5f, 1.2f));
                }
                else
                    yield return Idle();
            }
        }

        IEnumerator FindJob()
        {
            switch (kind)
            {
                case WispKind.Gardener: return GardenerJob();
                case WispKind.Sower: return SowerJob();
                case WispKind.Woodcutter: return WoodcutterJob();
                case WispKind.Alchemist: return AlchemistJob();
                case WispKind.Lamplighter: return LamplighterJob();
                default: return GathererJob();
            }
        }

        /// <summary>Bez pracy: krąży wokół gniazda, zagląda do drzew i lamp albo odpoczywa nad pniem.</summary>
        IEnumerator Idle()
        {
            idling = true;
            if (chatCooldown <= 0f && !Save.SaveSystem.InMenu)
            {
                chatCooldown = Random.Range(8f, 20f);   // nie wyszło: następna próba za chwilę
                var partner = activeChats < MaxChats && Random.value < 0.6f ? FindChatPartner() : null;
                if (partner != null)
                {
                    idling = false;
                    yield return Chat(partner);
                    yield break;
                }
            }
            float r = Random.value;
            if (r < 0.35f)
            {
                yield return FlyTo(Home, 0.2f);
                yield return HoverFor(Random.Range(2f, 5f));
                yield break;
            }
            Vector3 target;
            if (r < 0.75f || Lamp.All.Count == 0)
                target = (nest != null ? nest.transform.position : transform.position)
                         + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.6f, 1.8f), 0f);
            else
                target = Lamp.All[Random.Range(0, Lamp.All.Count)].transform.position + new Vector3(Random.Range(-0.6f, 0.6f), 1.5f, 0f);
            // powolny lot z kółkiem wokół celu
            flying = true;
            if (body != null) body.flipX = target.x < transform.position.x;
            yield return Flight.Arc(transform, transform.position, target, Speed * 0.5f, 0.3f);
            float phase = Random.value * 6.28f;
            var center = target;
            for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
            {
                float a = phase + t * 2.4f;
                transform.position = center + new Vector3(Mathf.Cos(a) * 0.35f, Mathf.Sin(a) * 0.18f, 0f);
                yield return null;
            }
            flying = false;
            yield return HoverFor(Random.Range(0.8f, 2f));
            idling = false;
        }

        // ------------------------------------------------------------ pogaduszki

        bool CanChat => idling && chatWith == null && carried.Count == 0 && loadAmount == 0 && isActiveAndEnabled;

        Wisp FindChatPartner()
        {
            Wisp best = null;
            float bestD = ChatRange * ChatRange;
            foreach (var w in All)
            {
                if (w == this || w == null || !w.CanChat)
                    continue;
                float d = (w.transform.position - transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = w;
                }
            }
            return best;
        }

        bool TalkingWith(Wisp other) =>
            other != null && other.isActiveAndEnabled && chatWith == other && other.chatWith == this;

        /// <summary>Głos ognika: stały dla imienia i zawodu (0.85-1.25), więc każdy brzmi trochę inaczej.</summary>
        float VoicePitch => 0.85f + ((nameIndex * 7 + (int)kind * 3) % 9) * 0.05f;

        Color RuneColor => Color.Lerp(WispNest.KindColor(kind), Color.white, 0.35f);

        /// <summary>
        /// Rozmowa: ognik zaprasza wolnego sąsiada, oba zlatują się w pół drogi, patrzą na siebie i na zmianę „mówią” runami
        /// (3-5 kwestii, na koniec reakcja: „!”, „?”, wielokropek albo nucenie). Robota czeka te kilka sekund.
        /// </summary>
        IEnumerator Chat(Wisp partner)
        {
            activeChats++;
            chatHost = true;
            chatWith = partner;
            var a = transform.position;
            var b = partner.transform.position;
            var mid = (a + b) * 0.5f;
            float side = a.x <= b.x ? -1f : 1f;
            var mine = mid + new Vector3(side * ChatGap, 0f, 0f);
            partner.JoinChat(this, mid - new Vector3(side * ChatGap, 0f, 0f));
            flying = true;
            if (body != null) body.flipX = mine.x < a.x;
            yield return Flight.Arc(transform, a, mine, Speed * 0.6f, 0.15f);
            flying = false;
            if (body != null) body.flipX = side > 0f;   // twarzą do rozmówcy
            var at = transform.position;
            for (float t = 0f; t < 3f && TalkingWith(partner) && !partner.chatReady; t += Time.unscaledDeltaTime)
            {
                transform.position = Flight.Hover(at, seed);
                yield return null;
            }
            int lines = Random.Range(3, 6);
            for (int i = 0; i < lines && TalkingWith(partner); i++)
            {
                var speaker = i % 2 == 0 ? this : partner;
                float duration = speaker.Speak(i == lines - 1);
                for (float t = 0f; t < duration + 0.2f && TalkingWith(partner); t += Time.unscaledDeltaTime)
                {
                    transform.position = Flight.Hover(at, seed);
                    yield return null;
                }
            }
            transform.position = at;
            LeaveChat();
            yield return HoverFor(Random.Range(0.4f, 1f));
        }

        /// <summary>Zaproszenie od innego ognika: przerywa krążenie, leci na miejsce i słucha, dopóki gospodarz mówi.</summary>
        void JoinChat(Wisp host, Vector3 spot)
        {
            StopAllCoroutines();
            idling = false;
            flying = false;
            chatWith = host;
            chatReady = false;
            StartCoroutine(Listen(host, spot));
        }

        IEnumerator Listen(Wisp host, Vector3 spot)
        {
            flying = true;
            if (body != null) body.flipX = spot.x < transform.position.x;
            yield return Flight.Arc(transform, transform.position, spot, Speed * 0.6f, 0.15f);
            flying = false;
            if (body != null && host != null) body.flipX = host.transform.position.x < transform.position.x;
            chatReady = true;
            var at = transform.position;
            for (float t = 0f; t < 30f && TalkingWith(host); t += Time.unscaledDeltaTime)
            {
                transform.position = Flight.Hover(at, seed);
                yield return null;
            }
            transform.position = at;
            LeaveChat();
            yield return HoverFor(Random.Range(0.4f, 1.2f));
            yield return Brain();
        }

        /// <summary>Koniec rozmowy (także przerwanej): dymek znika, następna dopiero za jakiś czas.</summary>
        void LeaveChat()
        {
            if (chatHost)
                activeChats = Mathf.Max(0, activeChats - 1);
            chatHost = false;
            chatWith = null;
            chatReady = false;
            chatCooldown = Random.Range(45f, 120f);
            if (bubble != null)
                bubble.Hide();
        }

        /// <summary>Jedna kwestia w dymku. last = reakcja na koniec. Zwraca czas trwania.</summary>
        float Speak(bool last)
        {
            if (bubble == null)
                return 0f;
            var glyphs = new List<int>();
            float r = Random.value;
            if (last && r < 0.7f)
                glyphs.Add(r < 0.2f ? RuneGlyphs.Exclaim : r < 0.35f ? RuneGlyphs.Question : r < 0.5f ? RuneGlyphs.Dots : RuneGlyphs.Note);
            else
            {
                int n = r < 0.15f ? 1 : r < 0.5f ? 2 : r < 0.85f ? 3 : 4;
                for (int i = 0; i < n; i++)
                    glyphs.Add(Random.Range(0, RuneGlyphs.RuneCount));
                if (Random.value < 0.25f)
                    glyphs.Add(Random.value < 0.5f ? RuneGlyphs.Question : RuneGlyphs.Exclaim);
            }
            float volume = OnScreen() ? 0.35f : 0f;   // szczebiot tylko z tego, co widać
            return bubble.Say(glyphs, RuneColor, VoicePitch, volume);
        }

        bool OnScreen()
        {
            var cam = Camera.main;
            if (cam == null)
                return false;
            var v = cam.WorldToViewportPoint(transform.position);
            return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        // ------------------------------------------------------------ ogrodnik

        int FreeCrateSpace()
        {
            int free = 0;
            foreach (var c in HarvestCrate.All)
                free += Mathf.Max(0, c.Capacity - c.Count);
            foreach (var w in All)
                free -= w.carried.Count;
            return free;
        }

        IEnumerator GardenerJob()
        {
            var ripe = new List<FarmPlot>();
            foreach (var plot in FarmPlot.All)
                if (plot.State.IsRipe && !reserved.Contains(plot) && InRange(plot.Cell.x, plot.Cell.y))
                    ripe.Add(plot);
            FruitTree tree = null;
            if (ripe.Count == 0)
                foreach (var t in FruitTree.All)
                    if (t != null && t.HasFruitForWisp && !reserved.Contains(t) && InRange(t.transform.position)
                        && (tree == null || (t.transform.position - transform.position).sqrMagnitude < (tree.transform.position - transform.position).sqrMagnitude))
                        tree = t;
            if (ripe.Count == 0 && tree == null)
                return null;
            int space = Mathf.Min(Carry, FreeCrateSpace());
            if (space <= 0)
            {
                if (!warnedFull)
                {
                    warnedFull = true;
                    GameEvents.RaiseToast(HarvestCrate.All.Count == 0 ? "toast.wisp.no_crate" : "toast.crate_full");
                }
                return null;
            }
            warnedFull = false;
            if (ripe.Count == 0)
                return Reserve(tree) ? TreeTrip(tree, space) : null;
            var route = new List<FarmPlot>();
            var from = transform.position;
            while (route.Count < space && ripe.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < ripe.Count; i++)
                    if ((ripe[i].transform.position - from).sqrMagnitude < (ripe[best].transform.position - from).sqrMagnitude)
                        best = i;
                if (Reserve(ripe[best]))
                {
                    route.Add(ripe[best]);
                    from = ripe[best].transform.position;
                }
                ripe.RemoveAt(best);
            }
            return route.Count > 0 ? HarvestTrip(route) : null;
        }

        IEnumerator HarvestTrip(List<FarmPlot> route)
        {
            foreach (var plot in route)
            {
                if (plot == null || !plot.isActiveAndEnabled)
                    continue;
                yield return FlyTo(plot.CropPosition + new Vector3(0f, 0.4f, 0f));
                if (plot == null || !plot.State.IsRipe)
                    continue;
                int quality = plot.State.Quality;
                Pose(1, 0.25f);   // zamach sierpem
                var crop = plot.HarvestSilently();
                if (crop == null)
                    continue;
                carried.Add(new Carried { crop = crop, quality = quality, sprite = crop.StageSprite(3) });
                ShowCarried();
                AddXp(1);
                var game = GameLoop.Instance;
                if (game != null)
                    game.AfterHarvest(plot, crop);   // bestiariusz, mutacje, zamówienia
            }
            yield return DeliverTrip();
        }

        IEnumerator TreeTrip(FruitTree tree, int toPick)
        {
            if (tree != null && tree.Fruiting)
            {
                float side = transform.position.x < tree.transform.position.x ? -1f : 1f;
                var perch = tree.transform.position + new Vector3(side * 0.6f, 1.1f, 0f);
                yield return FlyTo(perch);
                var hit = tree.transform.position + new Vector3(side * 0.25f, 1.15f, 0f);
                for (int i = 0; i < 2 && tree != null; i++)
                {
                    yield return Flight.Arc(transform, perch, hit, 6f, 0.05f);    // szybkie wejście w koronę
                    if (i == 1 && tree != null)
                        tree.KnockDown();
                    yield return Flight.Arc(transform, hit, perch, 4f, 0.05f);    // odskok
                    yield return new WaitForSecondsRealtime(0.12f);
                }
                yield return new WaitForSecondsRealtime(0.6f);   // owoce dolatują do ziemi
            }
            yield return PickFallen(tree, toPick);
            yield return DeliverTrip();
        }

        IEnumerator PickFallen(FruitTree tree, int toPick)
        {
            while (tree != null && toPick > 0 && tree.FallenCount > 0)
            {
                int nearest = 0;
                for (int i = 1; i < tree.FallenCount; i++)
                    if ((tree.FallenPosition(i) - transform.position).sqrMagnitude < (tree.FallenPosition(nearest) - transform.position).sqrMagnitude)
                        nearest = i;
                yield return FlyTo(tree.FallenPosition(nearest) + new Vector3(0f, 0.3f, 0f), 0.15f);
                if (tree == null)
                    break;
                var sprite = tree.PickFallen(transform.position);
                toPick--;
                if (sprite == null && tree.FallenCount == 0)
                    break;
                carried.Add(new Carried { fruit = tree.Species, sprite = sprite });
                GameEvents.RaiseHarvested("fruit:" + tree.Species.id, 1);
                ShowCarried();
                AddXp(1);
            }
        }

        IEnumerator DeliverTrip()
        {
            if (carried.Count == 0)
                yield break;
            var crate = HarvestCrate.NearestWithSpace(transform.position);
            if (crate != null)
                yield return FlyTo(crate.transform.position + new Vector3(0f, 0.7f, 0f));
            Deliver(crate);
        }

        /// <summary>Plony do skrzyni (najbliższej, potem innych z miejscem). Gdy nigdzie nie ma miejsca: sprzedaż po 90%.</summary>
        void Deliver(HarvestCrate preferred)
        {
            var game = GameLoop.Instance;
            foreach (var c in carried)
            {
                if (c.fruit != null)
                {
                    if (preferred != null && preferred.TryAddFruit(c.fruit)) continue;
                    var other = HarvestCrate.NearestWithSpace(transform.position);
                    if (other != null && other.TryAddFruit(c.fruit)) continue;
                    if (game != null) game.Wallet.Add(Economy.Market.SellFruit(c.fruit, 1, Economy.SaleChannel.Wisp));
                    continue;
                }
                if (c.crop == null)
                    continue;
                if (preferred != null && preferred.TryAdd(c.crop, c.quality)) continue;
                var crate = HarvestCrate.NearestWithSpace(transform.position);
                if (crate != null && crate.TryAdd(c.crop, c.quality)) continue;
                if (game != null) game.Wallet.Add(Economy.Market.SellCrop(c.crop, c.quality, 1, Economy.SaleChannel.Wisp));
            }
            carried.Clear();
            ShowCarried();
        }

        /// <summary>Niesione rzeczy jako małe sprite'y: zbieracz w koszyku, drwal pod sobą (drewno, kamień); ogrodnik i siewca bez.</summary>
        void ShowCarried(Sprite single = null, int singleCount = 0)
        {
            int count = single != null ? singleCount : carried.Count;
            // ogrodnik i siewca nie pokazują plonów ani nasion pod sobą (mają sierp i worek); zbieracz w koszyku
            if (kind == WispKind.Gardener || kind == WispKind.Sower)
                count = 0;
            while (carriedSprites.Count < count)
            {
                var go = new GameObject("Carried");
                go.transform.SetParent(visual != null ? visual : transform, false);
                go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                carriedSprites.Add(go.AddComponent<SpriteRenderer>());
            }
            for (int i = 0; i < carriedSprites.Count; i++)
            {
                bool on = i < count;
                carriedSprites[i].enabled = on;
                if (!on)
                    continue;
                carriedSprites[i].sprite = single != null ? single : carried[i].sprite;
                if (HasBasket)
                {
                    // zbieracz: rzeczy leżą w koszyku (mniejsze, nad brzegiem), koszyk idzie za stroną, w którą patrzy ognik
                    bool flip = body != null && body.flipX;
                    float x = (flip ? -HandX : HandX) + (i - (count - 1) / 2f) * 2.5f / 16f;
                    carriedSprites[i].transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                    carriedSprites[i].transform.localPosition = new Vector3(x, HandY - 4.5f / 16f, 0f);
                }
                else
                {
                    carriedSprites[i].transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                    carriedSprites[i].transform.localPosition = new Vector3((i - (count - 1) / 2f) * 0.35f, -0.15f, 0f);
                }
            }
        }

        // ------------------------------------------------------------ siewca

        IEnumerator SowerJob()
        {
            // osobne nasiona na dzień i na noc; o świcie i zmierzchu podmiana roślin, które teraz nie rosną
            var game = GameLoop.Instance;
            if (game == null || nest == null)
                return null;
            bool night = game.Clock.IsNight;
            string mode = nest.SeedFor(night);
            if (mode == WispNest.Off)
                return null;
            bool random = mode == WispNest.Random;   // losowo: osobne losowanie dla każdej grządki
            var chosen = mode == WispNest.Memory || random || game.Crops == null ? null : game.Crops.Find(mode);
            var route = new List<(FarmPlot plot, CropDefinition crop)>();
            foreach (var plot in FarmPlot.All)
            {
                if (route.Count >= Carry)
                    break;
                if (reserved.Contains(plot) || !InRange(plot.Cell.x, plot.Cell.y))
                    continue;
                CropDefinition crop;
                if (plot.State.IsEmpty)
                    crop = random ? game.RandomSeed(night) : chosen != null ? chosen : plot.LastCrop;
                else if (nest.Swap && (chosen != null || random) && plot.OutOfSeason(night, game.ContextFor(plot, night).lit))
                    crop = random ? game.RandomSeed(night) : chosen;   // podmiana: stara roślina wraca do worka jako nasiono
                else
                    continue;
                // nie sadzimy rośliny, która o tej porze i tak by nie rosła (np. pamięć ziemi z nocną o świcie)
                if (crop == null || (crop.kind == CropKind.Night) != night || crop == plot.State.crop)
                    continue;
                if (game.Seeds.Count(crop.id) <= 0 && (crop.hybrid || game.Wallet.Coins < Economy.Market.SeedPrice(crop)))
                    continue;
                if (Reserve(plot))
                    route.Add((plot, crop));
            }
            return route.Count > 0 ? SowTrip(route) : null;
        }

        IEnumerator SowTrip(List<(FarmPlot plot, CropDefinition crop)> route)
        {
            // po nasiona do gniazda, potem grządka po grządce
            yield return FlyTo(Home);
            ShowCarried(route[0].crop.StageSprite(0), route.Count);
            int left = route.Count;
            foreach (var (plot, crop) in route)
            {
                if (plot == null)
                    continue;
                yield return FlyTo(plot.CropPosition + new Vector3(0f, 0.35f, 0f));
                var game = GameLoop.Instance;
                if (game == null || plot == null || plot.State.IsRipe)
                    continue;
                if (!plot.State.IsEmpty)
                {
                    bool night = game.Clock.IsNight;
                    if (!nest.Swap || !plot.OutOfSeason(night, game.ContextFor(plot, night).lit))
                        continue;   // w międzyczasie zaczęła rosnąć albo gracz wyłączył podmianę
                    plot.DigUp();
                }
                if (!game.Seeds.TryTake(crop.id) && (crop.hybrid || !game.Wallet.TrySpend(Economy.Market.SeedPrice(crop))))
                    continue;
                Pose(1, 0.5f);    // worek przechylony, nasiona się sypią
                plot.Replant(crop);
                AddXp(1);
                left--;
                ShowCarried(route[0].crop.StageSprite(0), left);
            }
            ShowCarried(null, 0);
        }

        // ------------------------------------------------------------ drwal

        IEnumerator WoodcutterJob()
        {
            var island = Island.Instance;
            if (island == null)
                return null;
            Decoration best = null;
            foreach (var d in island.WildDecorations())
                if (!reserved.Contains(d) && InRange(d.Tile.c, d.Tile.r)
                    && (best == null || (d.transform.position - transform.position).sqrMagnitude < (best.transform.position - transform.position).sqrMagnitude))
                    best = d;
            return best != null && Reserve(best) ? ChopTrip(best) : null;
        }

        IEnumerator ChopTrip(Decoration d)
        {
            var pos = d.transform.position;
            var perch = pos + new Vector3(0.45f, 0.75f, 0f);
            yield return FlyTo(perch);
            int strikes = Mathf.Max(2, 5 - Level / 2);   // wyższy poziom: mniej uderzeń
            var hit = pos + new Vector3(0.1f, 0.45f, 0f);
            for (int i = 0; i < strikes && d != null && !d.Removing; i++)
            {
                yield return Flight.Arc(transform, perch, hit, 7f, 0.05f);
                Pose(1, 0.22f);   // siekiero-kilof spada na drzewo albo kamień
                GameEvents.PlaySound("chop", 0.5f);
                IslandFx.LeafBurst(hit, 2, 4000);
                yield return Flight.Arc(transform, hit, perch, 4f, 0.05f);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            var island = Island.Instance;
            if (d == null || d.Removing || island == null || !island.WispRemove(d, out loadType, out loadAmount))
                yield break;
            AddXp(3);
            ShowCarried(Ui(loadType == CurrencyType.Stone ? "stone" : "wood"), Mathf.Min(3, loadAmount));
            yield return FlyTo(Home);
            DepositLoad();
        }

        static Sprite Ui(string icon) => UI.Ui.Theme != null ? UI.Ui.Theme.Icon(icon) : null;

        void DepositLoad()
        {
            var game = GameLoop.Instance;
            if (game != null && loadAmount > 0)
            {
                game.Wallet.Add(loadType, loadAmount);
                GameEvents.RaiseCurrencyGained(transform.position, loadType, loadAmount);
            }
            loadAmount = 0;
            ShowCarried(null, 0);
        }

        // ------------------------------------------------------------ alchemik

        IEnumerator AlchemistJob()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return null;
            foreach (var c in Cauldron.All)
            {
                if (c == null || reserved.Contains(c) || !InRange(c.transform.position))
                    continue;
                var auto = Recipe.Find(c.AutoRecipe);
                if (auto == null)
                    continue;
                // gotowa mikstura: wypić, gdy już nie działa albo zaraz przestanie
                if (c.ReadyRecipe == auto.id)
                {
                    float left = Potions.SecondsLeft(auto.id);
                    if ((left >= 0f && left < 600f) && Reserve(c))
                        return DrinkTrip(c);
                    continue;
                }
                if (c.Idle && Potions.CanBrew(auto, game.Crops) && Reserve(c))
                    return BrewTrip(c, auto);
            }
            return null;
        }

        IEnumerator BrewTrip(Cauldron c, Recipe r)
        {
            var game = GameLoop.Instance;
            var crate = HarvestCrate.Fullest();
            if (crate != null)
                yield return FlyTo(crate.transform.position + new Vector3(0f, 0.7f, 0f));
            if (game == null || c == null || !c.Idle || !Potions.CanBrew(r, game.Crops))
                yield break;
            Potions.TakeIngredients(r, game.Crops);
            ShowCarried(Ui("ritual"), 1);   // składniki w zawiniątku
            yield return FlyTo(c.transform.position + new Vector3(0f, 0.9f, 0f));
            ShowCarried(null, 0);
            if (c != null)
            {
                c.StartByWisp(r);
                AddXp(2);
            }
        }

        IEnumerator DrinkTrip(Cauldron c)
        {
            yield return FlyTo(c.transform.position + new Vector3(0f, 0.9f, 0f));
            yield return HoverFor(0.6f);
            if (c != null && c.ReadyRecipe != null)
            {
                c.Drink();
                AddXp(2);
            }
        }

        // ------------------------------------------------------------ latarnik

        IEnumerator LamplighterJob()
        {
            var game = GameLoop.Instance;
            if (game == null || !game.Clock.IsNight)
                return null;
            Lamp best = null;
            foreach (var l in Lamp.All)
                if (l != null && l.RefueledUntil - Time.unscaledTime < 30f && !reserved.Contains(l)
                    && (best == null || (l.transform.position - transform.position).sqrMagnitude < (best.transform.position - transform.position).sqrMagnitude))
                    best = l;
            return best != null && Reserve(best) ? RefuelTrip(best) : null;
        }

        IEnumerator RefuelTrip(Lamp lamp)
        {
            yield return FlyTo(lamp.transform.position + new Vector3(0f, 1.4f, 0f));
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                if (lamp != null)
                    transform.position = lamp.transform.position + new Vector3(Mathf.Cos(t * 7f) * 0.2f, 1.4f + Mathf.Sin(t * 7f) * 0.1f, 0f);
                yield return null;
            }
            if (lamp == null)
                yield break;
            lamp.RefueledUntil = Time.unscaledTime + 120f + 20f * Level;
            IslandFx.FindPopped(lamp.transform.position + new Vector3(0f, 1.2f, 0f));
            AddXp(1);
        }

        // ------------------------------------------------------------ zbieracz

        IEnumerator GathererJob()
        {
            // spadła gwiazda: po odłamek
            foreach (var s in StarPickup.All)
                if (s != null && s.Landed && Reserve(s))
                    return StarTrip(s);
            // owoce leżące pod drzewami (gdy żaden ogrodnik ich nie zbiera)
            foreach (var t in FruitTree.All)
                if (t != null && t.FallenCount > 0 && FreeCrateSpace() > 0 && Reserve(t))
                    return FruitPickupTrip(t);
            // co kilka minut błyskotka w trawie
            shinyTimer -= 2f;
            if (shinyTimer <= 0f)
            {
                shinyTimer = Random.Range(180f, 360f);
                return ShinyTrip();
            }
            return null;
        }

        IEnumerator StarTrip(StarPickup s)
        {
            yield return FlyTo(s.transform.position + new Vector3(0f, 0.4f, 0f));
            if (s != null && s.Landed)
            {
                s.OnClick();
                AddXp(2);
            }
        }

        IEnumerator FruitPickupTrip(FruitTree t)
        {
            yield return PickFallen(t, Carry);
            yield return DeliverTrip();
        }

        IEnumerator ShinyTrip()
        {
            var island = Island.Instance;
            if (island == null || !island.Ready)
                yield break;
            var tiles = new List<TilePos>(island.Map.Tiles.Keys);
            var spot = Island.TileAnchor(tiles[Random.Range(0, tiles.Count)], 12, out _) + new Vector3(Random.Range(-5, 6) / 16f, 0f, 0f);
            yield return FlyTo(spot + new Vector3(0f, 0.25f, 0f));
            for (int i = 0; i < 3; i++)   // „grzebie” w trawie
            {
                IslandFx.FindPopped(spot);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            loadType = CurrencyType.Coins;
            loadAmount = Random.Range(3, 9) + Level * 2;
            ShowCarried(Ui("coin"), 1);
            AddXp(1);
            yield return FlyTo(Home);
            DepositLoad();
        }
    }
}
