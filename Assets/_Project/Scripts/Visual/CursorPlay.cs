using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;
using Wyspa.Input;
using Wyspa.Lighting;
using Wyspa.UI;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Zabawa kursorem (GDD §9 p. 115), same efekty, bez wpływu na grę:
    /// - kursor przechodzący przez rośliny i drzewa odgina je (shader kołysania, globalnie _WyspaCursor),
    /// - klik w wodę: dwa rozchodzące się kręgi, ryby w pobliżu dają nura, lilie i liście odpływają,
    /// - klik w drzewo albo krzak: sypią się liście, siedzące obok ptaki odlatują,
    /// - klik w zapaloną lampę nocą: z kloszy wylatuje kilka ciem.
    /// </summary>
    public class CursorPlay : MonoBehaviour
    {
        const float Ppu = 16f;
        const float RipplePpu = 32f;      // kręgi w siatce wody (32 px na kafelek)
        const int RippleOrder = -1955;    // nad ozdobnikami na wodzie, pod lądem
        static readonly int CursorId = Shader.PropertyToID("_WyspaCursor");
        static readonly Color RingColor = new Color32(0x84, 0x8e, 0x9b, 0xd0);

        readonly List<Sprite> rings = new List<Sprite>();
        const int MaxRipples = 6;         // seria klików nie zasypuje wody kręgami
        const float WaterClickGap = 0.15f;
        int ripples;
        float lastWaterClick = -1f;
        object spotted;          // ptak albo ryba, nad którą ostatnio był kursor (licznik „Widziane” raz na spotkanie)
        WaterAnimator water;
        Vector2 lastMouse;
        float strength;

        void OnEnable() => ClickRouter.Clicked += OnClicked;

        void OnDisable()
        {
            ClickRouter.Clicked -= OnClicked;
            ripples = 0;   // korutyny kręgów giną razem z komponentem
            Shader.SetGlobalVector(CursorId, Vector4.zero);
        }

        // ------------------------------------------------------------ rośliny pod kursorem

        void Update()
        {
            var cam = Camera.main;
            var mouse = UiInput.MousePosition;
            float speed = (mouse - lastMouse).magnitude / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            lastMouse = mouse;
            var ui = UiRoot.Instance;
            bool overWorld = cam != null && (ui == null || !ui.PointerOverUi);
            // ruch podbija siłę szybko, w bezruchu rośliny wolno się prostują
            float target = overWorld ? Mathf.Clamp01(speed / 500f) : 0f;
            strength = target > strength ? Mathf.MoveTowards(strength, target, Time.unscaledDeltaTime * 6f)
                                         : Mathf.MoveTowards(strength, target, Time.unscaledDeltaTime * 0.8f);
            if (cam == null)
                return;
            Vector2 world = cam.ScreenToWorldPoint(mouse);
            Shader.SetGlobalVector(CursorId, new Vector4(world.x * Ppu, world.y * Ppu, strength, 0f));
            Spot(world, overWorld);
        }

        // ------------------------------------------------------------ podpisy: ptak albo ryba pod kursorem

        /// <summary>
        /// Kursor nad ptakiem albo rybą: dymek z nazwą; pierwsze spotkanie gatunku trafia do bestiariusza („Widziane”)
        /// z toastem, każde kolejne spotkanie podbija licznik.
        /// </summary>
        void Spot(Vector2 world, bool overWorld)
        {
            var tip = UiRoot.Instance != null ? UiRoot.Instance.Tooltip : null;
            if (tip == null)
                return;
            string id = null, name = null;
            object who = null;
            if (overWorld && AmbientBirds.Instance != null)
            {
                string bird = AmbientBirds.Instance.BirdAt(world, out who);
                if (bird != null)
                {
                    id = "bird:" + bird;
                    name = Localization.Loc.Get("bird." + bird);
                }
            }
            if (id == null && overWorld)
            {
                if (water == null)
                    water = FindAnyObjectByType<WaterAnimator>();
                int i = water != null ? water.AnyFishAt(world) : -1;
                if (i >= 0)
                {
                    int kind = water.KindOf(i);
                    id = "species:" + kind;
                    name = Localization.Loc.Get("fishkind." + kind);
                    who = water.FishKey(i);
                }
            }
            if (id == null)
            {
                if (spotted != null)
                    tip.Hide(this);
                spotted = null;
                return;
            }
            tip.Show(this, name);
            if (Equals(who, spotted))
                return;
            spotted = who;
            var chronicle = Lore.Chronicle.Instance;
            if (chronicle != null && !chronicle.Found(id))
                GameEvents.RaiseToast("toast.seen.new", name);
            Lore.Chronicle.Note(id);
            Lore.Chronicle.Count("seen:" + id);
        }

        // ------------------------------------------------------------ kliknięcia

        void OnClicked(Vector2 world, Collider2D hit)
        {
            // woda: nic nie trafione albo tylko łowisko przy pomoście (obszar o niskim priorytecie)
            if (hit == null || hit.TryGetComponent(out ILowPriorityClick _))
            {
                if (IsWater(world))
                    WaterClick(world);
                if (hit == null)
                    return;
            }
            var go = hit.gameObject;
            if ((go.TryGetComponent(out Decoration d) && IsLeafy(d.Kind)) || go.GetComponent<FruitTree>() != null)
                TreeClick(go);
            var lamp = go.GetComponentInChildren<Lamp>();
            var game = GameLoop.Instance;
            if (lamp != null && lamp.IsOn && game != null && game.Clock.IsNight)
                Moths(lamp.LightCenter, hit);
        }

        static bool IsLeafy(DecoKind k) => k == DecoKind.TreeRound || k == DecoKind.Spruce || k == DecoKind.Bush || k == DecoKind.Mangrove;

        /// <summary>Otwarta woda pod punktem świata (nie ląd ani klif). Też dla kursora (GameCursor).</summary>
        public static bool IsWater(Vector2 world)
        {
            var island = Island.Instance;
            var pic = island != null ? island.Picture : null;
            if (pic == null)
                return false;
            var p = Island.WorldToPixel(world);
            return pic.IsWater(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        void WaterClick(Vector2 world)
        {
            if (Time.unscaledTime - lastWaterClick < WaterClickGap)
                return;
            lastWaterClick = Time.unscaledTime;
            if (ripples < MaxRipples)
            {
                StartCoroutine(Ripple(world, 0f));
                StartCoroutine(Ripple(world, 0.28f));
            }
            var water = FindAnyObjectByType<WaterAnimator>();
            if (water != null)
                water.Scare(world, 2.2f);
            WaterFlora.Nudge(world, 1.6f);
            GameEvents.PlaySound("bubble", 0.3f);
        }

        void TreeClick(GameObject tree)
        {
            var sr = tree.GetComponent<SpriteRenderer>();
            if (sr == null)
                return;
            var b = sr.bounds;
            for (int i = 0; i < 2; i++)
                IslandFx.LeafBurst(new Vector3(Random.Range(b.min.x + b.size.x * 0.3f, b.max.x - b.size.x * 0.3f),
                                               b.min.y + b.size.y * Random.Range(0.55f, 0.85f), 0f), 3, sr.sortingOrder + 2);
            if (AmbientBirds.Instance != null)
                AmbientBirds.Instance.ScareNear(b.center, Mathf.Max(1.2f, b.extents.y + 0.5f));
            GameEvents.PlaySound("plant", 0.3f);
        }

        void Moths(Vector2 at, Collider2D hit)
        {
            var island = Island.Instance;
            var moth = island != null && island.Art != null ? island.Art.moth : null;
            var fx = PixelParticles.Instance;
            if (moth == null || moth.Length == 0 || fx == null)
                return;
            int order = hit.TryGetComponent(out SpriteRenderer sr) ? sr.sortingOrder + 5 : 4500;
            for (int i = 0; i < 3; i++)
                fx.Spawn(at, moth, new Vector2(Random.Range(-14f, 14f), Random.Range(8f, 16f)), -4f, Random.Range(1.6f, 2.4f),
                         order, true, 12f, true);
        }

        // ------------------------------------------------------------ kręgi na wodzie

        /// <summary>Krąg (spłaszczona elipsa w siatce 32 px) o promieniu r pikseli wody, z pamięci podręcznej.</summary>
        Sprite Ring(int r)
        {
            while (rings.Count <= r)
                rings.Add(null);
            if (rings[r] != null)
                return rings[r];
            int w = 2 * r + 3, h = r + 3;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            float rx = r, ry = r * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - (w - 1) / 2f) / Mathf.Max(0.5f, rx), dy = (y - (h - 1) / 2f) / Mathf.Max(0.5f, ry);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool on = Mathf.Abs(d - 1f) < 0.5f / Mathf.Max(1f, ry) + 0.12f;
                    px[y * w + x] = on ? (Color32)RingColor : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            rings[r] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), RipplePpu);
            return rings[r];
        }

        IEnumerator Ripple(Vector2 at, float delay)
        {
            ripples++;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            var go = new GameObject("Ripple");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(Mathf.Round(at.x * RipplePpu) / RipplePpu, Mathf.Round(at.y * RipplePpu) / RipplePpu, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = RippleOrder;
            const float life = 1.1f;
            for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
            {
                float u = t / life;
                sr.sprite = Ring(2 + Mathf.RoundToInt(u * 9f));
                sr.color = new Color(1f, 1f, 1f, u < 0.7f ? 1f : (Mathf.FloorToInt(t * 12f) % 2 == 0 ? 0.6f : 0f));   // na końcu miga
                yield return null;
            }
            Destroy(go);
            ripples--;
        }
    }
}
