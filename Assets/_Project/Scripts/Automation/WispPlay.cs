using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Visual;

namespace Wyspa.Automation
{
    /// <summary>
    /// Ogniki między sobą (GDD §9 p. 101), poza pogaduszkami:
    /// - zabawy wolnych ogników: berek, wyścig do lampy albo drzewa, podrzucanie plonu, taniec w kółku, wspólna drzemka (noc);
    ///   ognik, który zaczyna, prowadzi całą zabawę (rusza też towarzyszami), towarzysze czekają, aż skończy;
    /// - mijanka w locie: uchylenie czapki albo „piątka” (iskierka między nimi);
    /// - gratulacje: przy awansie wolne ogniki obok zlatują się i krążą wokół bohatera z gwiazdkami.
    /// Do tego wyrywanie roślin przez ogrodnika: szarpanie, okruszki ziemi, wyrwanie z ziemi i plon lecący do ręki.
    /// </summary>
    public partial class Wisp
    {
        const int MaxPlays = 1;            // jedna zabawa naraz na wyspie (żeby nie było tłoku)
        const float PlayRange = 4f;        // jak daleko szuka towarzyszy
        static int activePlays;

        /// <summary>Liczniki statyczne od zera przy każdym starcie gry (także bez przeładowania domeny w edytorze).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCounters()
        {
            activePlays = 0;
            activeChats = 0;
        }

        float playCooldown, greetCooldown;
        float hatLiftUntil;
        Wisp leader;                       // kto prowadzi zabawę, w której ten ognik bierze udział
        bool playHost;
        readonly List<Wisp> playmates = new List<Wisp>();

        // ------------------------------------------------------------ wyrywanie

        /// <summary>
        /// Ognik zniża się, łapie roślinę i ciągnie: roślina drży coraz mocniej i wydłuża się, ognik szarpie w górę,
        /// spod rośliny podskakują okruszki ziemi.
        /// </summary>
        IEnumerator Uproot(FarmPlot plot)
        {
            tugging = plot;
            var top = transform.position;
            var grip = top - new Vector3(0f, 0.18f, 0f);
            yield return Flight.Arc(transform, top, grip, Speed * 0.8f, 0f);
            Pose(1, 0.5f);
            const float pull = 0.45f;
            bool crumbs = false;
            for (float t = 0f; t < pull && plot != null; t += Time.unscaledDeltaTime)
            {
                float k = t / pull;
                plot.Tug(k);
                // szarpnięcia: w górę i lekko na boki, coraz mocniej
                float jerk = Mathf.Abs(Mathf.Sin(t * 26f)) * 0.06f * (0.4f + k);
                transform.position = grip + new Vector3(Mathf.Sin(t * 40f) * 0.02f, jerk, 0f);
                if (!crumbs && k > 0.5f)
                {
                    crumbs = true;
                    IslandFx.SoilCrumbs(plot.CropPosition, plot.CropOrder);
                }
                yield return null;
            }
            if (plot != null)
                plot.EndTug();
            tugging = null;
            transform.position = grip;
            if (OnScreen())
                GameEvents.PlaySound("dig", 0.35f);
        }

        /// <summary>Wyrwany plon wylatuje z ziemi wydłużony, łukiem trafia do ręki ognika, a ognik podskakuje z odrzutu.</summary>
        IEnumerator CatchCrop(Sprite sprite, Vector3 from)
        {
            var at = transform.position;
            if (sprite != null)
            {
                var go = new GameObject("Uprooted");
                tempFx = go;
                go.transform.SetParent(transform.parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 4450;   // nad grządkami i ziemią, pod ognikami
                const float fly = 0.3f;
                for (float t = 0f; t < fly; t += Time.unscaledDeltaTime)
                {
                    float u = t / fly;
                    var hand = transform.position + new Vector3(body != null && body.flipX ? -HandX : HandX, HandY - 0.3f, 0f);
                    go.transform.position = Vector3.Lerp(from, hand, u * u) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.35f, 0f);
                    float s = Mathf.Lerp(1f, 0.5f, u);
                    float stretch = Mathf.Lerp(1.3f, 1f, u);   // wyskakuje wyciągnięty, w locie wraca do kształtu
                    go.transform.localScale = new Vector3(s / stretch, s * stretch, 1f);
                    // odrzut: ognik leci w górę, potem wraca
                    transform.position = at + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.22f, 0f);
                    yield return null;
                }
                Destroy(go);
            }
            transform.position = at;
            if (OnScreen())
                GameEvents.PlaySound("harvest", 0.5f);
            yield return Flight.Arc(transform, at, at + new Vector3(0f, 0.18f, 0f), Speed, 0f);
        }

        // ------------------------------------------------------------ nastrój i odpoczynek (GDD §9 p. 103)

