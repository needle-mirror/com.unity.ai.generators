using System;
using System.IO;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Texture";

        public static string CreateBlankTexture(string path, bool force = true)
        {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false) {hideFlags = HideFlags.HideAndDontSave};
            texture.SetPixel(0, 0, Color.clear);
            var bytes = texture.EncodeToPNG();
            if (bytes == null)
                return string.Empty;
            path = Path.ChangeExtension(path, ".png");
            if (force || !File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                FileIO.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);
                AssetDatabase.Refresh();
            }
            return path;
        }

        public static string CreateBlankTexture(string path, int width, int height)
        {
            var blankTexture = new Texture2D(width, height, TextureFormat.ARGB32, false) {hideFlags = HideFlags.HideAndDontSave};
            try
            {
                var clearColor = new Color(0, 0, 0, 0);
                var pixels = new Color[width * height];
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] = clearColor;

                blankTexture.SetPixels(pixels);
                blankTexture.Apply();

                var pngData = blankTexture.EncodeToPNG();
                FileIO.WriteAllBytes(path, pngData);

                AssetDatabase.Refresh();
            }
            finally
            {
                blankTexture.SafeDestroy();
            }
            return path;
        }

        static Texture2D CreateTexture(string name, bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{name}.png";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankTexture(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create texture file for '{path}'.");
                AssetDatabase.ImportAsset(path);
                AssetDatabase.Refresh();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Selection.activeObject = texture;
            return texture;
        }

        public static Texture2D CreateAndSelectBlankTexture(bool force = true) => CreateTexture(defaultNewAssetName, force);
    }
}
