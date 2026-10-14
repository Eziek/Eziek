using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.Farming
{
    /// <summary>Gatunek drzewa owocowego (GDD §15.1). Liczby wstępne, do balansu.</summary>
    public class TreeSpecies
    {
        public string id;
        public int saplingCoins;
        public CurrencyAmount extraCost;         // np. odłamki gwiazd za drzewo księżyca
        public float growSeconds;                // sekundy aktywnej pracy do dorosłego drzewa
        public float fruitSeconds;               // odrastanie owoców (warunek zależny od gatunku)
        public int fruitCount, fruitPrice;
        public bool fruitsAtNight;               // upiorna grusza: owoce dojrzewają w ciemności
        public bool fruitsAtFullMoon;            // drzewo księżyca: tylko w pełnię
        public bool cemeteryBonus;               // krwawa jarzębina: na cmentarzu rośnie x1.5
        public Color lightColor;                 // świecące owoce (w nocy, gdy owocuje)
        public float lightRadius;                // w kafelkach, 0 = nie świeci

        public string NameKey => "tree." + id;
        public string FruitKey => "fruit." + id;
        public bool Glows => lightRadius > 0f;

        public static readonly TreeSpecies[] All =
        {
            new TreeSpecies { id = "apple", saplingCoins = 120, growSeconds = 2 * 3600, fruitSeconds = 30 * 60, fruitCount = 4, fruitPrice = 8 },
            new TreeSpecies { id = "elder", saplingCoins = 200, growSeconds = 2.5f * 3600, fruitSeconds = 40 * 60, fruitCount = 5, fruitPrice = 10 },
            new TreeSpecies { id = "rowan", saplingCoins = 250, growSeconds = 3 * 3600, fruitSeconds = 45 * 60, fruitCount = 6, fruitPrice = 9, cemeteryBonus = true },
            new TreeSpecies { id = "ghostpear", saplingCoins = 500, extraCost = new CurrencyAmount(CurrencyType.NightHarvest, 5),
                              growSeconds = 4 * 3600, fruitSeconds = 2 * 3600, fruitCount = 4, fruitPrice = 30, fruitsAtNight = true,
                              lightColor = new Color(0.45f, 1.15f, 1.05f), lightRadius = 34f / 16f },
            new TreeSpecies { id = "moon", saplingCoins = 800, extraCost = new CurrencyAmount(CurrencyType.StarShard, 2),
                              growSeconds = 5 * 3600, fruitSeconds = 3600, fruitCount = 3, fruitPrice = 120, fruitsAtNight = true, fruitsAtFullMoon = true,
                              lightColor = new Color(0.9f, 0.85f, 1.25f), lightRadius = 40f / 16f },
        };

        public static TreeSpecies Find(string id)
        {
            foreach (var s in All)
                if (s.id == id)
                    return s;
            return null;
        }

        public List<CurrencyAmount> Cost()
        {
            var list = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, saplingCoins) };
            if (extraCost.amount > 0)
                list.Add(extraCost);
            return list;
        }
    }

    /// <summary>Sprite'y gatunku (IslandArt): fazy 0-3 (sadzonka, młode, dorosłe, owocujące), świecenie fazy 3, przewracanie.</summary>
    [System.Serializable]
    public class TreeArt
    {
        public string id;
        public Sprite[] stages = new Sprite[4];
        public Sprite fruitingGlow;
        public Sprite[] fall = new Sprite[4];
        [Tooltip("Owoc spadający przy zbiorze (ikona owocu).")]
        public Sprite fruit;
    }

    /// <summary>Klik w obiekt, który chce obsłużyć go sam zamiast panelu (np. drzewo z owocami: zbiór od razu).</summary>
    public interface IClickShortcut
    {
        bool TryClick();
    }

    /// <summary>
    /// Drzewo owocowe (GDD §15.1, prośba gracza): sadzonka rośnie od pracy przy komputerze (jak rośliny dnia, z tempem pracy),
    /// dorosłe owocuje cyklicznie. Klik w owocujące drzewo = zbiór (monety). Panel: stan, „Zetnij” (drewno, drzewo znika).
    /// Upiorna grusza i drzewo księżyca, gdy owocują w nocy, świecą jak mała lampa (liczy się do wzrostu roślin).
    /// </summary>
    public class FruitTree : MonoBehaviour, IContextActions, IClickShortcut
    {
        public static readonly List<FruitTree> All = new List<FruitTree>();

        TreeSpecies species;
        TreeArt art;
        SpriteRenderer body;
        SpriteRenderer glow;
        Light2D light2D;
        float growth;
        float fruit;
        bool chopping;
        bool chopArmed;
        bool glowing;
        Coroutine bounce;

        public TreeSpecies Species => species;
        public float Growth => growth;
        public float FruitProgress => fruit;
        public bool Adult => species != null && growth >= species.growSeconds;
        public bool Fruiting => Adult && fruit >= species.fruitSeconds;
        public bool IsGlowing => glowing;

        public void Setup(TreeSpecies treeSpecies, TreeArt treeArt, SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer,
                          float savedGrowth = 0f, float savedFruit = 0f)
        {
            species = treeSpecies;
            art = treeArt;
            body = bodyRenderer;
            glow = glowRenderer;
            growth = savedGrowth;
            fruit = savedFruit;
            if (species != null && species.Glows)
            {
                var go = new GameObject("FruitLight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                light2D = go.AddComponent<Light2D>();
                light2D.lightType = Light2D.LightType.Point;
                light2D.color = species.lightColor;
                light2D.pointLightOuterRadius = species.lightRadius;
                light2D.pointLightInnerRadius = species.lightRadius * 0.2f;
                light2D.falloffIntensity = 0.6f;
                light2D.intensity = 0.9f;
                light2D.volumeIntensityEnabled = true;
                light2D.volumeIntensity = 0.12f;
                light2D.enabled = false;
            }
            RefreshVisual();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        int Stage => !Adult ? (growth < species.growSeconds * 0.25f ? 0 : 1) : (Fruiting ? 3 : 2);

        /// <summary>Tick z GameLoop. active = gracz pracuje; multiplier = tempo pracy i bonus strefy.</summary>
        public void Tick(float dt, bool active, bool night, bool fullMoon, float multiplier)
        {
            if (species == null || chopping)
                return;
            int before = Stage;
            if (!Adult)
            {
                if (active)
                    growth = Mathf.Min(species.growSeconds, growth + dt * multiplier);
            }
            else if (!Fruiting)
            {
                bool grows = species.fruitsAtFullMoon ? night && fullMoon : species.fruitsAtNight ? night : active;
                if (grows)
                    fruit = Mathf.Min(species.fruitSeconds, fruit + dt * (species.fruitsAtNight ? 1f : multiplier));
            }
            bool shouldGlow = species.Glows && Fruiting && night;
            if (Stage != before || shouldGlow != glowing)
            {
                glowing = shouldGlow;
                RefreshVisual();
                if (Stage != before)
                {
                    IslandFx.Grew(transform.position + new Vector3(0f, 0.6f, 0f), body != null ? body.sortingOrder : 0);
                    Bounce(0.14f, 0.6f);   // nowa faza: sprężyste „pyknięcie”, jak przy roślinach
                }
            }
        }

        void RefreshVisual()
        {
            if (art == null || body == null)
                return;
            int st = Stage;
            if (art.stages != null && st < art.stages.Length && art.stages[st] != null)
                body.sprite = art.stages[st];
            if (glow != null)
            {
                glow.sprite = art.fruitingGlow;
                glow.enabled = st == 3 && art.fruitingGlow != null;
            }
            if (light2D != null)
                light2D.enabled = glowing;
        }

        /// <summary>Świecące owoce oświetlają okolicę jak lampa (GDD §15.1): rośliny dnia rosną w ich zasięgu.</summary>
        public bool Illuminates(Vector2 point)
        {
            if (!glowing || species == null)
                return false;
            var c = (Vector2)transform.position + new Vector2(0f, 1.3f);
            return (point - c).sqrMagnitude <= species.lightRadius * species.lightRadius;
        }

        /// <summary>Do testów (HUD debugowy): od razu dorosłe i z owocami.</summary>
        public void DebugMature()
        {
            if (species == null)
                return;
            growth = species.growSeconds;
            fruit = species.fruitSeconds;
            RefreshVisual();
        }

        public bool TryClick()
        {
            if (!Fruiting || chopping)
                return false;
            HarvestFruit();
            return true;
        }

        void HarvestFruit()
        {
            var game = GameLoop.Instance;
            if (game == null || !Fruiting)
                return;
            int coins = species.fruitCount * species.fruitPrice;
            game.Wallet.Add(coins);
            var at = transform.position + new Vector3(0f, 1.2f, 0f);
            GameEvents.RaiseCurrencyGained(at, CurrencyType.Coins, coins);
            GameEvents.RaiseToast("toast.fruit", species.fruitCount, Loc.Get(species.FruitKey), coins);
            // owoce spadają z korony, drzewo lekko się otrząsa (bez podskakiwania całego drzewa)
            IslandFx.FruitDrop(at, 18f, art != null ? art.fruit : null, Mathf.Min(species.fruitCount, 5), body != null ? body.sortingOrder : 0);
            Bounce(0.06f, 0.5f);
            Lore.Chronicle.Note("tree:" + species.id);
            Lore.Chronicle.Count("harvest", species.fruitCount);
            fruit = 0f;
            glowing = false;
            RefreshVisual();
        }

        /// <summary>Sprężyste kołysanie skali od podstawy (pivot sprite'a jest u dołu), gasnące w czasie.</summary>
        void Bounce(float strength, float duration)
        {
            if (!isActiveAndEnabled)
                return;
            if (bounce != null)
                StopCoroutine(bounce);
            bounce = StartCoroutine(BounceRoutine(strength, duration));
        }

        IEnumerator BounceRoutine(float strength, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                float k = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u) * strength;
                transform.localScale = new Vector3(1f + k * 0.6f, 1f - k, 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            bounce = null;
        }

        int Wood => Adult ? 6 : (Stage == 1 ? 2 : 0);

        public void FillContext(ContextInfo info)
        {
            if (species == null)
                return;
            if (!Adult)
                info.lines.Add(Loc.Get("ui.tree.growing", Mathf.FloorToInt(100f * growth / species.growSeconds)));
            else if (Fruiting)
                info.lines.Add(Loc.Get("ui.tree.fruiting", species.fruitCount, Loc.Get(species.FruitKey)));
            else
            {
                int pct = Mathf.FloorToInt(100f * fruit / species.fruitSeconds);
                string when = species.fruitsAtFullMoon ? Loc.Get("ui.tree.when_fullmoon")
                            : species.fruitsAtNight ? Loc.Get("ui.tree.when_night") : Loc.Get("ui.tree.when_work");
                info.lines.Add(Loc.Get("ui.tree.fruit_progress", pct) + " " + when);
            }
            if (Fruiting)
                info.Button(Loc.Get("ui.tree.harvest", species.fruitCount * species.fruitPrice), HarvestFruit);
            if (chopArmed)
                info.Button(Loc.Get("ui.tree.chop_confirm", Wood), () => { chopArmed = false; Chop(); });
            else
                info.Button(Loc.Get("ui.tree.chop", Wood), () => { chopArmed = true; StartCoroutine(Reopen()); });
        }

        IEnumerator Reopen()
        {
            yield return null;   // przycisk zamyka panel; otwieramy go od nowa z potwierdzeniem
            var obj = GetComponent<TileObject>();
            if (obj != null)
                GameEvents.RequestContext(obj);
            yield return new WaitForSecondsRealtime(5f);
            chopArmed = false;
        }

        /// <summary>Ścięcie: drzewo przewraca się (klatki z prototypu), drewno do portfela, kafelek wolny.</summary>
        public void Chop()
        {
            if (chopping)
                return;
            chopping = true;
            StartCoroutine(ChopRoutine());
        }

        IEnumerator ChopRoutine()
        {
            var basePos = transform.position;
            if (glow != null) glow.enabled = false;
            if (light2D != null) light2D.enabled = false;
            int order = body != null ? body.sortingOrder : 0;
            // trzy uderzenia siekierą: drzewo drży, sypią się liście, potem przewraca się coraz szybciej
            for (int hit = 0; hit < 3; hit++)
            {
                IslandFx.LeafBurst(basePos + new Vector3(0f, 1.4f, 0f), 2, order);
                for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.Sin(t * 70f) * (1f - t / 0.22f);
                    transform.position = basePos + new Vector3(k * 1.2f / 16f, 0f, 0f);
                    yield return null;
                }
                transform.position = basePos;
                yield return new WaitForSecondsRealtime(0.12f);
            }
            if (Stage >= 1 && art != null && art.fall != null)
            {
                float[] times = { 0.16f, 0.12f, 0.08f, 0.06f };   // przyspiesza jak padające drzewo
                for (int i = 0; i < art.fall.Length; i++)
                {
                    if (art.fall[i] != null && body != null) body.sprite = art.fall[i];
                    yield return new WaitForSecondsRealtime(times[Mathf.Min(i, times.Length - 1)]);
                }
                IslandFx.LeafBurst(basePos + new Vector3(1.2f, 0.4f, 0f), 7, order);
            }
            IslandFx.Planted(basePos, order);
            var game = GameLoop.Instance;
            int wood = Wood;
            if (game != null && wood > 0)
            {
                game.Wallet.Add(CurrencyType.Wood, wood);
                GameEvents.RaiseCurrencyGained(basePos + new Vector3(0f, 0.5f, 0f), CurrencyType.Wood, wood);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            var island = Island.Instance;
            var obj = GetComponent<TileObject>();
            if (island != null && obj != null)
                island.RemoveObject(obj);
            else
                Destroy(gameObject);
        }
    }
}
