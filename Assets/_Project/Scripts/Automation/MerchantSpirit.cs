using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Localization;

namespace Wyspa.Automation
{
    /// <summary>
    /// Duszek-handlarz (Etap 6b, zastępuje skrzynię kupca): co N sekund gry leci do najpełniejszej skrzyni na wyspie
    /// i sprzedaje jej zawartość po 90% ceny. Każdy duszek opróżnia jedną skrzynię, więc więcej duszków = więcej skrzyń naraz.
    /// </summary>
    public class MerchantSpirit : MonoBehaviour, IContextActions
    {
        public const float PriceMultiplier = 0.9f;
        public static readonly List<MerchantSpirit> All = new List<MerchantSpirit>();

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Sprite[] frames = new Sprite[4];
        [SerializeField] Sprite[] glowFrames = new Sprite[4];
        [SerializeField] float flightSeconds = 0.6f;

        Vector3 home;
        float timer;
        float lastInterval = 3600f;
        Coroutine flight;

        public float SecondsToNext(float interval) => Mathf.Max(0f, interval - timer);

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite[] bodyFrames, Sprite[] glowSprites)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            frames = bodyFrames;
            glowFrames = glowSprites;
        }

        void Awake() => home = transform.position;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnPlaced()
        {
            home = transform.position;
            if (flight != null)
            {
                StopCoroutine(flight);
                flight = null;
            }
        }

        /// <summary>Sprzedaż, gdy minął odstęp. skip = skrzynie już opróżniane w tym ticku przez inne duszki. Zwraca monety.</summary>
        public int Tick(float dt, float interval, Wallet wallet, CropDatabase crops, ICollection<HarvestCrate> skip)
        {
            lastInterval = interval;
            timer += dt;
            if (timer < interval)
                return 0;
            var crate = HarvestCrate.Fullest(skip);
            if (crate == null)
            {
                timer = interval;   // pusto - sprzeda, gdy tylko coś trafi do skrzyni
                return 0;
            }
            timer = 0f;
            skip?.Add(crate);
            int coins = crate.SellAll(wallet, crops, PriceMultiplier);
            if (coins > 0)
            {
                GameEvents.RaiseToast("toast.merchant_sold", coins);
                GameEvents.RaiseCurrencyGained(crate.transform.position, CurrencyType.Coins, coins);
                if (isActiveAndEnabled)
                {
                    if (flight != null) StopCoroutine(flight);
                    flight = StartCoroutine(Flight(crate.transform.position + new Vector3(0f, 0.5f, 0f)));
                }
            }
            return coins;
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.merchant_spirit.next", Mathf.CeilToInt(SecondsToNext(lastInterval) / 60f)));
            info.lines.Add(Loc.Get("ui.merchant_spirit.crates", HarvestCrate.All.Count, HarvestCrate.TotalCount()));
        }

        IEnumerator Flight(Vector3 target)
        {
            const float step = 0.1f;
            var wait = new WaitForSecondsRealtime(step);
            int n = Mathf.Max(1, Mathf.RoundToInt(flightSeconds / step));
            for (int i = 1; i <= n; i++) { transform.position = Vector3.Lerp(home, target, i / (float)n); yield return wait; }
            for (int i = 0; i < 3; i++) yield return wait;   // chwila przy skrzyni
            for (int i = 1; i <= n; i++) { transform.position = Vector3.Lerp(target, home, i / (float)n); yield return wait; }
            transform.position = home;
            flight = null;
        }

        void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            int f = Mathf.FloorToInt(Time.unscaledTime * 6f) % frames.Length;
            if (body != null) body.sprite = frames[f];
            if (glow != null && glowFrames != null && f < glowFrames.Length) glow.sprite = glowFrames[f];
        }
    }
}
