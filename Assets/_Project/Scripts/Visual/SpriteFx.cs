using UnityEngine;

namespace Wyspa.Visual
{
    /// <summary>
    /// Ustawia shaderowi Wyspa/SpriteSway fazę kołysania (z pozycji na świecie, żeby sąsiednie rośliny nie ruszały się równo)
    /// i kontur (podświetlenie, „gotowe do zbioru”). Wartości idą przez MaterialPropertyBlock, materiał zostaje wspólny.
    /// </summary>
    public class SpriteFx : MonoBehaviour
    {
        static readonly int PhaseId = Shader.PropertyToID("_SwayPhase");
        static readonly int OutlineId = Shader.PropertyToID("_OutlineColor");
        static readonly int SwayXId = Shader.PropertyToID("_SwayX");

        [Tooltip("Pierwszy dostaje kontur, wszystkie kołyszą się w tej samej fazie (np. roślina i jej piksele emisyjne).")]
        [SerializeField] SpriteRenderer[] renderers;
        [Tooltip("Faza = piksel X na świecie × ta wartość (prototyp: rośliny 0.15, drzewa 0.05).")]
        [SerializeField] float phasePerPixel = 0.15f;

        MaterialPropertyBlock block;
        Color outline;
        float phase;

        public void Setup(float perPixel, params SpriteRenderer[] targets)
        {
            phasePerPixel = perPixel;
            renderers = targets;
            // dodany w trakcie gry (np. grządka na nowym kafelku): OnEnable było przed Setup, więc liczymy fazę teraz
            if (isActiveAndEnabled)
                OnEnable();
        }

        void OnEnable()
        {
            phase = transform.position.x * 16f * phasePerPixel;
            Apply();
        }

        /// <summary>Island przestawił obiekt (np. przeniesione drzewo): nowa faza i miejsce dla podmuchów wiatru.</summary>
        void OnPlaced() => OnEnable();

        /// <summary>Kolor konturu; alfa 0 = bez konturu.</summary>
        public void SetOutline(Color c)
        {
            if (c == outline)
                return;
            outline = c;
            Apply();
        }

        void Apply()
        {
            if (renderers == null)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetFloat(PhaseId, phase);
                block.SetFloat(SwayXId, transform.position.x * 16f);
                block.SetColor(OutlineId, i == 0 ? outline : Color.clear);
                r.SetPropertyBlock(block);
            }
        }
    }
}
