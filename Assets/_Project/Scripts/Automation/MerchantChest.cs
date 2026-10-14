using System.Collections;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;

namespace Wyspa.Automation
{
    /// <summary>Skrzynia kupca: co N sekund gry sprzedaje zawartość skrzyni na plony po 90% ceny.</summary>
    public class MerchantChest : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite closed;
        [SerializeField] Sprite open;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Sprite closedGlow;
        [SerializeField] Sprite openGlow;
        [SerializeField] float priceMultiplier = 0.9f;

        float timer;

        public float SecondsToNext(float interval) => Mathf.Max(0f, interval - timer);

        public void Setup(SpriteRenderer bodyRenderer, Sprite closedSprite, Sprite openSprite,
                          SpriteRenderer glowRenderer, Sprite closedGlowSprite, Sprite openGlowSprite)
        {
            body = bodyRenderer;
            closed = closedSprite;
            open = openSprite;
            glow = glowRenderer;
            closedGlow = closedGlowSprite;
            openGlow = openGlowSprite;
        }

        /// <summary>Zwraca liczbę monet ze sprzedaży (0, gdy jeszcze nie czas albo skrzynia pusta).</summary>
        public int Tick(float dt, float interval, HarvestCrate crate, Wallet wallet, CropDatabase crops)
        {
            timer += dt;
            if (timer < interval)
                return 0;
            timer = 0f;
            int coins = crate != null ? crate.SellAll(wallet, crops, priceMultiplier) : 0;
            if (coins > 0)
                GameEvents.RaiseToast("toast.merchant_sold", coins);
            if (coins > 0 && isActiveAndEnabled)
                StartCoroutine(OpenBriefly());
            return coins;
        }

        IEnumerator OpenBriefly()
        {
            Set(open, openGlow);
            yield return new WaitForSecondsRealtime(0.6f);
            Set(closed, closedGlow);
        }

        void Set(Sprite s, Sprite g)
        {
            if (body != null && s != null) body.sprite = s;
            if (glow != null && g != null) glow.sprite = g;
        }
    }
}
