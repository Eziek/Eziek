using UnityEngine;
using Wyspa.Core;
using Wyspa.Input;
using Wyspa.Visual;

namespace Wyspa.Sea
{
    /// <summary>Błędny ognik nad wodą przy brzegu (GDD §4.7): dryfuje skokowo, po minucie gaśnie. Klik = +3 Esencji.</summary>
    public class WillOWisp : MonoBehaviour, IClickable
    {
        const float Ppu = 16f, Life = 60f, Essence = 3f;

        SpriteRenderer body, glow;
        Sprite[] frames, glowFrames;
        Vector3 home;
        float age, acc;
        int step;
        float phase;

        public void Setup(Sprite[] bodyFrames, Sprite[] glowSprites, Material glowMaterial)
        {
            frames = bodyFrames;
            glowFrames = glowSprites;
            home = transform.position;
            phase = Random.value * 10f;
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = frames[0];
            body.sortingOrder = 3100;
            if (glowMaterial != null)
                body.sharedMaterial = glowMaterial;   // cały ognik świeci
            if (glowFrames != null && glowFrames.Length > 0)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(transform, false);
                glow = g.AddComponent<SpriteRenderer>();
                glow.sprite = glowFrames[0];
                glow.sortingOrder = 3101;
                if (glowMaterial != null)
                    glow.sharedMaterial = glowMaterial;
            }
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1.2f);
            col.offset = new Vector2(0f, 0.6f);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            acc += Time.unscaledDeltaTime;
            if (acc < 0.1f)
                return;
            acc = 0f;
            step++;
            if (age > Life)
            {
                Destroy(gameObject);
                return;
            }
            // ósemka nad wodą, skokowo po pikselu
            float t = step * 0.1f + phase;
            var pos = home + new Vector3(Mathf.Round(6f * Mathf.Sin(t * 0.5f)) / Ppu, Mathf.Round(3f * Mathf.Sin(t)) / Ppu, 0f);
            transform.position = pos;
            int f = step % frames.Length;
            body.sprite = frames[f];
            if (glow != null && glowFrames != null && f < glowFrames.Length)
                glow.sprite = glowFrames[f];
            // ostatnie 5 s: miga, zanim zgaśnie
            bool visible = age < Life - 5f || step % 2 == 0;
            body.enabled = visible;
            if (glow != null) glow.enabled = visible;
        }

        public void OnClick()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            game.Essence.Set(game.Essence.Amount + Essence);
            GameEvents.RaiseToast("toast.wisp.caught", (int)Essence);
            IslandFx.FindPopped(transform.position);
            Destroy(gameObject);
        }
    }
}
