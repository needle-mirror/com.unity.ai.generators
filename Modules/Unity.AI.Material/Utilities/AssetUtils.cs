using System;
using System.IO;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.AI.Material.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Material";
        public const string defaultAssetExtension = ".mat";
        
        public static string CreateBlankMaterial(string path, bool force = true)
        {
            var defaultShader = GraphicsSettings.defaultRenderPipeline ? GraphicsSettings.defaultRenderPipeline.defaultShader : Shader.Find("Standard");
            var newMaterial = new UnityEngine.Material(defaultShader);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(newMaterial, assetPath);
            AssetDatabase.Refresh();
            return assetPath;
        }

        public static UnityEngine.Material CreateAndSelectBlankMaterial(bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{defaultNewAssetName}{defaultAssetExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankMaterial(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create material for '{path}'.");
                AssetDatabase.ImportAsset(path);
                AssetDatabase.Refresh();
            }
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            Selection.activeObject = material;
            return material;
        }
    }
}
