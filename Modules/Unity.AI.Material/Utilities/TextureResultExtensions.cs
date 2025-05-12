using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class TextureResultExtensions
    {
        public static void CopyToProject(this TextureResult textureResult, string cacheDirectory, string newFileName)
        {
            if (!textureResult.uri.IsFile)
                return; // DownloadToProject should be used for remote files

            var path = textureResult.uri.GetLocalPath();
            var extension = Path.GetExtension(path);
            if (!ImageFileUtilities.knownExtensions.Any(suffix => suffix.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                using var fileStream = FileIO.OpenReadAsync(path);
                extension = FileIO.GetFileExtension(fileStream);
            }

            var fileName = Path.GetFileName(path);
            if (!File.Exists(path) || string.IsNullOrEmpty(cacheDirectory))
                return;

            Directory.CreateDirectory(cacheDirectory);
            if (!string.IsNullOrEmpty(newFileName))
                fileName = newFileName;
            var newPath = Path.Combine(cacheDirectory, fileName);
            newPath = Path.ChangeExtension(newPath, extension);
            var newUri = new Uri(Path.GetFullPath(newPath));
            if (newUri == textureResult.uri)
                return;

            FileIO.CopyFile(path, newPath, overwrite: true);
            Generators.Asset.AssetReferenceExtensions.ImportAsset(newPath);
            textureResult.uri = newUri;
        }

        public static async Task DownloadToProject(this TextureResult textureResult, string cacheDirectory, string newFileName, HttpClient httpClient)
        {
            if (textureResult.uri.IsFile)
                return; // CopyToProject should be used for local files

            if (string.IsNullOrEmpty(cacheDirectory))
                return;
            Directory.CreateDirectory(cacheDirectory);

            var newUri = await UriExtensions.DownloadFile(textureResult.uri, cacheDirectory, httpClient, newFileName);
            if (newUri == textureResult.uri)
                return;

            textureResult.uri = newUri;
        }

        public static async Task<Texture2D> GetTexture(this TextureResult textureResult) => await TextureCache.GetTexture(textureResult.uri);

        public static Texture2D GetTextureUnsafe(this TextureResult textureResult) => TextureCache.GetTextureUnsafe(textureResult.uri);

        public static async Task<Texture2D> GetNormalMap(this TextureResult textureResult) => await TextureCache.GetNormalMap(textureResult.uri);

        public static Texture2D GetNormalMapUnsafe(this TextureResult textureResult) => TextureCache.GetNormalMapUnsafe(textureResult.uri);
    }
}
