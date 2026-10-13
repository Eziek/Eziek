using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wyspa.EditorTools
{
    /// <summary>
    /// Każdy PNG w Assets/_Project/Art/Sprites dostaje ustawienia pixel art:
    /// Sprite, PPU 16, filtr Point, bez kompresji i mipmap, pivot z pivots.json (punkt styku z ziemią).
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const string SpritesRoot = "Assets/_Project/Art/Sprites/";
        public const int PixelsPerUnit = 16;
        const string PivotsFile = SpritesRoot + "pivots.json";

        [Serializable]
        class PivotList
        {
            public List<PivotItem> items = new List<PivotItem>();
        }

        [Serializable]
        class PivotItem
        {
            public string name;
            public float x;
            public float y;
        }

        static Dictionary<string, Vector2> pivots;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(SpritesRoot, StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // pełny prostokąt - miejsce na kołysanie w shaderze
            settings.spriteExtrude = 0;
            if (TryGetPivot(Path.GetFileNameWithoutExtension(assetPath), out var pivot))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            importer.SetTextureSettings(settings);
        }

        static bool TryGetPivot(string spriteName, out Vector2 pivot)
        {
            if (pivots == null)
            {
                pivots = new Dictionary<string, Vector2>();
                if (File.Exists(PivotsFile))
                {
                    var list = JsonUtility.FromJson<PivotList>(File.ReadAllText(PivotsFile));
                    if (list != null)
                        foreach (var item in list.items)
                            pivots[item.name] = new Vector2(item.x, item.y);
                }
            }
            return pivots.TryGetValue(spriteName, out pivot);
        }

        /// <summary>Wymusza ponowny import sprite'ów (np. po zmianie pivots.json).</summary>
        [MenuItem("Wyspa/Narzędzia/Reimportuj sprite'y")]
        public static void ReimportSprites()
        {
            pivots = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesRoot.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
