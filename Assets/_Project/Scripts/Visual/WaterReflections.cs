using System.Collections.Generic;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Familiars;
using Wyspa.Farming;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Odbicia w wodzie (GDD §9 p. 114). Każdy obiekt przy brzegu (drzewa, budowle, lampy, monumenty, pomost, łódź,
    /// rośliny) i każdy ognik czy chowaniec dostaje odwróconą kopię swoich sprite'ów pod linią brzegu: oś lustra to pierwszy
    /// piksel wody pod podstawą obiektu (do MaxScan pikseli niżej), więc obiekt daleko od wody nie ma odbicia.
    /// Kopie rysuje Wyspa/WaterReflection (widać je tylko nad wodą, falują, słabną w głąb). Piksele emisyjne (lampy,
    /// okna, oczy) mają odbicie bez oświetlenia, więc nocą świecą na wodzie. Sprite'y kopiujemy co klatkę (animacje).
    /// </summary>
    public class WaterReflections : MonoBehaviour
    {
        const int Order = -1985;          // nad wodą (-2000), pod ozdobnikami na wodzie (-1960) i lądem (-1900)
        const int MaxScan = 28;           // tyle pikseli pod podstawą szukamy wody
        const float ScanEvery = 0.5f;
        const float Strength = 0.5f, GlowStrength = 0.65f;
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int UnlitId = Shader.PropertyToID("_Unlit");

        class Mirror
        {
            public Transform root;                                   // obiekt (podstawa = jego pozycja)
            public bool moving;                                      // ognik, chowaniec: oś liczona co klatkę
            public float axis;                                       // y osi w świecie
            public bool hasAxis;
            public readonly List<(SpriteRenderer src, SpriteRenderer copy)> parts = new List<(SpriteRenderer, SpriteRenderer)>();
        }

        Material material;
        Island island;
        readonly Dictionary<Transform, Mirror> mirrors = new Dictionary<Transform, Mirror>();
        readonly HashSet<Transform> seen = new HashSet<Transform>();
        readonly List<Transform> gone = new List<Transform>();
        readonly List<SpriteRenderer> buffer = new List<SpriteRenderer>();
        MaterialPropertyBlock litBlock, glowBlock;
        float scan;

        public void Setup(Island owner, Material reflection)
        {
            island = owner;
            material = reflection;
            litBlock = new MaterialPropertyBlock();
            litBlock.SetFloat(StrengthId, Strength);
            litBlock.SetFloat(UnlitId, 0f);
            glowBlock = new MaterialPropertyBlock();
            glowBlock.SetFloat(StrengthId, GlowStrength);
            glowBlock.SetFloat(UnlitId, 1f);
        }

        void LateUpdate()
        {
            if (island == null || material == null || island.Picture == null)
                return;
            scan -= Time.unscaledDeltaTime;
            if (scan <= 0f)
            {
                scan = ScanEvery;
                Scan();
            }
            foreach (var m in mirrors.Values)
                UpdateMirror(m);
        }

        void Scan()
        {
            seen.Clear();
            var root = island.DecoRoot;
            if (root != null)
                for (int i = 0; i < root.childCount; i++)
                    Track(root.GetChild(i), false);
            foreach (var w in Wisp.All)
                if (w != null) Track(w.transform, true);
            foreach (var f in Familiar.All)
                if (f != null) Track(f.transform, true);
            foreach (var p in FarmPlot.All)
            {
                var crop = p != null ? p.CropRenderer : null;
                if (crop != null) Track(crop.transform, false);
            }
            gone.Clear();
            foreach (var kv in mirrors)
                if (kv.Key == null || !seen.Contains(kv.Key))
                    gone.Add(kv.Key);
            foreach (var t in gone)
            {
                foreach (var part in mirrors[t].parts)
                    if (part.copy != null) Destroy(part.copy.gameObject);
                mirrors.Remove(t);
            }
            // statyczne obiekty: oś odświeżana co skan (np. przeniesione, nowy kafelek zmienił brzeg)
            foreach (var m in mirrors.Values)
                if (!m.moving)
                    m.hasAxis = FindAxis(m.root.position, out m.axis);
        }

        void Track(Transform t, bool moving)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return;
            seen.Add(t);
            if (!mirrors.TryGetValue(t, out var m))
            {
                m = new Mirror { root = t, moving = moving };
                m.hasAxis = FindAxis(t.position, out m.axis);
                mirrors[t] = m;
            }
            // nowe sprite'y w obiekcie (np. poświata dodana później, czapka ognika): dopinamy kopie
            buffer.Clear();
            t.GetComponentsInChildren(true, buffer);
            foreach (var src in buffer)
            {
                bool known = false;
                foreach (var part in m.parts)
                    if (part.src == src) { known = true; break; }
                if (known)
                    continue;
                var go = new GameObject("Reflection");
                go.transform.SetParent(transform, false);
                var copy = go.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = material;
                bool glow = IsGlow(src);
                copy.SetPropertyBlock(glow ? glowBlock : litBlock);
                copy.sortingOrder = Order + (glow ? 1 : 0);
                copy.enabled = false;
                m.parts.Add((src, copy));
            }
        }

        bool IsGlow(SpriteRenderer src)
        {
            var art = island.Art;
            var mat = src.sharedMaterial;
            return art != null && mat != null && (mat == art.glow || mat == art.swayTreeGlow || mat == art.swayCropGlow);
        }

        /// <summary>Oś lustra: pierwszy piksel wody pod podstawą (piksele obrazu wyspy, y w dół).</summary>
        bool FindAxis(Vector3 world, out float axis)
        {
            axis = 0f;
            var pic = island.Picture;
            var p = Island.WorldToPixel(world);
            int x = Mathf.RoundToInt(p.x), y0 = Mathf.FloorToInt(p.y);
            for (int d = 0; d <= MaxScan; d++)
                if (pic.ShoreDistance(x, y0 + d) > 0f)
                {
                    axis = Island.PixelToWorld(x, y0 + d).y;
                    return true;
                }
            return false;
        }

        void UpdateMirror(Mirror m)
        {
            if (m.root == null)
                return;
            if (m.moving)
                m.hasAxis = FindAxis(m.root.position, out m.axis);
            for (int i = m.parts.Count - 1; i >= 0; i--)
            {
                var (src, copy) = m.parts[i];
                if (copy == null)
                {
                    m.parts.RemoveAt(i);
                    continue;
                }
                bool show = m.hasAxis && src != null && src.enabled && src.sprite != null && src.gameObject.activeInHierarchy;
                copy.enabled = show;
                if (!show)
                    continue;
                copy.sprite = src.sprite;
                copy.flipX = src.flipX;
                copy.flipY = !src.flipY;   // lustro: pivot sprite'a zostaje na odbitym punkcie, obraz rośnie w dół
                copy.color = src.color;
                var p = src.transform.position;
                copy.transform.position = new Vector3(p.x, 2f * m.axis - p.y, 0f);
                copy.transform.localScale = src.transform.lossyScale;
            }
        }
    }
}
