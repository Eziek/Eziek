using UnityEngine;

namespace Wyspa.Visual
{
    /// <summary>
    /// Stworzenie tła w strefie (wrona, żaba, bąble na bagnie): skokowa animacja z klatek, losowe przerwy.
    /// Nie postać i nie da się go kliknąć - tylko życie wyspy (§13.8).
    /// </summary>
    public class ZoneCritter : MonoBehaviour
    {
        SpriteRenderer body, glow;
        Sprite[] frames, glowFrames;
        bool loop;
        System.Random rnd;
        float wait;
        int frame;
        bool playing;

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites, bool looping, uint seed)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
            loop = looping;
            rnd = new System.Random((int)(seed & 0x7fffffff));
            wait = (float)rnd.NextDouble() * 3f;
        }

        void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            wait -= Time.unscaledDeltaTime;
            if (wait > 0f)
                return;
            if (loop)
            {
                // bąble: klatki po kolei, potem chwila spokoju (z pustą klatką)
                frame++;
                if (frame >= frames.Length)
                {
                    frame = 0;
                    Set(-1);
                    wait = 1f + (float)rnd.NextDouble() * 3f;
                    return;
                }
                Set(frame);
                wait = 0.15f;
                return;
            }
            // wrona, żaba: zwykle klatka 0, czasem krótko inna (dziobnięcie, mrugnięcie)
            playing = !playing;
            Set(playing ? 1 + rnd.Next(Mathf.Max(1, frames.Length - 1)) % Mathf.Max(1, frames.Length - 1) : 0);
            wait = playing ? 0.3f : 1.5f + (float)rnd.NextDouble() * 4f;
        }

        void Set(int i)
        {
            bool visible = i >= 0 && i < frames.Length;
            body.enabled = visible;
            if (visible)
                body.sprite = frames[i];
            if (glow != null)
            {
                glow.enabled = visible && glowFrames != null && i < glowFrames.Length;
                if (glow.enabled)
                    glow.sprite = glowFrames[i];
            }
        }
    }
}
