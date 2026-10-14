using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Weather
{
    public enum WeatherKind { Clear, Rain, Storm, Mist }

    /// <summary>
    /// Pogoda (wybór gracza po Etapie 6d). Cykl gry dzielimy na trzy części po 20 min (dwie w dzień, jedna w nocy); każda losuje
    /// pogodę z numeru cyklu (ta sama po restarcie, bez zapisu). Deszcz: rośliny dnia +20%, krople nad wyspą, trochę ciemniej,
    /// częstszy wiatr. Burza: ulewa, błyski piorunów, czasem piorun zostawia odłamek gwiazdy. Mgła (tylko noc): gęstsza mgła,
    /// błędne ogniki częściej. Krople padają tylko nad lądem, więc w przezroczystym oknie nie leje na pulpit.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        const float Ppu = 16f;
        const int MaxDrops = 140;

        public static WeatherSystem Instance { get; private set; }
        public static WeatherKind Current { get; private set; } = WeatherKind.Clear;

        // ---- mnożniki dla innych systemów
        public static bool Raining => Current == WeatherKind.Rain || Current == WeatherKind.Storm;
        public static float DayGrowth => Raining ? 1.2f : 1f;
        public static float FogMultiplier => Current == WeatherKind.Mist ? 1.6f : Raining ? 1.15f : 1f;
        public static float WispMultiplier => Current == WeatherKind.Mist ? 2f : 1f;
        public static float WindMultiplier => Current == WeatherKind.Storm ? 2.5f : Current == WeatherKind.Rain ? 1.6f : 1f;
        /// <summary>Jasność otoczenia (deszcz przyciemnia) i błysk pioruna - czyta DayNightLighting.</summary>
        public static float LightMultiplier { get; private set; } = 1f;
        public static float Flash { get; private set; }

        /// <summary>Do testów (HUD debugowy): wymuszona pogoda, null = z cyklu.</summary>
        public static WeatherKind? DebugForce;

        class Drop { public SpriteRenderer sr; public Vector2 pos; public float endY; public float splash; public bool alive; }

        readonly List<Drop> drops = new List<Drop>();
        Sprite dropSprite, splashSprite;
        float spawnAcc;
        float nextBolt = 8f;
        float flashTime = 10f;   // bez błysku na starcie
        long lastSegmentKey = long.MinValue;
        Transform root;

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Current = WeatherKind.Clear;
            LightMultiplier = 1f;
            Flash = 0f;
        }

        static Sprite MakeSprite(int w, int h, Color32 c)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu);
        }

        /// <summary>Pogoda dla części cyklu (0, 1 = dzień, 2 = noc): stałe losowanie z numeru cyklu.</summary>
        static WeatherKind Roll(long cycle, int segment)
        {
            var rnd = new WRandom(cycle * 3 + segment + 7919);
            double r = rnd.Range(0.0, 1.0);
            bool night = segment == 2;
            if (r < 0.08) return WeatherKind.Storm;
            if (r < 0.30) return WeatherKind.Rain;
            if (night && r < 0.48) return WeatherKind.Mist;
            return WeatherKind.Clear;
        }

        void Update()
        {
            var game = GameLoop.Instance;
            var island = Island.Instance;
            if (game == null || island == null || !island.Ready)
                return;
            float dt = Time.unscaledDeltaTime;
            int segment = Mathf.Min(2, Mathf.FloorToInt(game.Clock.Second / 1200f));
            long key = game.Clock.CycleNumber * 3 + segment;
            var next = DebugForce ?? Roll(game.Clock.CycleNumber, segment);
            if (key != lastSegmentKey || next != Current)
            {
                bool announce = lastSegmentKey != long.MinValue && next != Current && next != WeatherKind.Clear;
                lastSegmentKey = key;
                Current = next;
                if (announce)
                    GameEvents.RaiseToast("toast.weather." + next.ToString().ToLowerInvariant());
            }

            // światło: deszcz przyciemnia, piorun błyska (krótko, dwa razy)
            float targetLight = Current == WeatherKind.Storm ? 0.75f : Current == WeatherKind.Rain ? 0.88f : 1f;
            LightMultiplier = Mathf.MoveTowards(LightMultiplier, targetLight, dt * 0.2f);
            if (Current == WeatherKind.Storm)
            {
                nextBolt -= dt;
                if (nextBolt <= 0f)
                {
                    nextBolt = Random.Range(12f, 35f);
                    flashTime = 0f;
                    GameEvents.PlaySound("thunder", 0.8f);
                    // co czwarty piorun zostawia odłamek gwiazdy (spada jak spadająca gwiazda)
                    if (Random.value < 0.25f && Sea.NightEvents.Instance != null)
                        Sea.NightEvents.Instance.SpawnStar();
                }
            }
            flashTime += dt;
            Flash = flashTime < 0.08f || (flashTime > 0.18f && flashTime < 0.24f) ? 0.9f : 0f;

            UpdateRain(island, dt);
        }

        void UpdateRain(Island island, float dt)
        {
            int rate = Current == WeatherKind.Storm ? 90 : Current == WeatherKind.Rain ? 40 : 0;   // kropel na sekundę
            if (dropSprite == null)
            {
                dropSprite = MakeSprite(1, 3, new Color32(170, 190, 220, 160));
                splashSprite = MakeSprite(3, 1, new Color32(170, 190, 220, 140));
            }
            if (root == null)
            {
                root = new GameObject("Rain").transform;
                root.SetParent(transform, false);
            }
            var land = island.LandRect;
            spawnAcc += rate * dt;
            while (spawnAcc >= 1f)
            {
                spawnAcc -= 1f;
                var d = GetDrop();
                if (d == null)
                    break;
                // start nad lądem (trochę na lewo, bo krople lecą ukośnie w prawo), koniec w losowym miejscu lądu
                float endX = Random.Range(land.xMin, land.xMax) * Ppu;
                d.endY = Random.Range(land.yMin + 0.2f, land.yMax) * Ppu;
                float fall = Random.Range(40f, 80f);
                d.pos = new Vector2(endX - fall * 0.35f, d.endY + fall);
                d.splash = 0f;
                d.alive = true;
                d.sr.sprite = dropSprite;
                d.sr.sortingOrder = 4400;
                d.sr.gameObject.SetActive(true);
            }
            float speed = Current == WeatherKind.Storm ? 210f : 160f;
            foreach (var d in drops)
            {
                if (!d.alive)
                    continue;
                if (d.splash > 0f)
                {
                    d.splash -= dt;
                    if (d.splash <= 0f) { d.alive = false; d.sr.gameObject.SetActive(false); }
                    continue;
                }
                d.pos += new Vector2(0.35f, -1f) * speed * dt;
                if (d.pos.y <= d.endY)
                {
                    d.pos.y = d.endY;
                    d.splash = 0.12f;                  // krótki rozprysk na ziemi
                    d.sr.sprite = splashSprite;
                }
                d.sr.transform.position = new Vector3(Mathf.Round(d.pos.x) / Ppu, Mathf.Round(d.pos.y) / Ppu, 0f);
            }
        }

        Drop GetDrop()
        {
            foreach (var d in drops)
                if (!d.alive) return d;
            if (drops.Count >= MaxDrops)
                return null;
            var go = new GameObject("Drop");
            go.transform.SetParent(root, false);
            var drop = new Drop { sr = go.AddComponent<SpriteRenderer>() };
            drops.Add(drop);
            return drop;
        }
    }
}
