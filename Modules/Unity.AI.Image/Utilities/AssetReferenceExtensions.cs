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
        public static Task<byte[]> GetFile(this AssetReference asset) => FileIO.ReadAllBytesAsync(asset.GetPath());

        public static byte[] GetFileSync(this AssetReference asset) => FileIO.ReadAllBytes(asset.GetPath());

        public static Stream GetFileStream(this AssetReference asset) =>
            FileIO.OpenFileStream(asset.GetPath(), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

        public static Stream GetCompatibleImageStream(this AssetReference asset)
        {
            var candidateStream = asset.GetFileStream();
            // check if the reference image is a jpg and has exif data, if so, it may be rotated and we should go 
            // through the Unity asset importer (at the cost of performance and sending a potentially downsized image)
            if (ImageFileUtilities.IsJpg(candidateStream) && ImageFileUtilities.HasJpgExifMetadata(candidateStream))
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
            // look at the file directly as the texture is likely not be readable
            return TextureUtils.AreAllPixelsSameColor(await FileIO.ReadAllBytesAsync(asset.GetPath()));
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
