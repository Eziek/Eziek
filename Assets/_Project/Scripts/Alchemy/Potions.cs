using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Farming;

namespace Wyspa.Alchemy
{
    /// <summary>Składnik przepisu: kategoria („day”, „night”, „fruit”) albo konkretna roślina („crop:mandrake”), ile sztuk.</summary>
    public struct Ingredient
    {
        public string what;
        public int count;
        public Ingredient(string what, int count) { this.what = what; this.count = count; }
    }

    /// <summary>Przepis kotła: składniki ze skrzyń na plony, czas warzenia, ile działa mikstura.</summary>
    public class Recipe
    {
        public string id;
        public Ingredient[] needs;
        public float brewSeconds;
        public float effectSeconds;   // 0 = działa do pierwszego użycia (wabik na kupca)

        public string NameKey => "potion." + id;
        public string EffectKey => "potion." + id + ".effect";

        public static readonly Recipe[] All =
        {
            new Recipe { id = "growth", needs = new[] { new Ingredient("day", 6) }, brewSeconds = 300, effectSeconds = 2 * GameClock.CycleSeconds },
            new Recipe { id = "night", needs = new[] { new Ingredient("night", 4) }, brewSeconds = 300, effectSeconds = 2 * GameClock.CycleSeconds },
            new Recipe { id = "focus", needs = new[] { new Ingredient("fruit", 3), new Ingredient("day", 3) }, brewSeconds = 420, effectSeconds = 2 * GameClock.CycleSeconds },
            new Recipe { id = "light", needs = new[] { new Ingredient("crop:moonflower", 3), new Ingredient("fruit", 2) }, brewSeconds = 420, effectSeconds = 2 * GameClock.CycleSeconds },
            new Recipe { id = "lure", needs = new[] { new Ingredient("crop:mandrake", 2), new Ingredient("fruit", 2) }, brewSeconds = 600, effectSeconds = 0 },
            new Recipe { id = "fog", needs = new[] { new Ingredient("crop:graveivy", 2), new Ingredient("crop:ghostpumpkin", 2) }, brewSeconds = 600, effectSeconds = 2 * GameClock.CycleSeconds },
        };

        public static Recipe Find(string id) => Array.Find(All, r => r.id == id);
    }

    [Serializable]
    public class PotionSave
    {
        public string id;
        public long untilUtcTicks;   // 0 = do pierwszego użycia (wabik)
    }

    /// <summary>
    /// Działające mikstury (kocioł alchemiczny): czas liczony w prawdziwym czasie (UTC), więc mija też offline.
    /// Inne systemy pytają o mnożniki: wzrost roślin dnia i nocy, przypływ Żaru, lampy bez Esencji, zdarzenia nocne,
    /// wabik na kupca-widmo (zużywa się, gdy kupiec przypłynie).
    /// </summary>
    public static class Potions
    {
        static readonly Dictionary<string, long> active = new Dictionary<string, long>();

        public static bool IsActive(string id)
        {
            if (!active.TryGetValue(id, out long until))
                return false;
            if (until == 0 || DateTime.UtcNow.Ticks < until)
                return true;
            active.Remove(id);
            GameEvents.RaiseToast("toast.potion.ended", Localization.Loc.Get("potion." + id));
            return false;
        }

        /// <summary>Ile sekund zostało (0 = nie działa, -1 = do pierwszego użycia).</summary>
        public static float SecondsLeft(string id)
        {
            if (!IsActive(id))
                return 0f;
            long until = active[id];
            return until == 0 ? -1f : (float)TimeSpan.FromTicks(until - DateTime.UtcNow.Ticks).TotalSeconds;
        }

        public static void Activate(Recipe r)
        {
            if (r == null)
                return;
            long until = r.effectSeconds <= 0f ? 0 : DateTime.UtcNow.AddSeconds(r.effectSeconds).Ticks;
            // ta sama mikstura jeszcze raz: czas się przedłuża
            if (until != 0 && active.TryGetValue(r.id, out long old) && old > DateTime.UtcNow.Ticks)
                until = old + (long)(r.effectSeconds * TimeSpan.TicksPerSecond);
            active[r.id] = until;
        }

        public static void Consume(string id) => active.Remove(id);

        public static IEnumerable<string> ActiveIds()
        {
            foreach (var r in Recipe.All)
                if (IsActive(r.id))
                    yield return r.id;
        }

        // ---- mnożniki dla innych systemów
        public static float DayGrowth => IsActive("growth") ? 1.3f : 1f;
        public static float NightGrowth => IsActive("night") ? 1.5f : 1f;
        public static float Essence => IsActive("focus") ? 1.5f : 1f;
        public static bool LampsFree => IsActive("light");
        public static float Events => IsActive("fog") ? 2f : 1f;
        public static bool Lure => IsActive("lure");

        // ---- zapis
        public static List<PotionSave> Snapshot()
        {
            var list = new List<PotionSave>();
            foreach (var kv in active)
                list.Add(new PotionSave { id = kv.Key, untilUtcTicks = kv.Value });
            return list;
        }

        public static void Restore(IEnumerable<PotionSave> list)
        {
            active.Clear();
            if (list != null)
                foreach (var p in list)
                    if (p != null && Recipe.Find(p.id) != null)
                        active[p.id] = p.untilUtcTicks;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        // ---- składniki ze skrzyń na plony

        /// <summary>Czy rzecz ze skrzyni („poppy” albo „fruit:apple”) pasuje do składnika.</summary>
        public static bool Matches(string itemId, string what, CropDatabase crops)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;
            bool fruit = itemId.StartsWith("fruit:");
            switch (what)
            {
                case "fruit": return fruit;
                case "day":
                case "night":
                    if (fruit || crops == null) return false;
                    var def = crops.Find(itemId);
                    return def != null && def.kind == (what == "day" ? CropKind.Day : CropKind.Night);
                default:
                    return what.StartsWith("crop:") ? itemId == what.Substring(5) : itemId == what;
            }
        }

        public static int Available(Ingredient ing, CropDatabase crops)
        {
            int n = 0;
            foreach (var c in HarvestCrate.All)
                n += c.CountWhere(id => Matches(id, ing.what, crops));
            return n;
        }

        public static bool CanBrew(Recipe r, CropDatabase crops)
        {
            foreach (var ing in r.needs)
                if (Available(ing, crops) < ing.count)
                    return false;
            return true;
        }

        /// <summary>Zabiera składniki ze skrzyń (najpierw z najpełniejszych). Wołać po CanBrew.</summary>
        public static void TakeIngredients(Recipe r, CropDatabase crops)
        {
            foreach (var ing in r.needs)
            {
                int left = ing.count;
                var crates = new List<HarvestCrate>(HarvestCrate.All);
                crates.Sort((a, b) => b.Count.CompareTo(a.Count));
                foreach (var c in crates)
                {
                    if (left <= 0) break;
                    left -= c.TakeWhere(id => Matches(id, ing.what, crops), left);
                }
            }
        }
    }
}
