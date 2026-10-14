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
        Vector3 home;
        float seed;
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

        void Awake()
        {
            home = transform.position;
            seed = Random.value * 10f;
        }
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
                    flight = StartCoroutine(Visit(crate.transform.position + new Vector3(0f, 0.7f, 0f)));
                }
            }
            return coins;
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.merchant_spirit.next", Mathf.CeilToInt(SecondsToNext(lastInterval) / 60f)));
            info.lines.Add(Loc.Get("ui.merchant_spirit.crates", HarvestCrate.All.Count, HarvestCrate.TotalCount()));
        }

        /// <summary>Płynny lot do skrzyni i z powrotem (sprzedaż dzieje się od razu, lot jest efektem).</summary>
        IEnumerator Visit(Vector3 target)
        {
            var from = transform.position;
            yield return Flight.Arc(transform, from, target, 2.5f, 0.5f);
            yield return new WaitForSecondsRealtime(0.4f);   // chwila przy skrzyni
            yield return Flight.Arc(transform, target, home, 2.5f, 0.5f);
            flight = null;
        }

        void Update()
        {
            if (flight == null)
                transform.position = Flight.Hover(home, seed);
            if (frames == null || frames.Length == 0)
                return;
            int f = Mathf.FloorToInt(Time.unscaledTime * 6f) % frames.Length;
            if (body != null) body.sprite = frames[f];
            if (glow != null && glowFrames != null && f < glowFrames.Length) glow.sprite = glowFrames[f];
        }
    }
}
