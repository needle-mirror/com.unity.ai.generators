using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using MapType = Unity.AI.Material.Services.Stores.States.MapType;
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

        public static Task<byte[]> GetFile(this AssetReference asset) => FileIO.ReadAllBytesAsync(asset.GetPath());

        public static Stream GetFileStream(this AssetReference asset) =>
            FileIO.OpenFileStream(asset.GetPath(), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

        public static Stream GetCompatibleImageStream(this AssetReference asset)
        {
            var candidateStream = asset.GetFileStream();
            // Check if the reference image is in a format our PBR pipeline can process directly.
            // Unity can load many texture formats internally (TGA, PSD, TIFF, etc.) that our remote
            // generation pipeline doesn't support. If we detect an unsupported format, we'll convert
            // the texture to PNG (guaranteed compatible) before proceeding with any resizing operations.
            if (!ImageFileUtilities.IsRuntimeLoadSupported(candidateStream))
            {
                var referenceTexture = asset.GetObject<Texture2D>();
                var readableTexture = ImageFileUtilities.MakeTextureReadable(referenceTexture);
                var bytes = readableTexture.EncodeToPNG();
                candidateStream.Dispose();
                candidateStream = new MemoryStream(bytes);

                if (readableTexture != referenceTexture)
                    readableTexture.SafeDestroy();
            }
            return candidateStream;
        }

        public static async Task<bool> ReplaceAsync(this AssetReference asset, MaterialResult generatedMaterial, Dictionary<MapType, string> generatedMaterialMapping)
        {
            if (await generatedMaterial.CopyToAsync(asset, generatedMaterialMapping))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static bool Replace(this AssetReference asset, MaterialResult generatedMaterial, Dictionary<MapType, string> generatedMaterialMapping)
        {
            if (generatedMaterial.CopyTo(asset, generatedMaterialMapping))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static Task<bool> IsBlank(this AssetReference asset)
        {
            var material = GetObject<UnityEngine.Material>(asset);
            return Task.FromResult(material.IsBlank());
        }

        public static MaterialResult ToResult(this AssetReference asset) => MaterialResult.FromPath(asset.GetPath());

        public static Object GetObject(this AssetReference asset)
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public static T GetObject<T>(this AssetReference asset) where T : Object
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

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

        public static bool TryGetDefaultTexturePropertyName(this AssetReference asset, MapType mapType, out string texturePropertyName)
        {
            var material = asset.GetObject<UnityEngine.Material>();
            return material.TryGetDefaultTexturePropertyName(mapType, out texturePropertyName);
        }
    }
}