        /// <summary>Poniżej tego nastroju ognik musi odpocząć; wstaje, gdy nastrój wróci do Rested.</summary>
        public const float TiredAt = 20f, Rested = 85f, Content = 60f;
        float mood = 80f;
        bool working, resting;
        float restRate;
        WispDreamHouse restHouse;
        float warmthCheck;
        bool nearFire;
        float baseGlowAlpha = -1f;

        /// <summary>Nastrój 0-100: praca obniża, zabawa, rozmowy, ognisko i odpoczynek podnoszą.</summary>
        public float Mood => mood;
        public bool Resting => resting;

        /// <summary>Tempo pracy: pełne od nastroju 60, niżej spada do 60%.</summary>
        public float MoodFactor => mood >= Content ? 1f : 0.6f + 0.4f * Mathf.Clamp01(mood / Content);

        public void SetMood(float value)
        {
            if (value >= 0f)
                mood = Mathf.Clamp(value, 0f, 100f);
        }

        void UpdateMood()
        {
            if (Save.SaveSystem.InMenu)
                return;
            float dt = Time.unscaledDeltaTime;
            warmthCheck -= dt;
            if (warmthCheck <= 0f)
            {
                warmthCheck = 0.5f;
                nearFire = false;
                foreach (var b in WispBonfire.All)
                    if (b != null && (b.Center - transform.position).sqrMagnitude < WispBonfire.Warmth * WispBonfire.Warmth)
                        nearFire = true;
            }
            float d;
            if (resting) d = restRate;
            else if (chatWith != null || leader != null || playHost) d = nearFire ? 0.8f : 0.5f;   // wspólny czas
            else if (working) d = -0.1f;                                                      // ok. 10 min pracy: 80 → 20
            else d = nearFire ? 0.35f : 0.05f;                                                // wolny czas, przy ognisku więcej
            mood = Mathf.Clamp(mood + d * dt, 0f, 100f);
            // śpiący ognik przygasa
            if (glow != null)
            {
                if (baseGlowAlpha < 0f) baseGlowAlpha = glow.color.a;
                var c = glow.color;
                c.a = Mathf.MoveTowards(c.a, resting ? baseGlowAlpha * 0.35f : baseGlowAlpha, dt);
                glow.color = c;
            }
        }

        /// <summary>Odpoczynek: do sennika (jeśli ma wolne miejsce), inaczej nad gniazdem; śpi z „Z”, aż nastrój wróci.</summary>
        IEnumerator Rest()
        {
            resting = true;
            restRate = 0f;
            Release();
            SayGlyph(RuneGlyphs.Dots);   // ziewnięcie
            Vector3 spot;
            restHouse = WispDreamHouse.Free(transform.position);
            if (restHouse != null && restHouse.TryEnter(this, out spot))
                restRate = 2f;           // sennik: ok. 30 s
            else
            {
                restHouse = null;
                spot = Home;
                restRate = 0.6f;         // przy gnieździe: ok. 2 min
            }
            float rate = restRate;
            restRate = 0f;               // w drodze jeszcze nie odpoczywa
            yield return FlyTo(spot, 0.3f);
            restRate = rate;
            float z = 1f;
            for (float t = 0f; mood < Rested && t < 300f; t += Time.unscaledDeltaTime)
            {
                transform.position = spot + new Vector3(0f, Mathf.Sin(Time.unscaledTime * 0.8f + seed) * 0.04f, 0f);   // spokojny oddech
                z -= Time.unscaledDeltaTime;
                if (z <= 0f)
                {
                    z = Random.Range(3.5f, 6f);
                    SayGlyph(RuneGlyphs.Sleep);
                }
                yield return null;
            }
            StopResting();
            SayGlyph(RuneGlyphs.Star);   // wypoczęty
        }

        /// <summary>Zwalnia miejsce przy ognisku (przerwany odpoczynek przy ogniu: zabawa, odpłynięcie).</summary>
        void LeaveFire()
        {
            foreach (var b in WispBonfire.All)
                if (b != null) b.Leave(this);
        }

        void StopResting()
        {
            if (restHouse != null)
                restHouse.Leave(this);
            restHouse = null;
            resting = false;
            restRate = 0f;
        }

        // ------------------------------------------------------------ chowańce

        float startleUntil;

        /// <summary>Kot skoczył na ognika: ognik podskakuje z „!” (praca idzie dalej).</summary>
        public void Startle()
        {
            startleUntil = Time.unscaledTime + 0.5f;
            SayGlyph(RuneGlyphs.Exclaim);
            if (OnScreen())
                GameEvents.PlaySound("bubble", 0.3f);
        }

        Vector3 StartleOffset()
        {
            float k = startleUntil - Time.unscaledTime;
            return k > 0f ? new Vector3(0f, Mathf.Sin(k / 0.5f * Mathf.PI) * 0.3f, 0f) : Vector3.zero;
        }

