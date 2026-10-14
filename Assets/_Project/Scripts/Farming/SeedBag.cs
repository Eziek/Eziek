using System;
using System.Collections.Generic;

namespace Wyspa.Farming
{
    [Serializable]
    public class SeedSave
    {
        public string id;
        public int count;
    }

    /// <summary>
    /// Worek nasion: nasiona, których nie kupuje się przy sadzeniu (hybrydy z mutacji, nasiona od kupca-widma, z wypraw).
    /// Koło nasion najpierw bierze z worka (za darmo), dopiero potem kupuje za monety.
    /// </summary>
    public class SeedBag
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public event Action Changed;

        public int Count(string id) => id != null && counts.TryGetValue(id, out int n) ? n : 0;

        public void Add(string id, int n = 1)
        {
            if (string.IsNullOrEmpty(id) || n <= 0)
                return;
            counts[id] = Count(id) + n;
            Changed?.Invoke();
        }

        public bool TryTake(string id)
        {
            int n = Count(id);
            if (n <= 0)
                return false;
            if (n == 1) counts.Remove(id);
            else counts[id] = n - 1;
            Changed?.Invoke();
            return true;
        }

        public List<SeedSave> Snapshot()
        {
            var list = new List<SeedSave>();
            foreach (var kv in counts)
                list.Add(new SeedSave { id = kv.Key, count = kv.Value });
            return list;
        }

        public void Restore(IEnumerable<SeedSave> list)
        {
            counts.Clear();
            if (list != null)
                foreach (var s in list)
                    if (s != null && !string.IsNullOrEmpty(s.id) && s.count > 0)
                        counts[s.id] = s.count;
            Changed?.Invoke();
        }
    }
}
