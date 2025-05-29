using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Image.Services.Utilities
{
    static class AssetReferenceExtensions
    {
        public static async Task<Stream> GetCompatibleImageStreamAsync(this AssetReference asset) =>
            asset.Exists() ? await ImageFileUtilities.GetCompatibleImageStreamAsync(new Uri(Path.GetFullPath(asset.GetPath()))) : null;

        public static async Task<bool> Replace(this AssetReference asset, TextureResult generatedTexture)
        {
            if (await generatedTexture.CopyTo(asset.GetPath()))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static async Task<bool> IsBlank(this AssetReference asset)
        {
            if (IsOneByOnePixel(asset))
                return true;

            // look at the file directly as the texture is likely not be readable
            return TextureUtils.AreAllPixelsSameColor(await FileIO.ReadAllBytesAsync(asset.GetPath()));
        }

        public static bool IsOneByOnePixel(this AssetReference asset)
        {
            var importer = AssetImporter.GetAtPath(asset.GetPath()) as TextureImporter;
            if (importer == null)
                return false;

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);

            return width == 1 && height == 1;
        }

        public static async Task<bool> IsOneByOnePixelOrLikelyBlank(this AssetReference asset)
        {
            if (IsOneByOnePixel(asset))
                return true;

            try { return new FileInfo(asset.GetPath()).Length < 25 * 1024 && TextureUtils.AreAllPixelsSameColor(await FileIO.ReadAllBytesAsync(asset.GetPath())); }
            catch { return false; }
        }

        public static bool IsSkydome(this AssetReference asset)
        {
            if (!asset.IsValid())
                return false;
            var importer = AssetImporter.GetAtPath(asset.GetPath()) as TextureImporter;
            return importer != null && importer.textureShape == TextureImporterShape.TextureCube;
        }

        public static bool IsSprite(this AssetReference asset)
        {
            if (!asset.IsValid())
                return false;
            var importer = AssetImporter.GetAtPath(asset.GetPath()) as TextureImporter;
            return importer != null && importer.textureType == TextureImporterType.Sprite;
        }

        public static TextureResult ToResult(this AssetReference asset) => TextureResult.FromPath(asset.GetPath());

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

        public static AssetReference FromPath(string assetPath) => new() { guid = AssetDatabase.AssetPathToGUID(assetPath) };

        public static async Task<bool> SaveToGeneratedAssets(this AssetReference asset)
        {
            try
            {
                await asset.ToResult().CopyToProject(new GenerationSetting().MakeMetadata(asset), asset.GetGeneratedAssetsPath());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