        /// <summary>Wolny ognik odwiedza chowańca: krąży nad nim, „głaszcze” (serce), chowaniec się cieszy.</summary>
        IEnumerator VisitFamiliar()
        {
            Familiars.Familiar best = null;
            float bestD = 36f;
            foreach (var f in Familiars.Familiar.All)
            {
                if (f == null || !f.Visible)
                    continue;
                float d = (f.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = f; }
            }
            if (best == null)
                return null;
            return Visit(best);
        }

        IEnumerator Visit(Familiars.Familiar f)
        {
            yield return FlyTo(f.transform.position + new Vector3(0f, 0.9f, 0f), 0.3f);
            if (f == null)
                yield break;
            flying = true;
            float phase = Random.value * 6.28f;
            for (float t = 0f; t < 2.5f && f != null; t += Time.unscaledDeltaTime)
            {
                float a = phase + t * 3f;
                var p = f.transform.position + new Vector3(Mathf.Cos(a) * 0.45f, 0.85f + Mathf.Sin(a) * 0.15f, 0f);
                Face(p);
                transform.position = p;
                yield return null;
            }
            flying = false;
            SayGlyph(RuneGlyphs.Heart);
            if (f != null)
                f.React(Familiars.Familiar.Mood.Petted);
            yield return HoverFor(Random.Range(0.6f, 1.2f));
        }

        FarmPlot tugging;     // roślina, którą ognik właśnie szarpie
        GameObject tempFx;    // lecący plon albo piłka z podrzucania

        /// <summary>Przerwana praca albo zabawa (przeniesione gniazdo, odpłynięcie): roślina wraca na miejsce, obrazki znikają.</summary>
        void CleanupInterrupted()
        {
            if (tugging != null)
                tugging.EndTug();
            tugging = null;
            if (tempFx != null)
                Destroy(tempFx);
            tempFx = null;
        }

        // ------------------------------------------------------------ mijanka i gratulacje

        /// <summary>Dwa ogniki mijają się w locie: uchylają czapki albo przybijają „piątkę” (iskierka). Robota idzie dalej.</summary>
        void TryGreet(Wisp other)
        {
            if (greetCooldown > 0f || other.greetCooldown > 0f || leader != null || other.leader != null || chatWith != null)
                return;
            if (!flying && !other.flying)
                return;   // tylko w przelocie (nie przy gnieździe, gdzie wiszą obok siebie)
            greetCooldown = Random.Range(25f, 60f);
            other.greetCooldown = Random.Range(25f, 60f);
            var mid = (BodyPosition + other.BodyPosition) * 0.5f + new Vector3(0f, 0.55f, 0f);
            bool hats = hat != null || other.hat != null;
            if (hats)
            {
                hatLiftUntil = Time.unscaledTime + 0.45f;
                other.hatLiftUntil = Time.unscaledTime + 0.5f;
            }
            else
                IslandFx.FindPopped(mid);   // „piątka”: iskry między nimi
            if (OnScreen())
                GameEvents.PlaySound("click", 0.25f);
            if (Random.value < 0.35f && bubble != null && !bubble.Showing)
                SayGlyph(Random.value < 0.5f ? RuneGlyphs.Note : RuneGlyphs.Heart);
        }

        /// <summary>Awans: do 3 wolnych ogników w pobliżu przylatuje pogratulować.</summary>
        void CallCheer()
        {
            int n = 0;
            foreach (var w in All)
            {
                if (n >= 3)
                    break;
                if (w == this || w == null || !w.CanChat || (w.transform.position - transform.position).sqrMagnitude > 25f)
                    continue;
                w.StopAllCoroutines();
                w.idling = false;
                w.LeaveFire();
                w.StartCoroutine(w.Cheer(this, n++));
            }
        }

        IEnumerator Cheer(Wisp hero, int index)
        {
            flying = true;
            yield return Flight.Arc(transform, transform.position, hero.transform.position + new Vector3(0f, 0.6f, 0f), Speed, 0.3f);
            SayGlyph(index == 0 ? RuneGlyphs.Star : RuneGlyphs.Exclaim);
            float phase = index * 2.1f;
            for (float t = 0f; t < 2.6f && hero != null && hero.isActiveAndEnabled; t += Time.unscaledDeltaTime)
            {
                float a = phase + t * 4f;
                transform.position = hero.transform.position + new Vector3(Mathf.Cos(a) * 0.65f, 0.3f + Mathf.Sin(a) * 0.3f, 0f);
                Face(hero.transform.position);
                if (Random.value < Time.unscaledDeltaTime * 2f)
                    IslandFx.FindPopped(transform.position + new Vector3(0f, 0.4f, 0f));
                yield return null;
            }
            flying = false;
            yield return HoverFor(Random.Range(0.3f, 0.8f));
            yield return Brain();
        }

        // ------------------------------------------------------------ zabawy: wybór i prowadzenie

