using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.Automation
{
    /// <summary>
    /// Ognisko ogników (GDD §9 p. 103): miejsce spotkań. Wolne ogniki z gniazd w pobliżu krążą wokół niego (zamiast nad
    /// gniazdem), tu odbywają się zabawy, a przy ogniu nastrój rośnie szybciej. Płomienie w kolorze ogników (3 klatki).
    /// </summary>
    public class WispBonfire : MonoBehaviour, IContextActions
    {
        public const float Reach = 6f;      // gniazda w tym promieniu „chodzą” do tego ogniska
        public const float Warmth = 2.2f;   // tak blisko ogniska nastrój rośnie szybciej

        public static readonly List<WispBonfire> All = new List<WispBonfire>();

        SpriteRenderer body, glow;
        Sprite[] frames, glows;
        float seed;

        /// <summary>Miejsc przy ogniu (krąg): więcej ogników zostaje przy swoich gniazdach, zamiast się tłoczyć.</summary>
        public const int Seats = 6;
        readonly Wisp[] seated = new Wisp[Seats];

        /// <summary>Wolne miejsce w kręgu przy ogniu (stałe dla ognika, póki siedzi). False = krąg pełny.</summary>
        public bool TrySeat(Wisp w, out Vector3 seat)
        {
            seat = default;
            int free = -1;
            for (int i = 0; i < Seats; i++)
            {
                if (seated[i] != null && !seated[i].isActiveAndEnabled)
                    seated[i] = null;
                if (seated[i] == w) { free = i; break; }
                if (free < 0 && seated[i] == null) free = i;
            }
            if (free < 0)
                return false;
            seated[free] = w;
            // elipsa wokół ognia (z przodu niżej, z tyłu wyżej), co 60 stopni
            float a = free * Mathf.PI * 2f / Seats + 0.3f;
            seat = Center + new Vector3(Mathf.Cos(a) * 1.15f, Mathf.Sin(a) * 0.5f + 0.15f, 0f);
            return true;
        }

        public void Leave(Wisp w)
        {
            for (int i = 0; i < Seats; i++)
                if (seated[i] == w) seated[i] = null;
        }

        /// <summary>Gdzie krążą ogniki: nad ogniem.</summary>
        public Vector3 Center => transform.position + new Vector3(0f, 0.9f, 0f);

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowFrames)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glows = glowFrames;
            seed = Random.value * 3f;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            int f = Mathf.FloorToInt(Time.unscaledTime * 6f + seed) % frames.Length;
            if (body != null && frames[f] != null) body.sprite = frames[f];
            if (glow != null && glows != null && f < glows.Length && glows[f] != null) glow.sprite = glows[f];
        }

        /// <summary>Najbliższe ognisko w zasięgu punktu (gniazda), null gdy brak.</summary>
        public static WispBonfire Near(Vector3 at)
        {
            WispBonfire best = null;
            float bestD = Reach * Reach;
            foreach (var b in All)
            {
                if (b == null)
                    continue;
                float d = (b.transform.position - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        public void FillContext(ContextInfo info)
        {
            int around = 0;
            foreach (var w in Wisp.All)
                if (w != null && (w.transform.position - Center).sqrMagnitude < Warmth * Warmth)
                    around++;
            info.lines.Add(Loc.Get("ui.bonfire.info"));
            info.lines.Add(Loc.Get("ui.bonfire.around", around));
        }
    }

    /// <summary>
    /// Sennik (GDD §9 p. 103): tu zmęczone ogniki odpoczywają (nastrój wraca ponad 3 razy szybciej niż przy gnieździe).
    /// Mieści 3 śpiących naraz; śpiący unoszą się przy okrągłych ścianach i pokazują „Z”.
    /// </summary>
    public class WispDreamHouse : MonoBehaviour, IContextActions
    {
        public const int Beds = 3;
        public static readonly List<WispDreamHouse> All = new List<WispDreamHouse>();
        readonly List<Wisp> sleepers = new List<Wisp>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Miejsce do spania dla ognika (null = pełny). Ognik zwalnia je przez Leave.</summary>
        public bool TryEnter(Wisp w, out Vector3 spot)
        {
            sleepers.RemoveAll(s => s == null || !s.isActiveAndEnabled);
            spot = default;
            if (!sleepers.Contains(w))
            {
                if (sleepers.Count >= Beds)
                    return false;
                sleepers.Add(w);
            }
            int i = sleepers.IndexOf(w);
            spot = transform.position + new Vector3((i - 1) * 0.55f, 1.2f + (i == 1 ? 0.35f : 0f), 0f);
            return true;
        }

        public void Leave(Wisp w) => sleepers.Remove(w);

        /// <summary>Najbliższy sennik z wolnym miejscem.</summary>
        public static WispDreamHouse Free(Vector3 at)
        {
            WispDreamHouse best = null;
            foreach (var h in All)
            {
                if (h == null)
                    continue;
                h.sleepers.RemoveAll(s => s == null || !s.isActiveAndEnabled);
                if (h.sleepers.Count < Beds && (best == null || (h.transform.position - at).sqrMagnitude < (best.transform.position - at).sqrMagnitude))
                    best = h;
            }
            return best;
        }

        public void FillContext(ContextInfo info)
        {
            sleepers.RemoveAll(s => s == null || !s.isActiveAndEnabled);
            info.lines.Add(Loc.Get("ui.dreamhouse.info"));
            info.lines.Add(Loc.Get("ui.dreamhouse.beds", sleepers.Count, Beds));
        }
    }
}
