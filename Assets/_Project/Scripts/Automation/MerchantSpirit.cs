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
    /// Duszek-handlarz (Etap 6b; od 6d na prośbę gracza przypięty do skrzyni): unosi się obok swojej skrzyni i gdy tylko
    /// skrzynia się zapełni, sprzedaje całą zawartość po 90% ceny. Nie ma własnego kafelka - idzie razem ze skrzynią.
    /// </summary>
    public class MerchantSpirit : MonoBehaviour
    {
        /// <summary>Sprzedaż po 90% ceny (ulepszenie „Uczciwy handel”: więcej).</summary>
        public static float PriceMultiplier => Upgrades.UpgradeEffects.MerchantPrice;
        public static readonly List<MerchantSpirit> All = new List<MerchantSpirit>();

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Sprite[] frames = new Sprite[4];
        [SerializeField] Sprite[] glowFrames = new Sprite[4];
        Vector3 home;
        float seed;
        Coroutine flight;
        HarvestCrate crate;

        /// <summary>Skrzynia, przy której stoi duszek.</summary>
        public HarvestCrate Crate => crate;

        public static MerchantSpirit Of(HarvestCrate c)
        {
            foreach (var m in All)
                if (m.crate == c)
                    return m;
            return null;
        }

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

        /// <summary>Island postawił duszka przy skrzyni (albo przeniósł skrzynię razem z nim).</summary>
        void OnPlaced()
        {
            home = transform.position;
            var obj = GetComponent<World.TileObject>();
            crate = obj != null && obj.Anchor != null ? obj.Anchor.GetComponent<HarvestCrate>() : null;
            if (flight != null)
            {
                StopCoroutine(flight);
                flight = null;
            }
        }

        /// <summary>Pełna skrzynia = sprzedaż od razu (bez czekania). Zwraca monety.</summary>
        public int Tick(Wallet wallet, CropDatabase crops)
        {
            if (crate == null || !crate.IsFull)
                return 0;
            int coins = crate.SellAll(wallet, crops, PriceMultiplier);
            if (coins > 0)
            {
                GameEvents.RaiseToast("toast.merchant_sold", coins);
                GameEvents.RaiseCurrencyGained(crate.transform.position, CurrencyType.Coins, coins);
                if (isActiveAndEnabled)
                {
                    if (flight != null) StopCoroutine(flight);
                    flight = StartCoroutine(Visit(crate.transform.position + new Vector3(0f, 0.75f, 0f)));
                }
            }
            return coins;
        }

        /// <summary>Krótki przelot nad skrzynię i z powrotem (sprzedaż dzieje się od razu, lot jest efektem).</summary>
        IEnumerator Visit(Vector3 target)
        {
            var from = transform.position;
            yield return Flight.Arc(transform, from, target, 2f, 0.15f);   // nad skrzynię obok, krótki skok
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Flight.Arc(transform, target, home, 2f, 0.15f);
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
