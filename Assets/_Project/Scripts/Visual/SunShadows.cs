using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Słońce i cienie (GDD §9 p. 100). Cień każdego stojącego obiektu na wyspie (drzewa, kamienie, budowle, lampy) to jego
    /// sylwetka położona na ziemi: ciemna, półprzezroczysta, ścięta w stronę od światła.
    /// Dzień: słońce wschodzi po prawej (wschód) i zachodzi po lewej, więc rano cienie są długie w lewo, w południe krótkie,
    /// wieczorem długie w prawo; o świcie i zmierzchu bledną przy horyzoncie. Noc: cień od najbliższej zapalonej lampy
    /// (dłuższy dalej od niej, wyraźniejszy bliżej), a poza lampami słaby cień od księżyca.
    /// Rośliny na grządkach rzucają cień tak samo. Ogniki (lecą nad ziemią) mają w dzień miękką plamkę cienia pod sobą,
    /// przesuniętą od słońca; w nocy jej nie ma, bo same świecą.
    /// Do tego ciepła poświata od strony słońca (Point Light 2D) o świcie i zmierzchu.
    /// Sylwetka: para obiektów (obrót, skala, obrót) daje dowolne ścięcie sprite'a bez własnego shadera.
    /// </summary>
    public class SunShadows : MonoBehaviour
    {
        const int Order = -1400;            // nad grządkami (-1500) i lądem, pod wszystkim, co stoi
        const int MinHeightPx = 7;          // płaskie rzeczy (runy, kwiatki) nie rzucają cienia
        const float ScanEvery = 0.5f;
        static readonly Color Shade = new Color(0.04f, 0.02f, 0.08f, 1f);

        class Shadow
        {
            public Transform root, skew;
            public SpriteRenderer sr, source;
            public Lamp lamp;   // gdy obiekt to lampa: jej własne światło cienia nie rzuca
        }

        const float WispGround = 0.42f;     // plamka cienia tyle pod ognikiem (ziemia pod nim)
        readonly Dictionary<Transform, Shadow> shadows = new Dictionary<Transform, Shadow>();
        readonly HashSet<Transform> seen = new HashSet<Transform>();
        readonly Dictionary<Wisp, SpriteRenderer> wispShadows = new Dictionary<Wisp, SpriteRenderer>();
        readonly List<Wisp> goneWisps = new List<Wisp>();
        Sprite blob;
        readonly List<Transform> gone = new List<Transform>();
        GameClock clock;
        Light2D sunGlow;
        float scan;

        public void Setup(GameClock gameClock) => clock = gameClock;

        void Start()
        {
            if (clock == null)
                clock = FindAnyObjectByType<GameClock>();
            var go = new GameObject("SunGlow");
            go.transform.SetParent(transform, false);
            sunGlow = go.AddComponent<Light2D>();
            sunGlow.lightType = Light2D.LightType.Point;
            sunGlow.pointLightOuterRadius = 16f;
            sunGlow.pointLightInnerRadius = 2f;
            sunGlow.falloffIntensity = 0.7f;
            sunGlow.intensity = 0f;
        }

        // ------------------------------------------------------------ słońce

        /// <summary>Słońce: x od 1 (wschód, prawo) do -1 (zachód), wysokość 0..1 (0 = horyzont), widoczne tylko w dzień.</summary>
        static bool Sun(GameClock c, out float x, out float height)
        {
            float u = c.Second / GameClock.DaySeconds;   // 0 = 06:00, 1 = 20:00
            x = Mathf.Cos(u * Mathf.PI);
            height = Mathf.Sin(u * Mathf.PI);
            return u < 1f;
        }

        void UpdateSunGlow(float sunX, float height, bool day)
        {
            if (sunGlow == null)
                return;
            // najmocniej nisko nad horyzontem (świt i zmierzch), znika w ciągu dnia i w nocy
            float low = day ? Mathf.Clamp01(1f - height / 0.6f) * Mathf.Clamp01(height / 0.08f) : 0f;
            sunGlow.intensity = 0.45f * low * Weather.WeatherSystem.LightMultiplier;
            sunGlow.color = sunX > 0f ? new Color(1f, 0.72f, 0.55f) : new Color(1f, 0.56f, 0.32f);   // świt różowo-złoty, zmierzch pomarańczowy
            var island = Island.Instance;
            Vector2 center = island != null && island.Ready ? island.LandRect.center : Vector2.zero;
            sunGlow.transform.position = new Vector3(center.x + sunX * 9f, center.y + 1.5f, 0f);
        }

        // ------------------------------------------------------------ cienie

        void Scan()
        {
            var island = Island.Instance;
            var root = island != null ? island.DecoRoot : null;
            if (root == null)
                return;
            var seen = this.seen;
            seen.Clear();
            for (int i = 0; i < root.childCount; i++)
            {
                var t = root.GetChild(i);
                if (t == null || !t.gameObject.activeInHierarchy || !t.TryGetComponent(out SpriteRenderer src))
                    continue;
                seen.Add(t);
                if (!shadows.ContainsKey(t))
                    shadows[t] = Create(t, src, island);
            }
            // rośliny na grządkach (sprite rośliny jest dzieckiem grządki)
            foreach (var plot in FarmPlot.All)
            {
                var src = plot != null ? plot.CropRenderer : null;
                if (src == null || !plot.isActiveAndEnabled)
                    continue;
                var t = src.transform;
                seen.Add(t);
                if (!shadows.ContainsKey(t))
                    shadows[t] = Create(t, src, island);
            }
            ScanWisps(island);
            ScanFamiliars(island);
            gone.Clear();
            foreach (var kv in shadows)
                if (kv.Key == null || !seen.Contains(kv.Key))
                    gone.Add(kv.Key);
            foreach (var t in gone)
            {
                if (shadows[t].root != null)
                    Destroy(shadows[t].root.gameObject);
                shadows.Remove(t);
            }
        }

        void ScanWisps(Island island)
        {
            foreach (var w in Wisp.All)
                if (w != null && !wispShadows.ContainsKey(w))
                {
                    var go = new GameObject("WispShadow");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = Blob();
                    sr.sortingOrder = Order;
                    sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    if (island.Art != null && island.Art.glow != null)
                        sr.sharedMaterial = island.Art.glow;
                    wispShadows[w] = sr;
                }
            goneWisps.Clear();
            foreach (var kv in wispShadows)
                if (kv.Key == null || !kv.Key.isActiveAndEnabled)
                    goneWisps.Add(kv.Key);
            foreach (var w in goneWisps)
            {
                if (wispShadows[w] != null)
                    Destroy(wispShadows[w].gameObject);
                wispShadows.Remove(w);
            }
        }

        /// <summary>Miękka plamka 9x3 px (środek pełny, brzegi półprzezroczyste), pivot w środku.</summary>
        Sprite Blob()
        {
            if (blob != null)
                return blob;
            const int w = 9, h = 3;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "WispShadow" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - (w - 1) / 2f) / (w / 2f), dy = (y - (h - 1) / 2f) / (h / 2f);
                    float d = dx * dx + dy * dy;
                    byte a = d < 0.45f ? (byte)255 : d < 1f ? (byte)140 : (byte)0;
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            blob = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            return blob;
        }

        readonly Dictionary<Familiars.Familiar, SpriteRenderer> familiarShadows = new Dictionary<Familiars.Familiar, SpriteRenderer>();
        readonly List<Familiars.Familiar> goneFamiliars = new List<Familiars.Familiar>();

        /// <summary>Chowańce: plamka cienia (na ziemi pod nimi, latające niżej i mniejsza).</summary>
        void ScanFamiliars(Island island)
        {
            foreach (var f in Familiars.Familiar.All)
                if (f != null && !familiarShadows.ContainsKey(f))
                {
                    var go = new GameObject("FamiliarShadow");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = Blob();
                    sr.sortingOrder = Order;
                    sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    if (island.Art != null && island.Art.glow != null)
                        sr.sharedMaterial = island.Art.glow;
                    familiarShadows[f] = sr;
                }
            goneFamiliars.Clear();
            foreach (var kv in familiarShadows)
                if (kv.Key == null || !kv.Key.isActiveAndEnabled)
                    goneFamiliars.Add(kv.Key);
            foreach (var f in goneFamiliars)
            {
                if (familiarShadows[f] != null)
                    Destroy(familiarShadows[f].gameObject);
                familiarShadows.Remove(f);
            }
        }

        void UpdateFamiliarShadows(Vector2 sunDir, float sunAlpha, float nightAlpha)
        {
            foreach (var kv in familiarShadows)
            {
                var f = kv.Key;
                var sr = kv.Value;
                if (f == null || sr == null)
                {
                    if (sr != null) sr.enabled = false;
                    continue;
                }
                // w nocy słaby cień (od księżyca); lis-widmo i ćma (świeci) bez cienia w nocy
                float a = (sunAlpha > 0.01f ? sunAlpha : nightAlpha) * f.Alpha;
                if (sunAlpha <= 0.01f && (f.Kind == Familiars.FamiliarSystem.Fox || f.Kind == Familiars.FamiliarSystem.Moth))
                    a = 0f;
                sr.enabled = a > 0.01f;
                if (!sr.enabled)
                    continue;
                var p = f.GroundPoint;
                float x = Mathf.Round((p.x + sunDir.x * 0.1f) * 16f) / 16f, y = Mathf.Round((p.y + 0.5f / 16f) * 16f) / 16f;
                sr.transform.position = new Vector3(x, y, 0f);
                sr.transform.localScale = f.Flying ? new Vector3(0.9f, 1f, 1f) : new Vector3(1.4f, 1f, 1f);
                sr.color = new Color(Shade.r, Shade.g, Shade.b, a * (f.Flying ? 0.7f : 1f));
            }
        }

        void UpdateWispShadows(Vector2 sunDir, float sunAlpha)
        {
            foreach (var kv in wispShadows)
            {
                var w = kv.Key;
                var sr = kv.Value;
                if (w == null || sr == null)
                {
                    if (sr != null) sr.enabled = false;
                    continue;
                }
                float a = sunAlpha * 0.85f;
                sr.enabled = a > 0.01f;
                if (!sr.enabled)
                    continue;
                var p = w.BodyPosition;
                // cień trochę od słońca, na pełne piksele (plamka nie pływa między pikselami)
                float x = Mathf.Round((p.x + sunDir.x * 0.12f) * 16f) / 16f, y = Mathf.Round((p.y - WispGround) * 16f) / 16f;
                sr.transform.position = new Vector3(x, y, 0f);
                sr.color = new Color(Shade.r, Shade.g, Shade.b, a);
            }
        }

        Shadow Create(Transform caster, SpriteRenderer src, Island island)
        {
            var root = new GameObject("Shadow_" + caster.name).transform;
            root.SetParent(transform, false);
            var skew = new GameObject("Skew").transform;
            skew.SetParent(root, false);
            var sr = skew.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order;
            sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // tylko na lądzie (maska LandMask), nie na wodzie
            if (island.Art != null && island.Art.glow != null)
                sr.sharedMaterial = island.Art.glow;   // bez oświetlenia: cień nie jaśnieje od lamp
            caster.TryGetComponent(out Lamp lamp);
            return new Shadow { root = root, skew = skew, sr = sr, source = src, lamp = lamp };
        }

        void LateUpdate()
        {
            if (clock == null)
                return;
            scan -= Time.unscaledDeltaTime;
            if (scan <= 0f)
            {
                scan = ScanEvery;
                Scan();
            }
            bool day = Sun(clock, out float sunX, out float height);
            UpdateSunGlow(sunX, height, day);

            // cień od słońca: długi przy horyzoncie, krótki w południe; blednie o świcie i zmierzchu i w deszczu
            float weather = Weather.WeatherSystem.Raining ? 0.35f : 1f;
            float length = 0.35f + 1.5f * Mathf.Pow(1f - height, 1.5f);
            var sunDir = new Vector2(-sunX * length + 0.15f, 0.3f + 0.1f * height);
            float sunAlpha = day ? 0.3f * Mathf.Clamp01(height / 0.12f) * weather : 0f;
            float moon = 1f - Mathf.Abs(clock.MoonPhase - 0.5f) * 2f;
            float moonAlpha = clock.IsNight ? 0.1f * (0.3f + 0.7f * moon) * weather : 0f;
            bool darkish = clock.IsNight || clock.IsTwilight;   // raz na klatkę, nie dla każdego obiektu
            UpdateWispShadows(sunDir, sunAlpha);
            UpdateFamiliarShadows(sunDir, sunAlpha, moonAlpha * 1.5f);

            foreach (var kv in shadows)
            {
                var s = kv.Value;
                var src = s.source;
                if (kv.Key == null || src == null || s.sr == null)
                {
                    if (s.sr != null) s.sr.enabled = false;   // zniszczony obiekt: cień znika od razu, nie przy skanowaniu
                    continue;
                }
                var sprite = src.sprite;
                if (sprite == null || !src.enabled || sprite.rect.height < MinHeightPx)
                {
                    s.sr.enabled = false;
                    continue;
                }
                Vector2 basePos = kv.Key.position;
                Vector2 dir = sunDir;
                float alpha = sunAlpha;
                if (darkish)
                {
                    // lampa: najbliższa zapalona w zasięgu (cień od niej w przeciwną stronę)
                    if (NearestLamp(basePos, s.lamp, out var away, out float closeness) && 0.42f * closeness * weather > alpha)
                    {
                        dir = away;
                        alpha = 0.42f * closeness * weather;
                    }
                    else if (moonAlpha > alpha)
                    {
                        dir = new Vector2(-0.6f, 0.3f);
                        alpha = moonAlpha;
                    }
                }
                if (alpha < 0.01f)
                {
                    s.sr.enabled = false;
                    continue;
                }
                s.sr.enabled = true;
                if (s.sr.sprite != sprite) s.sr.sprite = sprite;
                s.sr.flipX = src.flipX;
                s.sr.color = new Color(Shade.r, Shade.g, Shade.b, alpha);
                // pionowa składowa nie może zniknąć (cień zmieniłby się w kreskę)
                if (Mathf.Abs(dir.y) < 0.18f)
                    dir.y = dir.y < 0f ? -0.18f : 0.18f;
                var ls = kv.Key.lossyScale;
                Apply(s, basePos, Mathf.Abs(ls.x), dir * Mathf.Abs(ls.y));   // przysiad rośliny, potrząsanie drzewa
            }
        }

        bool NearestLamp(Vector2 at, Lamp self, out Vector2 away, out float closeness)
        {
            away = default;
            closeness = 0f;
            Lamp best = null;
            float bestK = 0f;
            Vector2 bestD = default;
            foreach (var l in Lamp.All)
            {
                if (l == null || l == self || !l.IsOn || l.Definition == null)
                    continue;
                Vector2 d = at - (Vector2)l.transform.position;
                float r = l.Definition.radius;
                float dist = d.magnitude;
                if (dist < 0.1f || dist > r)
                    continue;
                float k = 1f - dist / r;
                if (k > bestK)
                {
                    best = l;
                    bestK = k;
                    bestD = d;
                }
            }
            if (best == null)
                return false;
            float len = bestD.magnitude;
            float stretch = 0.5f + 1.2f * (1f - bestK);   // dalej od lampy = dłuższy cień
            away = bestD / len * stretch;
            closeness = Mathf.Pow(bestK, 0.7f);
            return true;
        }

        /// <summary>
        /// Ścięcie sylwetki: punkt sprite'a (x, y) od podstawy trafia w (x + dir.x * y, dir.y * y). Macierz rozkładamy na
        /// obrót × skala × obrót (SVD 2x2): korzeń dostaje obrót phi i skalę, dziecko obrót theta.
        /// </summary>
        static void Apply(Shadow s, Vector2 basePos, float scaleX, Vector2 dir)
        {
            float m00 = scaleX, m01 = dir.x, m10 = 0f, m11 = dir.y;
            float e = (m00 + m11) * 0.5f, f = (m00 - m11) * 0.5f, g = (m10 + m01) * 0.5f, h = (m10 - m01) * 0.5f;
            float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + g * g);
            float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(h, e);
            float theta = (a2 - a1) * 0.5f, phi = (a2 + a1) * 0.5f;
            s.root.position = new Vector3(basePos.x, basePos.y, 0f);
            s.root.localRotation = Quaternion.Euler(0f, 0f, phi * Mathf.Rad2Deg);
            s.root.localScale = new Vector3(q + r, q - r, 1f);
            s.skew.localRotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
        }

        void OnDestroy()
        {
            foreach (var kv in shadows)
                if (kv.Value.root != null)
                    Destroy(kv.Value.root.gameObject);
            shadows.Clear();
            foreach (var kv in wispShadows)
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            wispShadows.Clear();
            foreach (var kv in familiarShadows)
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            familiarShadows.Clear();
            if (blob != null)
                Destroy(blob.texture);
        }
    }
}
