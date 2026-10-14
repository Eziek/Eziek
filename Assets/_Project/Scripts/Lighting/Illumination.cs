using UnityEngine;

namespace Wyspa.Lighting
{
    /// <summary>Czy dany punkt wyspy jest oświetlony (słońce w dzień, lampy w nocy).</summary>
    public static class Illumination
    {
        public static bool IsLit(Vector2 point, bool night)
        {
            if (!night)
                return true;
            foreach (var lamp in Lamp.All)
                if (lamp.Illuminates(point))
                    return true;
            // świecące owoce drzew (GDD §15.1): liczą się jak lampa, bez zużycia Esencji
            foreach (var tree in Farming.FruitTree.All)
                if (tree.Illuminates(point))
                    return true;
            return false;
        }

        public static float GrowthMultiplier(Vector2 point)
        {
            float best = 1f;
            foreach (var lamp in Lamp.All)
                if (lamp.Illuminates(point))
                    best = Mathf.Max(best, lamp.Definition.growthMultiplier);
            return best;
        }
    }
}
