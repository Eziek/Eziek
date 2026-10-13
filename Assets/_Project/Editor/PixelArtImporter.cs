using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wyspa.EditorTools
{
    /// <summary>
    /// Każdy PNG w Assets/_Project/Art/Sprites dostaje ustawienia pixel art:
    /// Sprite, PPU 16, filtr Point, bez kompresji i mipmap, pivot z pivots.json (punkt styku z ziemią),
    /// a dla elementów UI także granice 9-slice ("border": [L, B, R, T] w pikselach).
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
            public int[] border;
        }

        struct SpriteMeta
        {
            public Vector2 pivot;
            public Vector4 border;
        }

        static Dictionary<string, SpriteMeta> metas;

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
            if (TryGetMeta(Path.GetFileNameWithoutExtension(assetPath), out var meta))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = meta.pivot;
                settings.spriteBorder = meta.border; // Vector4(L, B, R, T); zero = brak 9-slice
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            importer.SetTextureSettings(settings);
        }

        static bool TryGetMeta(string spriteName, out SpriteMeta meta)
        {
            if (metas == null)
            {
                metas = new Dictionary<string, SpriteMeta>();
                if (File.Exists(PivotsFile))
                {
                    var list = JsonUtility.FromJson<PivotList>(File.ReadAllText(PivotsFile));
                    if (list != null)
                    {
                        foreach (var item in list.items)
                        {
                            var b = item.border != null && item.border.Length == 4
                                ? new Vector4(item.border[0], item.border[1], item.border[2], item.border[3])
                                : Vector4.zero;
                            metas[item.name] = new SpriteMeta { pivot = new Vector2(item.x, item.y), border = b };
                        }
                    }
                }
            }
            return metas.TryGetValue(spriteName, out meta);
        }

        /// <summary>Wymusza ponowny import sprite'ów (np. po zmianie pivots.json).</summary>
        [MenuItem("Wyspa/Narzędzia/Reimportuj sprite'y")]
        public static void ReimportSprites()
        {
            metas = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesRoot.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
