using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Input;

namespace Wyspa.Automation
{
    [Serializable]
    public struct CrateEntry
    {
        public string cropId;
        public int count;
    }

    /// <summary>
    /// Skrzynia na plony (GDD §11.3 E). Ognik-ogrodnik zbiera do niej dojrzałe plony.
    /// Klik = sprzedaż wszystkiego po pełnej cenie; skrzynia kupca sprzedaje sama (90%).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class HarvestCrate : MonoBehaviour, IClickable
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

        public bool TryAdd(CropDefinition crop)
        {
            if (crop == null || IsFull)
                return false;
            contents.TryGetValue(crop.id, out int n);
            contents[crop.id] = n + 1;
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
                var def = crops.Find(kv.Key);
                if (def == null)
                    continue;
                coins += Mathf.RoundToInt(def.sellPrice * kv.Value * multiplier);
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

        public void OnClick()
        {
            var game = GameLoop.Instance;
            if (game != null)
                SellAll(game.Wallet, game.Crops, 1f);
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
                list.Add(new CrateEntry { cropId = kv.Key, count = kv.Value });
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
                        contents[e.cropId] = e.count;
                        count += e.count;
                    }
            RefreshVisual();
            Changed?.Invoke();
        }
    }
}
