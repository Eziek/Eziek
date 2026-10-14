using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;

namespace Wyspa.Lore
{
    [Serializable] public class StatSave { public string id; public int value; }

    [Serializable]
    public class ChronicleSave
    {
        public int cycle = -1;
        public int moonPoints;
        public List<StatSave> stats = new List<StatSave>();
        public List<string> rituals = new List<string>();
        public List<string> found = new List<string>();
        public List<string> claimed = new List<string>();
    }

    /// <summary>
    /// Kronika wyspy (GDD §11.3 G, H): cykl księżyca (od nowiu do nowiu) zbiera osiągnięcia i przy nowiu zamienia je
    /// w punkty Księgi rytuałów (wyspa nic nie traci), rytuały dają stałe bonusy, bestiariusz zbiera odkrycia i lore.
    /// </summary>
    public class Chronicle : MonoBehaviour
    {
        public static Chronicle Instance { get; private set; }

        int cycle = -1;
        int moonPoints;
        readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        readonly HashSet<string> rituals = new HashSet<string>();
        readonly HashSet<string> found = new HashSet<string>();
        readonly HashSet<string> claimed = new HashSet<string>();
        float timer;

        public int MoonPoints => moonPoints;
        public event Action Changed;

        void Awake()
        {
            Instance = this;
            Rituals.Apply(this);   // efekty są statyczne: po resecie zapisu nie mogą zostać z poprzedniej gry
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ------------------------------------------------------------ odkrycia i statystyki (wołane z gry)

        /// <summary>Wpis do bestiariusza (np. "crop:carrot", "fish:glow", "event:monster", "lore:lore.3"). Statycznie, bez sceny nic nie robi.</summary>
        public static void Note(string id)
        {
            var c = Instance;
            if (c == null || string.IsNullOrEmpty(id) || !c.found.Add(id))
                return;
            c.Changed?.Invoke();
        }

        /// <summary>Licznik tego cyklu (harvest, night, fish, event, tile) - z niego punkty przy nowiu.</summary>
        public static void Count(string stat, int n = 1)
        {
            var c = Instance;
            if (c == null || n <= 0)
                return;
            c.stats.TryGetValue(stat, out int v);
            c.stats[stat] = v + n;
        }

        public bool Found(string id) => found.Contains(id);
        public int Stat(string id) => stats.TryGetValue(id, out int v) ? v : 0;
        public IEnumerable<string> FoundWithPrefix(string prefix)
        {
            foreach (var f in found)
                if (f.StartsWith(prefix, StringComparison.Ordinal))
                    yield return f;
        }

        // ------------------------------------------------------------ cykl księżyca

        /// <summary>Punkty, które da obecny cykl przy nowiu (podgląd w Księdze).</summary>
        public int PendingPoints
        {
            get
            {
                int p = Stat("harvest") / 10 + Stat("night") / 5 + Stat("fish") / 5 + Stat("event") / 3 + Stat("tile") / 10;
                return Mathf.Min(12, p);
            }
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 5f;
            CheckCycle();
        }

        void CheckCycle()
        {
            int now = GameClock.CycleIndex(DateTime.UtcNow);
            if (cycle < 0)
            {
                cycle = now;
                return;
            }
            if (now <= cycle)
                return;   // tylko do przodu: nowa wyspa po rejsie ma inne przesunięcie zegara (GameClock)
            CloseCycle(now);
        }

        /// <summary>Nów: zebrane w cyklu osiągnięcia zamieniają się w punkty Księgi, liczniki od nowa.</summary>
        public void CloseCycle(int next)
        {
            int gained = PendingPoints;
            moonPoints += gained;
            stats.Clear();
            cycle = next;
            GameEvents.RaiseToast("toast.cycle.closed", gained);
            Changed?.Invoke();
        }

        /// <summary>Tylko do testów (HUD debugowy).</summary>
        public void DebugAddPoints(int n)
        {
            moonPoints += n;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ rytuały

        public bool Has(string ritual) => rituals.Contains(ritual);

        public bool TryPerform(RitualDef r, Wallet wallet)
        {
            if (r == null || rituals.Contains(r.id) || moonPoints < r.points || wallet == null || !wallet.TrySpend(r.cost))
                return false;
            moonPoints -= r.points;
            rituals.Add(r.id);
            Rituals.Apply(this);
            GameEvents.RaiseToast("toast.ritual.done", Localization.Loc.Get(r.nameKey));
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ nagrody za kategorie bestiariusza

        public bool Claimed(string category) => claimed.Contains(category);

        public bool Claim(string category, int points)
        {
            if (!claimed.Add(category))
                return false;
            moonPoints += points;
            GameEvents.RaiseToast("toast.bestiary.reward", points);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ zapis

        public ChronicleSave Snapshot()
        {
            var s = new ChronicleSave { cycle = cycle, moonPoints = moonPoints };
            foreach (var kv in stats) s.stats.Add(new StatSave { id = kv.Key, value = kv.Value });
            s.rituals.AddRange(rituals);
            s.found.AddRange(found);
            s.claimed.AddRange(claimed);
            return s;
        }

        public void Restore(ChronicleSave s)
        {
            stats.Clear(); rituals.Clear(); found.Clear(); claimed.Clear();
            if (s != null)
            {
                cycle = s.cycle;
                moonPoints = s.moonPoints;
                if (s.stats != null) foreach (var st in s.stats) if (st != null && st.id != null) stats[st.id] = st.value;
                if (s.rituals != null) foreach (var r in s.rituals) rituals.Add(r);
                if (s.found != null) foreach (var f in s.found) found.Add(f);
                if (s.claimed != null) foreach (var c in s.claimed) claimed.Add(c);
            }
            Rituals.Apply(this);
            CheckCycle();   // gra zamknięta przez nów: punkty za stary cykl od razu
            Changed?.Invoke();
        }
    }
}
