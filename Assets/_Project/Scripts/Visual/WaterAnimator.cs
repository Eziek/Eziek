using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;

namespace Wyspa.Visual
{
    /// <summary>
    /// Podaje shaderowi Wyspa/Water ścieżki ryb i miejsce potwora (Art/water_layout.json z tools/pixel-proto/export_unity_fx.py)
    /// oraz porę dnia (świecące ryby i oko potwora tylko w nocy).
    /// </summary>
    public class WaterAnimator : MonoBehaviour
    {
        const int MaxFish = 16;   // rozmiar tablic w Water.shader

        [Serializable] class FishData { public float cx, cy, rx, ry, phase; public bool glow; }
        [Serializable] class BeastData { public float x, y; }
        [Serializable] class Layout { public List<FishData> fish = new List<FishData>(); public BeastData beast; }

        static readonly int FishId = Shader.PropertyToID("_Fish");
        static readonly int FishBId = Shader.PropertyToID("_FishB");
        static readonly int FishCountId = Shader.PropertyToID("_FishCount");
        static readonly int BeastId = Shader.PropertyToID("_Beast");
        static readonly int NightId = Shader.PropertyToID("_Night");

        [SerializeField] SpriteRenderer water;
        [SerializeField] TextAsset layoutJson;
        [SerializeField] GameClock clock;

        Material material;
        float timer;

        public void Setup(SpriteRenderer waterRenderer, TextAsset layout, GameClock gameClock)
        {
            water = waterRenderer;
            layoutJson = layout;
            clock = gameClock;
        }

        void Start()
        {
            if (water == null)
                return;
            var swap = water.GetComponent<FxMaterial>();
            // kopia materiału tylko dla tej sceny (tablice nie zapisują się w assecie)
            material = new Material(swap != null && swap.Fx != null ? swap.Fx : water.sharedMaterial);
            if (swap != null)
                swap.SetFxMaterial(material);
            else
                water.sharedMaterial = material;
            var fish = new Vector4[MaxFish];
            var fishB = new Vector4[MaxFish];
            int count = 0;
            var beast = Vector4.zero;
            if (layoutJson != null)
            {
                var data = JsonUtility.FromJson<Layout>(layoutJson.text);
                if (data != null && data.fish != null)
                    foreach (var f in data.fish)
                    {
                        if (count >= MaxFish)
                            break;
                        fish[count] = new Vector4(f.cx, f.cy, f.rx, f.ry);
                        fishB[count] = new Vector4(f.phase, f.glow ? 1f : 0f, 0f, 0f);
                        count++;
                    }
                if (data != null && data.beast != null)
                    beast = new Vector4(data.beast.x, data.beast.y, 1f, 0f);
            }
            material.SetVectorArray(FishId, fish);
            material.SetVectorArray(FishBId, fishB);
            material.SetFloat(FishCountId, count);
            material.SetVector(BeastId, beast);
            UpdateNight();
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
                return;
            timer = 1f;
            UpdateNight();
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
