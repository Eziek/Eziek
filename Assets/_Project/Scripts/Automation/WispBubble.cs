using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Automation
{
    /// <summary>
    /// Runy w dymkach ogników: znaki 3x5 px (futhark, „?”, „!”, wielokropek, nutka), dymek liczony z kodu
    /// (ciemne wnętrze, śliwkowa obwódka, ogonek). Każda runa ma swoją sylabę „mowy ogników” (Resources/Audio/sfx_babble_*),
    /// więc ten sam znak zawsze brzmi tak samo. Bez tekstu: to nic nie znaczy w żadnym języku.
    /// </summary>
    public static class RuneGlyphs
    {
        const float Ppu = 16f;
        public const int RuneCount = 17;
        public const int Question = 17, Exclaim = 18, Dots = 19, Note = 20, Heart = 21, Sleep = 22, Star = 23;
        public const int SyllableCount = 12;

        static readonly string[][] Shapes =
        {
            new[] { "X.X", "XX.", "X.X", "XX.", "X.." },
            new[] { "XX.", "X.X", "X.X", "X.X", "X.X" },
            new[] { "X..", "XX.", "X.X", "XX.", "X.." },
            new[] { "X..", "XX.", "X.X", "XX.", "X.X" },
            new[] { "XX.", "X.X", "XX.", "X.X", "X.X" },
            new[] { "..X", ".X.", "X..", ".X.", "..X" },
            new[] { "X.X", "X.X", ".X.", "X.X", "X.X" },
            new[] { "XX.", "X.X", "XX.", "X..", "X.." },
            new[] { "X.X", "X.X", "XXX", "X.X", "X.X" },
            new[] { ".X.", ".XX", "XX.", ".X.", ".X." },
            new[] { "X", "X", "X", "X", "X" },
            new[] { ".XX", ".X.", ".X.", ".X.", "XX." },
            new[] { "X.X", "X.X", ".X.", ".X.", ".X." },
            new[] { "X..", "XX.", ".X.", ".XX", "..X" },
            new[] { ".X.", "XXX", "X.X", ".X.", ".X." },
            new[] { ".X.", "X.X", ".X.", "X.X", "X.X" },
            new[] { "X.X", "XXX", "X.X", "XXX", "X.X" },
            new[] { "XX.", "..X", ".X.", "...", ".X." },            // ?
            new[] { "X", "X", "X", ".", "X" },                      // !
            new[] { ".....", ".....", ".....", ".....", "X.X.X" },  // ...
            new[] { ".XX", ".X.", ".X.", "XX.", "XX." },            // nutka (nucenie, śmiech)
            new[] { ".X.X.", "XXXXX", "XXXXX", ".XXX.", "..X.." },  // serce
            new[] { "XXX", "..X", ".X.", "X..", "XXX" },            // Z (drzemka)
            new[] { "..X..", ".XXX.", "XXXXX", ".X.X.", "X...X" },  // gwiazdka (wygrana, gratulacje)
        };

        static readonly Color32 Outline = new Color32(0x0f, 0x0a, 0x10, 0xff);
        static readonly Color32 Rim = new Color32(0x56, 0x34, 0x47, 0xff);
        static readonly Color32 Fill = new Color32(0x1c, 0x15, 0x20, 0xeb);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static readonly Dictionary<int, Sprite> glyphs = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> bubbles = new Dictionary<int, Sprite>();

        public static int Width(int glyph) => Shapes[Mathf.Clamp(glyph, 0, Shapes.Length - 1)][0].Length;

        /// <summary>Dźwięk znaku: runy dzielą 12 sylab, znaki specjalne mają własne.</summary>
        public static string Sound(int glyph)
        {
            switch (glyph)
            {
                case Question: return "babble_q";
                case Exclaim: return "babble_ex";
                case Dots: return "babble_hum";
                case Note: return "babble_laugh";
                case Heart: return "babble_laugh";
                case Sleep: return "babble_hum";
                case Star: return "babble_ex";
                default: return "babble_" + (Mathf.Abs(glyph) % SyllableCount);
            }
        }

        /// <summary>Znak jako biały sprite (kolor nadaje SpriteRenderer), punkt zaczepienia w lewym górnym rogu.</summary>
        public static Sprite Glyph(int glyph)
        {
            glyph = Mathf.Clamp(glyph, 0, Shapes.Length - 1);
            if (glyphs.TryGetValue(glyph, out var cached) && cached != null)
                return cached;
            var shape = Shapes[glyph];
            int w = shape[0].Length, h = shape.Length;
            var tex = NewTexture(w, h, "Rune" + glyph);
            var px = new Color32[w * h];
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                    px[(h - 1 - r) * w + x] = shape[r][x] == 'X' ? new Color32(255, 255, 255, 255) : Clear;
            tex.SetPixels32(px);
            tex.Apply(false);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 1f), Ppu, 0, SpriteMeshType.FullRect);
            glyphs[glyph] = s;
            return s;
        }

        /// <summary>Wysokość dymku w pikselach (z ogonkiem) i górna krawędź znaków od dołu dymku.</summary>
        public const int BubbleHeight = 13, GlyphTop = 10, GlyphLeft = 3;

        /// <summary>Szerokość dymku dla treści o danej szerokości (piksele znaków z odstępami).</summary>
        public static int BubbleWidth(int content) => content + 6;

        /// <summary>
        /// Dymek: obrys, śliwkowa obwódka, ciemne wnętrze, ścięte rogi, ogonek w dół przy środku.
        /// Punkt zaczepienia na dole, na granicy pikseli (szerokość jest nieparzysta).
        /// </summary>
        public static Sprite Bubble(int content)
        {
            int W = BubbleWidth(Mathf.Max(1, content)), H = BubbleHeight;
            if (bubbles.TryGetValue(W, out var cached) && cached != null)
                return cached;
            var tex = NewTexture(W, H, "Bubble" + W);
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++)
                px[i] = Clear;
            void Put(int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c; }
            for (int y = 2; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool edge = x == 0 || x == W - 1 || y == 2 || y == H - 1;
                    bool rim = x == 1 || x == W - 2 || y == 3 || y == H - 2;
                    Put(x, y, edge ? Outline : rim ? Rim : Fill);
                }
            Put(0, 2, Clear); Put(W - 1, 2, Clear); Put(0, H - 1, Clear); Put(W - 1, H - 1, Clear);
            Put(1, 3, Outline); Put(W - 2, 3, Outline); Put(1, H - 2, Outline); Put(W - 2, H - 2, Outline);
            int c = Mathf.Max(2, (W - 1) / 2 - 1);   // ogonek trochę w lewo od środka, skośnie w dół
            Put(c, 3, Fill); Put(c, 2, Rim); Put(c + 1, 2, Outline);
            Put(c - 1, 1, Outline); Put(c, 1, Rim); Put(c + 1, 1, Outline);
            Put(c - 1, 0, Outline);
            tex.SetPixels32(px);
            tex.Apply(false);
            var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(((W - 1) / 2) / (float)W, 0f), Ppu, 0, SpriteMeshType.FullRect);
            bubbles[W] = s;
            return s;
        }

        static Texture2D NewTexture(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
    }

    /// <summary>
    /// Dymek nad ognikiem: runy pojawiają się po kolei, każda z sylabą „mowy ogników”, potem dymek chwilę wisi i znika.
    /// Siedzi na osobnym obiekcie z odwróconą skalą ognika, więc piksele dymku zostają równe przy każdym poziomie.
    /// </summary>
    public class WispBubble : MonoBehaviour
    {
        const float Ppu = 16f;
        const float RuneStep = 0.15f;    // sylaba co tyle sekund
        const float Hold = 1.0f;
        const float Fade = 0.25f;
        const int Order = 4510;          // nad ognikami (4400-4499), pod ptakami (4600)

        Transform anchor;
        float headHeight;

        /// <summary>Wysokość dymku nad punktem ognika (wyżej, gdy ma czapkę).</summary>
        public float HeadHeight { get => headHeight; set => headHeight = value; }
        SpriteRenderer back;
        readonly List<SpriteRenderer> runes = new List<SpriteRenderer>();
        Coroutine running;

        public bool Showing => running != null;

        public static WispBubble Create(Transform owner, Transform follow, float head)
        {
            var go = new GameObject("Bubble");
            go.transform.SetParent(owner, false);
            var b = go.AddComponent<WispBubble>();
            b.anchor = follow != null ? follow : owner;
            b.headHeight = head;
            b.back = b.NewRenderer("Back", Order);
            b.back.enabled = false;
            return b;
        }

        SpriteRenderer NewRenderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            var art = Island.Instance != null ? Island.Instance.Art : null;
            if (art != null && art.glow != null)
                sr.sharedMaterial = art.glow;   // bez oświetlenia: dymek czytelny także w nocy
            return sr;
        }

        /// <summary>Pokazuje zdanie z run. Zwraca, ile potrwa (do zniknięcia). volume 0 = bez dźwięku.</summary>
        public float Say(IList<int> glyphs, Color runeColor, float pitch, float volume)
        {
            Hide();
            running = StartCoroutine(Show(new List<int>(glyphs), runeColor, pitch, volume));
            return glyphs.Count * RuneStep + Hold + Fade;
        }

        public void Hide()
        {
            if (running != null)
                StopCoroutine(running);
            running = null;
            if (back != null) back.enabled = false;
            foreach (var r in runes)
                if (r != null) r.enabled = false;
        }

        IEnumerator Show(List<int> glyphs, Color runeColor, float pitch, float volume)
        {
            int content = -1;
            foreach (var g in glyphs)
                content += RuneGlyphs.Width(g) + 1;
            int W = RuneGlyphs.BubbleWidth(content);
            back.sprite = RuneGlyphs.Bubble(content);
            back.color = Color.white;
            back.enabled = true;
            while (runes.Count < glyphs.Count)
                runes.Add(NewRenderer("Rune", Order + 1));
            int left = -((W - 1) / 2) + RuneGlyphs.GlyphLeft, x = left;
            for (int i = 0; i < runes.Count; i++)
            {
                runes[i].enabled = false;
                if (i >= glyphs.Count)
                    continue;
                runes[i].sprite = RuneGlyphs.Glyph(glyphs[i]);
                runes[i].transform.localPosition = new Vector3(x / Ppu, RuneGlyphs.GlyphTop / Ppu, 0f);
                x += RuneGlyphs.Width(glyphs[i]) + 1;
            }
            // runa po runie: rozbłysk bieli, potem kolor ognika
            for (int i = 0; i < glyphs.Count; i++)
            {
                runes[i].enabled = true;
                if (volume > 0f)
                    GameEvents.PlayVoice(RuneGlyphs.Sound(glyphs[i]), volume, pitch * Random.Range(0.97f, 1.03f));
                for (float t = 0f; t < RuneStep; t += Time.unscaledDeltaTime)
                {
                    float u = Mathf.Clamp01(t / (RuneStep * 0.7f));
                    runes[i].color = Color.Lerp(Color.white, runeColor, u);
                    yield return null;
                }
                runes[i].color = runeColor;
            }
            yield return new WaitForSecondsRealtime(Hold);
            for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime)
            {
                float a = 1f - t / Fade;
                back.color = new Color(1f, 1f, 1f, a);
                for (int i = 0; i < glyphs.Count; i++)
                    runes[i].color = new Color(runeColor.r, runeColor.g, runeColor.b, runeColor.a * a);
                yield return null;
            }
            running = null;
            Hide();
        }

        void LateUpdate()
        {
            if (anchor == null || !back.enabled)
                return;
            // nad głową ognika, przyciągnięte do siatki pikseli sprite'ów
            var p = anchor.position + new Vector3(0f, headHeight, 0f);
            transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, p.z);
            var parent = transform.parent;
            float s = parent != null ? parent.lossyScale.x : 1f;
            transform.localScale = s > 0.01f ? new Vector3(1f / s, 1f / s, 1f) : Vector3.one;
        }

        void OnDisable() => Hide();
    }
}
