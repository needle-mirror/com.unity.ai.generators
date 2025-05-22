﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.AI.Toolkit;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    static class UriExtensions
    {
        public static string GetAbsolutePath(this Uri uri) => uri != null ? Uri.UnescapeDataString(uri.AbsolutePath) : string.Empty;
        public static string GetLocalPath(this Uri uri) => uri != null ? uri.LocalPath : string.Empty;

        public static async Task<Uri> DownloadFile(Uri sourceUri, string destinationFolder, HttpClient httpClient, string destinationFileNameWithoutExtension = null)
        {
            if (sourceUri.IsFile || !sourceUri.IsAbsoluteUri)
                throw new ArgumentException("The URI must represent a remote file (http, https, etc.)", nameof(sourceUri));

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceUri.Segments[^1]);
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{fileNameWithoutExtension}_{Guid.NewGuid()}");

            try
            {
                {
                    using var response = await httpClient.GetAsync(sourceUri).ConfigureAwaitMainThread();
                    response.EnsureSuccessStatusCode();

                    {
                        await using var writeFileStream = FileIO.OpenWriteAsync(tempFilePath);
                        await response.Content.CopyToAsync(writeFileStream).ConfigureAwaitMainThread();
                    }
                }

                string destinationPath;

                {
                    await using var fileStream = FileIO.OpenReadAsync(tempFilePath);
                    var extension = FileIO.GetFileExtension(fileStream);

                    destinationFileNameWithoutExtension ??= fileNameWithoutExtension;
                    destinationPath = Path.Combine(destinationFolder, $"{destinationFileNameWithoutExtension}{extension}");
                }

                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                File.Move(tempFilePath, destinationPath);

                return new Uri(Path.GetFullPath(destinationPath));
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        public static GeneratedAssetMetadata GetGenerationMetadata(Uri resultUri)
        {
            var data = new GeneratedAssetMetadata();
            try { data = JsonUtility.FromJson<GeneratedAssetMetadata>(FileIO.ReadAllText($"{resultUri.GetLocalPath()}.json")); }
            catch { /*Debug.LogWarning($"Could not read {animationClipResult.uri.GetLocalPath()}.json");*/ }
            return data;
        }
    }
}
