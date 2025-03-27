using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    static class TemporaryAssetUtilities
    {
        const string k_ToolkitTemp = "Assets/AI Toolkit/Temp";

        public static async Task<TemporaryAsset.Scope> ImportAssetsAsync(IEnumerable<string> filenames)
        {
            var tasks = filenames.Select(ImportAssetAsync).ToList();
            var temporaryAssets = await Task.WhenAll(tasks);

            var validAssets = temporaryAssets.Where(asset => asset != null).ToList();
            return new TemporaryAsset.Scope(validAssets);
        }

        public static TemporaryAsset.Scope ImportAssets(IEnumerable<string> filenames)
        {
            var assets = filenames.Select(ImportAsset).ToList();
            var validAssets = assets.Where(asset => asset != null).ToList();
            return new TemporaryAsset.Scope(validAssets);
        }

        public static async Task<TemporaryAsset.Scope> ImportAssetsAsync(IEnumerable<(string filename, byte[] fileContents)> files)
        {
            var tasks = files.Select(pair => ImportAssetAsync(pair.filename, pair.fileContents)).ToList();
            var temporaryAssets = await Task.WhenAll(tasks);

            var validAssets = temporaryAssets.Where(asset => asset != null).ToList();
            return new TemporaryAsset.Scope(validAssets);
        }

        public static TemporaryAsset.Scope ImportAssets(IEnumerable<(string filename, byte[] fileContents)> files)
        {
            var assets = files.Select(pair => ImportAsset(pair.filename, pair.fileContents)).ToList();
            var validAssets = assets.Where(asset => asset != null).ToList();
            return new TemporaryAsset.Scope(validAssets);
        }

        public static TemporaryAsset.Scope ImportAssets(IEnumerable<UnityEngine.Object> objects)
        {
            var assets = objects.Select(ImportAsset).ToList();
            var validAssets = assets.Where(asset => asset != null).ToList();
            return new TemporaryAsset.Scope(validAssets);
        }

        static async Task<TemporaryAsset> ImportAssetAsync(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Debug.LogError($"File not found: {fileName}");
                return null;
            }

            var tempFolder = $"{k_ToolkitTemp}/{Guid.NewGuid():N}";
            Directory.CreateDirectory(tempFolder);

            var destFileName = Path.Combine(tempFolder, Path.GetFileName(fileName));
            try
            {
                File.Copy(fileName, destFileName, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error copying file {fileName} to {destFileName}: {ex}");
                return null;
            }

            AssetDatabase.Refresh();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                while (AssetImporter.GetAtPath(destFileName) == null)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogError($"Timed out waiting for asset importer at path: {destFileName}");
                return null;
            }

            return new TemporaryAsset(new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) }, tempFolder);
        }

        static TemporaryAsset ImportAsset(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Debug.LogError($"File not found: {fileName}");
                return null;
            }

            var tempFolder = $"{k_ToolkitTemp}/{Guid.NewGuid():N}";
            Directory.CreateDirectory(tempFolder);

            var destFileName = Path.Combine(tempFolder, Path.GetFileName(fileName));
            try
            {
                File.Copy(fileName, destFileName, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error copying file {fileName} to {destFileName}: {ex}");
                return null;
            }

            AssetDatabase.ImportAsset(destFileName, ImportAssetOptions.ForceUpdate);

            return new TemporaryAsset(new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) }, tempFolder);
        }

        static async Task<TemporaryAsset> ImportAssetAsync(string fileName, byte[] fileContents)
        {
            var tempFolder = $"{k_ToolkitTemp}/{Guid.NewGuid():N}";
            Directory.CreateDirectory(tempFolder);

            var destFileName = Path.Combine(tempFolder, Path.GetFileName(fileName));
            try
            {
                await FileIO.WriteAllBytesAsync(destFileName, fileContents);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error copying file {fileName} to {destFileName}: {ex}");
                return null;
            }

            AssetDatabase.Refresh();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                while (AssetImporter.GetAtPath(destFileName) == null)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogError($"Timed out waiting for asset importer at path: {destFileName}");
                return null;
            }

            return new TemporaryAsset(new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) }, tempFolder);
        }

        static TemporaryAsset ImportAsset(string fileName, byte[] fileContents)
        {
            var tempFolder = $"{k_ToolkitTemp}/{Guid.NewGuid():N}";
            Directory.CreateDirectory(tempFolder);

            var destFileName = Path.Combine(tempFolder, Path.GetFileName(fileName));
            try
            {
                FileIO.WriteAllBytes(destFileName, fileContents);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error copying file {fileName} to {destFileName}: {ex}");
                return null;
            }

            AssetDatabase.ImportAsset(destFileName, ImportAssetOptions.ForceUpdate);

            return new TemporaryAsset(new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) }, tempFolder);
        }

        static TemporaryAsset ImportAsset(UnityEngine.Object obj)
        {
            var tempFolder = $"{k_ToolkitTemp}/{Guid.NewGuid():N}";
            Directory.CreateDirectory(tempFolder);

            var guid = Guid.NewGuid().ToString();
            var fileName = $"Temp{obj.GetType().Name}_{guid}{GetFileExtensionForType(obj.GetType())}";
            var destFileName = Path.Combine(tempFolder, Path.GetFileName(fileName));

            var clone = UnityEngine.Object.Instantiate(obj);
            clone.name = Path.GetFileNameWithoutExtension(fileName);

            AssetDatabase.CreateAsset(clone, destFileName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new TemporaryAsset(new AssetReference { guid = AssetDatabase.AssetPathToGUID(destFileName) }, tempFolder);
        }

        public static string GetFileExtensionForType(Type type)
        {
            if (type == typeof(AnimationClip)) return ".anim";
            if (type == typeof(AudioClip)) return ".wav";
            if (type == typeof(Material)) return ".mat";
            if (type == typeof(Mesh)) return ".asset";
            if (type == typeof(Texture2D)) return ".png";

            // Default case
            return ".asset"; // Generic asset extension
        }
    }
}
