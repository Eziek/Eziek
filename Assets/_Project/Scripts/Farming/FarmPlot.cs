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
        public Vector3 CropPosition => cropRenderer != null ? cropRenderer.transform.position : transform.position;
        int CropOrder => cropRenderer != null ? cropRenderer.sortingOrder : 0;
        public PlotState State => state;

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
                game.Wallet.Add(crop.sellPrice);
                GameEvents.RaiseCurrencyGained(CropPosition, CurrencyType.Coins, crop.sellPrice);
                if (crop.kind == CropKind.Night)
                {
                    game.Wallet.Add(CurrencyType.NightHarvest, crop.nightHarvestYield);
                    GameEvents.RaiseCurrencyGained(CropPosition, CurrencyType.NightHarvest, crop.nightHarvestYield);
                }
                IslandFx.Harvest(CropPosition, crop.StageSprite(3), CropOrder);
                state.Clear();
                RefreshVisual();
                game.AfterHarvest(this, crop);
            }
            else if (state.IsEmpty)
            {
                if (UiRoot.Instance != null)
                {
                    UiRoot.Instance.Wheel.Open(this);
                    return;
                }
                var seed = SeedSelection.Current;
                if (seed != null && game.Wallet.TrySpend(seed.seedPrice))
                    Replant(seed);
            }
        }

        /// <summary>Zbiór bez zapłaty (np. ognik-ogrodnik wkłada plon do skrzyni). Zwraca zebraną roślinę.</summary>
        public CropDefinition HarvestSilently()
        {
            var crop = state.crop;
            if (crop != null)
                IslandFx.Harvest(CropPosition, crop.StageSprite(3), CropOrder);
            state.Clear();
            RefreshVisual();
            Pop();
            return crop;
        }

        /// <summary>Ponowne sadzenie (pamięć ziemi) - koszt nasiona płaci wywołujący.</summary>
        public void Replant(CropDefinition crop)
        {
            state.Plant(crop);
            RefreshVisual();
            Pop();
            IslandFx.Planted(CropPosition, CropOrder);
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

        /// <summary>Squash &amp; stretch przy zmianie fazy - nagroda za pracę. Skokowo, jak reszta animacji.</summary>
        IEnumerator PopRoutine()
        {
            var t = cropRenderer.transform;
            var wait = new WaitForSecondsRealtime(0.1f);
            t.localScale = new Vector3(0.85f, 1.3f, 1f);
            yield return wait;
            t.localScale = new Vector3(1.15f, 0.85f, 1f);
            yield return wait;
            t.localScale = Vector3.one;
            popRoutine = null;
        }
    }
}
