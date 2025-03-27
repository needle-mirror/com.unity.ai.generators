using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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
                    using var response = await httpClient.GetAsync(sourceUri);
                    response.EnsureSuccessStatusCode();

                    {
                        await using var writeFileStream = FileIO.OpenFileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(writeFileStream);
                    }
                }

                string destinationPath;

                {
                    await using var fileStream = FileIO.OpenFileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
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
    }
}
