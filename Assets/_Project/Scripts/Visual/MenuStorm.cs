using System.Collections;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Tło menu głównego: burza nad wyspą. Co kilka sekund piorun (pikselowy, z rozgałęzieniami, losowany za każdym razem)
    /// wychodzi z chmur mgły i uderza w morze za wyspą: dwa mignięcia, rozbłysk całej sceny (WeatherSystem.Flash), piana
    /// w miejscu uderzenia, grzmot z opóźnieniem. Klik w tło (poza przyciskami menu) przywołuje piorun w to miejsce.
    /// Pogodę (deszcz, przyciemnienie) w menu wymusza WeatherSystem.
    /// </summary>
    public class MenuStorm : MonoBehaviour
    {
        const float Ppu = 16f;
        const int Order = -1950;   // między wodą (-2000) a lądem (-1900): za wyspą, chmury mgły (5000) zakrywają górę pioruna
        static readonly Color32 Core = new Color32(0xf4, 0xf0, 0xff, 0xff);
        static readonly Color32 Halo = new Color32(0x9a, 0x8c, 0xc8, 0xb0);

        float nextStrike;

        void Start() => nextStrike = Random.Range(1.5f, 3f);

        void Update()
        {
            nextStrike -= Time.unscaledDeltaTime;
            if (nextStrike > 0f)
                return;
            nextStrike = Random.Range(3.5f, 8f);
            if (TryPickSea(out var target))
                StartCoroutine(Strike(target));
        }

        /// <summary>Piorun w punkt ekranu (klik w tło menu).</summary>
        public void StrikeAtScreen(Vector2 screen)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            var p = cam.ScreenToWorldPoint(screen);
            StartCoroutine(Strike(new Vector3(p.x, p.y, 0f)));
            nextStrike = Mathf.Max(nextStrike, 2f);   // bez dwóch uderzeń naraz
        }

        /// <summary>Morze za wyspą (nad nią na ekranie), w kadrze, nie na lądzie.</summary>
        static bool TryPickSea(out Vector3 target)
        {
            target = default;
            var island = Island.Instance;
            var cam = Camera.main;
            if (island == null || !island.Ready || cam == null)
                return false;
            var land = island.LandRect;
            float camTop = cam.transform.position.y + cam.orthographicSize;
            for (int i = 0; i < 12; i++)
            {
                float x = Random.Range(land.xMin - 2f, land.xMax + 2f);
                float y = Random.Range(land.yMax + 0.3f, Mathf.Min(land.yMax + 3.5f, camTop - 1f));
                var p = new Vector3(x, y, 0f);
                if (!island.Map.Owns(Island.WorldToTile(p)))
                {
                    target = p;
                    return true;
                }
            }
            return false;
        }

        IEnumerator Strike(Vector3 target)
        {
            var cam = Camera.main;
            if (cam == null)
                yield break;
            float top = cam.transform.position.y + cam.orthographicSize + 1f;   // z chmur nad kadrem
            int heightPx = Mathf.Clamp(Mathf.RoundToInt((top - target.y) * Ppu), 16, 400);
            var sprite = Bolt(heightPx, out float tipX);
            var go = new GameObject("Lightning");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = Order;
            var island = Island.Instance;
            if (island != null && island.Art != null && island.Art.glow != null)
                sr.sharedMaterial = island.Art.glow;   // bez oświetlenia: jasny także w nocy
            // pivot na dole w środku tekstury; przesunięcie tak, by koniec pioruna trafił w cel
            go.transform.position = new Vector3(Mathf.Round((target.x - tipX / Ppu) * Ppu) / Ppu, Mathf.Round(target.y * Ppu) / Ppu, 0f);

            Weather.WeatherSystem.TriggerFlash();
            IslandFx.TileClaimed(target, island != null && island.Art != null ? island.Art.foam : null);   // piana w miejscu uderzenia
            // dwa mignięcia, potem gaśnie
            float[] on = { 0.07f, 0.05f, 0.12f };
            sr.enabled = true;
            yield return new WaitForSecondsRealtime(on[0]);
            sr.enabled = false;
            yield return new WaitForSecondsRealtime(on[1]);
            sr.enabled = true;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                sr.color = new Color(1f, 1f, 1f, 1f - t / 0.3f);
                yield return null;
            }
            Destroy(go);
            Destroy(sprite.texture);
            // grzmot dochodzi chwilę później (daleko za wyspą)
            yield return new WaitForSecondsRealtime(Random.Range(0.2f, 0.7f));
            GameEvents.PlaySound("thunder", 0.6f);
        }

        /// <summary>
        /// Piorun jako tekstura: zygzak z góry na dół (co kilka pikseli skok w bok), 1-2 odnogi, jasny rdzeń 1 px
        /// i fioletowa poświata obok. tipX = x końca pioruna w pikselach od pivota (środek dołu).
        /// </summary>
        static Sprite Bolt(int h, out float tipX)
        {
            const int w = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "Lightning" };
            var px = new Color32[w * h];
            void Put(int x, int y, Color32 c)
            {
                if (x < 0 || x >= w || y < 0 || y >= h)
                    return;
                int i = y * w + x;
                if (c.a > px[i].a)
                    px[i] = c;
            }
            void Line(int x, int yTop, int yBottom, int len, bool branch)
            {
                int y = yTop;
                for (int k = 0; k < len && y >= yBottom; k++, y--)
                {
                    if (k % Random.Range(2, 5) == 0)
                        x += Random.Range(-2, 3) + (branch ? (x < w / 2 ? -1 : 1) : 0);
                    x = Mathf.Clamp(x, 3, w - 4);
                    Put(x, y, Core);
                    Put(x - 1, y, Halo);
                    Put(x + 1, y, Halo);
                    if (!branch && k % 7 == 0)
                        Put(x + (Random.value < 0.5f ? -2 : 2), y, Halo);
                }
            }
            // główny pień: od góry (y = h-1) do dołu (y = 0); x zapamiętany co wiersz do odnóg
            int cx = w / 2 + Random.Range(-6, 7);
            var path = new int[h];
            for (int y = h - 1; y >= 0; y--)
            {
                if (y % Random.Range(2, 6) == 0)
                    cx += Random.Range(-2, 3);
                cx = Mathf.Clamp(cx, 4, w - 5);
                path[y] = cx;
                Put(cx, y, Core);
                Put(cx - 1, y, Halo);
                Put(cx + 1, y, Halo);
            }
            int branches = Random.Range(1, 3);
            for (int b = 0; b < branches; b++)
            {
                int from = Random.Range(h / 4, h * 3 / 4);
                Line(path[from], from, 0, Random.Range(h / 6, h / 3), true);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            tipX = path[0] + 0.5f - w / 2f;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
