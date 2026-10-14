using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.World;

namespace Wyspa.Visual
{
    /// <summary>
    /// Podaje shaderowi Wyspa/Water mapę danych wody (malowaną przez Island), ścieżki ryb, miejsce potwora
    /// i porę dnia (świecące ryby i oko potwora tylko w nocy).
    /// </summary>
    public class WaterAnimator : MonoBehaviour
    {
        const int MaxFish = 16;   // rozmiar tablic w Water.shader

        static readonly int DataTexId = Shader.PropertyToID("_DataTex");
        static readonly int FishId = Shader.PropertyToID("_Fish");
        static readonly int FishBId = Shader.PropertyToID("_FishB");
        static readonly int FishCountId = Shader.PropertyToID("_FishCount");
        static readonly int BeastId = Shader.PropertyToID("_Beast");
        static readonly int NightId = Shader.PropertyToID("_Night");

        [SerializeField] SpriteRenderer water;
        [SerializeField] GameClock clock;

        Material material;
        float timer;

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

        /// <summary>Nowy obraz wyspy: mapa danych, ryby i potwór.</summary>
        public void SetData(Texture2D data, IslandPicture picture)
        {
            var m = EnsureMaterial();
            if (m == null)
                return;
            m.SetTexture(DataTexId, data);
            var fish = new Vector4[MaxFish];
            var fishB = new Vector4[MaxFish];
            int count = 0;
            if (picture != null)
                foreach (var f in picture.fish)
                {
                    if (count >= MaxFish)
                        break;
                    fish[count] = new Vector4(f.cx, f.cy, f.rx, f.ry);
                    fishB[count] = new Vector4(f.phase, f.glow ? 1f : 0f, 0f, 0f);
                    count++;
                }
            m.SetVectorArray(FishId, fish);
            m.SetVectorArray(FishBId, fishB);
            m.SetFloat(FishCountId, count);
            m.SetVector(BeastId, picture != null && picture.hasBeast
                ? new Vector4(picture.beastX, picture.beastY, 1f, 0f) : Vector4.zero);
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
