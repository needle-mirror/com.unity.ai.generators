using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AI.Generators.UI.Utilities
{
    enum TextureSizeHint
    {
        Partner = 31,
        Carousel = 255,
        Generation = 127
    }

    [Serializable]
    class TextureCachePersistence : ScriptableSingleton<TextureCachePersistence>
    {
        [SerializeField]
        internal SerializableUriDictionary<Texture> cache = new();
    }

    static class TextureCache
    {
        // Changed the inner dictionary key from an int to a tuple of (width, height)
        static readonly Dictionary<Uri, Dictionary<(int, int), RenderTexture>> k_RenderCache = new();

        public static bool Peek(Uri uri) => TextureCachePersistence.instance.cache.ContainsKey(uri) && TextureCachePersistence.instance.cache[uri];

        public static async Task<Texture2D> GetTexture(Uri uri)
        {
            var textureCache = TextureCachePersistence.instance.cache;
            if (textureCache.ContainsKey(uri) && textureCache[uri])
                return textureCache[uri] as Texture2D;

            // Otherwise, try to load or download the texture.
            var data = uri.IsFile ? File.Exists(uri.GetLocalPath()) ? await FileIO.ReadAllBytesAsync(uri.GetLocalPath()) : null : await DownloadImage(uri);
            if (data != null)
            {
                var loaded = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                loaded.LoadImage(data);
                textureCache[uri] = loaded;

                return loaded;
            }

            return null;
        }

        public static Texture2D GetTextureUnsafe(Uri uri)
        {
            if (!uri.IsFile || !uri.IsAbsoluteUri)
                throw new ArgumentException("The URI must represent a local file.", nameof(uri));

            var textureCache = TextureCachePersistence.instance.cache;
            if (textureCache.ContainsKey(uri) && textureCache[uri])
                return textureCache[uri] as Texture2D;

            var fileName = uri.GetLocalPath();
            if (!File.Exists(fileName))
                return null;
            var data = FileIO.ReadAllBytes(uri.GetLocalPath());
            var loaded = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            loaded.LoadImage(data);
            textureCache[uri] = loaded;

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

            // To minimize the frequency of resizing operations, we round up all size hints to the next power of two.
            // This bucketing ensures that textures are only resized when the size hint surpasses the current power-of-two bucket.
            sizeHint = Mathf.NextPowerOfTwo(sizeHint);

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
                var preview = new RenderTexture(targetWidth, targetHeight, 0) { hideFlags = HideFlags.HideAndDontSave};
                var previousRT = RenderTexture.active;
                Graphics.Blit(texture, preview);
                RenderTexture.active = previousRT;

                k_RenderCache[uri][key] = preview;
                return preview;
            }

            var textureCache = TextureCachePersistence.instance.cache;

            // If we already have the texture loaded in cache, use it.
            if (textureCache.ContainsKey(uri) && textureCache[uri])
            {
                var cached = textureCache[uri];
                return BlitAndAssign(cached, sizeHint);
            }

            // Otherwise, try to load or download the texture.
            var data = uri.IsFile ? File.Exists(uri.GetLocalPath()) ? await FileIO.ReadAllBytesAsync(uri.GetLocalPath()) : null : await DownloadImage(uri);
            if (data != null)
            {
                var loaded = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                loaded.LoadImage(data);
                textureCache[uri] = loaded;
                return BlitAndAssign(loaded, sizeHint);
            }

            // If we failed to load image data, then return a new RenderTexture with the requested size.
            // (Note: This fallback may result in an "upscaled" texture if no image exists,
            //  but in general you would only get here if the file did not exist.)
            var fallbackKey = (sizeHint, sizeHint);
            if (!k_RenderCache[uri].ContainsKey(fallbackKey))
                k_RenderCache[uri][fallbackKey] = new RenderTexture(sizeHint, sizeHint, 0) { hideFlags = HideFlags.HideAndDontSave};
            return k_RenderCache[uri][fallbackKey];
        }

        static byte[] s_FailImageBytes = null;

        static async Task<byte[]> DownloadImage(Uri uri)
        {
            using var uwr = UnityWebRequest.Get(uri.AbsoluteUri);
            await uwr.SendWebRequest();

            byte[] data;
            if (uwr.result != UnityWebRequest.Result.Success)
            {
                s_FailImageBytes ??= await FileIO.ReadAllBytesAsync("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/Fail.png");
                data = s_FailImageBytes;
            }
            else
                data = uwr.downloadHandler.data;
            return data;
        }

        public static async Task<Texture2D> GetNormalMap(Uri uri)
        {
            var textureCache = TextureCachePersistence.instance.cache;
            if (textureCache.ContainsKey(uri) && textureCache[uri])
            {
                var texture = textureCache[uri] as Texture2D;
                Debug.Assert(texture && !texture.isDataSRGB);
                return texture;
            }

            var filePath = uri.GetLocalPath();
            var fileData = await FileIO.ReadAllBytesAsync(filePath);

            var normalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            normalTexture.LoadImage(fileData);
            textureCache[uri] = normalTexture;

            return normalTexture;
        }

        public static Texture2D GetNormalMapUnsafe(Uri uri)
        {
            var textureCache = TextureCachePersistence.instance.cache;
            if (textureCache.ContainsKey(uri) && textureCache[uri])
            {
                var texture = textureCache[uri] as Texture2D;
                Debug.Assert(texture && !texture.isDataSRGB);
                return texture;
            }

            var filePath = uri.GetLocalPath();
            var fileData = FileIO.ReadAllBytes(filePath);

            var normalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            normalTexture.LoadImage(fileData);
            textureCache[uri] = normalTexture;

            return normalTexture;
        }
    }
}
