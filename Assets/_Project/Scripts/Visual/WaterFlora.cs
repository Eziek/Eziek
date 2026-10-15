using System.Collections.Generic;
using UnityEngine;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Ozdobniki na wodzie (GDD §9 p. 105): liście lilii, kwitnące lilie, opadłe liście, rzęsa i gałązka unoszą się przy brzegu
    /// wyspy (i wysepek) i powoli dryfują po małej elipsie, każdy we własnym rytmie. Mglista lilia nocą lekko świeci.
    /// Liczba rośnie z wyspą. Po przemalowaniu wyspy (zakup kafelka) ozdobniki, na które wszedł ląd albo pomost,
    /// odpływają w inne miejsce, reszta zostaje. Grafiki: IslandArt.waterFlora (tools/pixel-proto/water_flora.py).
    /// </summary>
    public class WaterFlora : MonoBehaviour
    {
        const float Ppu = 16f;
        const int Order = -1960;            // nad wodą (-2000), pod lądem (-1900), mgłą i łodziami
        const float MinShore = 8f;          // piksele od brzegu (mapa odległości wody)
        const float MaxShore = 30f;         // dalej woda ginie w mgle (i w przezroczystym oknie pas wody ma 46 px)
        const float Spacing = 14f;          // minimalny odstęp między ozdobnikami (piksele)
        static readonly Color MistGlow = new Color(0.8f, 0.6f, 1f);
        // waga losowania na indeks w IslandArt.waterFlora: liście lilii częściej, kwiaty i gałązka rzadko
        static readonly int[] Weights = { 10, 7, 6, 3, 2, 4, 3, 4, 3, 1 };
        const int MistIndex = 4;

        class Item
        {
            public Transform t;
            public Vector2 home;            // piksele obrazu wyspy (y w dół)
            public float phase, period, rx, ry;
            public Vector2 kick;            // odepchnięcie (klik w wodę), wygasa w KickSeconds
            public float kickAt = -100f;
        }

        readonly List<Item> items = new List<Item>();
        Island island;

        public void Setup(Island owner)
        {
            island = owner;
            if (isActiveAndEnabled)
                island.Rebuilt += Relocate;
            Relocate();
        }

        void OnEnable()
        {
            if (island != null)
                island.Rebuilt += Relocate;
        }

        void OnDisable()
        {
            if (island != null)
                island.Rebuilt -= Relocate;
        }

        bool Valid(IslandPicture pic, Vector2 px)
        {
            int x = Mathf.RoundToInt(px.x), y = Mathf.RoundToInt(px.y);
            // środek i boki ozdobnika (z zapasem na dryf) muszą być w wodzie przy brzegu
            float d = pic.ShoreDistance(x, y);
            if (d < MinShore || d > MaxShore || pic.ShoreDistance(x - 7, y) < MinShore - 3f || pic.ShoreDistance(x + 7, y) < MinShore - 3f)
                return false;
            var world = (Vector2)Island.PixelToWorld(px.x, px.y);
            if (island.SeaBlocked(Island.WorldToTile(world)))
                return false;
            var pier = island.PierObject;
            if (pier != null && (pier.Tip - world).sqrMagnitude < 2.2f * 2.2f)
                return false;   // pomost, łódź i sieci
            foreach (var it in items)
                if (it.t != null && (it.home - px).sqrMagnitude < Spacing * Spacing)
                    return false;
            return true;
        }

        void Relocate()
        {
            var art = island != null ? island.Art : null;
            var pic = island != null ? island.Picture : null;
            if (pic == null || art == null || art.waterFlora == null || art.waterFlora.Length == 0)
                return;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                items.RemoveAt(i);   // bez siebie samego przy sprawdzaniu odstępu
                if (it.t != null && Valid(pic, it.home))
                    items.Insert(i, it);
                else if (it.t != null)
                    Destroy(it.t.gameObject);
            }
            int want = Mathf.Clamp(5 + island.Map.Count / 4, 5, 18);
            int x0 = Mathf.Max(8, pic.minX - (int)MaxShore), x1 = Mathf.Min(pic.width - 8, pic.maxX + (int)MaxShore);
            int y0 = Mathf.Max(8, pic.minY - (int)MaxShore), y1 = Mathf.Min(pic.height - 8, pic.maxY + (int)MaxShore);
            for (int tries = 0; tries < 400 && items.Count < want; tries++)
            {
                var px = new Vector2(Random.Range(x0, x1), Random.Range(y0, y1));
                if (Valid(pic, px))
                    items.Add(Spawn(art, px));
            }
        }

        Item Spawn(IslandArt art, Vector2 px)
        {
            int total = 0;
            for (int i = 0; i < art.waterFlora.Length; i++)
                total += i < Weights.Length ? Weights[i] : 1;
            int pick = 0, roll = Random.Range(0, total);
            for (int i = 0; i < art.waterFlora.Length; i++)
            {
                roll -= i < Weights.Length ? Weights[i] : 1;
                if (roll < 0) { pick = i; break; }
            }
            var go = new GameObject("Flora");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.waterFlora[pick];
            sr.sortingOrder = Order;
            sr.flipX = Random.value < 0.5f;
            var glowSprite = art.waterFloraGlow != null && pick < art.waterFloraGlow.Length ? art.waterFloraGlow[pick] : null;
            if (glowSprite != null)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(go.transform, false);
                var gsr = g.AddComponent<SpriteRenderer>();
                gsr.sprite = glowSprite;
                gsr.flipX = sr.flipX;
                gsr.sortingOrder = Order + 1;
                if (art.glow != null)
                    gsr.sharedMaterial = art.glow;
            }
            if (pick == MistIndex)
                NightGlow.Add(go, new Vector2(0f, 2f / Ppu), MistGlow, 0.9f, 0.35f);
            var it = new Item
            {
                t = go.transform,
                home = px,
                phase = Random.Range(0f, Mathf.PI * 2f),
                period = Random.Range(24f, 46f),
                rx = Random.Range(1.5f, 3.5f),
                ry = Random.Range(0.6f, 1.4f),
            };
            Place(it, Time.unscaledTime);
            return it;
        }

        const float KickSeconds = 2.5f;

        /// <summary>Klik w wodę: ozdobniki w promieniu odpływają od miejsca kliknięcia i wolno wracają.</summary>
        public static void Nudge(Vector2 world, float radius)
        {
            foreach (var f in All)
                f.NudgeHere(world, radius);
        }

        static readonly List<WaterFlora> All = new List<WaterFlora>();
        void Awake() => All.Add(this);
        void OnDestroy() => All.Remove(this);

        void NudgeHere(Vector2 world, float radius)
        {
            var c = Island.WorldToPixel(world);
            float r = radius * Ppu;
            foreach (var it in items)
            {
                if (it.t == null)
                    continue;
                var p = Island.WorldToPixel(it.t.position);
                var d = p - c;
                float dist = d.magnitude;
                if (dist > r)
                    continue;
                it.kick = (dist > 0.01f ? d / dist : Vector2.right) * 7f * (1f - dist / r);
                it.kickAt = Time.unscaledTime;
            }
        }

        static void Place(Item it, float time)
        {
            float a = time * Mathf.PI * 2f / it.period + it.phase;
            var px = it.home + new Vector2(Mathf.Sin(a) * it.rx, Mathf.Sin(a * 0.7f + it.phase) * it.ry);
            float k = (time - it.kickAt) / KickSeconds;
            if (k >= 0f && k < 1f)
                px += it.kick * Mathf.Sin(Mathf.Min(1f, k * 4f) * Mathf.PI * 0.5f) * (1f - k);   // szybko odpływa, wolno wraca
            // przyciągnięte do siatki wody (32 px na kafelek): ruch po pół piksela grafiki, bez rozmycia
            var w = Island.PixelToWorld(px.x, px.y);
            it.t.position = new Vector3(Mathf.Round(w.x * 32f) / 32f, Mathf.Round(w.y * 32f) / 32f, 0f);
        }

        void Update()
        {
            float time = Time.unscaledTime;
            foreach (var it in items)
                if (it.t != null)
                    Place(it, time);
        }
    }
}
