using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;
using Wyspa.Localization;

namespace Wyspa.Sea
{
    /// <summary>
    /// Wynurzenie potwora (GDD §4.7): macki i oko wychodzą z wody pod wyspą. Prosi o ofiarę (5 plonów nocy),
    /// w zamian daje dar z głębin. Po 3 minutach (albo po ofierze) zanurza się z powrotem.
    /// </summary>
    public class MonsterEvent : MonoBehaviour, IClickable, IContextActions
    {
        const float Ppu = 16f, Stay = 180f;
        const int OfferingNight = 5;

        readonly List<SpriteRenderer> tentacles = new List<SpriteRenderer>();
        SpriteRenderer eye, eyeGlow;
        Sprite[] tentacleFrames, eyeFrames;
        bool leaving;
        bool finale;
        int page;
        float age;

        public void Setup(Sprite[] tentacle, Sprite[] eyeSprites, Sprite eyeGlowSprite, Material glowMaterial, bool finaleMode = false)
        {
            finale = finaleMode;
            tentacleFrames = tentacle;
            eyeFrames = eyeSprites;
            foreach (var dx in new[] { -22, 22 })
            {
                var go = new GameObject("Tentacle");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(dx / Ppu, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tentacle[0];
                sr.flipX = dx > 0;
                sr.sortingOrder = 690;
                tentacles.Add(sr);
            }
            var e = new GameObject("Eye");
            e.transform.SetParent(transform, false);
            eye = e.AddComponent<SpriteRenderer>();
            eye.sprite = eyeSprites != null && eyeSprites.Length > 0 ? eyeSprites[0] : null;
            eye.sortingOrder = 691;
            if (eyeGlowSprite != null)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(e.transform, false);
                eyeGlow = g.AddComponent<SpriteRenderer>();
                eyeGlow.sprite = eyeGlowSprite;
                eyeGlow.sortingOrder = 692;
                eyeGlow.sharedMaterial = glowMaterial;
                eyeGlow.enabled = false;
            }
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(4f, 2f);
            col.offset = new Vector2(0f, 1f);
            StartCoroutine(Rise(true));
        }

        IEnumerator Rise(bool up)
        {
            var wait = new WaitForSecondsRealtime(0.15f);
            for (int i = 0; i < tentacleFrames.Length; i++)
            {
                int f = up ? i : tentacleFrames.Length - 1 - i;
                foreach (var t in tentacles)
                    t.sprite = tentacleFrames[f];
                yield return wait;
            }
            if (!up)
                Destroy(gameObject);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (!leaving && !finale && age > Stay)
                Leave();
            // oko mruga: klatki 0-1, rzadko 2 (świecąca źrenica)
            if (eyeFrames == null || eyeFrames.Length == 0 || leaving)
                return;
            int f = Mathf.FloorToInt(age * 2f) % 12 == 0 ? Mathf.Min(2, eyeFrames.Length - 1) : Mathf.FloorToInt(age) % 2;
            eye.sprite = eyeFrames[Mathf.Min(f, eyeFrames.Length - 1)];
            if (eyeGlow != null)
                eyeGlow.enabled = f == 2 || finale;   // w finale oko świeci cały czas
        }

        void Leave()
        {
            if (leaving)
                return;
            leaving = true;
            if (eye != null) eye.enabled = false;
            if (eyeGlow != null) eyeGlow.enabled = false;
            StartCoroutine(Rise(false));
        }

        public void OnClick()
        {
            if (!leaving)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            var game = GameLoop.Instance;
            info.title = Loc.Get(finale || Lore.Story.Finished ? "term.first_keeper" : "term.monster");
            info.worldAnchor = transform.position + new Vector3(0f, 2.5f, 0f);
            info.verticalButtons = true;
            if (finale)
            {
                // epilog: trzy zapiski po kolei, na końcu pożegnanie
                var ending = Lore.Story.Ending;
                page = Mathf.Clamp(page, 0, ending.Length - 1);
                info.lines.Add(Loc.Get(ending[page]));
                if (page < ending.Length - 1)
                    info.Button(Loc.Get("ui.monster.next"), () => { page++; GameEvents.RequestContext(this); });
                else
                    info.Button(Loc.Get("ui.monster.farewell"), Leave);
                return;
            }
            info.lines.Add(Loc.Get(Lore.Story.Finished ? "lore.monster.after" : "lore.monster"));
            bool can = game != null && game.Wallet.Get(CurrencyType.NightHarvest) >= OfferingNight;
            info.Button(Loc.Get("ui.monster.offer", OfferingNight), Offer, can);
        }

        void Offer()
        {
            var game = GameLoop.Instance;
            if (game == null || leaving)
                return;
            var cost = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.NightHarvest, OfferingNight) };
            if (!game.Wallet.TrySpend(cost))
                return;
            game.Wallet.Add(CurrencyType.DeepGift, 1);
            Lore.Chronicle.Note("event:monster");
            Lore.Chronicle.Note("lore:lore.monster");
            Lore.Chronicle.Count("event");
            GameEvents.RaiseCurrencyGained(transform.position + new Vector3(0f, 1f, 0f), CurrencyType.DeepGift, 1);
            GameEvents.RaiseToast("toast.monster.gift");
            Leave();
        }
    }
}
