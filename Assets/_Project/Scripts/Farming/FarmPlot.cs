using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;
using Wyspa.UI;
using Wyspa.Visual;

namespace Wyspa.Farming
{
    /// <summary>Grządka: klik na pustą = koło nasion (albo wybrane nasiono, gdy nie ma UI), klik na dojrzałą = zbierz.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class FarmPlot : MonoBehaviour, IClickable
    {
        public static readonly List<FarmPlot> All = new List<FarmPlot>();

        /// <summary>Grządka pod kursorem (ustawia UI) - dostaje jasny kontur.</summary>
        public static FarmPlot Hovered;

        static readonly Color HoverOutline = new Color32(0xd8, 0xcc, 0xab, 0xff);
        static readonly Color RipeOutline = new Color32(0xc4, 0x9a, 0x55, 0xff);

        [SerializeField] string plotId;
        [SerializeField] SpriteRenderer cropRenderer;
        [SerializeField] SpriteRenderer glowRenderer;
        [SerializeField] SpriteFx cropFx;

        readonly PlotState state = new PlotState();
        Coroutine popRoutine;

        public string PlotId => plotId;
        /// <summary>Kafelek grządki (ustawia wyspa) - do sąsiedztwa przy mutacjach.</summary>
        public Vector2Int Cell { get; set; }
        public Vector3 CropPosition => cropRenderer != null ? cropRenderer.transform.position : transform.position;
        int CropOrder => cropRenderer != null ? cropRenderer.sortingOrder : 0;
        public PlotState State => state;
        /// <summary>Ostatnio zebrana roślina (runa siewu w trybie „pamięć ziemi” sadzi ją ponownie). Bez zapisu.</summary>
        public CropDefinition LastCrop { get; private set; }

        /// <summary>Punkt, w którym sprawdzamy, czy grządka jest oświetlona.</summary>
        public Vector2 LightProbe => cropRenderer != null ? (Vector2)cropRenderer.transform.position : (Vector2)transform.position;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Setup(string id, SpriteRenderer crop, SpriteRenderer glow = null, SpriteFx fx = null)
        {
            plotId = id;
            cropRenderer = crop;
            glowRenderer = glow;
            cropFx = fx;
        }

        void Update()
        {
            // świecące rośliny (księżycowy kwiat, upiorna dynia...) delikatnie pulsują, każda w swoim rytmie
            if (glowRenderer != null && glowRenderer.sprite != null)
            {
                float p = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 1.8f + (Cell.x * 7 + Cell.y * 13) * 0.7f);
                var gc = glowRenderer.color;
                gc.a = p;
                glowRenderer.color = gc;
            }
            if (cropFx == null)
                return;
            Color c = Color.clear;
            if (Hovered == this && !state.IsEmpty)
                c = HoverOutline;
            else if (state.IsRipe && Mathf.FloorToInt(Time.unscaledTime / 0.6f) % 2 == 0)
                c = RipeOutline;   // puls „gotowe do zbioru”
            cropFx.SetOutline(c);
        }

        public void Tick(float dt, in GrowthContext ctx)
        {
            if (state.Tick(dt, ctx))
            {
                RefreshVisual();
                Pop();
                IslandFx.Grew(CropPosition, CropOrder);
            }
        }

        public void OnClick()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;

