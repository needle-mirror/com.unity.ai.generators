using System;
using System.IO;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

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
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            Selection.activeObject = material;
            return material;
        }

        public static UnityEngine.Material CreateMaterialFromShaderGraph(string shaderGraphPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderGraphPath);
            if (shader == null)
            {
                Debug.LogError($"Could not load shader from {shaderGraphPath}");
                return null;
            }

            var shaderGraphName = Path.GetFileNameWithoutExtension(shaderGraphPath);
            var materialName = $"{shaderGraphName} Material";

            var directory = Path.GetDirectoryName(shaderGraphPath);
            var materialPath = Path.Combine(directory, materialName + ".mat");
            materialPath = AssetDatabase.GenerateUniqueAssetPath(materialPath);

            var material = new UnityEngine.Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(material);
            Selection.activeObject = material;

            return material;
        }

        public static bool IsShaderGraph(Object obj)
        {
            if (obj == null)
                return false;

            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase);
        }
    }
}
