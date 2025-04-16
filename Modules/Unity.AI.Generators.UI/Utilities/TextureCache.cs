using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    enum TextureSizeHint
    {
        Partner = 31,
        Carousel = 255,
        Generation = 127
    }

    [Serializable]
    record TextureWithTimestamp(Texture texture, long timestamp);

    [Serializable]
    class TextureCachePersistence : ScriptableSingleton<TextureCachePersistence>
    {
        [SerializeField]
        internal SerializableUriDictionary<TextureWithTimestamp> cache = new();
    }

    static class TextureCache
    {
        static readonly Dictionary<Uri, Dictionary<(int, int), RenderTexture>> k_RenderCache = new();
        static readonly Dictionary<Uri, TextureWithTimestamp> k_PreviewTextureCache = new();

        public static bool Peek(Uri uri)
        {
            // Check if the timestamp has changed
            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);
            var textureCache = TextureCachePersistence.instance.cache;

            return textureCache.TryGetValue(uri, out var entry) &&
                   entry.texture &&
                   entry.timestamp == currentTimestamp;
        }

        public static async Task<Texture2D> GetTexture(Uri uri)
        {
            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);
            var textureCache = TextureCachePersistence.instance.cache;

            // Check if we have a valid cached entry with matching timestamp
            if (textureCache.TryGetValue(uri, out var entry) &&
                entry.texture &&
                entry.timestamp == currentTimestamp)
                return entry.texture as Texture2D;

            // Clear cached entry if timestamp doesn't match
            if (textureCache.ContainsKey(uri))
                textureCache.Remove(uri);

            // Otherwise, try to load or download the texture.
            var (loaded, timestamp) = await ImageFileUtilities.GetCompatibleImageTextureAsync(uri);
            if (loaded != null)
            {
                textureCache[uri] = new TextureWithTimestamp(loaded, timestamp);
            }

            return loaded;
        }

        public static Texture2D GetTextureUnsafe(Uri uri)
        {
            if (!uri.IsFile || !uri.IsAbsoluteUri)
                throw new ArgumentException("The URI must represent a local file.", nameof(uri));

            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);
            var textureCache = TextureCachePersistence.instance.cache;

            // Check if we have a valid cached entry with matching timestamp
            if (textureCache.TryGetValue(uri, out var entry) &&
                entry.texture &&
                entry.timestamp == currentTimestamp)
                return entry.texture as Texture2D;

            // Clear cached entry if timestamp doesn't match
            if (textureCache.ContainsKey(uri))
                textureCache.Remove(uri);

            // Otherwise, try to load the texture. Note that a remote url will return null.
            var (loaded, timestamp) = ImageFileUtilities.GetCompatibleImageTexture(uri);
            if (loaded != null)
            {
                textureCache[uri] = new TextureWithTimestamp(loaded, timestamp);
            }

            return loaded;
        }

        /// <summary>
        /// Returns a preview RenderTexture for the image at uri.
        /// This method computes preview dimensions from the provided requestedSize such that
        /// the preview is never larger than the original image.
        /// It caches the preview according to its actual (width, height).
        /// </summary>
        public static async Task<RenderTexture> GetPreview(Uri uri, int sizeHint)
        {
            if (sizeHint <= 1)
                return null;

            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);
            var textureCache = TextureCachePersistence.instance.cache;

            // To minimize the frequency of resizing operations, we round up all size hints to the next power of two.
            // This bucketing ensures that textures are only resized when the size hint surpasses the current power-of-two bucket.
            sizeHint = Mathf.NextPowerOfTwo(sizeHint);

            // Clear render cache if the timestamp has changed
            if (k_RenderCache.TryGetValue(uri, out var renderDictionary) &&
                (!textureCache.TryGetValue(uri, out var entry) || entry.timestamp != currentTimestamp))
            {
                // Release render textures
                foreach (var rt in renderDictionary.Values)
                {
                    if (rt != null)
                        rt.Release();
                }
                k_RenderCache.Remove(uri);
            }

            // Ensure we have a render texture dictionary for this uri.
            if (!k_RenderCache.ContainsKey(uri))
                k_RenderCache.Add(uri, new Dictionary<(int, int), RenderTexture>());

            // Helper function that computes the preview without upscaling.
            RenderTexture BlitAndAssign(Texture texture, int size)
            {
                // We calculate a scale factor that is at most 1, so that we only downscale.
                var scale = Mathf.Min(1f, (float)size / Mathf.Max(texture.width, texture.height));
                var targetWidth = Mathf.RoundToInt(texture.width * scale);
                var targetHeight = Mathf.RoundToInt(texture.height * scale);
                var key = (targetWidth, targetHeight);

                // Re-use cached render texture if available.
                if (k_RenderCache[uri].ContainsKey(key) && k_RenderCache[uri][key])
                    return k_RenderCache[uri][key];

                // Create the RenderTexture for the preview.
                var preview = new RenderTexture(targetWidth, targetHeight, 0) { hideFlags = HideFlags.HideAndDontSave };
                var previousRT = RenderTexture.active;
                Graphics.Blit(texture, preview);
                RenderTexture.active = previousRT;

                k_RenderCache[uri][key] = preview;
                return preview;
            }

            // If we already have the texture loaded in cache with matching timestamp, use it.
            if (textureCache.TryGetValue(uri, out var cachedEntry) &&
                cachedEntry.texture && cachedEntry.timestamp == currentTimestamp)
            {
                var cached = cachedEntry.texture;
                return BlitAndAssign(cached, sizeHint);
            }

            // Otherwise, try to load or download the texture.
            var (loaded, timestamp) = await ImageFileUtilities.GetCompatibleImageTextureAsync(uri);
            if (loaded != null)
            {
                textureCache[uri] = new TextureWithTimestamp(loaded, timestamp);
                return BlitAndAssign(loaded, sizeHint);
            }

            // If we failed to load image data, then return a new RenderTexture with the requested size.
            // (Note: This fallback may result in a larger texture if no image exists,
            //  but in general you would only get here if the file did not exist.)
            var fallbackKey = (sizeHint, sizeHint);
            if (!k_RenderCache[uri].ContainsKey(fallbackKey))
                k_RenderCache[uri][fallbackKey] = new RenderTexture(sizeHint, sizeHint, 0) { hideFlags = HideFlags.HideAndDontSave};
            return k_RenderCache[uri][fallbackKey];
        }

        public static async Task<Texture2D> GetNormalMap(Uri uri)
        {
            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);
            var textureCache = TextureCachePersistence.instance.cache;

            // Check if we have a valid cached entry with matching timestamp
            if (textureCache.TryGetValue(uri, out var entry) &&
                entry.texture && entry.timestamp == currentTimestamp)
            {
                var texture = entry.texture as Texture2D;
                if (texture && !texture.isDataSRGB)
                    return texture;
            }

            // Clear cached entry if timestamp doesn't match
            if (textureCache.ContainsKey(uri))
                textureCache.Remove(uri);

            // Otherwise, try to load or download the texture.
            var (loaded, timestamp) = await ImageFileUtilities.GetCompatibleImageTextureAsync(uri, true);
            if (loaded != null)
                textureCache[uri] = new TextureWithTimestamp(loaded, timestamp);

            return loaded;
        }

        public static Texture2D GetNormalMapUnsafe(Uri uri)
        {
            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);
            var textureCache = TextureCachePersistence.instance.cache;

            // Check if we have a valid cached entry with matching timestamp
            if (textureCache.TryGetValue(uri, out var entry) &&
                entry.texture && entry.timestamp == currentTimestamp)
            {
                var texture = entry.texture as Texture2D;
                if (texture && !texture.isDataSRGB)
                    return texture;
            }

            // Clear cached entry if timestamp doesn't match
            if (textureCache.ContainsKey(uri))
                textureCache.Remove(uri);

            // Otherwise, try to load the texture.
            var (loaded, timestamp) = ImageFileUtilities.GetCompatibleImageTexture(uri, true);
            if (loaded != null)
                textureCache[uri] = new TextureWithTimestamp(loaded, timestamp);

            return loaded;
        }

        static int s_PreviewTextureConcurrency = 0;
        const int k_PreviewTextureMaxConcurrency = 2;

        public static Texture2D GetPreviewTexture(Uri uri, int sizeHint, Texture2D initial = null)
        {
            // Check the timestamp for the file
            var currentTimestamp = ImageFileUtilities.GetLastModifiedUtcTime(uri);

            // To minimize the frequency of resizing operations, we round up all size hints to the next power of two.
            // This bucketing ensures that textures are only resized when the size hint surpasses the current power-of-two bucket.
            sizeHint = Mathf.NextPowerOfTwo(sizeHint);

            // Check if we have a valid cached entry with matching timestamp
            if (k_PreviewTextureCache.TryGetValue(uri, out var entry) && entry.texture && entry.timestamp == currentTimestamp)
                return entry.texture as Texture2D;

            // Clear cached entry if timestamp doesn't match
            if (k_PreviewTextureCache.ContainsKey(uri))
            {
                if (k_PreviewTextureCache[uri].texture != null)
                    k_PreviewTextureCache[uri].texture.SafeDestroy();
                k_PreviewTextureCache.Remove(uri);
            }

            var texture = new Texture2D(sizeHint, sizeHint, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            k_PreviewTextureCache[uri] = new(texture, currentTimestamp);

            // synchronously fill in the texture with the initial data if provided
            if (initial)
                Blit(initial, texture);

            // Start loading the data asynchronously
            _ = BlitAndAssign(uri, texture);

            return texture;

            static async Task BlitAndAssign(Uri uri, Texture2D texture)
            {
                while (s_PreviewTextureConcurrency > k_PreviewTextureMaxConcurrency)
                    await Task.Yield();

                ++s_PreviewTextureConcurrency;

                try
                {
                    var realTexture = await GetTexture(uri);
                    Blit(realTexture, texture);
                }
                finally
                {
                    --s_PreviewTextureConcurrency;
                }
            }

            static void Blit(Texture2D source, Texture2D dest)
            {
                if (!dest || !source)
                    return;

                var tempRT = RenderTexture.GetTemporary(dest.width, dest.height, 0, RenderTextureFormat.ARGB32);
                var prevActive = RenderTexture.active;
                Graphics.Blit(source, tempRT);
                RenderTexture.active = tempRT;

                dest.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
                dest.Apply();

                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(tempRT);
            }
        }
    }
}
