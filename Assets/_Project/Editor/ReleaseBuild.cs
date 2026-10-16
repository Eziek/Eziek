using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wyspa.EditorTools
{
    /// <summary>
    /// Wydanie gry (docs/RELEASE.md). Menu Wyspa → Wydanie:
    /// 1. Ustawienia: nazwa, firma, wersja, ikony, ekran startowy, jedna kopia gry naraz (ProjectSettings ustawia kod, nie ręcznie).
    /// 2. Zbuduj Windows: build 64-bit do Builds/MistyPackland-wersja-win64, pliki README i licencji obok, bez folderów
    ///    „DoNotShip”, a na końcu archiwum .zip (PowerShell Compress-Archive).
    /// 3. Zbuduj Android: Builds/MistyPackland-wersja.apk do instalacji z pliku (testy na telefonie).
    /// Z linii poleceń: Unity.exe -batchmode -quit -projectPath . -executeMethod Wyspa.EditorTools.ReleaseBuild.BuildWindowsCli
    /// Grafiki (ikony, logo) generuje tools/release/release_art.py.
    /// </summary>
    public static class ReleaseBuild
    {
        public const string ProductName = "Misty Packland";
        public const string CompanyName = "Eziek";              // folder zapisów: LocalLow/Eziek/Misty Packland
        public const string Version = "0.9.0";                   // podbijaj przed każdym wydaniem (docs/RELEASE.md)
        public const string BundleId = "com.eziek.mistypackland";
        const string ExeName = "MistyPackland";
        const string ArtDir = "Assets/_Project/Art/Release/";
        const string ReleaseDir = "release/";                    // README, licencje (poza Assets, katalog główny repo)
        static readonly Color SplashBackground = new Color32(0x12, 0x1c, 0x27, 0xff);

        [MenuItem("Wyspa/Wydanie/1. Ustawienia (nazwa, ikony, ekran startowy)", priority = 20)]
        public static void ApplySettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            // gra na pulpicie z zapisem co chwilę: druga kopia nadpisywałaby zapis pierwszej
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            ImportIcons();
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            var icons = sizes.Select(IconFor).ToArray();
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { IconFor(1024) }, IconKind.Any);   // ikona domyślna

            // ekran startowy: logo gry na nocnym granacie (od Unity 6 logo Unity można wyłączyć także w Personal)
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "splash_logo.png");
            PlayerSettings.SplashScreen.show = logo != null;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = SplashBackground;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            if (logo != null)
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
            else
                Debug.LogWarning("[Wyspa] Brak " + ArtDir + "splash_logo.png - uruchom tools/release/release_art.py.");

            AssetDatabase.SaveAssets();
            Debug.Log($"[Wyspa] Ustawienia wydania: {ProductName} {Version} ({CompanyName}), ikony: {string.Join(", ", sizes)}");
        }

        /// <summary>Ikony i logo ekranu startowego: ostre piksele, bez kompresji, logo jako sprite.</summary>
        static void ImportIcons()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
                    continue;
                bool splash = path.EndsWith("splash_logo.png");
                ti.textureType = splash ? TextureImporterType.Sprite : TextureImporterType.Default;
                if (splash)
                    ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
        }

        /// <summary>Najbliższa wielkość z tools/release (bez skalowania w górę z małych projektów).</summary>
        static Texture2D IconFor(int size)
        {
            int[] have = { 16, 32, 48, 64, 128, 256, 512, 1024 };
            int pick = have.FirstOrDefault(h => h >= size);
            if (pick == 0)
                pick = 1024;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}icon_{pick}.png");
            if (tex == null)
                Debug.LogWarning($"[Wyspa] Brak {ArtDir}icon_{pick}.png - uruchom tools/release/release_art.py.");
            return tex;
        }

        [MenuItem("Wyspa/Wydanie/2. Zbuduj Windows (64-bit, zip)", priority = 21)]
        public static void BuildWindowsMenu()
        {
            string dir = BuildWindows();
            if (dir != null)
                EditorUtility.RevealInFinder(dir);
        }

        /// <summary>Build z linii poleceń (kod wyjścia 1 = błąd).</summary>
        public static void BuildWindowsCli()
        {
            EditorApplication.Exit(BuildWindows() != null ? 0 : 1);
        }

        static string BuildWindows()
        {
            ApplySettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Wyspa] Brak scen w Build Settings - uruchom Wyspa → 3.");
                return null;
            }
            string name = $"{ExeName}-{Version}-win64";
            string dir = Path.GetFullPath(Path.Combine("Builds", name));
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ExeName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Wyspa] Build nieudany: {report.summary.result}, błędów: {report.summary.totalErrors}");
                return null;
            }

            // pliki dla gracza obok gry; foldery „DoNotShip” (symbole debugowania) poza paczkę
            foreach (var f in new[] { "README.txt", "THIRD_PARTY_NOTICES.txt", "PRIVACY.txt" })
            {
                string src = Path.Combine(ReleaseDir, f);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(dir, f), true);
                else
                    Debug.LogWarning("[Wyspa] Brak " + src);
            }
            string symbols = Path.GetFullPath(Path.Combine("Builds", name + "-symbols"));
            if (Directory.Exists(symbols))
                Directory.Delete(symbols, true);
            foreach (var sub in Directory.GetDirectories(dir).Where(d => d.Contains("DoNotShip") || d.Contains("DontShip")))
            {
                Directory.CreateDirectory(symbols);
                Directory.Move(sub, Path.Combine(symbols, Path.GetFileName(sub)));
            }

            Zip(dir, dir + ".zip");
            Debug.Log($"[Wyspa] Gotowe: {dir} ({report.summary.totalSize / (1024 * 1024)} MB)");
            return dir;
        }

        // ------------------------------------------------------------ Android (APK do testów, §9 p. 135)

        [MenuItem("Wyspa/Wydanie/3. Zbuduj Android (APK do testów)", priority = 22)]
        public static void BuildAndroidMenu()
        {
            string apk = BuildAndroid();
            if (apk != null)
                EditorUtility.RevealInFinder(apk);
        }

        /// <summary>
        /// APK do wysłania na telefon (instalacja z pliku, podpis kluczem debug Unity): ARM64, IL2CPP, poziomo (obie strony).
        /// Wymaga modułu Android Build Support (z OpenJDK, Android SDK i NDK) w Unity Hub. Unity przełącza platformę sam
        /// (pierwszy raz trwa długo: przeimportowanie tekstur). Do sklepu Google Play potrzebny .aab i własny klucz (docs/RELEASE.md).
        /// </summary>
        static string BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Wyspa] Brak modułu Android Build Support: Unity Hub → Installs → Unity 6.6 → Add modules → "
                               + "Android Build Support (z OpenJDK oraz Android SDK & NDK Tools).");
                return null;
            }
            ApplySettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Wyspa] Brak scen w Build Settings - uruchom Wyspa → 3.");
                return null;
            }
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.Android.bundleVersionCode = VersionCode();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // współczesne telefony; Google Play wymaga 64 bitów
            // wyspa jest szeroka: tylko poziomo, w obie strony
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;   // .apk do wysłania znajomemu (sklep: .aab)

            Directory.CreateDirectory("Builds");
            string apk = Path.GetFullPath(Path.Combine("Builds", $"{ExeName}-{Version}.apk"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Wyspa] Build Android nieudany: {report.summary.result}, błędów: {report.summary.totalErrors}");
                return null;
            }
            Debug.Log("[Wyspa] Gotowe: " + apk);
            return apk;
        }

        /// <summary>Numer wersji dla Androida z Version („0.9.0” → 900): musi rosnąć z każdym wydaniem.</summary>
        static int VersionCode()
        {
            var parts = Version.Split('.');
            int code = 0;
            for (int i = 0; i < 3; i++)
                code = code * 100 + (i < parts.Length && int.TryParse(parts[i], out int v) ? v : 0);
            return Mathf.Max(1, code);
        }

        static void Zip(string dir, string zip)
        {
            try
            {
                if (File.Exists(zip))
                    File.Delete(zip);
                var psi = new System.Diagnostics.ProcessStartInfo("powershell",
                    $"-NoProfile -Command \"Compress-Archive -Path '{dir}\\*' -DestinationPath '{zip}'\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                        Debug.LogWarning("[Wyspa] Nie udało się spakować builda (PowerShell). Spakuj folder ręcznie.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Wyspa] Nie udało się spakować builda: " + e.Message);
            }
        }
    }
}
