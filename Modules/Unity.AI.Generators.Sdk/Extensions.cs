using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AiEditorToolsSdk.Components.Asset;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using UnityEngine;

namespace Unity.AI.Generators.Sdk
{
    static class Extensions
    {
        /// <summary>
        /// Note that the input stream is disposed after the method call.
        /// </summary>
        public static async Task<OperationResult<DownloadableAssetResult>> StoreAssetWithResult(this IAssetComponent assetComponent, Stream stream, HttpClient client)
        {
            var assetResult = await assetComponent.CreateAssetUploadUrl();

            using var content = new StreamContent(stream);
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            using var response = await client.PutAsync(assetResult.Result.Value.AssetUrl.Url, content);
            response.EnsureSuccessStatusCode();

            return assetResult;
        }

        /// <summary>
        /// Note that the input stream is not disposed after the method call.
        /// </summary>
        public static async Task<OperationResult<DownloadableAssetResult>> StoreAssetWithResultPreservingStream(this IAssetComponent assetComponent, Stream stream, HttpClient client)
        {
            long originalPosition = 0;
            var canSeek = stream.CanSeek;
            if (canSeek)
                originalPosition = stream.Position;

            try
            {
                // Use a wrapper that prevents the StreamContent from disposing the underlying stream
                await using var wrapper = new NonDisposingStreamWrapper(stream);
                return await StoreAssetWithResult(assetComponent, wrapper, client);
            }
            finally
            {
                if (canSeek)
                {
                    try { stream.Position = originalPosition; }
                    catch { /* ignored */ }
                }
            }
        }
    }
}
