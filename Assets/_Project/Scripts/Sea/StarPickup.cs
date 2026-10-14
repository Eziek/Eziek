using System.Collections;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;
using Wyspa.Visual;

namespace Wyspa.Sea
{
    /// <summary>
    /// Spadająca gwiazda (GDD §4.7): przelatuje ukosem nad wyspą i zostawia odłamek gwiazdy na lądzie.
    /// Odłamek leży 2 minuty; klik = +1 odłamek gwiazdy.
    /// </summary>
    public class StarPickup : MonoBehaviour, IClickable
    {
        const float Ppu = 16f, PickupLife = 120f;

        SpriteRenderer body, glow;
        BoxCollider2D col;
        bool landed;

        public void Setup(Vector3 target, int order, Sprite[] fall, Sprite[] fallGlow, Sprite shard, Sprite shardGlow, Material glowMaterial)
        {
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sortingOrder = 3200;
            if (glowMaterial != null)
                body.sharedMaterial = glowMaterial;
            var g = new GameObject("Glow");
            g.transform.SetParent(transform, false);
            glow = g.AddComponent<SpriteRenderer>();
            glow.sortingOrder = 3201;
            if (glowMaterial != null)
                glow.sharedMaterial = glowMaterial;
            col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.offset = new Vector2(0f, 0.4f);
            col.enabled = false;
            StartCoroutine(Fall(target, order, fall, fallGlow, shard, shardGlow));
        }

        IEnumerator Fall(Vector3 target, int order, Sprite[] fall, Sprite[] fallGlow, Sprite shard, Sprite shardGlow)
        {
            // z lewej góry ukosem w dół, 12 skokowych klatek
            var start = target + new Vector3(-5f, 4f, 0f);
            var wait = new WaitForSecondsRealtime(0.06f);
            for (int i = 0; i <= 12; i++)
            {
                transform.position = Vector3.Lerp(start, target, i / 12f);
                int f = Mathf.Min(fall.Length - 1, i * fall.Length / 13);
                body.sprite = fall[f];
                glow.sprite = fallGlow != null && f < fallGlow.Length ? fallGlow[f] : null;
                yield return wait;
            }
            transform.position = target;
            body.sprite = shard;
            body.sortingOrder = order + 2;
            glow.sprite = shardGlow;
            glow.sortingOrder = order + 3;
            col.enabled = true;
            landed = true;
            IslandFx.FindPopped(target);
            GameEvents.RaiseToast("toast.star.fell");
            float t = 0f;
            while (t < PickupLife)
            {
                t += 0.25f;
                bool visible = t < PickupLife - 8f || Mathf.FloorToInt(t * 4f) % 2 == 0;
                body.enabled = visible;
                glow.enabled = visible;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Destroy(gameObject);
        }

        public void OnClick()
        {
            var game = GameLoop.Instance;
            if (!landed || game == null)
                return;
            game.Wallet.Add(CurrencyType.StarShard, 1);
            GameEvents.RaiseCurrencyGained(transform.position, CurrencyType.StarShard, 1);
            GameEvents.RaiseToast("toast.star.found");
            Destroy(gameObject);
        }
    }
}
