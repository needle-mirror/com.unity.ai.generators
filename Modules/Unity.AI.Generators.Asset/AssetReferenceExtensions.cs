using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Toolkit.Compliance;
using UnityEditor;

namespace Unity.AI.Generators.Asset
{
    static class AssetReferenceExtensions
    {
        public static string GetGeneratedAssetsPath(string assetGuid) => Path.Combine("Generated Assets", assetGuid);
        public static string GetGeneratedAssetsPath(this AssetReference asset) => GetGeneratedAssetsPath(asset.GetGuid());

        public static string GetPath(this AssetReference asset) => !asset.IsValid() ? string.Empty : AssetDatabase.GUIDToAssetPath(asset.guid);

        public static string GetGuid(this AssetReference asset) => asset.guid;

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

        public static void FixObjectName(this AssetReference asset)
        {
            var actual = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.GetPath());
            actual?.FixObjectName();
        }

        public static void FixObjectName(this UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            var desiredName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
            if (asset.name == desiredName)
                return;

            asset.name = desiredName;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
