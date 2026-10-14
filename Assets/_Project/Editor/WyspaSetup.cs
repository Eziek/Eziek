using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Input;
using Wyspa.Lighting;
using Wyspa.Save;
using Wyspa.UI;
using Wyspa.Upgrades;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.EditorTools
{
    /// <summary>
    /// Menu "Wyspa": ustawienia projektu, dane (rośliny, lampy) i scena prototypu zbudowana z prototype_layout.json.
    /// Wszystko można uruchamiać wielokrotnie - istniejące dane są aktualizowane, scena budowana od nowa.
    /// </summary>
    public static class WyspaSetup
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string ScenePath = Root + "/Scenes/Prototype.unity";
        const string LayoutPath = Root + "/Art/prototype_layout.json";
        const string Sprites = PixelArtImporter.SpritesRoot;
        const int Ppu = PixelArtImporter.PixelsPerUnit;
        const int WindowScale = 3;

        static readonly Color OutlineColor = new Color32(0x0f, 0x0a, 0x10, 0xff);

        // ---------- układ z prototypu (współrzędne w pikselach kadru, y w dół) ----------
        [Serializable] class Layout { public int width; public int height; public int ppu; public PlotL[] plots; public ObjL[] objects; public LampL[] lamps; }
        [Serializable] class PlotL { public int row; public int col; public int x; public int y; public int cropX; public int cropY; }
        [Serializable] class ObjL { public string sprite; public int x; public int y; }
        [Serializable] class LampL { public string id; public string sprite; public int x; public int y; public int lightX; public int lightY; public float radius; public float r; public float g; public float b; }

        struct CropSpec
        {
            public string id, name, sprite;
            public CropKind kind;
            public float seconds;
            public int seed, sell;
            public bool fullMoon, hybrid;
            public string parentA, parentB, seedIcon;
        }
        struct LampSpec { public string name; public int price; public float perMinute, growth; public bool flicker; }

        static readonly CropSpec[] CropSpecs =
        {
            new CropSpec { id = "carrot", name = "Marchew", sprite = "crop_carrot", kind = CropKind.Day, seconds = 600, seed = 5, sell = 12 },
            new CropSpec { id = "wheat", name = "Zboże", sprite = "crop_wheat", kind = CropKind.Day, seconds = 900, seed = 8, sell = 18 },
            new CropSpec { id = "pumpkin", name = "Dynia", sprite = "crop_pumpkin", kind = CropKind.Day, seconds = 1500, seed = 15, sell = 40 },
            // rośliny nocy: czas = sekundy ciemności (rosną też offline)
            new CropSpec { id = "corpseshroom", name = "Trupi grzyb", sprite = "crop_corpseshroom", kind = CropKind.Night, seconds = 3 * 3600, seed = 15, sell = 45 },
            new CropSpec { id = "moonflower", name = "Księżycowy kwiat", sprite = "crop_moonflower", kind = CropKind.Night, seconds = 4 * 3600, seed = 20, sell = 60 },
            new CropSpec { id = "ghostpumpkin", name = "Upiorna dynia", sprite = "crop_ghostpumpkin", kind = CropKind.Night, seconds = 6 * 3600, seed = 30, sell = 90 },
            new CropSpec { id = "bloodrose", name = "Krwawa róża", sprite = "crop_bloodrose", kind = CropKind.Night, seconds = 8 * 3600, seed = 50, sell = 200, fullMoon = true },
            // hybryda (Etap 4a): nasiona tylko z mutacji dyni i upiornej dyni na sąsiednich grządkach
            new CropSpec { id = "hybridpumpkin", name = "Dynia-upiór", sprite = "crop_hybridpumpkin", kind = CropKind.Night, seconds = 5 * 3600, seed = 0, sell = 150,
                           hybrid = true, parentA = "pumpkin", parentB = "ghostpumpkin", seedIcon = "seed_hybrid" },
        };

        struct UpgradeSpec
        {
            public string id, name;
            public UpgradeCategory category;
            public (float value, CurrencyAmount[] cost)[] levels;
            public string[] requires;
        }

        static CurrencyAmount Coins(int n) => new CurrencyAmount(CurrencyType.Coins, n);
        static CurrencyAmount Night(int n) => new CurrencyAmount(CurrencyType.NightHarvest, n);

        // GDD §11.3 (wartości wstępne do balansu)
        static readonly UpgradeSpec[] UpgradeSpecs =
        {
            new UpgradeSpec { id = UpgradeIds.EssenceJar, name = "Słój Esencji", category = UpgradeCategory.Essence,
                levels = new[] { (120f, new[] { Coins(100) }), (240f, new[] { Coins(400) }), (480f, new[] { Coins(1500) }) } },
            new UpgradeSpec { id = UpgradeIds.WispGardener, name = "Ognik-ogrodnik", category = UpgradeCategory.Automation,
                levels = new[] { (30f, new[] { Coins(300), Night(5) }) } },
            new UpgradeSpec { id = UpgradeIds.SoilMemory, name = "Pamięć ziemi", category = UpgradeCategory.Automation,
                levels = new[] { (1f, new[] { Coins(500) }) }, requires = new[] { UpgradeIds.WispGardener } },
            new UpgradeSpec { id = UpgradeIds.MerchantChest, name = "Skrzynia kupca", category = UpgradeCategory.Automation,
                levels = new[] { (3600f, new[] { Coins(800) }) }, requires = new[] { UpgradeIds.WispGardener } },
        };

        static readonly Dictionary<string, LampSpec> LampSpecs = new Dictionary<string, LampSpec>
        {
            { "lantern", new LampSpec { name = "Krzywa latarnia", price = 30, perMinute = 0.1f, growth = 1f, flicker = true } },
            { "skull", new LampSpec { name = "Czaszka ze świecą", price = 15, perMinute = 0.05f, growth = 1f, flicker = true } },
            { "wisp", new LampSpec { name = "Klatka z ognikiem", price = 80, perMinute = 0.2f, growth = 1.5f, flicker = false } },
            { "mushrooms", new LampSpec { name = "Świecące grzyby", price = 10, perMinute = 0f, growth = 1f, flicker = false } },
        };

        // =====================================================================
        [MenuItem("Wyspa/1. Ustawienia projektu", priority = 1)]
        public static void ConfigureProject()
        {
            var layout = LoadLayout();
            int w = layout != null ? layout.width : 272;
            int h = layout != null ? layout.height : 200;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            // okno startowe = kadr wyspy 2x2 (144x112) x3; potem rośnie razem z wyspą (ViewController)
            w = StartView.x;
            h = StartView.y;
            PlayerSettings.defaultScreenWidth = w * WindowScale;
            PlayerSettings.defaultScreenHeight = h * WindowScale;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            // przezroczyste okno (DWM, opcja gracza) wymaga łańcucha wymiany bez „flip model” (GDD §12.7)
            PlayerSettings.useFlipModelSwapchain = false;
            // font UI powstaje w locie (UiRoot), a TMP szuka wtedy shadera przez Shader.Find - w buildzie musi być dołączony
            AlwaysIncludeShader("TextMeshPro/Mobile/Bitmap");

            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null)
                Debug.LogWarning("[Wyspa] Brak URP! Utwórz projekt z szablonu 'Universal 2D' albo przypisz URP Asset z 2D Rendererem w Project Settings > Graphics.");
            else if (!Uses2DRenderer(rp))
                Debug.LogWarning("[Wyspa] URP używa renderera 3D, więc światła 2D (dzień/noc, lampy) nie zadziałają. " +
                                 "Project: Create > Rendering > URP Asset (with 2D Renderer), potem ustaw go w " +
                                 "Project Settings > Graphics (Default Render Pipeline) i w Quality (Render Pipeline Asset) dla każdego poziomu. " +
                                 "Szczegóły: docs/UNITY_SETUP.md.");
            else
                Debug.Log("[Wyspa] URP z 2D Rendererem: OK.");

            Debug.Log($"[Wyspa] Player Settings: okno {w * WindowScale}x{h * WindowScale}, praca w tle włączona.");
        }

        /// <summary>Czy domyślny renderer URP to 2D Renderer (czyta pola assetu - klasa Renderer2D jest wewnętrzna).</summary>
        /// <summary>Dopisuje shader do Graphics Settings → Always Included Shaders (Unity nie wytnie go z buildu).</summary>
        static void AlwaysIncludeShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Wyspa] Brak shadera '{shaderName}': Window > TextMeshPro > Import TMP Essential Resources, potem Wyspa > 1 jeszcze raz.");
                return;
            }
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null)
                return;
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
                return;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Wyspa] Shader '{shaderName}' dodany do Always Included Shaders.");
        }

        static bool Uses2DRenderer(UniversalRenderPipelineAsset rp)
        {
            var so = new SerializedObject(rp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray || list.arraySize == 0)
                return false;
            var index = so.FindProperty("m_DefaultRendererIndex");
            int i = index != null ? Mathf.Clamp(index.intValue, 0, list.arraySize - 1) : 0;
            var data = list.GetArrayElementAtIndex(i).objectReferenceValue;
            return data != null && data.GetType().Name == "Renderer2DData";
        }

        // =====================================================================
        [MenuItem("Wyspa/2. Utwórz dane (rośliny, lampy)", priority = 2)]
        public static void CreateData()
        {
            AssetDatabase.Refresh();
            EnsureFolder(DataDir + "/Crops");
            EnsureFolder(DataDir + "/Lamps");

            var db = LoadOrCreate<CropDatabase>(DataDir + "/CropDatabase.asset");
            db.crops.Clear();
            foreach (var spec in CropSpecs)
            {
                var crop = LoadOrCreate<CropDefinition>($"{DataDir}/Crops/Crop_{spec.id}.asset");
                crop.id = spec.id;
                crop.displayName = spec.name;
                crop.kind = spec.kind;
                crop.growSeconds = spec.seconds;
                crop.seedPrice = spec.seed;
                crop.sellPrice = spec.sell;
                crop.requiresFullMoon = spec.fullMoon;
                crop.hybrid = spec.hybrid;
                crop.parentA = spec.parentA;
                crop.parentB = spec.parentB;
                crop.seedIcon = spec.seedIcon;
                crop.stages = new Sprite[4];
                crop.glowStages = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    crop.stages[i] = LoadSprite($"Crops/{spec.sprite}_{i}");
                    crop.glowStages[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Crops/{spec.sprite}_{i}_glow.png");
                }
                EditorUtility.SetDirty(crop);
                db.crops.Add(crop);
            }
            EditorUtility.SetDirty(db);

            var layout = LoadLayout();
            if (layout != null)
            {
                foreach (var l in layout.lamps)
                {
                    if (!LampSpecs.TryGetValue(l.id, out var spec))
                        continue;
                    var lamp = LoadOrCreate<LampDefinition>($"{DataDir}/Lamps/Lamp_{l.id}.asset");
                    lamp.id = l.id;
                    lamp.displayName = spec.name;
                    lamp.price = spec.price;
                    lamp.essencePerMinute = spec.perMinute;
                    lamp.growthMultiplier = spec.growth;
                    lamp.flicker = spec.flicker;
                    lamp.radius = l.radius / Ppu;
                    lamp.color = new Color(l.r, l.g, l.b);
                    lamp.intensity = 1f;
                    // wygląd do stawiania w trakcie gry: sprite'y i światło względem podstawy (jak w układzie prototypu)
                    lamp.sprite = LoadSprite("Lamps/" + l.sprite);
                    lamp.glowSprite = LoadSprite("Lamps/" + l.sprite + "_glow");
                    lamp.lightOffset = new Vector2((l.lightX - l.x - 0.5f) / Ppu, (l.y + 1 - l.lightY) / Ppu);
                    EditorUtility.SetDirty(lamp);
                }
            }

            // latarnia morska (Etap 5b): lampa spoza układu prototypu, stawiana z Morza
            var lh = LoadOrCreate<LampDefinition>($"{DataDir}/Lamps/Lamp_lighthouse.asset");
            lh.id = "lighthouse";
            lh.displayName = "Latarnia morska";
            lh.price = 2000;
            lh.essencePerMinute = 0.3f;
            lh.growthMultiplier = 1f;
            lh.flicker = false;
            lh.radius = 5f;
            lh.color = new Color(0.95f, 0.88f, 0.68f);
            lh.intensity = 1.1f;
            lh.sprite = LoadSprite("Sea/lighthouse");
            lh.glowSprite = LoadSprite("Sea/lighthouse_glow");
            lh.lightOffset = new Vector2(0f, 45.5f / Ppu);   // środek okna latarni (glow) nad podstawą
            EditorUtility.SetDirty(lh);

            CreateUpgrades();
            CreateUiTheme();
            GlowMaterial();
            CreateFxMaterials();
            CreateIslandArt();
            AssetDatabase.SaveAssets();
            Debug.Log("[Wyspa] Dane utworzone w " + DataDir);
        }

        static UpgradeDatabase CreateUpgrades()
        {
            EnsureFolder(DataDir + "/Upgrades");
            var db = LoadOrCreate<UpgradeDatabase>(DataDir + "/UpgradeDatabase.asset");
            db.upgrades.Clear();
            var byId = new Dictionary<string, UpgradeDefinition>();
            foreach (var spec in UpgradeSpecs)
            {
                var u = LoadOrCreate<UpgradeDefinition>($"{DataDir}/Upgrades/Upgrade_{spec.id}.asset");
                u.id = spec.id;
                u.nameKey = $"upgrade.{spec.id}.name";
                u.descriptionKey = $"upgrade.{spec.id}.desc";
                u.debugName = spec.name;
                u.category = spec.category;
                u.levels = new List<UpgradeLevel>();
                foreach (var (value, cost) in spec.levels)
                    u.levels.Add(new UpgradeLevel { value = value, cost = new List<CurrencyAmount>(cost) });
                byId[spec.id] = u;
                db.upgrades.Add(u);
            }
            foreach (var spec in UpgradeSpecs)
            {
                var u = byId[spec.id];
                u.requires = new List<UpgradeRequirement>();
                if (spec.requires != null)
                    foreach (var r in spec.requires)
                        u.requires.Add(new UpgradeRequirement { upgrade = byId[r], minLevel = 1 });
                EditorUtility.SetDirty(u);
            }
            EditorUtility.SetDirty(db);
            return db;
        }

        // =====================================================================
        const string ThemePath = DataDir + "/UiTheme.asset";
        const string IslandArtPath = DataDir + "/IslandArt.asset";
        // startowy kadr wyspy 2x2 (GDD §13.6): 144x112, okno x3 = 432x336
        static readonly Vector2Int StartView = new Vector2Int(144, 112);

        /// <summary>Grafika wyspy z kafelków: grządka, dekoracje, ramki trybu rozbudowy, materiały efektów.</summary>
        static IslandArt CreateIslandArt()
        {
            var art = LoadOrCreate<IslandArt>(IslandArtPath);
            art.plotTile = LoadSprite("Ground/plot_tile");
            Sprite R(string n) => LoadSprite("Removal/" + n);
            Sprite[] wood = { R("fx_woodchip"), R("fx_leaf") }, stone = { R("fx_stonechip") };
            // koszty i nagrody z GDD §14.3 (wstępne, do balansu)
            art.decorations = new List<DecoArt>
            {
                new DecoArt { kind = DecoKind.TreeRound, sprite = LoadSprite("Objects/tree_round"), sway = true, nameKey = "deco.tree_round",
                    essenceCost = 5, reward = CurrencyType.Wood, rewardAmount = 3,
                    bonusChance = 0.1f, bonus = CurrencyType.NightHarvest, bonusAmount = 1, bonusToast = "toast.deco.nest",
                    anim = RemoveAnim.Fall, frames = new[] { R("tree_round_fall_0"), R("tree_round_fall_1"), R("tree_round_fall_2"), R("tree_round_fall_3") },
                    chips = wood, hasRemains = true, remains = DecoKind.Stump },
                new DecoArt { kind = DecoKind.Spruce, sprite = LoadSprite("Objects/tree_spruce"), sway = true, nameKey = "deco.spruce",
                    essenceCost = 4, reward = CurrencyType.Wood, rewardAmount = 2, anim = RemoveAnim.Poof,
                    chips = wood, hasRemains = true, remains = DecoKind.StumpSpruce },
                new DecoArt { kind = DecoKind.DeadTree, sprite = LoadSprite("Zones/dead_tree"), sway = false, nameKey = "deco.dead_tree",
                    essenceCost = 3, reward = CurrencyType.Wood, rewardAmount = 2, anim = RemoveAnim.Poof,
                    chips = new[] { R("fx_woodchip") }, hasRemains = true, remains = DecoKind.StumpDead },
                new DecoArt { kind = DecoKind.Bush, sprite = LoadSprite("Objects/bush"), sway = true, nameKey = "deco.bush",
                    essenceCost = 2, reward = CurrencyType.Wood, rewardAmount = 1,
                    bonusChance = 1f, bonus = CurrencyType.Coins, bonusAmount = 5, bonusToast = "toast.deco.berries",
                    anim = RemoveAnim.Cut, frames = new[] { R("bush_cut") }, chips = new[] { R("fx_leaf") } },
                new DecoArt { kind = DecoKind.Rock, sprite = LoadSprite("Objects/rock"), sway = false, nameKey = "deco.rock",
                    essenceCost = 6, reward = CurrencyType.Stone, rewardAmount = 3,
                    bonusChance = 0.15f, bonus = CurrencyType.Coins, bonusAmount = 15, bonusToast = "toast.deco.coins",
                    anim = RemoveAnim.Crack, frames = new[] { R("rock_cracked") }, chips = stone, hasRemains = true, remains = DecoKind.Rubble },
                new DecoArt { kind = DecoKind.Stump, sprite = R("stump_round"), nameKey = "deco.stump",
                    essenceCost = 1, reward = CurrencyType.Wood, rewardAmount = 1, anim = RemoveAnim.Poof, chips = new[] { R("fx_woodchip") } },
                new DecoArt { kind = DecoKind.StumpSpruce, sprite = R("stump_spruce"), nameKey = "deco.stump",
                    essenceCost = 1, reward = CurrencyType.Wood, rewardAmount = 1, anim = RemoveAnim.Poof, chips = new[] { R("fx_woodchip") } },
                new DecoArt { kind = DecoKind.StumpDead, sprite = R("stump_dead"), nameKey = "deco.stump",
                    essenceCost = 1, reward = CurrencyType.Wood, rewardAmount = 1, anim = RemoveAnim.Poof, chips = new[] { R("fx_woodchip") } },
                new DecoArt { kind = DecoKind.Rubble, sprite = R("rubble"), nameKey = "deco.rubble",
                    essenceCost = 2, reward = CurrencyType.Stone, rewardAmount = 1, anim = RemoveAnim.Poof, chips = stone },
            };
            // --- Etap 5a: dekoracje stref (§13.8, §14.3). Zabytki (krypta, posąg, schody) są nieusuwalne i opowiadają lore.
            Sprite Z(string n) => LoadSprite("Zones/" + n);
            Sprite Zg(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Zones/{n}_glow.png");
            DecoArt Zone(DecoKind kind, string sprite, string nameKey, int essence, CurrencyType reward, int amount, bool stoneChips,
                         bool sway = false, float loreChance = 0f, DecoKind? remains = null, bool glow = false)
            {
                return new DecoArt
                {
                    kind = kind, sprite = Z(sprite), glowSprite = glow ? Zg(sprite) : null, sway = sway, nameKey = nameKey,
                    essenceCost = essence, reward = reward, rewardAmount = amount,
                    bonusLore = loreChance > 0f, bonusChance = loreChance,
                    anim = remains.HasValue && kind <= DecoKind.Gravestone2 ? RemoveAnim.Crack : RemoveAnim.Poof,
                    frames = remains.HasValue && kind <= DecoKind.Gravestone2 ? new[] { R("gravestone_broken") } : null,
                    chips = stoneChips ? stone : wood, hasRemains = remains.HasValue, remains = remains ?? DecoKind.Rubble,
                };
            }
            DecoArt Landmark(DecoKind kind, string sprite, string nameKey, string lore) =>
                new DecoArt { kind = kind, sprite = Z(sprite), nameKey = nameKey, removable = false, landmark = true, loreKey = lore };
            var S = CurrencyType.Stone; var Wd = CurrencyType.Wood; var C = CurrencyType.Coins;
            art.decorations.AddRange(new[]
            {
                Zone(DecoKind.Gravestone0, "gravestone_0", "deco.gravestone", 3, S, 2, true, false, 0.1f, DecoKind.GravestoneBroken),
                Zone(DecoKind.Gravestone1, "gravestone_1", "deco.gravestone", 3, S, 2, true, false, 0.1f, DecoKind.GravestoneBroken),
                Zone(DecoKind.Gravestone2, "gravestone_2", "deco.gravestone", 3, S, 2, true, false, 0.1f, DecoKind.GravestoneBroken),
                new DecoArt { kind = DecoKind.GravestoneBroken, sprite = R("gravestone_broken"), nameKey = "deco.gravestone_broken",
                              essenceCost = 1, reward = S, rewardAmount = 1, anim = RemoveAnim.Poof, chips = stone },
                Zone(DecoKind.WoodenCross, "Cemetery/cemetery_wooden_cross", "deco.wooden_cross", 1, Wd, 1, false),
                Zone(DecoKind.Obelisk, "Cemetery/cemetery_obelisk", "deco.obelisk", 4, S, 3, true),
                Zone(DecoKind.OpenGrave, "Cemetery/cemetery_open_grave", "deco.open_grave", 2, S, 1, true, false, 0.15f),
                Landmark(DecoKind.Crypt, "Cemetery/cemetery_crypt", "deco.crypt", "lore.crypt"),
                Zone(DecoKind.SkullPile, "Cemetery/cemetery_skull_pile", "deco.skull_pile", 2, S, 1, true, false, 0.2f),
                Zone(DecoKind.Candles, "Cemetery/cemetery_candles", "deco.candles", 1, C, 3, false, glow: true),
                Zone(DecoKind.IronGate, "Cemetery/cemetery_iron_gate", "deco.iron_gate", 3, S, 2, true),
                Zone(DecoKind.Thorns, "Cemetery/cemetery_thorns", "deco.thorns", 1, Wd, 1, false, true),
                Zone(DecoKind.Mangrove, "Swamp/swamp_mangrove", "deco.mangrove", 5, Wd, 3, false, true, 0f, DecoKind.StumpDead),
                Zone(DecoKind.LilyPads, "Swamp/swamp_lily_pads", "deco.lily_pads", 1, C, 4, false, glow: true),
                Zone(DecoKind.RottenLog, "Swamp/swamp_rotten_log", "deco.rotten_log", 2, Wd, 2, false, glow: true),
                Zone(DecoKind.Cattails, "Swamp/swamp_cattails", "deco.cattails", 1, Wd, 1, false, true),
                Zone(DecoKind.Reeds, "reeds", "deco.reeds", 1, Wd, 1, false, true),
                Zone(DecoKind.SwampPool, "swamp_pool", "deco.swamp_pool", 3, S, 1, true),
                Zone(DecoKind.BrokenWall, "Ruins/ruins_broken_wall", "deco.broken_wall", 4, S, 3, true),
                Zone(DecoKind.FallenColumn, "Ruins/ruins_fallen_column", "deco.fallen_column", 4, S, 3, true),
                Landmark(DecoKind.Statue, "Ruins/ruins_statue", "deco.statue", "lore.statue"),
                Landmark(DecoKind.StoneStairs, "Ruins/ruins_stone_stairs", "deco.stone_stairs", "lore.stairs"),
                Zone(DecoKind.RuinsRubble, "Ruins/ruins_rubble", "deco.rubble", 2, S, 1, true),
                Zone(DecoKind.Brazier, "Ruins/ruins_brazier", "deco.brazier", 2, S, 2, true, glow: true),
                Zone(DecoKind.RuneTablet, "Ruins/ruins_rune_tablet", "deco.rune_tablet", 3, S, 2, true, false, 0.3f, null, true),
                Zone(DecoKind.VinePillar, "Ruins/ruins_vine_pillar", "deco.vine_pillar", 3, S, 2, true),
                Zone(DecoKind.BrokenPillar, "broken_pillar", "deco.broken_pillar", 3, S, 2, true),
                Zone(DecoKind.RuinedArch, "ruined_arch", "deco.ruined_arch", 4, S, 3, true),
            });
            art.crow = new[] { Z("Cemetery/cemetery_crow_0"), Z("Cemetery/cemetery_crow_1") };
            art.crowGlow = new[] { Zg("Cemetery/cemetery_crow_0"), Zg("Cemetery/cemetery_crow_1") };
            art.frog = new[] { Z("Swamp/swamp_frog_0"), Z("Swamp/swamp_frog_1") };
            art.frogGlow = new[] { Zg("Swamp/swamp_frog_0"), Zg("Swamp/swamp_frog_1") };
            art.bubbles = new[] { Z("Swamp/swamp_bog_bubbles_0"), Z("Swamp/swamp_bog_bubbles_1"), Z("Swamp/swamp_bog_bubbles_2") };
            art.dust = new[] { R("fx_dust_0"), R("fx_dust_1"), R("fx_dust_2") };
            art.pier = LoadSprite("Sea/pier");
            art.netBuoy = LoadSprite("Sea/net_buoy");
            art.boat = LoadSprite("Sea/boat");
            art.boatGlow = LoadSprite("Sea/boat_glow");
            art.altar = LoadSprite("Sea/altar");
            art.altarGlow = LoadSprite("Sea/altar_glow");
            art.lighthouse = AssetDatabase.LoadAssetAtPath<LampDefinition>($"{DataDir}/Lamps/Lamp_lighthouse.asset");
            art.lamps = new List<LampDefinition>();
            foreach (var id in new[] { "lantern", "skull", "wisp", "mushrooms" })
            {
                var def = AssetDatabase.LoadAssetAtPath<LampDefinition>($"{DataDir}/Lamps/Lamp_{id}.asset");
                if (def != null)
                    art.lamps.Add(def);
            }
            art.frameBuy = LoadSprite("UI/Frames/frame_buy");
            art.frameBuyHover = LoadSprite("UI/Frames/frame_buy_hover");
            art.frameDig = LoadSprite("UI/Frames/frame_dig");
            art.frameDigHover = LoadSprite("UI/Frames/frame_dig_hover");
            art.foam = new[] { LoadSprite("Fx/fx_foam_0"), LoadSprite("Fx/fx_foam_1") };
            art.swayCrop = LoadMat("SwayCrop");
            art.swayCropGlow = LoadMat("SwayCropGlow");
            art.swayTree = LoadMat("SwayTree");
            art.glow = GlowMaterial();
            EditorUtility.SetDirty(art);
            return art;
        }
        const string LocDir = Root + "/Localization";
        const string FontPath = Root + "/Art/Fonts/WyspaPixel.ttf";
        static readonly string[] LocTables = { "glossary", "game", "ui" };

        /// <summary>Motyw UI: elementy 9-slice z Art/Sprites/UI/Kit, ikony z UI/Icons, font i tabele lokalizacji.</summary>
        static UiTheme CreateUiTheme()
        {
            var theme = LoadOrCreate<UiTheme>(ThemePath);
            Sprite Kit(string n) => LoadSprite("UI/Kit/ui_" + n);
            theme.panel = Kit("panel");
            theme.panelPage = Kit("panel_page");
            theme.buttonNormal = Kit("button_normal");
            theme.buttonHover = Kit("button_hover");
            theme.buttonPressed = Kit("button_pressed");
            theme.buttonDisabled = Kit("button_disabled");
            theme.tabActive = Kit("tab_active");
            theme.tabInactive = Kit("tab_inactive");
            theme.slot = Kit("slot");
            theme.slotSelected = Kit("slot_selected");
            theme.slotLocked = Kit("slot_locked");
            theme.tooltip = Kit("tooltip");
            theme.toast = Kit("toast");
            theme.barFrame = Kit("bar_frame");
            theme.barFillEssence = Kit("bar_fill_essence");
            theme.barFillGrowth = Kit("bar_fill_growth");
            theme.barFillRhythm = Kit("bar_fill_rhythm");
            theme.wheel = Kit("wheel");

            theme.icons = new List<NamedSprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { Sprites + "UI/Icons" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("icon_", StringComparison.Ordinal))
                    continue;
                theme.icons.Add(new NamedSprite { name = file.Substring(5), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
            }

            theme.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (theme.font == null)
                Debug.LogWarning("[Wyspa] Brak fontu: " + FontPath);
            var tables = new List<TextAsset>();
            foreach (var t in LocTables)
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{LocDir}/{t}.csv");
                if (ta != null) tables.Add(ta);
                else Debug.LogWarning($"[Wyspa] Brak tabeli lokalizacji: {LocDir}/{t}.csv");
            }
            theme.tables = tables.ToArray();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        // ----------------------------------------------------------------- Etap 3a: materiały efektów
        const string MatDir = DataDir + "/Materials";
        const string ShaderDir = Root + "/Shaders";
        // prototyp (anim.py): rośliny 2 cykle na 48 klatek (10 kl./s), drzewa 1 cykl
        const float CropSwaySpeed = 2f / 4.8f, TreeSwaySpeed = 1f / 4.8f;

        static Material FxMaterialAsset(string name, string shaderFile, Action<Material> configure)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/{shaderFile}.shader");
            if (shader == null)
            {
                Debug.LogWarning($"[Wyspa] Brak shadera {ShaderDir}/{shaderFile}.shader - efekt wyłączony.");
                return null;
            }
            EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void CreateFxMaterials()
        {
            FxMaterialAsset("SwayCrop", "SpriteSway", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", CropSwaySpeed); });
            FxMaterialAsset("SwayCropGlow", "SpriteSwayUnlit", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", CropSwaySpeed); });
            FxMaterialAsset("SwayTree", "SpriteSway", m => { m.SetFloat("_SwayAmount", 1f); m.SetFloat("_SwaySpeed", TreeSwaySpeed); });
            var data = AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + "Ground/water_data.png");
            if (data == null)
                Debug.LogWarning("[Wyspa] Brak Ground/water_data.png - uruchom tools/pixel-proto/export_unity_fx.py.");
            FxMaterialAsset("Water", "Water", m => m.SetTexture("_DataTex", data));
            FxMaterialAsset("Fog", "Fog", null);
        }

        /// <summary>Klikalny obszar = kafelek pod obiektem (podstawa 6 px pod środkiem kafelka), jak w Island.</summary>
        static void TileCollider(GameObject go)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null)
                col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.offset = new Vector2(-0.5f / Ppu, 6f / Ppu);
        }

        static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        /// <summary>Materiał efektu z przełącznikiem (F1): zapasowy = materiał, który renderer miał wcześniej.</summary>
        static void UseFx(SpriteRenderer sr, Material fx, bool hideWithoutFx = false)
        {
            if (sr == null)
                return;
            var fallback = hideWithoutFx ? null : sr.sharedMaterial;
            if (fx != null)
                sr.sharedMaterial = fx;
            sr.gameObject.AddComponent<FxMaterial>().Setup(sr, fx, fallback);
        }

        [MenuItem("Wyspa/3. Zbuduj scenę prototypu", priority = 3)]
        public static void BuildScene()
        {
            var layout = LoadLayout();
            if (layout == null)
            {
                Debug.LogError("[Wyspa] Brak " + LayoutPath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            CreateData();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Assety ładujemy dopiero PO NewScene - tworzenie sceny zwalnia nieużywane obiekty
            // i wcześniejsze referencje stawały się puste (pole Crops w GameLoop było null).
            var db = AssetDatabase.LoadAssetAtPath<CropDatabase>(DataDir + "/CropDatabase.asset");
            var upgradeDb = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(DataDir + "/UpgradeDatabase.asset");
            var glowMat = GlowMaterial();
            var uiTheme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            Material swayCrop = LoadMat("SwayCrop"), swayCropGlow = LoadMat("SwayCropGlow"), swayTree = LoadMat("SwayTree");
            Material waterMat = LoadMat("Water"), fogMat = LoadMat("Fog");
            if (db == null)
                Debug.LogError("[Wyspa] Nie znaleziono " + DataDir + "/CropDatabase.asset - uruchom Wyspa > 2.");
            float W = layout.width, H = layout.height;

            Vector3 Center(float x, float y) => new Vector3((x - W / 2f) / Ppu, (H / 2f - y) / Ppu, 0f);
            Vector3 Anchor(float x, float y) => new Vector3((x + 0.5f - W / 2f) / Ppu, (H / 2f - (y + 1f)) / Ppu, 0f);

            // --- kamera pixel perfect ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = StartView.y / 2f / Ppu;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = OutlineColor;
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = Ppu;
            ppc.refResolutionX = StartView.x;   // kadr rośnie z wyspą (ViewController)
            ppc.refResolutionY = StartView.y;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;

            // --- światło globalne (pora dnia) ---
            var globalGo = new GameObject("Global Light 2D");
            var global = globalGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
            global.color = Color.white;

            // --- wyspa z kafelków (Etap 3b): ląd, woda i mgła malowane w locie przez Island ---
            var world = new GameObject("World").transform;
            // woda liczona w shaderze (ryby, fale, przybój); bez shadera widać statyczną wodę z Island
            var water = MakeSprite("Water", world, LoadSprite("Ground/water_base"), Vector3.zero, -2000);
            UseFx(water, waterMat);
            var islandSr = MakeSprite("Land", world, null, Vector3.zero, -1900);
            // mgła nad morzem, gęstnieje z odległością od brzegu (sprite = mapa danych wody, rysuje tylko shader)
            var fog = MakeSprite("Fog", world, LoadSprite("Ground/water_data"), Vector3.zero, 5000);
            UseFx(fog, fogMat, true);

            // --- obiekty stawiane na kafelkach (do Etapu 3c samoczynnie, na wolnej trawie) ---
            var objectsRoot = new GameObject("TileObjects").transform;
            var tileObjects = new List<TileObject>();
            TileObject AsTileObject(Component c, string id, string nameKey, TileObject anchor = null, Vector2Int offset = default)
            {
                var t = c.gameObject.AddComponent<TileObject>();
                t.Setup(id, nameKey, anchor, offset);
                c.gameObject.SetActive(false);
                tileObjects.Add(t);
                return t;
            }

            // skrzynia na plony: pojawia się z ognikiem albo skrzynią kupca
            var fill = new[] { LoadSprite("Automation/harvest_crate_0"), LoadSprite("Automation/harvest_crate_1"),
                               LoadSprite("Automation/harvest_crate_2") };
            var crateSr = MakeSprite("HarvestCrate", objectsRoot, fill[0], Vector3.zero, 0);
            var crate = crateSr.gameObject.AddComponent<HarvestCrate>();
            TileCollider(crate.gameObject);
            crate.Setup(fill, crateSr);
            var crateTile = AsTileObject(crate, "crate", "bld.harvest_crate");

            var wispFrames = new Sprite[4];
            var wispGlow = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                wispFrames[i] = LoadSprite($"Automation/wisp_gardener_{i}");
                wispGlow[i] = LoadSprite($"Automation/wisp_gardener_{i}_glow");
            }
            var wispBody = MakeSprite("WispGardener", objectsRoot, wispFrames[0], Vector3.zero, 0);
            var wispGlowSr = MakeGlow(wispBody, wispGlow[0], 1, glowMat);
            var wisp = wispBody.gameObject.AddComponent<WispGardener>();
            wisp.Setup(wispBody, wispGlowSr, wispFrames, wispGlow);
            AsTileObject(wisp, "wisp", "bld.wisp_gardener", crateTile, new Vector2Int(14, 8));   // unosi się przy skrzyni

            var chestBody = MakeSprite("MerchantChest", objectsRoot, LoadSprite("Automation/merchant_chest"), Vector3.zero, 0);
            var chestGlow = MakeGlow(chestBody, LoadSprite("Automation/merchant_chest_glow"), 1, glowMat);
            var chest = chestBody.gameObject.AddComponent<MerchantChest>();
            chest.Setup(chestBody, LoadSprite("Automation/merchant_chest"), LoadSprite("Automation/merchant_chest_open"),
                chestGlow, LoadSprite("Automation/merchant_chest_glow"), LoadSprite("Automation/merchant_chest_open_glow"));
            TileCollider(chest.gameObject);
            AsTileObject(chest, "merchant_chest", "bld.merchant_chest");

            var rune = MakeSprite("RuneStone", objectsRoot, LoadSprite("Automation/rune_stone"), Vector3.zero, 0);
            MakeGlow(rune, LoadSprite("Automation/rune_stone_glow"), 1, glowMat);
            TileCollider(rune.gameObject);
            AsTileObject(rune, "rune", "bld.soil_memory");

            // lampy (także startową latarnię) stawia Island w trakcie gry: IslandArt.lamps, sklep, tryb stawiania

            // nocne zdarzenia (Etap 5b): ogniki, spadające gwiazdy, potwór
            Sprite E(string n) => LoadSprite("Events/" + n);
            Sprite Eg(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}Events/{n}_glow.png");
            var eventsGo = new GameObject("NightEvents");
            eventsGo.AddComponent<Wyspa.Sea.NightEvents>().Setup(
                new[] { LoadSprite("Automation/wisp_gardener_0"), LoadSprite("Automation/wisp_gardener_1"),
                        LoadSprite("Automation/wisp_gardener_2"), LoadSprite("Automation/wisp_gardener_3") },
                new[] { LoadSprite("Automation/wisp_gardener_0_glow"), LoadSprite("Automation/wisp_gardener_1_glow"),
                        LoadSprite("Automation/wisp_gardener_2_glow"), LoadSprite("Automation/wisp_gardener_3_glow") },
                new[] { E("falling_star_0"), E("falling_star_1"), E("falling_star_2") },
                new[] { Eg("falling_star_0"), Eg("falling_star_1"), Eg("falling_star_2") },
                E("star_shard"), Eg("star_shard"),
                new[] { E("tentacle_0"), E("tentacle_1"), E("tentacle_2") },
                new[] { E("monster_eye_0"), E("monster_eye_1"), E("monster_eye_2") }, Eg("monster_eye_2"), glowMat);

            // kupiec-widmo (Etap 4b): łódź pod wyspą raz na noc; obiekt zawsze aktywny (harmonogram), sprite'y chowa sam
            var merchantGo = new GameObject("GhostMerchant");
            merchantGo.transform.SetParent(world, false);
            var merchantBody = MakeSprite("Body", merchantGo.transform, LoadSprite("Sea/ghost_merchant"), Vector3.zero, 700);
            merchantBody.transform.localPosition = Vector3.zero;
            var merchantGlow = MakeGlow(merchantBody, LoadSprite("Sea/ghost_merchant_glow"), 701, glowMat);
            var merchantArea = merchantGo.AddComponent<BoxCollider2D>();
            merchantArea.size = new Vector2(2f, 2f);
            merchantArea.offset = new Vector2(0f, 1f);
            merchantGo.AddComponent<Wyspa.Sea.GhostMerchant>().Setup(merchantBody, merchantGlow, merchantArea);

            var islandArt = AssetDatabase.LoadAssetAtPath<IslandArt>(IslandArtPath);
            var waterAnim = water.gameObject.AddComponent<WaterAnimator>();
            var island = world.gameObject.AddComponent<Island>();
            island.Setup(islandArt, islandSr, water, fog, waterAnim, tileObjects);

            // --- logika gry ---
            var game = new GameObject("Game");
            var activity = game.AddComponent<ActivityTracker>();
            var clock = game.AddComponent<GameClock>();
            var essence = game.AddComponent<EssenceStore>();
            var wallet = game.AddComponent<Wallet>();
            var save = game.AddComponent<SaveSystem>();
            var rhythm = game.AddComponent<WorkRhythm>();
            var upgradeState = game.AddComponent<UpgradeState>();
            var loop = game.AddComponent<GameLoop>();
            game.AddComponent<AppPerformance>();
            var automation = game.AddComponent<AutomationSystem>();
            automation.Setup(crate, wisp, chest, rune.gameObject);
            camGo.AddComponent<ViewController>().Setup(ppc);
            var dayNight = game.AddComponent<DayNightLighting>();
            var click = game.AddComponent<ClickRouter>();
            game.AddComponent<Wyspa.Lore.Chronicle>();
            game.AddComponent<DebugHud>();

            SetRefs(loop, ("activity", activity), ("clock", clock), ("essence", essence), ("wallet", wallet),
                ("crops", db), ("saveSystem", save), ("rhythm", rhythm), ("upgrades", upgradeState), ("automation", automation), ("island", island));
            SetRefs(upgradeState, ("database", upgradeDb));
            SetRefs(dayNight, ("clock", clock), ("globalLight", global));
            SetRefs(click, ("targetCamera", cam));

            // --- efekty: woda, mgła, cząsteczki (Etap 3a) ---
            waterAnim.Setup(water, clock);
            fog.gameObject.AddComponent<FogController>().Setup(fog, clock);
            var fxGo = new GameObject("Fx");
            var particles = fxGo.AddComponent<PixelParticles>();
            particles.Setup(glowMat);
            Sprite Fx(string n) => LoadSprite("Fx/" + n);
            // świetliki i spadające liście biorą obszar lądu i drzewa z Island
            fxGo.AddComponent<IslandFx>().Setup(particles, clock, glowMat,
                new[] { Fx("fx_soil_0"), Fx("fx_soil_1") }, new[] { Fx("fx_leaf_0"), Fx("fx_leaf_1") }, Fx("fx_leaf_fall"),
                new[] { Fx("fx_sparkle_0"), Fx("fx_sparkle_1"), Fx("fx_sparkle_2") }, new[] { Fx("fx_firefly_0"), Fx("fx_firefly_1") },
                new Transform[0], new Rect(-2f, -2f, 4f, 4f));

            // --- UI (płótno budowane w kodzie przy starcie, GDD §12) ---
            var ui = new GameObject("UI");
            var uiRoot = ui.AddComponent<UiRoot>();
            SetRefs(uiRoot, ("theme", uiTheme));

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Wyspa] Scena zapisana: " + ScenePath + ". Naciśnij Play.");
        }

        // =====================================================================
        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer MakeGlow(SpriteRenderer parent, Sprite sprite, int order, Material glowMat)
        {
            var glow = MakeSprite("Glow", parent.transform, sprite, Vector3.zero, order);
            glow.transform.localPosition = Vector3.zero;
            if (glowMat != null)
                glow.sharedMaterial = glowMat;
            return glow;
        }

        static Sprite LoadSprite(string relative)
        {
            string path = Sprites + relative + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning("[Wyspa] Brak sprite'a: " + path);
            return sprite;
        }

        static Layout LoadLayout()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            return text != null ? JsonUtility.FromJson<Layout>(text.text) : null;
        }

        static Material GlowMaterial()
        {
            const string path = DataDir + "/GlowUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                Debug.LogWarning("[Wyspa] Nie znaleziono shadera Sprite-Unlit-Default (URP). Glow lamp będzie przyciemniany w nocy.");
                return null;
            }
            EnsureFolder(DataDir);
            mat = new Material(shader) { name = "GlowUnlit" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void SetRefs(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[Wyspa] {target.GetType().Name}: brak pola '{field}'");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
