using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Lighting;
using Wyspa.World;

namespace Wyspa.Sea
{
    /// <summary>
    /// Nocne zdarzenia (GDD §4.7): błędne ogniki (klik = Esencja), spadające gwiazdy (odłamek do podniesienia),
    /// rzadko potwór z głębin (ofiara = dar z głębin). Nagradzają, nie karzą: przegapione po prostu znikają.
    /// Latarnia morska podwaja częstość, ołtarz przywołuje potwora.
    /// </summary>
    public class NightEvents : MonoBehaviour
    {
        public static NightEvents Instance { get; private set; }

        const float Ppu = 16f;
        // szanse na minutę nocy
        const float WispPerMinute = 0.15f, StarPerMinute = 0.03f, MonsterPerMinute = 0.005f;

        [SerializeField] Sprite[] wispFrames;
        [SerializeField] Sprite[] wispGlow;
        [SerializeField] Sprite[] starFrames;
        [SerializeField] Sprite[] starGlow;
        [SerializeField] Sprite shard;
        [SerializeField] Sprite shardGlow;
        [SerializeField] Sprite[] tentacle;
        [SerializeField] Sprite[] eye;
        [SerializeField] Sprite eyeGlow;
        [SerializeField] Material glowMaterial;

        readonly List<GameObject> wisps = new List<GameObject>();
        float timer;
        string monsterNight = "";
        MonsterEvent monster;

        public string MonsterNight { get => monsterNight; set => monsterNight = value ?? ""; }

        public void Setup(Sprite[] wispBody, Sprite[] wispGlowFrames, Sprite[] star, Sprite[] starGlowFrames, Sprite shardSprite,
                          Sprite shardGlowSprite, Sprite[] tentacleFrames, Sprite[] eyeFrames, Sprite eyeGlowSprite, Material glow)
        {
            wispFrames = wispBody;
            wispGlow = wispGlowFrames;
            starFrames = star;
            starGlow = starGlowFrames;
            shard = shardSprite;
            shardGlow = shardGlowSprite;
            tentacle = tentacleFrames;
            eye = eyeFrames;
            eyeGlow = eyeGlowSprite;
            glowMaterial = glow;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Latarnia morska świeci: zdarzenia przychodzą dwa razy częściej (GDD §11.3 C).</summary>
        public static bool LighthouseLit
        {
            get
            {
                foreach (var l in Lamp.All)
                    if (l.IsOn && l.Definition != null && l.Definition.id == "lighthouse")
                        return true;
                return false;
            }
        }

        static bool HasAltar => Island.Instance != null && Island.Instance.HasBuilding("altar");

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 1f;
            var game = GameLoop.Instance;
            var island = Island.Instance;
            if (game == null || island == null || !island.Ready || !game.Clock.IsNight)
                return;
            wisps.RemoveAll(w => w == null);
            float mult = LighthouseLit ? 2f : 1f;
            if (wisps.Count < 2 && Random.value < WispPerMinute / 60f * mult)
                SpawnWisp();
            if (Random.value < StarPerMinute / 60f * mult)
                SpawnStar();
            string night = NightKey(game.Clock.Now);
            if (monster == null && monsterNight != night && Random.value < MonsterPerMinute / 60f * mult * (HasAltar ? 3f : 1f))
                SpawnMonster();
        }

        static string NightKey(System.DateTime now) => (now.Hour < 12 ? now.Date.AddDays(-1) : now.Date).ToString("yyyy-MM-dd");

        // ------------------------------------------------------------- zdarzenia (także z HUD debugowego)

        /// <summary>Punkt nad wodą tuż przy lądzie (na obrzeżu prostokąta wyspy).</summary>
        static Vector3 NearShore(float outside)
        {
            var r = Island.Instance.LandRect;
            float t = Random.value;
            switch (Random.Range(0, 4))
            {
                case 0: return new Vector3(Mathf.Lerp(r.xMin, r.xMax, t), r.yMax + outside, 0f);
                case 1: return new Vector3(Mathf.Lerp(r.xMin, r.xMax, t), r.yMin - outside, 0f);
                case 2: return new Vector3(r.xMin - outside, Mathf.Lerp(r.yMin, r.yMax, t), 0f);
                default: return new Vector3(r.xMax + outside, Mathf.Lerp(r.yMin, r.yMax, t), 0f);
            }
        }

        public void SpawnWisp()
        {
            if (Island.Instance == null || wispFrames == null || wispFrames.Length == 0)
                return;
            var go = new GameObject("WillOWisp");
            go.transform.SetParent(transform, false);
            go.transform.position = NearShore(0.6f);
            go.AddComponent<WillOWisp>().Setup(wispFrames, wispGlow, glowMaterial);
            wisps.Add(go);
        }

        public void SpawnStar()
        {
            var island = Island.Instance;
            if (island == null || starFrames == null || starFrames.Length == 0)
                return;
            // spada na losowy kafelek lądu
            var tiles = new List<TilePos>(island.Map.Tiles.Keys);
            var target = Island.TileAnchor(tiles[Random.Range(0, tiles.Count)], 10, out int order);
            var go = new GameObject("FallingStar");
            go.transform.SetParent(transform, false);
            go.AddComponent<StarPickup>().Setup(target, order, starFrames, starGlow, shard, shardGlow, glowMaterial);
        }

        public void SpawnMonster()
        {
            var island = Island.Instance;
            var game = GameLoop.Instance;
            if (island == null || monster != null || tentacle == null || tentacle.Length == 0)
                return;
            if (game != null)
                monsterNight = NightKey(game.Clock.Now);
            var go = new GameObject("Monster");
            go.transform.SetParent(transform, false);
            go.transform.position = island.WaterBelow(30) + new Vector3(Random.Range(-2f, 2f), 0f, 0f);
            monster = go.AddComponent<MonsterEvent>();
            monster.Setup(tentacle, eye, eyeGlow, glowMaterial);
            GameEvents.RaiseToast("toast.monster.rises");
        }
    }
}
