using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.World
{
    [Serializable]
    public class DecoArt
    {
        public DecoKind kind;
        public Sprite sprite;
        [Tooltip("Kołysze się (drzewa, krzaki).")]
        public bool sway;
    }

    /// <summary>Grafiki i materiały, z których Island buduje w locie grządki, dekoracje i ramki trybu rozbudowy.</summary>
    [CreateAssetMenu(menuName = "Wyspa/Grafika wyspy", fileName = "IslandArt")]
    public class IslandArt : ScriptableObject
    {
        public Sprite plotTile;
        public List<DecoArt> decorations = new List<DecoArt>();

        [Header("Tryb rozbudowy")]
        public Sprite frameBuy;
        public Sprite frameBuyHover;
        public Sprite frameDig;
        public Sprite frameDigHover;
        public Sprite[] foam;

        [Header("Materiały (puste = zwykły materiał sprite'ów)")]
        public Material swayCrop;
        public Material swayCropGlow;
        public Material swayTree;
        public Material glow;

        public DecoArt Deco(DecoKind kind)
        {
            foreach (var d in decorations)
                if (d != null && d.kind == kind)
                    return d;
            return null;
        }
    }
}
