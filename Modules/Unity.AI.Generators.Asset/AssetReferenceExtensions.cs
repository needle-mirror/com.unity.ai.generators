using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Toolkit.Compliance;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Asset
{
    static class AssetReferenceExtensions
    {
        internal static string GetGeneratedAssetsRoot() => "Generated Assets";
        public static string GetGeneratedAssetsPath(string assetGuid) => Path.Combine(GetGeneratedAssetsRoot(), assetGuid);
        public static string GetGeneratedAssetsPath(this AssetReference asset) => GetGeneratedAssetsPath(asset.GetGuid());

        public static string GetPath(this AssetReference asset) => !asset.IsValid() ? string.Empty : AssetDatabase.GUIDToAssetPath(asset.guid);

        public static string GetGuid(this AssetReference asset) => asset.guid;

        public static Uri GetUri(this AssetReference asset) => !asset.IsValid() ? null : new(Path.GetFullPath(asset.GetPath()));

        public static bool IsValid(this AssetReference asset) => asset != null && !string.IsNullOrEmpty(asset.GetGuid());

        public static bool Exists(this AssetReference asset)
        {
            var path = asset.GetPath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public static void EnableGenerationLabel(this AssetReference asset)
        {
            var actual = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.GetPath());
            actual?.EnableGenerationLabel();
        }

        public static void EnableGenerationLabel(this UnityEngine.Object asset)
        {
            var labelList = new List<string>(AssetDatabase.GetLabels(asset));
            if (labelList.Contains(Legal.UnityAIGeneratedLabel))
                return;
            labelList.Add(Legal.UnityAIGeneratedLabel);
            AssetDatabase.SetLabels(asset, labelList.ToArray());
        }

        public static bool FixObjectName(this AssetReference asset)
        {
            var actual = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.GetPath());
            return actual && actual.FixObjectName();
        }

        public static bool FixObjectName(this UnityEngine.Object asset)
        {
            if (asset == null)
                return false;

            var desiredName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
            if (asset.name == desiredName)
                return false;

            asset.name = desiredName;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

        public static bool TryGetProjectAssetsRelativePath(string path, out string projectPath)
        {
            projectPath = null;
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var normalizedAbsolutePath = Path.GetFullPath(path).Replace('\\', '/');
                var normalizedDataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');

                if (normalizedAbsolutePath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
                {
                    var remainingPath = normalizedAbsolutePath[normalizedDataPath.Length..];
                    projectPath = "Assets" + (remainingPath.StartsWith("/") ? remainingPath : "/" + remainingPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing path: {ex.Message}");
                return false;
            }

            return false;
        }

        public static bool ImportAsset(string path)
        {
            if (!TryGetProjectAssetsRelativePath(path, out var assetPath))
                return false;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            asset.EnableGenerationLabel();
            return true;
        }
    }
}
