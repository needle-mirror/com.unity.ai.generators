using System;
using System.ComponentModel;
using System.IO;
using UnityEditor;

namespace Unity.AI.Generators.Asset
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AssetUtilities
    {
        public static string GetSelectionPath()
        {
            var assetSelectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            var isFolder = false;
            if (!string.IsNullOrEmpty(assetSelectionPath))
                isFolder = File.GetAttributes(assetSelectionPath).HasFlag(FileAttributes.Directory);
            var path = ProjectWindowUtilWrapper.GetActiveFolderPath();
            if (isFolder)
                path = assetSelectionPath;
            return path;
        }
    }
}
