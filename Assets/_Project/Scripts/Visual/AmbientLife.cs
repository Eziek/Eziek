using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Lighting;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Więcej życia na wyspie: ćmy krążą nocą wokół zapalonych lamp,
    /// a co jakiś czas ryba wyskakuje z wody przy brzegu z pluskiem. Bez postaci, sam ruch sprite'ów i cząsteczki.
    /// Tworzy go GameLoop (bez przebudowy sceny); grafiki z IslandArt (Wyspa → 2).
    /// </summary>
    public class AmbientLife : MonoBehaviour
    {
        const int MothsPerLamp = 2;
        const float LampLight = 1.6f;          // wysokość klatki latarni nad podstawą (świat)

        class Moth { public SpriteRenderer sr; public Lamp lamp; public float phase, speed; }
        readonly List<Moth> moths = new List<Moth>();
        float fishTimer = 25f;
        bool jumping;

        IslandArt Art => Island.Instance != null ? Island.Instance.Art : null;

        void Update()
        {
            var game = GameLoop.Instance;
            var island = Island.Instance;
            if (game == null || island == null || !island.Ready || Save.SaveSystem.InMenu)
                return;
            UpdateMoths(game.Clock.IsNight);
            fishTimer -= Time.unscaledDeltaTime;
            if (fishTimer <= 0f && !jumping)
            {
                fishTimer = Random.Range(18f, 45f);
                StartCoroutine(FishJump(island));
            }
        }

        // ------------------------------------------------------------ ćmy

        void UpdateMoths(bool night)
        {
            var art = Art;
            if (art == null || art.moth == null || art.moth.Length == 0)
                return;
            // ćma na każdą zapaloną lampę (do dwóch), znika o świcie albo gdy lampa zgaśnie
            int want = 0;
            if (night)
                foreach (var l in Lamp.All)
                    if (l != null && l.IsOn) want += MothsPerLamp;
            while (moths.Count < want)
                moths.Add(NewMoth(art));
            int slot = 0;
            foreach (var l in Lamp.All)
            {
                if (!night || l == null || !l.IsOn)
                    continue;
                for (int k = 0; k < MothsPerLamp && slot < moths.Count; k++, slot++)
                    moths[slot].lamp = l;
            }
            float t = Time.unscaledTime;
            for (int i = 0; i < moths.Count; i++)
            {
                var m = moths[i];
                bool on = i < slot && m.lamp != null;
                m.sr.enabled = on;
                if (!on)
                    continue;
                // nerwowy lot wokół światła: dwie różne częstotliwości i drganie skrzydeł
                float a = t * m.speed + m.phase;
                var c = m.lamp.transform.position + new Vector3(0f, LampLight, 0f);
                var p = c + new Vector3(Mathf.Cos(a) * 0.55f + Mathf.Sin(a * 2.7f) * 0.12f,
                                        Mathf.Sin(a * 1.3f) * 0.35f + Mathf.Sin(t * 9f + m.phase) * 0.04f, 0f);
                m.sr.flipX = Mathf.Sin(a) > 0f;
                m.sr.transform.position = p;
                m.sr.sprite = art.moth[Mathf.FloorToInt(t * 14f + m.phase) % art.moth.Length];
            }
        }

        Moth NewMoth(IslandArt art)
        {
            var go = new GameObject("Moth");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.moth[0];
            sr.sortingOrder = 4300;
            return new Moth { sr = sr, phase = Random.value * 6.28f, speed = Random.Range(1.6f, 2.6f) };
        }

        // ------------------------------------------------------------ ryba wyskakuje z wody

        System.Collections.IEnumerator FishJump(Island island)
        {
            var art = Art;
            if (art == null || art.fishJump == null || art.fishJump.Length < 2)
                yield break;
            jumping = true;
            // woda przy brzegu: pod wyspą albo z boku (jak potwór, w pasie wody)
            var rect = island.LandRect;
            Vector3 start;
            float side = Random.value;
            if (side < 0.5f)
                start = island.WaterBelow(Random.Range(10, 22)) + new Vector3(Random.Range(-rect.width * 0.4f, rect.width * 0.4f), 0f, 0f);
            else
                start = new Vector3(side < 0.75f ? rect.xMin - Random.Range(0.8f, 1.4f) : rect.xMax + Random.Range(0.8f, 1.4f),
                                    Random.Range(rect.yMin, rect.yMax), 0f);
            float dir = Random.value < 0.5f ? -1f : 1f;
            var end = start + new Vector3(dir * Random.Range(0.9f, 1.4f), 0f, 0f);
            var go = new GameObject("FishJump");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 800;
            sr.flipX = dir < 0f;
            Splash(start);
            const float duration = 0.9f;
            for (float tt = 0f; tt < duration; tt += Time.unscaledDeltaTime)
            {
                float u = tt / duration;
                go.transform.position = Vector3.Lerp(start, end, u) + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.9f, 0f);
                sr.sprite = u < 0.5f ? art.fishJump[0] : art.fishJump[1];
                yield return null;
            }
            Destroy(go);
            Splash(end);
            jumping = false;
        }

        void Splash(Vector3 at)
        {
            var art = Art;
            IslandFx.TileClaimed(at, art != null ? art.foam : null);   // kilka kropel piany w górę
            GameEvents.PlaySound("bubble", 0.25f);
        }
    }
}
