using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Texture";
        public const string defaultNewAssetNameSprite = "New Sprite";
        public const string defaultNewAssetNameCube = "New Cubemap";
        public const string defaultAssetExtension = ".png";
        public static readonly IReadOnlyList<string> supportedAssetExtensions = ImageFileUtilities.knownExtensions.Append(SpriteSheetExtensions.defaultAssetExtension).ToArray();

        static string CreateBlankTexture(string path, bool force, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false) {hideFlags = HideFlags.HideAndDontSave};
            try
            {
                // Fill texture with clear color
                var clearColor = new Color(0, 0, 0, 0);
                var pixels = new Color[width * height];
                Array.Fill(pixels, clearColor);
                texture.SetPixels(pixels);
                texture.Apply();

                var bytes = texture.EncodeToPNG();
                if (bytes == null)
                    return string.Empty;

                path = Path.ChangeExtension(path, ".png");
                if (force || !File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    FileIO.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                return path;
            }
            finally
            {
                texture.SafeDestroy();
            }
        }

        public static string CreateBlankTexture(string path, bool force = true)
        {
            const int size = 256;
            var texturePath = CreateBlankTexture(path, force, size, size);
            if (string.IsNullOrEmpty(texturePath))
                return string.Empty;

            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.alphaIsTransparency = true;
                textureImporter.SaveAndReimport();
            }

            return texturePath;
        }

        public static string CreateBlankSprite(string path) => CreateBlankSprite(path, false);

        public static string CreateBlankSprite(string path, bool force)
        {
            const int size = 1024;
            var texturePath = CreateBlankTexture(path, force, size, size);
            if (string.IsNullOrEmpty(texturePath))
                return string.Empty;

            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.spritePixelsPerUnit = size;
                textureImporter.SaveAndReimport();
            }

            return texturePath;
        }

        public static string CreateBlankCubemap(string path) => CreateBlankCubemap(path, false);

        public static string CreateBlankCubemap(string path, bool force)
        {
            const int size = 1024;
            var texturePath = CreateBlankTexture(path, force, size, size);
            if (string.IsNullOrEmpty(texturePath))
                return string.Empty;

            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.textureShape = TextureImporterShape.TextureCube;
                textureImporter.SaveAndReimport();
            }

            return texturePath;
        }
    }
}