        IEnumerator StartPlay()
        {
            var friends = new List<Wisp>();
            foreach (var w in All)
                if (w != this && w != null && w.CanChat && (w.transform.position - transform.position).sqrMagnitude < PlayRange * PlayRange)
                    friends.Add(w);
            if (friends.Count == 0 || Random.value < 0.2f)
                return Random.value < 0.5f ? VisitFamiliar() : null;   // sam: czasem odwiedza chowańca
            friends.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude
                         .CompareTo((b.transform.position - transform.position).sqrMagnitude));
            var game = GameLoop.Instance;
            bool night = game != null && game.Clock.IsNight;
            float r = Random.value;
            // noc: częściej taniec i drzemka; dzień: berek, wyścig, podrzucanie
            if (night)
            {
                if (r < 0.4f) return Host(friends, 3, Dance);
                if (r < 0.7f) return Host(friends, 2, Nap);
                if (r < 0.85f) return Host(friends, 1, Tag);
                return Host(friends, 1, Toss);
            }
            if (r < 0.3f) return Host(friends, 1, Tag);
            if (r < 0.55f) return Host(friends, 1, Race);
            if (r < 0.8f) return Host(friends, 1, Toss);
            return Host(friends, 3, Dance);
        }

        /// <summary>Zaprasza do maxFriends towarzyszy, prowadzi zabawę, na koniec puszcza ich wolno.</summary>
        IEnumerator Host(List<Wisp> friends, int maxFriends, System.Func<List<Wisp>, IEnumerator> game)
        {
            activePlays++;
            playHost = true;
            playmates.Clear();
            for (int i = 0; i < friends.Count && playmates.Count < maxFriends; i++)
            {
                friends[i].Join(this);
                playmates.Add(friends[i]);
            }
            yield return game(playmates);
            AddXp(1);   // wspólna zabawa zbliża: doświadczenie dla wszystkich
            foreach (var p in playmates)
                if (Still(p)) p.AddXp(1);
            EndPlay();
            yield return HoverFor(Random.Range(0.4f, 1f));
        }

        /// <summary>Zaproszenie do zabawy: ognik przestaje krążyć i daje się prowadzić gospodarzowi.</summary>
        void Join(Wisp host)
        {
            StopAllCoroutines();
            LeaveFire();
            idling = false;
            flying = false;
            leader = host;
            StartCoroutine(Follow(host));
        }

        IEnumerator Follow(Wisp host)
        {
            for (float t = 0f; t < 40f && leader == host && host != null && host.isActiveAndEnabled; t += Time.unscaledDeltaTime)
                yield return null;
            leader = null;
            flying = false;
            yield return HoverFor(Random.Range(0.3f, 1f));
            yield return Brain();
        }

        /// <summary>Koniec zabawy (także przerwanej): towarzysze są wolni, licznik zabaw spada.</summary>
        void EndPlay()
        {
            foreach (var p in playmates)
                if (p != null && p.leader == this)
                {
                    p.leader = null;
                    p.flying = false;
                }
            playmates.Clear();
            if (playHost)
                activePlays = Mathf.Max(0, activePlays - 1);
            playHost = false;
            flying = false;
        }

        /// <summary>Towarzysz dalej w zabawie (nie zniknął, nikt go nie odwołał).</summary>
        bool Still(Wisp p) => p != null && p.isActiveAndEnabled && p.leader == this;

        void Face(Vector3 target)
        {
            if (body != null && Mathf.Abs(target.x - transform.position.x) > 0.02f)
                body.flipX = target.x < transform.position.x;
        }

        void SayGlyph(int glyph)
        {
            if (bubble != null)
                bubble.Say(new[] { glyph }, RuneColor, VoicePitch, OnScreen() ? 0.3f : 0f);
        }

        static Vector3 ArcPos(Vector3 a, Vector3 b, float u, float arc)
        {
            u = Mathf.Clamp01(u);
            float e = u * u * (3f - 2f * u);
            return Vector3.Lerp(a, b, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * arc, 0f);
        }

        /// <summary>Wszyscy (gospodarz i towarzysze) lecą na swoje miejsca naraz.</summary>
        IEnumerator Gather(List<Wisp> who, List<Vector3> spots, float speed)
        {
            var from = new List<Vector3>();
            float longest = 0.3f;
            for (int i = 0; i < who.Count; i++)
            {
                if (who[i] != this && !Still(who[i]))
                {
                    from.Add(Vector3.zero);   // towarzysz zniknął (rozebrane gniazdo): pomijany
                    continue;
                }
                from.Add(who[i].transform.position);
                longest = Mathf.Max(longest, Vector3.Distance(from[i], spots[i]) / speed);
                who[i].flying = true;
                who[i].Face(spots[i]);
            }
            for (float t = 0f; t < longest; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < who.Count; i++)
                    if (who[i] == this || Still(who[i]))
                        who[i].transform.position = ArcPos(from[i], spots[i], t / longest, 0.2f);
                yield return null;
            }
            foreach (var w in who)
                if (w == this || Still(w)) w.flying = false;
        }

        Vector3 PlayCenter
        {
            get
            {
                var fire = nest != null ? WispBonfire.Near(nest.transform.position) : null;   // przy ognisku, gdy jest
                return fire != null ? fire.Center + new Vector3(0f, 0.3f, 0f)
                                    : (nest != null ? nest.transform.position : transform.position) + new Vector3(0f, 1.3f, 0f);
            }
        }

        // ------------------------------------------------------------ berek

        /// <summary>Berek: goniący leci za uciekającym; złapany = iskry i „!”, role się zamieniają (raz), na koniec śmiech.</summary>
        IEnumerator Tag(List<Wisp> mates)
        {
            var other = mates[0];
            var center = PlayCenter;
            Wisp chaser = this, runner = other;
            Vector3 goal = center + RandomOffset();
            int swaps = 0;
            float pause = 0.6f;   // goniący daje fory na start
            for (float t = 0f; t < 9f && Still(other); t += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                chaser.flying = runner.flying = true;
                // uciekający: od punktu do punktu wokół gniazda
                runner.transform.position = Vector3.MoveTowards(runner.transform.position, goal, 2.1f * dt);
                runner.Face(goal);
                if ((runner.transform.position - goal).sqrMagnitude < 0.01f)
                    goal = center + RandomOffset();
                pause -= dt;
                if (pause <= 0f)
                {
                    chaser.transform.position = Vector3.MoveTowards(chaser.transform.position, runner.transform.position, 2.5f * dt);
                    chaser.Face(runner.transform.position);
                }
                if (pause <= 0f && (chaser.transform.position - runner.transform.position).sqrMagnitude < 0.09f)
                {
                    IslandFx.FindPopped((chaser.transform.position + runner.transform.position) * 0.5f + new Vector3(0f, 0.5f, 0f));
                    if (OnScreen())
                        GameEvents.PlaySound("bubble", 0.4f);
                    runner.SayGlyph(RuneGlyphs.Exclaim);
                    if (++swaps > 1)
                        break;
                    (chaser, runner) = (runner, chaser);   // berek!
                    pause = 0.8f;
                    goal = center + RandomOffset();
                }
                yield return null;
            }
            chaser.flying = runner.flying = false;
            SayGlyph(RuneGlyphs.Note);
            if (Still(other))
                other.SayGlyph(RuneGlyphs.Note);
            yield return Bob(mates, 1.2f);
        }

        static Vector3 RandomOffset() => new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-0.4f, 0.8f), 0f);

        // ------------------------------------------------------------ wyścig

        /// <summary>Wyścig do lampy albo drzewa i z powrotem: odliczanie „! ! !”, okrążenie celu; zwycięzca robi pętlę z gwiazdką.</summary>
        IEnumerator Race(List<Wisp> mates)
        {
            var other = mates[0];
            var center = PlayCenter;
            var goal = RaceGoal(center);
            var startA = center + new Vector3(-0.3f, 0f, 0f);
            var startB = center + new Vector3(0.3f, 0f, 0f);
            yield return Gather(new List<Wisp> { this, other }, new List<Vector3> { startA, startB }, Speed * 0.7f);
            Face(goal);
            if (Still(other))
                other.Face(goal);
            for (int i = 0; i < 3 && Still(other); i++)
            {
                SayGlyph(RuneGlyphs.Exclaim);
                yield return new WaitForSecondsRealtime(0.45f);
            }
            if (!Still(other))
                yield break;
            float distance = Vector3.Distance(center, goal) * 2f + 2.5f;
            float timeA = distance / Random.Range(2.3f, 3f), timeB = distance / Random.Range(2.3f, 3f);
            flying = other.flying = true;
            float end = Mathf.Max(timeA, timeB);
            for (float t = 0f; t < end && Still(other); t += Time.unscaledDeltaTime)
            {
                transform.position = RacePos(startA, goal, t / timeA, 0f);
                other.transform.position = RacePos(startB, goal, t / timeB, Mathf.PI);
                Face(transform.position + (t / timeA < 0.4f ? goal - startA : startA - goal));
                other.Face(other.transform.position + (t / timeB < 0.4f ? goal - startB : startB - goal));
                yield return null;
            }
            flying = other.flying = false;
            if (!Still(other))
                yield break;
            var winner = timeA <= timeB ? this : other;
            var loser = winner == this ? other : this;
            winner.SayGlyph(RuneGlyphs.Star);
            loser.SayGlyph(RuneGlyphs.Dots);
            IslandFx.FindPopped(winner.transform.position + new Vector3(0f, 0.5f, 0f));
            if (OnScreen())
                GameEvents.PlaySound("magic", 0.3f);
            yield return Loop(winner);
            yield return new WaitForSecondsRealtime(0.6f);
        }

        Vector3 RaceGoal(Vector3 center)
        {
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var l in Lamp.All)
                Consider(l != null ? l.transform.position + new Vector3(0f, 1f, 0f) : (Vector3?)null);
            foreach (var t in FruitTree.All)
                Consider(t != null ? t.transform.position + new Vector3(0f, 1.4f, 0f) : (Vector3?)null);
            void Consider(Vector3? p)
            {
                if (p == null) return;
                float d = Vector3.Distance(center, p.Value);
                if (d > 1.8f && d < 5f && d < bestD) { bestD = d; best = p; }
            }
            return best ?? center + new Vector3(Random.value < 0.5f ? -2.6f : 2.6f, 0.4f, 0f);
        }

        /// <summary>Tor wyścigu: 0-0,4 łuk do celu, 0,4-0,6 okrążenie celu, 0,6-1 łuk z powrotem.</summary>
        static Vector3 RacePos(Vector3 start, Vector3 goal, float u, float phase)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.4f)
                return ArcPos(start, goal, u / 0.4f, 0.35f);
            if (u < 0.6f)
            {
                float a = (u - 0.4f) / 0.2f * Mathf.PI * 2f + phase;
                return goal + new Vector3(Mathf.Sin(a) * 0.45f, (Mathf.Cos(a) - 1f) * 0.22f * (phase == 0f ? 1f : -1f), 0f);
            }
            return ArcPos(goal, start, (u - 0.6f) / 0.4f, 0.35f);
        }

        /// <summary>Pętla w powietrzu (radość): koło w pionie.</summary>
        IEnumerator Loop(Wisp w)
        {
            var at = w.transform.position;
            w.flying = true;
            for (float t = 0f; t < 0.6f && w != null; t += Time.unscaledDeltaTime)
            {
                float a = t / 0.6f * Mathf.PI * 2f;
                w.transform.position = at + new Vector3(Mathf.Sin(a) * 0.25f, (1f - Mathf.Cos(a)) * 0.25f, 0f);
                yield return null;
            }
            if (w != null)
            {
                w.transform.position = at;
                w.flying = false;
            }
        }

        // ------------------------------------------------------------ podrzucanie

        /// <summary>Podrzucanie plonu: kilka rzutów łukiem między ognikami; czasem ktoś nie złapie (spada, „!”).</summary>
        IEnumerator Toss(List<Wisp> mates)
        {
            var other = mates[0];
            var center = PlayCenter;
            var a = center + new Vector3(-0.7f, 0f, 0f);
            var b = center + new Vector3(0.7f, 0f, 0f);
            yield return Gather(new List<Wisp> { this, other }, new List<Vector3> { a, b }, Speed * 0.7f);
            if (!Still(other))
                yield break;
            Face(b);
            other.Face(a);
            var go = new GameObject("TossBall");
            tempFx = go;
            go.transform.SetParent(transform.parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallSprite();
            sr.sortingOrder = 4500;
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            int throws = Random.Range(4, 8);
            bool drop = Random.value < 0.3f;
            var hand = new Vector3(0f, HandY - 0.25f, 0f);
            Wisp from = this, to = other;
            for (int i = 0; i < throws && Still(other); i++)
            {
                bool last = i == throws - 1;
                var p0 = from.transform.position + hand;
                var p1 = last && drop ? to.transform.position + new Vector3(0f, -1f, 0f) : to.transform.position + hand;
                const float flight = 0.55f;
                for (float t = 0f; t < flight && Still(other); t += Time.unscaledDeltaTime)
                {
                    float u = t / flight;
                    go.transform.position = Vector3.Lerp(p0, p1, u) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.6f, 0f);
                    // łapiący wyciąga się w stronę piłki
                    to.transform.position = Flight.Hover(to == this ? a : b, to.seed) + new Vector3(0f, u > 0.8f ? -0.05f : 0f, 0f);
                    yield return null;
                }
                if (last && drop)
                {
                    IslandFx.SoilCrumbs(go.transform.position, 4450);
                    to.SayGlyph(RuneGlyphs.Exclaim);
                    from.SayGlyph(RuneGlyphs.Note);
                    yield return new WaitForSecondsRealtime(0.5f);
                    IslandFx.FindPopped(go.transform.position);   // plon znika w iskrach (nic nie ginie z magazynu)
                    break;
                }
                if (OnScreen())
                    GameEvents.PlaySound("click", 0.2f);
                (from, to) = (to, from);
            }
            Destroy(go);
            if (!drop)
            {
                SayGlyph(RuneGlyphs.Note);
                yield return Bob(mates, 0.8f);
            }
        }

        /// <summary>Czym rzucają: dojrzały plon losowej rośliny z bazy (bez bazy: iskra).</summary>
        static Sprite BallSprite()
        {
            var game = GameLoop.Instance;
            if (game != null && game.Crops != null && game.Crops.crops.Count > 0)
            {
                var c = game.Crops.crops[Random.Range(0, game.Crops.crops.Count)];
                if (c != null && c.RipeSprite != null)
                    return c.RipeSprite;
            }
            return null;
        }

        // ------------------------------------------------------------ taniec

        /// <summary>Taniec w kółku: ogniki krążą wokół wspólnego środka, w rytmie się unoszą, koło się zwęża i rozszerza;
        /// na koniec zlatują się w środek i rozbłyskują.</summary>
        IEnumerator Dance(List<Wisp> mates)
        {
            var who = new List<Wisp> { this };
            who.AddRange(mates);
            var center = PlayCenter;
            int n = who.Count;
            var spots = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n;
                spots.Add(center + new Vector3(Mathf.Cos(a0) * 0.8f, Mathf.Sin(a0) * 0.35f, 0f));
            }
            yield return Gather(who, spots, Speed * 0.7f);
            foreach (var w in who) w.flying = true;
            const float length = 6f;
            for (float t = 0f; t < length && Still(mates[0]); t += Time.unscaledDeltaTime)
            {
                float radius = 0.8f - 0.35f * Mathf.Sin(t / length * Mathf.PI);   // zwęża się w połowie tańca
                for (int i = 0; i < n; i++)
                {
                    var w = who[i];
                    if (w != this && !Still(w))
                        continue;
                    float a = i * Mathf.PI * 2f / n + t * 2.2f;
                    float bob = Mathf.Sin(t * 6f + i) * 0.08f;
                    w.transform.position = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.45f + bob, 0f);
                    w.Face(w.transform.position + new Vector3(-Mathf.Sin(a), 0f, 0f));   // twarzą w kierunku ruchu
                }
                yield return null;
            }
            // finał: do środka i rozbłysk
            var mid = new List<Vector3>();
            for (int i = 0; i < n; i++)
                mid.Add(center + new Vector3((i - (n - 1) / 2f) * 0.25f, 0.1f, 0f));
            yield return Gather(who, mid, Speed);
            IslandFx.FindPopped(center + new Vector3(0f, 0.6f, 0f));
            IslandFx.FindPopped(center + new Vector3(-0.3f, 0.3f, 0f));
            IslandFx.FindPopped(center + new Vector3(0.3f, 0.3f, 0f));
            if (OnScreen())
                GameEvents.PlaySound("magic", 0.35f);
            foreach (var w in who)
                if (w == this || Still(w))
                    w.SayGlyph(Random.value < 0.5f ? RuneGlyphs.Note : RuneGlyphs.Heart);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        // ------------------------------------------------------------ drzemka

        /// <summary>Wspólna drzemka (noc): ogniki tulą się nad gniazdem, unoszą się w jednym rytmie, po kolei „Z”.</summary>
        IEnumerator Nap(List<Wisp> mates)
        {
            var who = new List<Wisp> { this };
            who.AddRange(mates);
            var home = (nest != null ? nest.transform.position : transform.position) + new Vector3(0f, 1.0f, 0f);
            var spots = new List<Vector3>();
            for (int i = 0; i < who.Count; i++)
                spots.Add(home + new Vector3((i - (who.Count - 1) / 2f) * 0.42f, Mathf.Abs(i - (who.Count - 1) / 2f) * -0.05f, 0f));
            yield return Gather(who, spots, Speed * 0.5f);
            for (int i = 0; i < who.Count; i++)
                if (who[i] == this || Still(who[i]))
                    who[i].Face(home);   // przytuleni do środka
            float next = 0.5f;
            int speaker = 0;
            for (float t = 0f; t < 8f && Still(mates[0]); t += Time.unscaledDeltaTime)
            {
                float breathe = Mathf.Sin(t * 1.3f) * 0.06f;   // wspólny oddech
                for (int i = 0; i < who.Count; i++)
                    if (who[i] == this || Still(who[i]))
                        who[i].transform.position = spots[i] + new Vector3(0f, breathe, 0f);
                next -= Time.unscaledDeltaTime;
                if (next <= 0f)
                {
                    next = Random.Range(1.6f, 2.6f);
                    var w = who[speaker++ % who.Count];
                    if (w == this || Still(w))
                        w.SayGlyph(RuneGlyphs.Sleep);
                }
                yield return null;
            }
            SayGlyph(RuneGlyphs.Dots);   // ziewnięcie na pobudkę
            yield return new WaitForSecondsRealtime(0.6f);
        }

        /// <summary>Wspólne podskoki na koniec zabawy (radość).</summary>
        IEnumerator Bob(List<Wisp> mates, float seconds)
        {
            var who = new List<Wisp> { this };
            who.AddRange(mates);
            var at = new List<Vector3>();
            foreach (var w in who)
                at.Add(w == this || Still(w) ? w.transform.position : Vector3.zero);   // zniszczony towarzysz: pomijany
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < who.Count; i++)
                    if (who[i] == this || Still(who[i]))
                        who[i].transform.position = at[i] + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 9f + i)) * 0.12f, 0f);
                yield return null;
            }
            for (int i = 0; i < who.Count; i++)
                if (who[i] == this || Still(who[i]))
                    who[i].transform.position = at[i];
        }

        // ------------------------------------------------------------ budowa monumentu (§13.11)

        /// <summary>Monument, przy którym ognik teraz buduje (null = nie buduje).</summary>
        public World.Monument BuildSite { get; private set; }
        /// <summary>Doleciał na rusztowanie i stuka (liczy się do tempa budowy).</summary>
        public bool AtBuildSpot { get; private set; }

        void LeaveBuild()
        {
            if (BuildSite != null)
                BuildSite.Leave(this);
            BuildSite = null;
            AtBuildSpot = false;
        }

        /// <summary>
        /// Budowa: lot na miejsce przy rusztowaniu, potem pół minuty stukania (krótki dołek co 0.8 s, wióry sypie monument),
        /// dopóki etap trwa. Potem wraca do zwykłego krążenia; inny wolny ognik może go zmienić.
        /// </summary>
        IEnumerator BuildTrip(World.Monument site, Vector3 spot)
        {
            BuildSite = site;
            AtBuildSpot = false;
            yield return FlyTo(spot, 0.35f);
            AtBuildSpot = true;
            float stay = Random.Range(30f, 50f);
            for (float t = 0f; t < stay && site != null && site.Building && BuildSite == site; t += Time.unscaledDeltaTime)
            {
                float k = (t + seed) % 0.8f / 0.8f;
                float dip = k < 0.25f ? Mathf.Sin(k / 0.25f * Mathf.PI) * 2f / 16f : 0f;
                transform.position = spot - new Vector3(0f, dip, 0f);
                if (body != null) body.flipX = site.transform.position.x < spot.x;
                yield return null;
            }
            transform.position = spot;
            LeaveBuild();
            AddXp(1);   // budowa uczy
            yield return HoverFor(Random.Range(0.5f, 1f));
        }

        // ------------------------------------------------------------ drewno do pieca Żaru (GDD §9 p. 110)

        /// <summary>Piec, do którego ognik właśnie niesie drewno (null = nie niesie).</summary>
        public EmberKiln FeedSite { get; private set; }

        void LeaveKiln()
        {
            if (FeedSite != null)
                FeedSite.Release(this);
            FeedSite = null;
            ShowCarried(null, 0);
        }

        /// <summary>
        /// Wolny ognik: bierze polana ze składu przy gnieździe, leci z nimi do pieca i wrzuca je do paleniska
        /// (drewno ubywa ze składu dopiero przy wrzuceniu, więc przerwany kurs niczego nie gubi).
        /// </summary>
        IEnumerator StokeTrip(EmberKiln kiln)
        {
            FeedSite = kiln;
            yield return FlyTo(Home, 0.2f);
            if (kiln == null)
            {
                LeaveKiln();   // piec rozebrany w trakcie kursu
                yield break;
            }
            ShowCarried(Ui("wood"), EmberKiln.Wood, true);
            yield return FlyTo(kiln.Mouth + new Vector3(0.35f, 0.25f, 0f));
            yield return Toss(kiln);
            var game = GameLoop.Instance;
            if (kiln != null && game != null && game.Wallet.Get(CurrencyType.Wood) >= kiln.Reserve + EmberKiln.Wood
                && game.Wallet.TrySpend(new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Wood, EmberKiln.Wood) }))
                game.Wallet.Add(CurrencyType.Wood, kiln.Feed(EmberKiln.Wood));
            LeaveKiln();
            yield return HoverFor(Random.Range(0.4f, 0.8f));
        }

        /// <summary>Drwal: świeżo ścięte drewno prosto do paleniska; co się nie zmieści, wraca do składu.</summary>
        IEnumerator StokeWithLoad(EmberKiln kiln)
        {
            FeedSite = kiln;
            yield return FlyTo(kiln.Mouth + new Vector3(0.35f, 0.25f, 0f));
            yield return Toss(kiln);
            if (kiln == null && loadAmount > 0)
            {
                yield return FlyTo(Home);   // piec zniknął po drodze: drewno do składu
                DepositLoad();
            }
            else if (kiln != null && loadAmount > 0)
            {
                loadAmount = kiln.Feed(loadAmount);
                if (loadAmount > 0)
                    yield return FlyTo(Home);
                DepositLoad();
            }
            LeaveKiln();
        }

        /// <summary>Zamach i wrzut: ognik nurkuje do paleniska, polana znikają w ogniu.</summary>
        IEnumerator Toss(EmberKiln kiln)
        {
            if (kiln == null)
                yield break;
            var from = transform.position;
            if (body != null) body.flipX = kiln.Mouth.x < from.x;
            yield return Flight.Arc(transform, from, kiln.Mouth + new Vector3(0.15f, 0.1f, 0f), 6f, 0.08f);
            Pose(1, 0.2f);
            ShowCarried(null, 0);
            yield return Flight.Arc(transform, transform.position, from, 4f, 0.08f);
        }
    }
}
