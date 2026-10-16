using UnityEngine;

namespace Wyspa.World
{
    /// <summary>
    /// Ścinanie i rozbijanie przytrzymaniem (GDD §9 p. 136, wcześniej serie kliknięć z p. 121): gracz trzyma przycisk albo palec
    /// na drzewie, kamieniu czy krzaku, a nad obiektem napełnia się kółko (żar na ciemnym tle). Puszczenie przed końcem kasuje
    /// postęp. Kółkiem steruje Input/ClickRouter, obiekt podaje tylko czas i miejsce (IHoldAction).
    /// </summary>
    public class ChopProgress : MonoBehaviour
    {
        const float Ppu = 16f;
        const int PipOrder = 4600;
        const int Steps = 16;   // ile różnych stanów kółka (sprite'y w pamięci podręcznej)
        static readonly Color32 Filled = new Color32(240, 138, 58, 255), Empty = new Color32(28, 21, 32, 255),
                                Outline = new Color32(15, 10, 16, 255);

        SpriteRenderer ring;

        public static ChopProgress For(GameObject go)
        {
            var cp = go.GetComponent<ChopProgress>();
            return cp != null ? cp : go.AddComponent<ChopProgress>();
        }

        /// <summary>Kółko nad punktem top, wypełnione w progress (0..1).</summary>
        public void Show(Vector3 top, float progress)
        {
            if (ring == null)
            {
                var go = new GameObject("ChopRing");
                // w korzeniu sceny: nie trzęsie się ani nie skaluje z obiektem i nie trafia do odbić w wodzie (skan dzieci DecoRoot)
                ring = go.AddComponent<SpriteRenderer>();
                ring.sortingOrder = PipOrder;
                var art = Island.Instance != null ? Island.Instance.Art : null;
                if (art != null && art.glow != null)
                    ring.sharedMaterial = art.glow;   // bez oświetlenia: czytelne także nocą
            }
            ring.sprite = Ring(Mathf.Clamp(Mathf.FloorToInt(progress * Steps), 0, Steps));
            ring.enabled = true;
            ring.transform.position = new Vector3(Mathf.Round(top.x * Ppu) / Ppu, Mathf.Round((top.y + 3f / Ppu) * Ppu) / Ppu, 0f);
        }

        public void Hide()
        {
            if (ring != null)
                ring.enabled = false;
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (ring != null)
                Destroy(ring.gameObject);
        }

        static readonly Sprite[] cache = new Sprite[Steps + 1];

        /// <summary>Pierścień 11x11 px w ciemnej obwódce, wypełniany od góry zgodnie z ruchem wskazówek zegara (step z Steps).</summary>
        static Sprite Ring(int step)
        {
            if (cache[step] != null)
                return cache[step];
            const int size = 11;
            var px = new Color32[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 5.3f)
                        continue;   // poza kółkiem: przezroczyste
                    if (r > 4.4f || r < 1.6f)
                    {
                        px[y * size + x] = Outline;
                        continue;
                    }
                    // kąt od góry, zgodnie z ruchem wskazówek zegara (0..1)
                    float a = Mathf.Atan2(dx, dy) / (2f * Mathf.PI);
                    if (a < 0f) a += 1f;
                    px[y * size + x] = a < step / (float)Steps ? Filled : Empty;
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply(false);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), Ppu);
            cache[step] = s;
            return s;
        }
    }
}
