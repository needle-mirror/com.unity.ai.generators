using System;
using System.IO;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Texture";
        public const string defaultNewAssetNameAlt = "New Sprite";
        public const string defaultAssetExtension = ".png";

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

        public static string CreateBlankSprite(string path, bool force = true)
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

        static Texture2D CreateTexture(string name, bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{name}{defaultAssetExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankTexture(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create texture file for '{path}'.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Selection.activeObject = texture;
            return texture;
        }

        static Texture2D CreateSprite(string name, bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{name}{defaultAssetExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankSprite(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create sprite file for '{path}'.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Selection.activeObject = texture;
            return texture;
        }

        public static Texture2D CreateAndSelectBlankTexture(bool force = true) => CreateTexture(defaultNewAssetName, force);

        public static Texture2D CreateAndSelectBlankSprite(bool force = true) => CreateSprite(defaultNewAssetNameAlt, force);
    }
}
