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
    /// Skrzynia na plony (GDD §11.3 E). Ogniki-ogrodnicy niosą do najbliższej skrzyni z miejscem (Etap 6b: może ich być wiele).
    /// Klik = panel z przyciskiem „Sprzedaj” (pełna cena) i „Przenieś”; duszki-handlarze sprzedają same (90%).
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

        public static readonly List<HarvestCrate> All = new List<HarvestCrate>();

        public event Action Changed;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Najbliższa skrzynia z wolnym miejscem (null = wszystkie pełne albo brak skrzyń).</summary>
        public static HarvestCrate NearestWithSpace(Vector3 from)
        {
            HarvestCrate best = null;
            float bestD = float.MaxValue;
            foreach (var c in All)
            {
                if (c.IsFull)
                    continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>Najpełniejsza niepusta skrzynia z pominięciem już wybranych (duszki-handlarze w tym samym ticku).</summary>
        public static HarvestCrate Fullest(ICollection<HarvestCrate> skip = null)
        {
            HarvestCrate best = null;
            foreach (var c in All)
                if (c.count > 0 && (skip == null || !skip.Contains(c)) && (best == null || c.count > best.count))
                    best = c;
            return best;
        }

        public static int TotalCount()
        {
            int n = 0;
            foreach (var c in All) n += c.count;
            return n;
        }

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

        const string FruitPrefix = "fruit:";

        /// <summary>Ile sztuk w skrzyni pasuje do warunku (id rośliny „poppy” albo owocu „fruit:apple”) - kocioł alchemiczny.</summary>
        public int CountWhere(Func<string, bool> match)
        {
            int n = 0;
            foreach (var kv in contents)
            {
                Split(kv.Key, out string id, out _);
                if (match(id)) n += kv.Value;
            }
            return n;
        }

        /// <summary>Zabiera do max sztuk pasujących do warunku (najpierw najniższej jakości). Zwraca, ile zabrano.</summary>
        public int TakeWhere(Func<string, bool> match, int max)
        {
            var keys = new List<string>(contents.Keys);
            keys.Sort((a, b) => { Split(a, out _, out int qa); Split(b, out _, out int qb); return qa.CompareTo(qb); });
            int taken = 0;
            foreach (var key in keys)
            {
                if (taken >= max) break;
                Split(key, out string id, out _);
                if (!match(id)) continue;
                int take = Mathf.Min(max - taken, contents[key]);
                contents[key] -= take;
                if (contents[key] <= 0) contents.Remove(key);
                count -= take;
                taken += take;
            }
            if (taken > 0)
            {
                RefreshVisual();
                Changed?.Invoke();
            }
            return taken;
        }

        /// <summary>Owoc z drzewa (ognik strąca i zbiera z ziemi): zajmuje miejsce jak plon, sprzedaje się po cenie owocu.</summary>
        public bool TryAddFruit(TreeSpecies species)
        {
            if (species == null || IsFull)
                return false;
            string key = Key(FruitPrefix + species.id, 1);
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
                if (id.StartsWith(FruitPrefix))
                {
                    var tree = TreeSpecies.Find(id.Substring(FruitPrefix.Length));
                    if (tree != null)
                        coins += Mathf.RoundToInt(tree.fruitPrice * kv.Value * multiplier);
                    continue;
                }
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
            if (MerchantSpirit.Of(this) != null)
                info.lines.Add(Loc.Get("ui.crate.merchant"));
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
