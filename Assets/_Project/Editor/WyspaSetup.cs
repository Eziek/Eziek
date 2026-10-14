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

        struct CropSpec { public string id, name, sprite; public CropKind kind; public float seconds; public int seed, sell; }
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
            new CropSpec { id = "bloodrose", name = "Krwawa róża", sprite = "crop_bloodrose", kind = CropKind.Night, seconds = 8 * 3600, seed = 50, sell = 200 },
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
            PlayerSettings.defaultScreenWidth = w * WindowScale;
            PlayerSettings.defaultScreenHeight = h * WindowScale;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

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
                crop.stages = new Sprite[4];
                for (int i = 0; i < 4; i++)
                    crop.stages[i] = LoadSprite($"Crops/{spec.sprite}_{i}");
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
                    EditorUtility.SetDirty(lamp);
                }
            }

            CreateUpgrades();
            GlowMaterial();
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
            cam.orthographicSize = H / 2f / Ppu;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = OutlineColor;
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = Ppu;
            ppc.refResolutionX = layout.width;
            ppc.refResolutionY = layout.height;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;

            // --- światło globalne (pora dnia) ---
            var globalGo = new GameObject("Global Light 2D");
            var global = globalGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
            global.color = Color.white;

            // --- teren ---
            var world = new GameObject("World").transform;
            MakeSprite("Water", world, LoadSprite("Ground/water"), Vector3.zero, -2000);
            MakeSprite("Island", world, LoadSprite("Ground/island"), Vector3.zero, -1900);

            HarvestCrate crate = null;
            foreach (var o in layout.objects)
            {
                if (o.sprite == "crate")
                {
                    // skrzynia z prototypu staje się skrzynią na plony (automatyka, Etap 2b)
                    var fill = new[] { LoadSprite("Automation/harvest_crate_0"), LoadSprite("Automation/harvest_crate_1"),
                                       LoadSprite("Automation/harvest_crate_2") };
                    var sr = MakeSprite("HarvestCrate", world, fill[0], Anchor(o.x, o.y), o.y);
                    crate = sr.gameObject.AddComponent<HarvestCrate>();
                    if (fill[0] != null)
                        sr.GetComponent<BoxCollider2D>().size = fill[0].bounds.size;
                    crate.Setup(fill, sr);
                    continue;
                }
                MakeSprite(o.sprite, world, LoadSprite("Objects/" + o.sprite), Anchor(o.x, o.y), o.y);
            }

            // --- automatyka: obiekty pojawiają się po zakupie ulepszeń (GDD §11.3 E) ---
            var autoRoot = new GameObject("Automation").transform;
            var wispFrames = new Sprite[4];
            var wispGlow = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                wispFrames[i] = LoadSprite($"Automation/wisp_gardener_{i}");
                wispGlow[i] = LoadSprite($"Automation/wisp_gardener_{i}_glow");
            }
            var wispBody = MakeSprite("WispGardener", autoRoot, wispFrames[0], Anchor(196, 88), 88);
            var wispGlowSr = MakeGlow(wispBody, wispGlow[0], 89, glowMat);
            var wisp = wispBody.gameObject.AddComponent<WispGardener>();
            wisp.Setup(wispBody, wispGlowSr, wispFrames, wispGlow);
            wispBody.gameObject.SetActive(false);

            var chestBody = MakeSprite("MerchantChest", autoRoot, LoadSprite("Automation/merchant_chest"), Anchor(198, 132), 132);
            var chestGlow = MakeGlow(chestBody, LoadSprite("Automation/merchant_chest_glow"), 133, glowMat);
            var chest = chestBody.gameObject.AddComponent<MerchantChest>();
            chest.Setup(chestBody, LoadSprite("Automation/merchant_chest"), LoadSprite("Automation/merchant_chest_open"),
                chestGlow, LoadSprite("Automation/merchant_chest_glow"), LoadSprite("Automation/merchant_chest_open_glow"));
            chestBody.gameObject.SetActive(false);

            var rune = MakeSprite("RuneStone", autoRoot, LoadSprite("Automation/rune_stone"), Anchor(126, 142), 142);
            MakeGlow(rune, LoadSprite("Automation/rune_stone_glow"), 143, glowMat);
            rune.gameObject.SetActive(false);

            // --- grządki ---
            var plotSprite = LoadSprite("Ground/plot");
            var plotsRoot = new GameObject("Plots").transform;
            foreach (var p in layout.plots)
            {
                var sr = MakeSprite($"Plot_r{p.row}_c{p.col}", plotsRoot, plotSprite, Center(p.x, p.y), -1000);
                var plot = sr.gameObject.AddComponent<FarmPlot>();
                var col = sr.GetComponent<BoxCollider2D>();
                if (plotSprite != null)
                    col.size = plotSprite.bounds.size;
                var crop = MakeSprite("Crop", sr.transform, null, Vector3.zero, p.cropY);
                crop.transform.position = Anchor(p.cropX, p.cropY);
                plot.Setup($"plot_{p.row}_{p.col}", crop);
            }

            // --- lampy ---
            var lampsRoot = new GameObject("Lamps").transform;
            foreach (var l in layout.lamps)
            {
                var def = AssetDatabase.LoadAssetAtPath<LampDefinition>($"{DataDir}/Lamps/Lamp_{l.id}.asset");
                var body = MakeSprite("Lamp_" + l.id, lampsRoot, LoadSprite("Lamps/" + l.sprite), Anchor(l.x, l.y), l.y);
                var glow = MakeSprite("Glow", body.transform, LoadSprite("Lamps/" + l.sprite + "_glow"), Vector3.zero, l.y + 1);
                glow.transform.localPosition = Vector3.zero;
                if (glowMat != null)
                    glow.sharedMaterial = glowMat;

                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(body.transform, false);
                lightGo.transform.position = Center(l.lightX, l.lightY);
                var light = lightGo.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                if (def != null)
                {
                    light.pointLightOuterRadius = def.radius;
                    light.pointLightInnerRadius = def.radius * 0.25f;
                    light.color = def.color;
                    light.intensity = def.intensity;
                }
                light.falloffIntensity = 0.6f;

                var lamp = body.gameObject.AddComponent<Lamp>();
                lamp.Setup(def, light, glow);
            }

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
            var dayNight = game.AddComponent<DayNightLighting>();
            var click = game.AddComponent<ClickRouter>();
            game.AddComponent<DebugHud>();

            SetRefs(loop, ("activity", activity), ("clock", clock), ("essence", essence), ("wallet", wallet),
                ("crops", db), ("saveSystem", save), ("rhythm", rhythm), ("upgrades", upgradeState), ("automation", automation));
            SetRefs(upgradeState, ("database", upgradeDb));
            SetRefs(dayNight, ("clock", clock), ("globalLight", global));
            SetRefs(click, ("targetCamera", cam));

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
