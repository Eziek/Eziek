using UnityEngine;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Atmosfera nad wyspą (GDD §9 p. 120): cienie chmur w dzień, poranna mgiełka nad wodą i rosa na trawie o świcie.
    /// Jeden sprite z obrazem lądu (ta sama tekstura co wyspa) i shaderem Wyspa/Atmosphere, nad lądem i obiektami, pod mgłą.
    /// Siły (słońce, pogoda, świt) podaje shaderowi WaterAnimator; tu tylko sprite podąża za obrazem wyspy.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        const int Order = 4900;   // nad obiektami i cząsteczkami (do ~4500), pod mgłą (5000)

        Island island;
        SpriteRenderer sr;

        public void Setup(Island owner, Material material)
        {
            island = owner;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.sortingOrder = Order;
            island.Rebuilt += Sync;
            Sync();
        }

        void OnDestroy()
        {
            if (island != null)
                island.Rebuilt -= Sync;
        }

        void Sync()
        {
            var land = island != null ? island.LandRenderer : null;
            if (sr == null || land == null)
                return;
            sr.sprite = land.sprite;
            transform.position = land.transform.position;
            transform.localScale = land.transform.lossyScale;
        }
    }
}
