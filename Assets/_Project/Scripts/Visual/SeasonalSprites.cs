using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Pory roku na sprite'ach (GDD §9 p. 127): każdy sprite świata, który ma warianty (IslandArt.seasonal, generowane przez
    /// tools/pixel-proto/export_unity_seasons.py), jest podmieniany na wariant bieżącej pory: drzewa i krzaki (wiosną kwiaty,
    /// jesienią rude, zimą ze śniegiem), zimą śnieg na budowlach, lampach, skrzyniach i kamieniach. Działa niezależnie od
    /// tego, kto ustawił sprite (fazy wzrostu, animacje, ścinanie): co klatkę po Update zamienia sprite z zestawu na
    /// wersję pory. Uruchamia się przed odbiciami i cieniami (kopiują już wersję pory). Lista rendererów odświeżana co sekundę.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class SeasonalSprites : MonoBehaviour
    {
        const float ScanEvery = 1f;

        readonly Dictionary<Sprite, SeasonalSet> sets = new Dictionary<Sprite, SeasonalSet>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> buffer = new List<SpriteRenderer>();
        readonly List<GameObject> roots = new List<GameObject>();
        float scan;

        public void Setup(IslandArt art)
        {
            if (art == null || art.seasonal == null)
                return;
            foreach (var set in art.seasonal)
            {
                if (set == null || set.summer == null)
                    continue;
                // każdy wariant prowadzi do całego zestawu (sprite mógł już zostać podmieniony w poprzedniej klatce)
                foreach (var s in new[] { set.summer, set.spring, set.autumn, set.winter })
                    if (s != null)
                        sets[s] = set;
            }
        }

        void LateUpdate()
        {
            if (sets.Count == 0)
                return;
            scan -= Time.unscaledDeltaTime;
            if (scan <= 0f)
            {
                // wszystkie korzenie sceny: wyspa (dekoracje, budowle, drzewa) i obiekty ze sceny (TileObjects: skrzynia, tablica)
                scan = ScanEvery;
                renderers.Clear();
                gameObject.scene.GetRootGameObjects(roots);
                foreach (var r in roots)
                {
                    r.GetComponentsInChildren(true, buffer);
                    renderers.AddRange(buffer);
                }
            }
            var season = Seasons.Current;
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                var sprite = r.sprite;
                if (sprite == null || !sets.TryGetValue(sprite, out var set))
                    continue;
                var want = set.For(season);
                if (want != null && want != sprite)
                    r.sprite = want;
            }
        }
    }
}
