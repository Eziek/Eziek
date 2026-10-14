using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.Visual
{
    /// <summary>
    /// Przełącznik shaderów efektów (HUD debugowy, F1): wraca do zwykłego materiału sprite'ów.
    /// Do porównania i na wypadek, gdyby shader nie działał na czyimś komputerze.
    /// Brak materiału zapasowego = renderer się chowa (np. mgła).
    /// </summary>
    public class FxMaterial : MonoBehaviour
    {
        static readonly List<FxMaterial> all = new List<FxMaterial>();
        static bool enabledAll = true;

        [SerializeField] SpriteRenderer target;
        [SerializeField] Material fx;
        [SerializeField] Material fallback;

        public static bool Enabled
        {
            get => enabledAll;
            set
            {
                enabledAll = value;
                foreach (var m in all)
                    m.Apply();
            }
        }

        public Material Fx => fx;

        public void Setup(SpriteRenderer renderer, Material fxMaterial, Material fallbackMaterial)
        {
            target = renderer;
            fx = fxMaterial;
            fallback = fallbackMaterial;
        }

        /// <summary>Podmiana materiału efektu (np. kopia z tablicami ustawionymi w Start).</summary>
        public void SetFxMaterial(Material m)
        {
            fx = m;
            Apply();
        }

        void OnEnable()
        {
            all.Add(this);
            Apply();
        }

        void OnDisable() => all.Remove(this);

        void Apply()
        {
            if (target == null)
                return;
            if (enabledAll && fx != null)
            {
                target.sharedMaterial = fx;
                target.enabled = true;
            }
            else if (fallback != null)
            {
                target.sharedMaterial = fallback;
                target.enabled = true;
            }
            else
                target.enabled = false;
        }
    }
}
