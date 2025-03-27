using System;
using System.IO;
using UnityEditor;

namespace Unity.AI.Generators.Asset
{
    class AssetReferenceDeletionProcessor : AssetModificationProcessor
    {
        static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var asset = new AssetReference { guid = guid };
            var folderPath = asset.GetGeneratedAssetsPath();

            if (!Directory.Exists(folderPath))
                return AssetDeleteResult.DidNotDelete;

            var deletedFolderPath = folderPath + "_deleted";
            if (!Directory.Exists(deletedFolderPath))
                Directory.Move(folderPath, deletedFolderPath);
            else
            {
                CopyContents(folderPath, deletedFolderPath);
                Directory.Delete(folderPath, true);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        static void CopyContents(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(sourceFolder) || !Directory.Exists(destFolder))
                return;

            var files = Directory.GetFiles(sourceFolder);
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                var destFilePath = Path.Combine(destFolder, fileName);
                try { File.Move(filePath, destFilePath); }
                catch { /* ignored */ }
            }
        }
    }
}
