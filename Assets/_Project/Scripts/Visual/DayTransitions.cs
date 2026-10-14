using System.Collections;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Lighting;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Scenki zmierzchu i świtu (GDD §9 p. 86).
    /// Zmierzch: lampy zapalają się łańcuchem od latarni przy środku wyspy (każda z błyskiem), ćmy nadlatują do światła
    /// (AmbientLife). Świt: mgła stoi pod brzegiem i powoli się cofa (FogController), z drzew zrywa się kilka ptaków
    /// (AmbientBirds), ogniki ziewają (dymek z wielokropkiem). Tylko przy prawdziwej zmianie pory w trakcie gry, nie przy wczytaniu.
    /// </summary>
    public class DayTransitions : MonoBehaviour
    {
        bool? wasNight;
        float ignoreUntil;

        void Start() => ignoreUntil = Time.unscaledTime + 2f;   // wczytanie i przesunięcie zegara zapisu: bez scenek

        void Update()
        {
            var game = GameLoop.Instance;
            if (game == null || game.Clock == null)
                return;
            bool night = game.Clock.IsNight;
            if (wasNight.HasValue && wasNight.Value != night && Time.unscaledTime >= ignoreUntil && !UI.VoyageScene.Playing)
            {
                if (night)
                    Dusk();
                else
                    StartCoroutine(Dawn());
            }
            wasNight = night;
        }

        static Vector2 Center()
        {
            var c = (Vector2)Island.TileCenter(new TilePos(15, 15));
            var lamp = Lamp.NearestTo(c);
            return lamp != null ? (Vector2)lamp.transform.position : c;
        }

        static void Dusk() => Lamp.ChainIgnite(Center(), 0.35f);

        IEnumerator Dawn()
        {
            var fog = FindAnyObjectByType<FogController>();
            if (fog != null)
                fog.Dawn();
            var birds = FindAnyObjectByType<AmbientBirds>();
            if (birds != null)
                birds.DawnFlock(Random.Range(2, 5));
            foreach (var w in Wisp.All.ToArray())
            {
                yield return new WaitForSecondsRealtime(Random.Range(0.2f, 1.2f));
                if (w != null)
                    w.Yawn();
            }
        }
    }
}
