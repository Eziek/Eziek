using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Podaje shaderowi Wyspa/Water mapę danych wody (malowaną przez Island), ścieżki ryb, miejsce potwora
    /// i porę dnia (świecące ryby i oko potwora tylko w nocy). Co klatkę także niebo nad wodą (GDD §9 p. 119):
    /// księżycową ścieżkę (gdzie jest księżyc i jak jasno świeci), deszcz (kółka od kropli) i słońce (refleksy na płyciznach).
    /// Wartości zmieniają się płynnie (kilka sekund), np. gdy zaczyna padać.
    /// </summary>
    public class WaterAnimator : MonoBehaviour
    {
        const int MaxFish = 16;   // rozmiar tablic w Water.shader

        static readonly int DataTexId = Shader.PropertyToID("_DataTex");
        static readonly int WaterDataGlobalId = Shader.PropertyToID("_WyspaWaterData");
        static readonly int WaterSizeGlobalId = Shader.PropertyToID("_WyspaWaterSize");
        static readonly int FishId = Shader.PropertyToID("_Fish");
        static readonly int FishBId = Shader.PropertyToID("_FishB");
        static readonly int FishCountId = Shader.PropertyToID("_FishCount");
        static readonly int BeastId = Shader.PropertyToID("_Beast");
        static readonly int NightId = Shader.PropertyToID("_Night");
        static readonly int MoonPathId = Shader.PropertyToID("_WyspaMoonPath");
        static readonly int RainId = Shader.PropertyToID("_WyspaRain");
        static readonly int SunlightId = Shader.PropertyToID("_WyspaSunlight");
        static readonly int CloudShadowId = Shader.PropertyToID("_WyspaCloudShadow");
        static readonly int MorningMistId = Shader.PropertyToID("_WyspaMorningMist");
        static readonly int ShoreRangeGlobalId = Shader.PropertyToID("_WyspaShoreRange");
        static readonly int WinterId = Shader.PropertyToID("_WyspaWinter");

        [SerializeField] SpriteRenderer water;
        [SerializeField] GameClock clock;

        static readonly int FishPeriodId = Shader.PropertyToID("_FishPeriod");

        Material material;
        float timer;
        float moonPath, rain, sunlight, cloudShadow, morningMist, winter;

        /// <summary>Przezroczyste okno: woda widoczna tylko do tylu pikseli od brzegu (0 = całe morze).</summary>
        public float ShoreRange => shoreRange;
        readonly List<FishPath> fish = new List<FishPath>();
        float[] hiddenUntil = new float[0];
        bool anyHidden;

        /// <summary>Sprite morza (przezroczyste okno go chowa).</summary>
        public SpriteRenderer Renderer => water;

        public void Setup(SpriteRenderer waterRenderer, GameClock gameClock)
        {
            water = waterRenderer;
            clock = gameClock;
        }

        // kopia materiału tylko dla tej sceny (tablice i tekstura z kodu nie zapisują się w assecie)
        Material EnsureMaterial()
        {
            if (material != null || water == null)
                return material;
            var swap = water.GetComponent<FxMaterial>();
            material = new Material(swap != null && swap.Fx != null ? swap.Fx : water.sharedMaterial);
            if (swap != null)
                swap.SetFxMaterial(material);
            else
                water.sharedMaterial = material;
            return material;
        }

        static readonly int ShoreRangeId = Shader.PropertyToID("_ShoreRange");
        float shoreRange;

        /// <summary>Przezroczyste okno: woda tylko w pasie przy brzegu (piksele od lądu, 0 = całe morze).</summary>
        public void SetShoreOnly(float pixels)
        {
            shoreRange = Mathf.Max(0f, pixels);
            Shader.SetGlobalFloat(ShoreRangeGlobalId, shoreRange);   // atmosfera (Atmosphere.shader) nie maluje pulpitu
            var m = EnsureMaterial();
            if (m != null)
                m.SetFloat(ShoreRangeId, shoreRange);
        }

        /// <summary>Nowy obraz wyspy: mapa danych, ryby i potwór.</summary>
        public void SetData(Texture2D data, IslandPicture picture)
        {
            var m = EnsureMaterial();
            if (m == null)
                return;
            m.SetTexture(DataTexId, data);
            m.SetFloat(ShoreRangeId, shoreRange);
            // maska wody dla odbić (WaterReflection.shader)
            Shader.SetGlobalTexture(WaterDataGlobalId, data);
            if (data != null)
                Shader.SetGlobalVector(WaterSizeGlobalId, new Vector4(data.width, data.height, 0f, 0f));
            fish.Clear();
            if (picture != null)
                foreach (var f in picture.fish)
                    if (fish.Count < MaxFish)
                        fish.Add(f);
            hiddenUntil = new float[fish.Count];
            PushFish();
            m.SetVector(BeastId, picture != null && picture.hasBeast
                ? new Vector4(picture.beastX, picture.beastY, 1f, 0f) : Vector4.zero);
            UpdateNight();
        }

        /// <summary>Ryby do shadera; złowione (ukryte) odpływają poza obraz, aż wrócą.</summary>
        void PushFish()
        {
            if (material == null)
                return;
            var a = new Vector4[MaxFish];
            var b = new Vector4[MaxFish];
            float now = Time.timeSinceLevelLoad;
            anyHidden = false;
            for (int i = 0; i < fish.Count; i++)
            {
                var f = fish[i];
                bool hidden = hiddenUntil[i] > now;
                anyHidden |= hidden;
                a[i] = new Vector4(hidden ? -1000f : f.cx, f.cy, f.rx, f.ry);
                b[i] = new Vector4(f.phase, f.glow ? 1f : 0f, f.kind, Mathf.Max(0.1f, f.slow));
            }
            material.SetVectorArray(FishId, a);
            material.SetVectorArray(FishBId, b);
            material.SetFloat(FishCountId, fish.Count);
        }

        /// <summary>
        /// Środek ryby w świecie - ta sama rachuba co w Water.shader (płynny czas, ósemka, zaokrąglenie do pikseli),
        /// więc klik trafia tam, gdzie gracz widzi rybę.
        /// </summary>
        Vector2 FishCenter(int i)
        {
            var f = fish[i];
            float period = material != null && material.HasProperty(FishPeriodId) ? material.GetFloat(FishPeriodId) : 30f;
            float t = Time.timeSinceLevelLoad;   // jak _Time.y w shaderze
            float a = 2f * Mathf.PI * Frac(t / Mathf.Max(1f, period * Mathf.Max(0.1f, f.slow))) + f.phase;
            float fx = Mathf.Round(f.cx + f.rx * Mathf.Sin(a));
            float fy = Mathf.Round(f.cy + f.ry * Mathf.Sin(2f * a));
            return Island.PixelToWorld(fx + FishKinds.CenterX(f.kind), fy + FishKinds.CenterY(f.kind));
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>Najbliższa widoczna ryba przy punkcie (do 10 px) i w zasięgu od środka łowiska. -1 = brak.</summary>
        public int FishAt(Vector2 world, Vector2 center, float range)
        {
            int best = -1;
            float bestD = 10f / 16f;
            float now = Time.timeSinceLevelLoad;
            for (int i = 0; i < fish.Count; i++)
            {
                if (hiddenUntil[i] > now || !FishKinds.Catchable(fish[i].kind))
                    continue;   // meduzy się nie łowi
                var c = FishCenter(i);
                float d = Vector2.Distance(c, world);
                if (d <= bestD && Vector2.Distance(c, center) <= range)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Ryba pod kursorem (do podpisu i „Widzianego”): indeks albo -1. Także meduza.</summary>
        public int AnyFishAt(Vector2 world)
        {
            float now = Time.timeSinceLevelLoad;
            for (int i = 0; i < fish.Count; i++)
            {
                if (hiddenUntil != null && i < hiddenUntil.Length && hiddenUntil[i] > now)
                    continue;
                float r = Mathf.Max(0.4f, FishKinds.Width(fish[i].kind) / 2f / 16f + 0.15f);
                if (Vector2.Distance(FishCenter(i), world) <= r)
                    return i;
            }
            return -1;
        }

        /// <summary>Stały klucz ryby (do rozpoznania tego samego spotkania).</summary>
        public object FishKey(int i) => i;

        public int KindOf(int i) => i >= 0 && i < fish.Count ? fish[i].kind : 0;

        public bool IsGlowing(int i) => i >= 0 && i < fish.Count && fish[i].glow;
        public Vector2 FishPosition(int i) => FishCenter(i);

        float[] dash;   // ryby spłoszone kliknięciem: pozostały zryw (radiany fazy)

        /// <summary>Klik w wodę: ryby w promieniu dają nura - przez chwilę płyną kilka razy szybciej po swojej ścieżce.</summary>
        public void Scare(Vector2 world, float radius)
        {
            if (dash == null || dash.Length != fish.Count)
                dash = new float[fish.Count];
            for (int i = 0; i < fish.Count; i++)
                if (Vector2.Distance(FishCenter(i), world) <= radius)
                    dash[i] = 1.6f;
        }

        void UpdateDash()
        {
            if (dash == null || dash.Length != fish.Count)
                return;
            bool any = false;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < dash.Length; i++)
            {
                if (dash[i] <= 0f)
                    continue;
                float step = Mathf.Min(dash[i], dt * 3.2f);
                dash[i] -= step;
                var f = fish[i];
                f.phase += step;
                fish[i] = f;
                any = true;
            }
            if (any)
                PushFish();
        }

        /// <summary>Złowiona ryba znika na chwilę (wraca po seconds).</summary>
        public void Hide(int i, float seconds)
        {
            if (i < 0 || i >= fish.Count)
                return;
            hiddenUntil[i] = Time.timeSinceLevelLoad + seconds;
            PushFish();
        }

        void Update()
        {
            UpdateSky();
            UpdateDash();
            if (anyHidden)
            {
                float now = Time.timeSinceLevelLoad;
                bool expired = false;
                for (int i = 0; i < hiddenUntil.Length; i++)
                    if (hiddenUntil[i] > 0f && hiddenUntil[i] <= now)
                    {
                        hiddenUntil[i] = 0f;
                        expired = true;
                    }
                if (expired)
                    PushFish();
            }
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 1f;
            UpdateNight();
        }

        /// <summary>
        /// Księżyc wędruje nocą z prawej na lewą jak słońce w dzień (SunShadows), nad środkiem lądu ±9 kafelków; ścieżka
        /// jaśniejsza w pełnię, w deszczu znika, we mgle słabnie. Deszcz: ulewa 0.6, burza 1. Słońce: od wysokości, w deszczu słabiej.
        /// </summary>
        void UpdateSky()
        {
            if (clock == null)
                return;
            float dt = Time.unscaledDeltaTime;
            var weather = Weather.WeatherSystem.Current;
            bool raining = Weather.WeatherSystem.Raining;
            float sec = clock.Second;
            float moonX = 0f, moonTarget = 0f, sunTarget = 0f;
            if (clock.IsNight)
            {
                float u = Mathf.Clamp01((sec - GameClock.DaySeconds) / GameClock.NightSeconds);
                moonX = Mathf.Cos(u * Mathf.PI);
                float full = 1f - Mathf.Abs(clock.MoonPhase - 0.5f) * 2f;
                moonTarget = (0.25f + 0.75f * full) * Mathf.Clamp01(Mathf.Sin(u * Mathf.PI) * 4f)
                           * (raining ? 0f : weather == Weather.WeatherKind.Mist ? 0.4f : 1f);
            }
            else
            {
                float u = Mathf.Clamp01(sec / GameClock.DaySeconds);
                sunTarget = Mathf.Clamp01(Mathf.Sin(u * Mathf.PI) / 0.35f) * (raining ? 0.2f : 1f);
            }
            bool snowy = Seasons.Current == Season.Winter;
            // zimą pada śnieg: płatki prawie nie zostawiają kółek na wodzie
            float rainTarget = (weather == Weather.WeatherKind.Storm ? 1f : raining ? 0.6f : 0f) * (snowy ? 0.2f : 1f);
            winter = Mathf.MoveTowards(winter, snowy ? 1f : 0f, dt * 0.05f);   // lód przy brzegu narasta przez ok. 20 s
            // cienie chmur: w dzień przy pogodzie (w deszczu niebo całe zasnute); mgiełka: od 6:00 do 8:00, najgęstsza o świcie
            float shadowTarget = !clock.IsNight && !raining ? 1f : 0f;
            float mistTarget = 0f;
            if (!clock.IsNight && sec < GameClock.DawnEndSecond)
                mistTarget = (1f - sec / GameClock.DawnEndSecond) * (raining ? 0.6f : 1f);
            cloudShadow = Mathf.MoveTowards(cloudShadow, shadowTarget, dt * 0.2f);
            morningMist = Mathf.MoveTowards(morningMist, mistTarget, dt * 0.2f);
            moonPath = Mathf.MoveTowards(moonPath, moonTarget, dt * 0.25f);
            sunlight = Mathf.MoveTowards(sunlight, sunTarget, dt * 0.25f);
            rain = Mathf.MoveTowards(rain, rainTarget, dt * 0.3f);
            var island = Island.Instance;
            Vector2 center = island != null && island.Ready ? island.LandRect.center : Vector2.zero;
            float px = Island.WorldToPixel(new Vector2(center.x + moonX * 9f, 0f)).x;   // piksele mapy danych
            Shader.SetGlobalVector(MoonPathId, new Vector4(px, moonPath, 0f, 0f));
            Shader.SetGlobalFloat(RainId, rain);
            Shader.SetGlobalFloat(SunlightId, sunlight);
            Shader.SetGlobalFloat(CloudShadowId, cloudShadow);
            Shader.SetGlobalFloat(MorningMistId, morningMist);
            Shader.SetGlobalFloat(WinterId, winter);
        }

        void UpdateNight()
        {
            if (material != null)
                material.SetFloat(NightId, clock != null && clock.IsNight ? 1f : 0f);
        }

        void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
