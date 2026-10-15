using UnityEngine;

namespace Wyspa.World
{
    /// <summary>
    /// Ścinanie i rozbijanie seriami kliknięć (GDD §9 p. 121): każde kliknięcie w drzewo, kamień czy krzak to jedno uderzenie,
    /// a obiekt znika po kilku szybkich uderzeniach. Przerwa dłuższa niż Window kasuje serię (pojedyncze kliknięcie nic nie
    /// niszczy). Nad obiektem rząd kropek: ile uderzeń już padło (żar) i ile zostało (ciemne).
    /// </summary>
    public class ChopProgress : MonoBehaviour
    {
        public const float Window = 2.5f;   // tyle sekund może minąć między kliknięciami w serii
        const float Ppu = 16f;
        const int PipOrder = 4600;
        static readonly Color32 Filled = new Color32(240, 138, 58, 255), Empty = new Color32(28, 21, 32, 255),
                                Outline = new Color32(15, 10, 16, 255);

        int hits, need;
        float last = -100f;
        SpriteRenderer pips;

        /// <summary>Czy seria trwa (było uderzenie w ostatnich Window sekundach).</summary>
        public bool Active => hits > 0 && Time.unscaledTime - last <= Window;

        public static ChopProgress For(GameObject go)
        {
            var cp = go.GetComponent<ChopProgress>();
            return cp != null ? cp : go.AddComponent<ChopProgress>();
        }

        /// <summary>Jedno uderzenie; zwraca, ile padło w tej serii. showFrom: od którego uderzenia pokazać kropki.</summary>
        public int Hit(int required, Vector3 top, int showFrom = 1)
        {
            if (!Active)
                hits = 0;
            need = Mathf.Max(1, required);
            hits++;
            last = Time.unscaledTime;
            if (hits >= showFrom && hits < need)
                ShowPips(top);
            else
                HidePips();
            return hits;
        }

        public void Clear()
        {
            hits = 0;
            HidePips();
        }

        void Update()
        {
            if (hits > 0 && !Active)
                Clear();
        }

        void OnDisable() => HidePips();

        void ShowPips(Vector3 top)
        {
            if (pips == null)
            {
                var go = new GameObject("ChopPips");
                go.transform.SetParent(transform.parent, false);   // nie w obiekcie: nie trzęsie się i nie skaluje z nim
                pips = go.AddComponent<SpriteRenderer>();
                pips.sortingOrder = PipOrder;
            }
            pips.sprite = Pips(hits, need);
            pips.enabled = true;
            pips.transform.position = new Vector3(Mathf.Round(top.x * Ppu) / Ppu, Mathf.Round((top.y + 3f / Ppu) * Ppu) / Ppu, 0f);
        }

        void HidePips()
        {
            if (pips != null)
                pips.enabled = false;
        }

        void OnDestroy()
        {
            if (pips != null)
                Destroy(pips.gameObject);
        }

        static readonly System.Collections.Generic.Dictionary<int, Sprite> cache = new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>Kropki 2x2 z odstępem 1 px w ciemnej obwódce: done pełnych z need.</summary>
        static Sprite Pips(int done, int need)
        {
            int key = need * 100 + done;
            if (cache.TryGetValue(key, out var s) && s != null)
                return s;
            int w = need * 3 + 1, h = 4;
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Outline;
            for (int i = 0; i < need; i++)
                for (int y = 1; y <= 2; y++)
                    for (int x = 0; x < 2; x++)
                        px[y * w + 1 + i * 3 + x] = i < done ? Filled : Empty;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu);
            cache[key] = s;
            return s;
        }
    }
}
