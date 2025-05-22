using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Compliance;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Asset
{
    static class AssetReferenceExtensions
    {
        internal static string GetGeneratedAssetsRoot() => "GeneratedAssets";
        public static string GetGeneratedAssetsPath(string assetGuid) => Path.Combine(GetGeneratedAssetsRoot(), assetGuid);
        public static string GetGeneratedAssetsPath(this AssetReference asset) => GetGeneratedAssetsPath(asset.GetGuid());

#if UNITY_AI_GENERATED_ASSETS_FOLDER_FIX
        [InitializeOnLoadMethod]
        static void FixGeneratedAssetsFolderOnLoad()
        {
            if (Application.isBatchMode)
                return;

            FolderIO.TryMergeAndAlwaysDeleteFolder(k_GeneratedAssetsRootDeprecated, GetGeneratedAssetsRoot());
        }

        const string k_GeneratedAssetsRootDeprecated = "Generated Assets";
#endif
        public static string GetPath(this AssetReference asset) => !asset.IsValid() ? string.Empty : AssetDatabase.GUIDToAssetPath(asset.guid);

        public static string GetGuid(this AssetReference asset) => asset.guid;

        public static Uri GetUri(this AssetReference asset) => !asset.IsValid() ? null : new(Path.GetFullPath(asset.GetPath()));

        public static bool IsValid(this AssetReference asset) => asset != null && !string.IsNullOrEmpty(asset.GetGuid());

        public static bool Exists(this AssetReference asset)
        {
            var path = asset.GetPath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public static bool IsImported(this AssetReference asset) => asset.IsValid() && AssetDatabase.LoadMainAssetAtPath(asset.GetPath());

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
            AssetDatabase.Refresh(); // Force refresh to ensure the label is applied, this is done very sparingly

            if (Selection.activeObject != asset || asset is not AudioClip)
                return;

            _ = RefreshInspector();
            return;

            async Task RefreshInspector()
            {
                if (Selection.activeObject != asset)
                    return;

                try
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets"); // null doesn't apparently force a refresh
                    await EditorTask.Yield();
                }
                finally
                {
                    Selection.activeObject = asset;
                }
            }
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
            asset.SafeCall(AssetDatabase.SaveAssetIfDirty);
            return true;
        }

        public static void SafeCall(this UnityEngine.Object asset, Action<UnityEngine.Object> action)
        {
            try { action?.Invoke(asset); }
            catch { /* ignored */ }
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
