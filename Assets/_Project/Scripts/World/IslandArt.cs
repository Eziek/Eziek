using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Economy;
using Wyspa.Lighting;

namespace Wyspa.World
{
    public enum RemoveAnim { Poof, Fall, Crack, Cut }

    [Serializable]
    public class DecoArt
    {
        public DecoKind kind;
        public Sprite sprite;
        [Tooltip("Kołysze się (drzewa, krzaki).")]
        public bool sway;
        [Tooltip("Klucz lokalizacji nazwy (deco.*).")]
        public string nameKey;
        [Tooltip("Piksele emisyjne (świece, palenisko, tablica runiczna).")]
        public Sprite glowSprite;
        [Tooltip("Zabytek: nieusuwalny, klik pokazuje fragment lore (§13.8).")]
        public bool landmark;
        public string loreKey;
        [Tooltip("Dodatkowa nagroda to fragment lore zamiast waluty (nagrobek).")]
        public bool bonusLore;

        [Header("Usuwanie (GDD §14)")]
        public bool removable = true;
        public int essenceCost = 2;
        public CurrencyType reward = CurrencyType.Wood;
        public int rewardAmount = 1;
        [Tooltip("Dodatkowa nagroda z szansą (gniazdo z nasionem, monety pod kamieniem, jagody).")]
        [Range(0f, 1f)] public float bonusChance;
        public CurrencyType bonus = CurrencyType.Coins;
        public int bonusAmount;
        [Tooltip("Klucz toastu dla dodatkowej nagrody.")]
        public string bonusToast;
        public RemoveAnim anim;
        [Tooltip("Klatki animacji (przewracanie, pęknięcie, ścięcie).")]
        public Sprite[] frames;
        [Tooltip("Cząsteczki przy usuwaniu (wióry, liście, odpryski).")]
        public Sprite[] chips;
        public bool hasRemains;
        [Tooltip("Co zostaje (pień, gruz). Pozostałość też można usunąć.")]
        public DecoKind remains;
    }

    /// <summary>Grafiki i materiały, z których Island buduje w locie grządki, dekoracje i ramki trybu rozbudowy.</summary>
    [CreateAssetMenu(menuName = "Wyspa/Grafika wyspy", fileName = "IslandArt")]
    public class IslandArt : ScriptableObject
    {
        public Sprite plotTile;
        public List<DecoArt> decorations = new List<DecoArt>();

        [Header("Lampy (sklep, tryb stawiania)")]
        public List<LampDefinition> lamps = new List<LampDefinition>();
        public Sprite[] dust;

        [Header("Życie stref (Etap 5a): wrona, żaba, bąble")]
        public Sprite[] crow;
        public Sprite[] crowGlow;
        public Sprite[] frog;
        public Sprite[] frogGlow;
        public Sprite[] bubbles;

        [Header("Morze (Etap 4)")]
        public Sprite pier;
        public Sprite netBuoy;
        public Sprite boat;
        public Sprite boatGlow;
        public Sprite altar;
        public Sprite altarGlow;
        [Tooltip("Latarnia morska (Etap 5b): lampa spoza sklepu lamp, stawiana z Morza.")]
        public LampDefinition lighthouse;

        [Header("Drzewa owocowe (GDD §15.1)")]
        public List<Farming.TreeArt> trees = new List<Farming.TreeArt>();
        public Sprite sapling;
        [Tooltip("Kołysanie świecących owoców w rytmie drzewa (SpriteSwayUnlit, prędkość drzew).")]
        public Material swayTreeGlow;

        [Header("Ptaki (życie na wyspie)")]
        [Tooltip("sit0, sit1 (dziób w dół), fly0, fly1")]
        public Sprite[] birdCrow;
        public Sprite[] birdFinch;

        public Farming.TreeArt Tree(string id)
        {
            foreach (var t in trees)
                if (t != null && t.id == id)
                    return t;
            return null;
        }

        [Header("Automatyka (Etap 6b): budowle w wielu egzemplarzach")]
        [Tooltip("Skrzynia na plony: pusta, do połowy, pełna.")]
        public Sprite[] crateFill;
        public Sprite[] wispFrames;
        public Sprite[] wispGlow;
        public Sprite[] merchantFrames;
        public Sprite[] merchantGlow;
        public Sprite rune;
        public Sprite runeGlow;

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

        public LampDefinition Lamp(string id)
        {
            foreach (var l in lamps)
                if (l != null && l.id == id)
                    return l;
            return lighthouse != null && lighthouse.id == id ? lighthouse : null;
        }

        public DecoArt Deco(DecoKind kind)
        {
            foreach (var d in decorations)
                if (d != null && d.kind == kind)
                    return d;
            return null;
        }
    }
}
