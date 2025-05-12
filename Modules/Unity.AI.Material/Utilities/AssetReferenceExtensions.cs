using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Material.Services.Utilities
{
    static class AssetReferenceExtensions
    {
        public static string GetMapsPath(this AssetReference asset) => GetMapsPath(asset.GetPath());

        public static string GetMapsPath(string assetPath)
        {
            var destinationDirectoryName = Path.GetDirectoryName(assetPath);
            return string.IsNullOrEmpty(destinationDirectoryName)
                ? string.Empty
                : Path.Combine(destinationDirectoryName, $"{Path.GetFileNameWithoutExtension(assetPath)}{MaterialUtilities.mapsFolderSuffix}");
        }

        public static string GetMaterialName(this AssetReference asset) => Path.GetFileNameWithoutExtension(asset.GetPath());

        public static async Task<Stream> GetCompatibleImageStreamAsync(this AssetReference asset) =>
            asset.Exists() ? await ImageFileUtilities.GetCompatibleImageStreamAsync(new Uri(Path.GetFullPath(asset.GetPath()))) : null;

        public static Task<bool> IsBlank(this AssetReference asset)
        {
            var material = GetMaterialAdapter(asset);
            return Task.FromResult(material.IsBlank());
        }

        public static MaterialResult ToResult(this AssetReference asset) => MaterialResult.FromPath(asset.GetPath());

        public static Object GetObject(this AssetReference asset)
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public static IMaterialAdapter GetMaterialAdapter(this AssetReference asset) => MaterialAdapterFactory.Create(asset.GetObject());

        public static AssetReference FromObject(Object obj)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            return new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
        }

        public static async Task<bool> SaveToGeneratedAssets(this AssetReference asset)
        {
            try
            {
                await asset.ToResult().CopyToProject(asset.GetMaterialName(), new GenerationSetting().MakeMetadata(asset), asset.GetGeneratedAssetsPath());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
