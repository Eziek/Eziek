using System;
using System.Collections.Generic;
using UnityEngine;
using Wyspa.Economy;
using Wyspa.Lighting;

namespace Wyspa.World
{
    public enum RemoveAnim { Poof, Fall, Crack, Cut }

    /// <summary>Klatki ognika danego zawodu (rój ogników).</summary>
    [Serializable]
    public class WispKindArt
    {
        public Automation.WispKind kind;
        public Sprite[] frames;
        public Sprite[] glow;
        /// <summary>Narzędzie zawodu: 2 klatki (koszyk zbieracza: tył i przód) i piksele emisyjne (tools/pixel-proto/wisp_tools.py).</summary>
        public Sprite[] tool;
        public Sprite[] toolGlow;
    }

    /// <summary>Monument wyspy fabularnej (§13.11): fazy 0 (ruina) - 4 (gotowy) i świecące okna gotowego (Story/monument_*).</summary>
    [Serializable]
    public class StoryArt
    {
        public string id;
        public Sprite[] phases;
        public Sprite doneGlow;
    }

    /// <summary>Ptak tła: klatki sit0, sit1, fly0, fly1 (Critters/bird_&lt;id&gt;_&lt;poza&gt;).</summary>
    [Serializable]
    public class BirdArt
    {
        public string id;
        public Sprite[] frames;
    }

    [Serializable]
    public class FamiliarArt
    {
        public string id;
        public Sprite[] frames;
        [Tooltip("Piksele emisyjne (oczy, plamki), puste gdy poza nie świeci.")]
        public Sprite[] glows;
    }

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

        [Header("Życie stref: wrona, żaba, bąble")]
        public Sprite[] crow;
        public Sprite[] crowGlow;
        public Sprite[] frog;
        public Sprite[] frogGlow;
        public Sprite[] bubbles;

        [Header("Morze")]
        public Sprite pier;
        public Sprite netBuoy;
        public Sprite boat;
        public Sprite boatGlow;
        public Sprite altar;
        public Sprite altarGlow;
        [Tooltip("Latarnia morska: lampa spoza sklepu lamp, stawiana z Morza.")]
        public LampDefinition lighthouse;

        [Header("Ozdobniki na wodzie (WaterFlora): kolejność jak FLORA w tools/pixel-proto/water_flora.py")]
        public Sprite[] waterFlora;
        [Tooltip("Piksele emisyjne (ten sam indeks), puste gdy ozdobnik nie świeci.")]
        public Sprite[] waterFloraGlow;

        [Header("Wyspy fabularne: monumenty w fazach odbudowy (tools/pixel-proto/monuments.py)")]
        public List<StoryArt> story = new List<StoryArt>();

        public StoryArt Story(string id)
        {
            if (story == null)
                return null;
            foreach (var s in story)
                if (s != null && s.id == id) return s;
            return null;
        }

        [Header("Drzewa owocowe (GDD §15.1)")]
        public List<Farming.TreeArt> trees = new List<Farming.TreeArt>();
        public Sprite sapling;
        [Tooltip("Kołysanie świecących owoców w rytmie drzewa (SpriteSwayUnlit, prędkość drzew).")]
        public Material swayTreeGlow;

        [Header("Ptaki (życie na wyspie)")]
        [Tooltip("sit0, sit1 (dziób w dół), fly0, fly1")]
        public Sprite[] birdCrow;
        public Sprite[] birdFinch;
        [Tooltip("Pozostałe ptaki (sroka, gil, sikora, mewa, czapla), te same 4 pozy (tools/pixel-proto/birds2.py).")]
        public List<BirdArt> birds = new List<BirdArt>();

        public Sprite[] Bird(string id)
        {
            if (birds == null)
                return null;
            foreach (var b in birds)
                if (b != null && b.id == id) return b.frames;
            return null;
        }

        [Header("Rój ogników: gniazdo (2 klatki) i ogniki w kolorach zawodów")]
        public Sprite[] wispNest;
        public List<WispKindArt> wispKinds = new List<WispKindArt>();
        [Tooltip("Czapki ogników (hat_<id>, Economy/Hats) i ich piksele emisyjne.")]
        public List<UI.NamedSprite> hats = new List<UI.NamedSprite>();
        public List<UI.NamedSprite> hatGlows = new List<UI.NamedSprite>();

        public Sprite Hat(string id, bool glow)
        {
            var list = glow ? hatGlows : hats;
            if (id == null || list == null)
                return null;
            foreach (var h in list)
                if (h != null && h.name == id) return h.sprite;
            return null;
        }

        [Header("Chowańce: klatki w kolejności póz z Familiars.FamiliarSystem.Defs (familiar_<id>_<poza>.png)")]
        public List<FamiliarArt> familiars = new List<FamiliarArt>();

        [Header("Domki chowańców (home_<id>), ognisko ogników (3 klatki) i sennik")]
        public List<UI.NamedSprite> homes = new List<UI.NamedSprite>();
        public List<UI.NamedSprite> homeGlows = new List<UI.NamedSprite>();
        public Sprite[] bonfire;
        public Sprite[] bonfireGlow;
        public Sprite dreamHouse;
        public Sprite dreamHouseGlow;

        [Header("Generatory Żaru: piec (3 klatki płomienia) i kryształ (tools/pixel-proto/ember_gens.py)")]
        public Sprite[] emberKiln;
        public Sprite[] emberKilnGlow;
        public Sprite emberCrystal;
        public Sprite emberCrystalGlow;

        public Sprite Home(string familiar, bool glow)
        {
            var list = glow ? homeGlows : homes;
            if (familiar == null || list == null)
                return null;
            foreach (var h in list)
                if (h != null && h.name == familiar) return h.sprite;
            return null;
        }

        public FamiliarArt Familiar(string id)
        {
            if (familiars == null)
                return null;
            foreach (var f in familiars)
                if (f != null && f.id == id) return f;
            return null;
        }

        [Header("Kocioł alchemiczny")]
        public Sprite storehouse;   // skład: nadwyżka drewna i kamienia na monety
        public Sprite[] moth;       // ćmy przy lampach nocą (AmbientLife)
        public Sprite[] fishJump;   // ryba wyskakująca z wody: nos w górę, nos w dół
        public Sprite cauldronIdle;
        public Sprite[] cauldronBrew;
        public Sprite[] cauldronBrewGlow;
        public Sprite[] cauldronReady;
        public Sprite[] cauldronReadyGlow;

        [Header("Wprowadzenie: tablica z karteczkami")]
        public Sprite noticeBoard;
        public Sprite noticeBoardEmpty;
        [Tooltip("Złota strzałka nad celem kroku wprowadzenia.")]
        public Sprite introArrow;

        public Farming.TreeArt Tree(string id)
        {
            foreach (var t in trees)
                if (t != null && t.id == id)
                    return t;
            return null;
        }

        [Header("Automatyka: budowle w wielu egzemplarzach")]
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
        [Tooltip("Sprite na wodzie: linia wody, piana, kołysanie (SpriteFloat.shader, Visual/Floating).")]
        public Material floatMat;

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
