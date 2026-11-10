using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>
    /// Автонастройка импорта сгенерированной графики:
    /// Art/UI — спрайты (9-slice границы из ui_slices.json), Art/UI/Cursors — курсоры, Art/FX — текстуры эффектов.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        public const string UiDir = "Assets/_Project/Art/UI";
        public const string FxDir = "Assets/_Project/Art/FX";
        private const string SlicesFile = UiDir + "/ui_slices.json";

        [Serializable]
        private sealed class SliceEntry
        {
            public string path;
            public int l, b, r, t;
        }

        [Serializable]
        private sealed class SliceFile
        {
            public List<SliceEntry> borders = new List<SliceEntry>();
        }

        private static Dictionary<string, Vector4> borders;

        private static Vector4 BorderFor(string assetPath)
        {
            if (borders == null)
            {
                borders = new Dictionary<string, Vector4>();
                if (File.Exists(SlicesFile))
                {
                    var data = JsonUtility.FromJson<SliceFile>(File.ReadAllText(SlicesFile));
                    foreach (SliceEntry e in data.borders)
                        borders["Assets/_Project/Art/" + e.path] = new Vector4(e.l, e.b, e.r, e.t);
                }
            }
            return borders.TryGetValue(assetPath, out Vector4 v) ? v : Vector4.zero;
        }

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            var importer = (TextureImporter)assetImporter;

            if (path.StartsWith(UiDir + "/Cursors/"))
            {
                importer.textureType = TextureImporterType.Cursor;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                return;
            }

            if (path.StartsWith(UiDir + "/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 100f;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = BorderFor(path);
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                return;
            }

            if (path.StartsWith(FxDir + "/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }

        /// <summary>Сбросить кэш границ (после перегенерации ui_slices.json).</summary>
        public static void ReloadSlices() => borders = null;
    }
}
