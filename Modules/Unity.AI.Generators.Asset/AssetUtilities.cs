using System.ComponentModel;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Asset
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AssetUtilities
    {
        public static string GetSelectionPath()
        {
            if (!Selection.activeObject)
                return ProjectWindowUtilWrapper.GetActiveFolderPath();
            var assetSelectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetSelectionPath))
                return ProjectWindowUtilWrapper.GetActiveFolderPath();
            var isFolder = File.GetAttributes(assetSelectionPath).HasFlag(FileAttributes.Directory);
            var path = !isFolder ? GetAssetFolder(Selection.activeObject) : assetSelectionPath;
            return path;
        }

        // very useful when displaying the project view under one-column layout
        static string GetAssetFolder(Object asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var folderPath = Path.GetDirectoryName(assetPath);
                return folderPath;
            }
            return null;
        }
    }
}
