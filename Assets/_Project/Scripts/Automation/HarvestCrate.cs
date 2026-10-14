using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Localization;

namespace Wyspa.Automation
{
    [Serializable]
    public struct CrateEntry
    {
        public string cropId;
        public int count;
        public int quality;   // 0 w zapisach sprzed Etapu 4a = 1
    }

    /// <summary>
    /// Skrzynia na plony (GDD §11.3 E). Ognik-ogrodnik zbiera do niej dojrzałe plony.
    /// Klik = panel z przyciskiem „Sprzedaj” (pełna cena) i „Przenieś”; skrzynia kupca sprzedaje sama (90%).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class HarvestCrate : MonoBehaviour, IContextActions
    {
        [SerializeField] int capacity = 20;
        [Tooltip("Sprite'y napełnienia: pusta, do połowy, pełna.")]
        [SerializeField] Sprite[] fillSprites = new Sprite[3];
        [SerializeField] SpriteRenderer spriteRenderer;

        readonly Dictionary<string, int> contents = new Dictionary<string, int>();
        int count;

        public event Action Changed;

        public int Count => count;
        public int Capacity => capacity;
        public bool IsFull => count >= capacity;

        public void Setup(Sprite[] sprites, SpriteRenderer renderer)
        {
            fillSprites = sprites;
            spriteRenderer = renderer;
        }

        // klucz zawartości: id rośliny + jakość (cena zależy od jakości)
        static string Key(string id, int quality) => id + "#" + Mathf.Clamp(quality, 1, 3);

        static void Split(string key, out string id, out int quality)
        {
            int i = key.LastIndexOf('#');
            id = i < 0 ? key : key.Substring(0, i);
            quality = i < 0 || !int.TryParse(key.Substring(i + 1), out int q) ? 1 : q;
        }

        public bool TryAdd(CropDefinition crop, int quality = 1)
        {
            if (crop == null || IsFull)
                return false;
            string key = Key(crop.id, quality);
            contents.TryGetValue(key, out int n);
            contents[key] = n + 1;
            count++;
            RefreshVisual();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sprzedaje całą zawartość. multiplier = 1 (ręcznie) albo 0.9 (skrzynia kupca). Zwraca monety.</summary>
        public int SellAll(Wallet wallet, CropDatabase crops, float multiplier)
        {
            if (count == 0 || wallet == null || crops == null)
                return 0;
            int coins = 0;
            foreach (var kv in contents)
            {
                Split(kv.Key, out string id, out int quality);
                var def = crops.Find(id);
                if (def == null)
                    continue;
                coins += Mathf.RoundToInt(def.SellPrice(quality) * kv.Value * multiplier);
                if (def.kind == CropKind.Night)
                    wallet.Add(CurrencyType.NightHarvest, def.nightHarvestYield * kv.Value);
            }
            wallet.Add(CurrencyType.Coins, coins);
            contents.Clear();
            count = 0;
            RefreshVisual();
            Changed?.Invoke();
            return coins;
        }

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("crate.contents", count, capacity));
            info.Button(Loc.Get("ui.crate.sell"), SellNow, count > 0);
        }

        /// <summary>Sprzedaż ręczna: pełna cena, monety lecą do licznika.</summary>
        public void SellNow()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;
            int coins = SellAll(game.Wallet, game.Crops, 1f);
            if (coins > 0)
            {
                GameEvents.RaiseToast("toast.crate_sold", coins);
                GameEvents.RaiseCurrencyGained(transform.position, CurrencyType.Coins, coins);
            }
        }

        void RefreshVisual()
        {
            if (spriteRenderer == null || fillSprites == null || fillSprites.Length == 0)
                return;
            int i = count == 0 ? 0 : (count < capacity ? 1 : 2);
            spriteRenderer.sprite = fillSprites[Mathf.Min(i, fillSprites.Length - 1)];
        }

        public List<CrateEntry> Snapshot()
        {
            var list = new List<CrateEntry>();
            foreach (var kv in contents)
            {
                Split(kv.Key, out string id, out int quality);
                list.Add(new CrateEntry { cropId = id, count = kv.Value, quality = quality });
            }
            return list;
        }

        public void Restore(IEnumerable<CrateEntry> list)
        {
            contents.Clear();
            count = 0;
            if (list != null)
                foreach (var e in list)
                    if (!string.IsNullOrEmpty(e.cropId) && e.count > 0)
                    {
                        string key = Key(e.cropId, e.quality <= 0 ? 1 : e.quality);
                        contents.TryGetValue(key, out int n);
                        contents[key] = n + e.count;
                        count += e.count;
                    }
            RefreshVisual();
            Changed?.Invoke();
        }
    }
}
