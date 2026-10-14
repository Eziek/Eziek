using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Input;

namespace Wyspa.Farming
{
    /// <summary>Grządka: klik na pustą = posadź wybrane nasiono, klik na dojrzałą = zbierz.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class FarmPlot : MonoBehaviour, IClickable
    {
        public static readonly List<FarmPlot> All = new List<FarmPlot>();

        [SerializeField] string plotId;
        [SerializeField] SpriteRenderer cropRenderer;

        readonly PlotState state = new PlotState();
        Coroutine popRoutine;

        public string PlotId => plotId;
        public PlotState State => state;

        /// <summary>Punkt, w którym sprawdzamy, czy grządka jest oświetlona.</summary>
        public Vector2 LightProbe => cropRenderer != null ? (Vector2)cropRenderer.transform.position : (Vector2)transform.position;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Setup(string id, SpriteRenderer crop)
        {
            plotId = id;
            cropRenderer = crop;
        }

        public void Tick(float dt, in GrowthContext ctx)
        {
            if (state.Tick(dt, ctx))
            {
                RefreshVisual();
                Pop();
            }
        }

        public void OnClick()
        {
            var game = GameLoop.Instance;
            if (game == null)
                return;

            if (state.IsRipe)
            {
                game.Wallet.Add(state.crop.sellPrice);
                if (state.crop.kind == CropKind.Night)
                    game.Wallet.Add(CurrencyType.NightHarvest, state.crop.nightHarvestYield);
                state.Clear();
                RefreshVisual();
            }
            else if (state.IsEmpty)
            {
                var seed = SeedSelection.Current;
                if (seed != null && game.Wallet.TrySpend(seed.seedPrice))
                {
                    state.Plant(seed);
                    RefreshVisual();
                    Pop();
                }
            }
        }

        public void RefreshVisual()
        {
            if (cropRenderer == null)
                return;
            cropRenderer.sprite = state.IsEmpty ? null : state.crop.StageSprite(state.Stage);
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
