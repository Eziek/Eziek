using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.UI;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Familiars
{
    /// <summary>
    /// Chowaniec na wyspie (bez kolizji, nie zajmuje kafelka). Każdy żyje po swojemu:
    /// kot chodzi po lądzie i siada (w nocy czasem śpi), kruk przelatuje między drzewami, ropucha skacze przy grządkach,
    /// nietoperz w nocy krąży wokół lamp, a w dzień wisi pod koroną drzewa. Kursor obok: reakcja i dymek z bonusem.
    /// </summary>
    public class Familiar : MonoBehaviour
    {
        const float Ppu = 16f;

        string kind;
        SpriteRenderer body, glow;
        Sprite[] frames, glows;   // kolejność klatek jak w FamiliarArt (kot: sit0 sit1 walk0 walk1 sleep, ...)
        bool flying;
        bool hovered;
        float reactUntil;
        readonly object tipOwner = new object();

        public string Kind => kind;

        public void Setup(string familiarKind, SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowFrames)
        {
            kind = familiarKind;
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glows = glowFrames;
            SetFrame(0);
            StartCoroutine(Life());
        }

        void SetFrame(int i)
        {
            if (frames == null || frames.Length == 0)
                return;
            i = Mathf.Clamp(i, 0, frames.Length - 1);
            body.sprite = frames[i];
            if (glow != null)
            {
                var g = glows != null && i < glows.Length ? glows[i] : null;
                glow.sprite = g;
                glow.enabled = g != null;
            }
        }

        void LateUpdate()
        {
            // kolejność rysowania z wysokości na ekranie (jak obiekty na kafelkach); w locie ponad wszystkim
            int order = flying ? 4600 : Mathf.RoundToInt(Island.WorldToPixel(transform.position).y) + 4;
            body.sortingOrder = order;
            if (glow != null) glow.sortingOrder = order + 1;
            UpdateHover();
        }

        void UpdateHover()
        {
            var cam = Camera.main;
            var ui = UiRoot.Instance;
            if (cam == null || ui == null || Save.SaveSystem.InMenu)
                return;
            Vector2 mouse = cam.ScreenToWorldPoint(UiInput.MousePosition);
            bool over = !ui.PointerOverUi && ((Vector2)transform.position + new Vector2(0f, 0.3f) - mouse).sqrMagnitude < 0.45f * 0.45f;
            if (over && !hovered)
            {
                ui.Tooltip.Show(tipOwner, Loc.Get("familiar." + kind) + "\n" + Loc.Get("familiar." + kind + ".bonus"));
                if (Time.unscaledTime > reactUntil)
                {
                    reactUntil = Time.unscaledTime + 3f;
                    IslandFx.FindPopped(transform.position + new Vector3(0f, 0.4f, 0f));   // mruknięcie, krakanie: iskierki
                }
            }
            else if (!over && hovered)
                ui.Tooltip.Hide(tipOwner);
            hovered = over;
        }

        void OnDisable()
        {
            if (hovered && UiRoot.Instance != null)
                UiRoot.Instance.Tooltip.Hide(tipOwner);
            hovered = false;
        }

        // ------------------------------------------------------------ zachowanie

        IEnumerator Life()
        {
            switch (kind)
            {
                case FamiliarSystem.Cat: return CatLife();
                case FamiliarSystem.Raven: return RavenLife();
                case FamiliarSystem.Toad: return ToadLife();
                default: return BatLife();
            }
        }

        static bool Night => GameLoop.Instance != null && GameLoop.Instance.Clock.IsNight;

        /// <summary>Losowe miejsce na lądzie (podstawa kafelka, z przesunięciem w poziomie).</summary>
        static Vector3 RandomLand()
        {
            var island = Island.Instance;
            if (island == null || !island.Ready)
                return Vector3.zero;
            var tiles = new List<TilePos>(island.Map.Tiles.Keys);
            var t = tiles[Random.Range(0, tiles.Count)];
            return Island.TileAnchor(t, Random.Range(9, 15), out _) + new Vector3(Random.Range(-5, 6) / Ppu, 0f, 0f);
        }

        IEnumerator CatLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                // spacer do losowego miejsca (łapki na przemian), potem siedzenie z machaniem ogonem albo drzemka w nocy
                var to = RandomLand();
                body.flipX = to.x < transform.position.x;
                float dist = Vector3.Distance(transform.position, to);
                var from = transform.position;
                for (float t = 0f; t < dist / 1.1f; t += Time.unscaledDeltaTime)
                {
                    transform.position = Vector3.Lerp(from, to, t / (dist / 1.1f));
                    SetFrame(2 + Mathf.FloorToInt(Time.unscaledTime * 6f) % 2);
                    yield return null;
                }
                transform.position = Snap(to);
                bool sleep = Night && Random.value < 0.4f;
                float stay = sleep ? Random.Range(15f, 30f) : Random.Range(4f, 12f);
                for (float t = 0f; t < stay; t += Time.unscaledDeltaTime)
                {
                    SetFrame(sleep ? 4 : (Mathf.FloorToInt(Time.unscaledTime * 1.5f) % 4 == 0 ? 1 : 0));
                    yield return null;
                }
            }
        }

        /// <summary>Gdzie może usiąść kruk: korony drzew (owocowych i dzikich), tablica, lampy.</summary>
        static List<Vector3> Perches()
        {
            var list = new List<Vector3>();
            foreach (var t in FruitTree.All)
            {
                var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
                if (t != null && t.Adult && sr != null)
                    list.Add(new Vector3(sr.bounds.center.x + Random.Range(-0.3f, 0.3f), sr.bounds.max.y - 0.45f, 0f));
            }
            var island = Island.Instance;
            if (island != null)
            {
                foreach (var tr in island.Trees)
                {
                    var sr = tr != null ? tr.GetComponent<SpriteRenderer>() : null;
                    if (sr != null)
                        list.Add(new Vector3(sr.bounds.center.x, sr.bounds.max.y - 0.5f, 0f));
                }
                var board = island.Board;
                if (board != null)
                    list.Add(board.transform.position + new Vector3(0f, 1.1f, 0f));
            }
            foreach (var l in Lamp.All)
                if (l != null)
                    list.Add(l.transform.position + new Vector3(0f, 1.4f, 0f));
            return list;
        }

        IEnumerator RavenLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                var perches = Perches();
                var to = perches.Count > 0 ? perches[Random.Range(0, perches.Count)] : RandomLand();
                flying = true;
                body.flipX = to.x < transform.position.x;
                var from = transform.position;
                float dur = Mathf.Max(0.5f, Vector3.Distance(from, to) / 3f);
                for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
                {
                    float u = t / dur, e = u * u * (3f - 2f * u);
                    transform.position = Vector3.Lerp(from, to, e) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.6f, 0f);
                    SetFrame(2 + Mathf.FloorToInt(Time.unscaledTime * 10f) % 2);
                    yield return null;
                }
                transform.position = Snap(to);
                flying = false;
                float stay = Random.Range(8f, 20f);
                float peck = Random.Range(1f, 3f);
                for (float t = 0f; t < stay; t += Time.unscaledDeltaTime)
                {
                    peck -= Time.unscaledDeltaTime;
                    SetFrame(peck < 0f && peck > -0.25f ? 1 : 0);
                    if (peck < -0.25f) peck = Random.Range(1f, 3f);
                    yield return null;
                }
            }
        }

        IEnumerator ToadLife()
        {
            transform.position = RandomLand();
            while (true)
            {
                // cel: grządka (wilgotna ziemia) albo losowe miejsce, droga małymi skokami
                Vector3 to;
                var plots = FarmPlot.All;
                to = plots.Count > 0 ? plots[Random.Range(0, plots.Count)].transform.position + new Vector3(Random.Range(-6, 7) / Ppu, -6f / Ppu, 0f)
                                     : RandomLand();
                int hops = 0;
                while ((to - transform.position).sqrMagnitude > 0.05f && hops++ < 12)
                {
                    var from = transform.position;
                    var step = Vector3.ClampMagnitude(to - from, 0.35f);
                    body.flipX = step.x < 0f;
                    for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                    {
                        float u = t / 0.3f;
                        transform.position = from + step * u + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.18f, 0f);
                        SetFrame(1);
                        yield return null;
                    }
                    transform.position = Snap(from + step);
                    SetFrame(0);
                    yield return new WaitForSecondsRealtime(Random.Range(0.4f, 1.2f));
                }
                yield return new WaitForSecondsRealtime(Random.Range(4f, 10f));
            }
        }

        IEnumerator BatLife()
        {
            transform.position = RandomLand() + new Vector3(0f, 1f, 0f);
            while (true)
            {
                if (Night && Lamp.All.Count > 0)
                {
                    // noc: kręgi wokół lampy (owady w świetle)
                    var lamp = Lamp.All[Random.Range(0, Lamp.All.Count)];
                    var center = lamp.transform.position + new Vector3(0f, 1.3f, 0f);
                    flying = true;
                    float phase = Random.value * 6.28f;
                    float circle = Random.Range(10f, 20f);
                    for (float t = 0f; t < circle && Night && lamp != null; t += Time.unscaledDeltaTime)
                    {
                        float a = phase + t * 2.2f;
                        var target = center + new Vector3(Mathf.Cos(a) * 1.1f, Mathf.Sin(a * 2f) * 0.35f, 0f);
                        transform.position = Vector3.Lerp(transform.position, target, Mathf.Min(1f, Time.unscaledDeltaTime * 4f));
                        body.flipX = Mathf.Sin(a) > 0f;
                        SetFrame(Mathf.FloorToInt(Time.unscaledTime * 10f) % 2);
                        yield return null;
                    }
                    continue;
                }
                // dzień: wisi pod koroną drzewa (albo pod tablicą / lampą)
                var perches = Perches();
                var spot = perches.Count > 0 ? perches[Random.Range(0, perches.Count)] - new Vector3(0f, 0.55f, 0f) : RandomLand();
                flying = true;
                var from = transform.position;
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
                {
                    transform.position = Vector3.Lerp(from, spot, t / 1.2f);
                    SetFrame(Mathf.FloorToInt(Time.unscaledTime * 10f) % 2);
                    yield return null;
                }
                transform.position = Snap(spot);
                flying = false;
                SetFrame(2);
                while (!Night || Lamp.All.Count == 0)
                    yield return new WaitForSecondsRealtime(2f);
            }
        }

        static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
    }
}