            if (state.IsRipe)
            {
                var crop = state.crop;
                int price = Market.SellCrop(crop, state.Quality, 1, SaleChannel.Hand);
                game.Wallet.Add(price);
                GameEvents.RaiseCurrencyGained(CropPosition, CurrencyType.Coins, price);
                int night = Market.NightYield(crop);
                if (night > 0)
                {
                    game.Wallet.Add(CurrencyType.NightHarvest, night);
                    GameEvents.RaiseCurrencyGained(CropPosition, CurrencyType.NightHarvest, night);
                }
                IslandFx.Harvest(CropPosition, crop.StageSprite(3), CropOrder);
                GameEvents.PlaySound("harvest");
                LastCrop = crop;
                state.Clear();
                RefreshVisual();
                game.AfterHarvest(this, crop);
            }
            else if (!state.IsEmpty)
            {
                // rosnąca roślina (np. nocna o świcie): koło nasion podmienia ją na inną, stare nasiono wraca do worka
                if (UiRoot.Instance != null)
                    UiRoot.Instance.Wheel.Open(this);
            }
            else if (state.IsEmpty)
            {
                if (UiRoot.Instance != null)
                {
                    UiRoot.Instance.Wheel.Open(this);
                    return;
                }
                var seed = SeedSelection.Current;
                if (seed != null && game.Wallet.TrySpend(Economy.Market.SeedPrice(seed)))
                    Replant(seed);
            }
        }

        /// <summary>Zbiór bez zapłaty (np. ognik-ogrodnik wkłada plon do skrzyni). Zwraca zebraną roślinę.</summary>
        public CropDefinition HarvestSilently()
        {
            var crop = state.crop;
            if (crop != null)
            {
                IslandFx.Harvest(CropPosition, crop.StageSprite(3), CropOrder);
                LastCrop = crop;
            }
            state.Clear();
            RefreshVisual();
            Pop();
            return crop;
        }

        /// <summary>
        /// Wykopanie niedojrzałej rośliny (prośba gracza: po nocy posadzone rośliny nocne stały i nie dało się nic zrobić).
        /// Nasiono wraca do worka, postęp wzrostu przepada. Zwraca wykopaną roślinę (null, gdy nic nie rosło albo jest dojrzała).
        /// </summary>
        public CropDefinition DigUp()
        {
            var crop = state.crop;
            if (crop == null || state.IsRipe)
                return null;
            var game = GameLoop.Instance;
            if (game != null)
                game.Seeds.Add(crop.id);
            state.Clear();
            RefreshVisual();
            Pop();
            IslandFx.Planted(CropPosition, CropOrder);
            GameEvents.PlaySound("dig", 0.6f);
            return crop;
        }

        /// <summary>Roślina, która teraz nie może rosnąć z powodu pory: nocna w dzień, dzienna nocą bez lampy.</summary>
        public bool OutOfSeason(bool night, bool lit)
        {
            var c = state.crop;
            if (c == null || state.IsRipe)
                return false;
            return c.kind == CropKind.Night ? !night : night && !lit;
        }

        /// <summary>Sadzenie (koło nasion, runa siewu) - koszt nasiona płaci wywołujący.</summary>
        public void Replant(CropDefinition crop)
        {
            state.Plant(crop);
            RefreshVisual();
            Pop();
            IslandFx.Planted(CropPosition, CropOrder);
            GameEvents.PlaySound("plant");
        }

        public void RefreshVisual()
        {
            if (cropRenderer == null)
                return;
            cropRenderer.sprite = state.IsEmpty ? null : state.crop.StageSprite(state.Stage);
            if (glowRenderer != null)
                glowRenderer.sprite = state.IsEmpty ? null : state.crop.StageGlow(state.Stage);
        }

        void Pop()
        {
            if (!isActiveAndEnabled || cropRenderer == null)
                return;
            if (popRoutine != null)
                StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopRoutine());
        }

        /// <summary>Squash &amp; stretch przy zmianie fazy - nagroda za pracę. Płynnie: wydłużenie, spłaszczenie, sprężyste wygaszenie.</summary>
        IEnumerator PopRoutine()
        {
            var t = cropRenderer.transform;
            const float duration = 0.35f;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                float u = time / duration;
                float k = Mathf.Sin(u * Mathf.PI * 2.5f) * (1f - u);   // gasnąca sprężyna
                t.localScale = new Vector3(1f - 0.18f * k, 1f + 0.3f * k, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
            popRoutine = null;
        }
    }
}
