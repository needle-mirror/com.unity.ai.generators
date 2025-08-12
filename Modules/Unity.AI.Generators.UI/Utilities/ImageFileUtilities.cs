using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Sdk;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Unity.AI.Generators.UI.Utilities
{
    static class ImageFileUtilities
    {
        public const string failedDownloadIcon = "Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/Warning.png";

        /// <summary>
        /// Gets the last modified UTC time for a URI as ticks.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>The last modified time in UTC ticks, or 0 if the URI is not a valid local file.</returns>
        public static long GetLastModifiedUtcTime(Uri uri)
        {
            if (uri == null)
                return 0;

            if (!uri.IsFile)
                return 0;

            var path = uri.GetLocalPath();
            if (string.IsNullOrEmpty(path))
                return 0;

            if (!File.Exists(path))
                return 0;

            return new FileInfo(path).LastWriteTimeUtc.Ticks;
        }

        public static bool TryGetImageExtension(IReadOnlyList<byte> imageBytes, out string extension)
        {
            extension = null;

            if (imageBytes == null || imageBytes.Count < 4)
                return false;

            if (FileIO.IsPng(imageBytes))
            {
                extension = ".png";
                return true;
            }

            if (FileIO.IsJpg(imageBytes))
            {
                extension = ".jpg";
                return true;
            }

            if (FileIO.IsExr(imageBytes))
            {
                extension = ".exr";
                return true;
            }

            return false; // Unsupported image type
        }

        public static bool TryGetImageExtension(Stream imageStream, out string extension)
        {
            extension = null;

            if (imageStream == null || imageStream.Length < 4)
                return false;

            if (IsPng(imageStream))
            {
                extension = ".png";
                return true;
            }

            if (IsJpg(imageStream))
            {
                extension = ".jpg";
                return true;
            }

            if (IsExr(imageStream))
            {
                extension = ".exr";
                return true;
            }

            return false; // Unsupported image type
        }

        public static bool TryGetImageDimensions(IReadOnlyList<byte> imageBytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (!TryGetImageExtension(imageBytes, out var extension))
                return false;

            return extension switch
            {
                ".png" => TryGetPngDimensions(imageBytes, out width, out height),
                ".jpg" => TryGetJpegDimensions(imageBytes, out width, out height),
                _ => false
            };
        }

        public static bool TryGetImageDimensions(Stream imageStream, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (imageStream is not { CanSeek: true })
                return false;

            var originalPosition = imageStream.Position;
            try
            {
                imageStream.Position = 0;

                const int headerSize = 1024;
                var headerBuffer = new byte[headerSize];
                var bytesRead = imageStream.Read(headerBuffer, 0, headerBuffer.Length);

                if (bytesRead < 24)
                    return false;

                var headerBytes = headerBuffer.Take(bytesRead).ToArray();
                return TryGetImageDimensions(headerBytes, out width, out height);
            }
            finally
            {
                // Restore original position
                imageStream.Position = originalPosition;
            }
        }

        public static bool TryGetPngDimensions(IReadOnlyList<byte> imageBytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            // Check if it's a valid PNG file
            if (imageBytes == null || imageBytes.Count < 24)
                return false;

            // Check PNG signature
            if (!FileIO.IsPng(imageBytes))
                return false;

            // Width: bytes 16-19, Height: bytes 20-23
            width = ReadInt32(imageBytes, 16, false);
            height = ReadInt32(imageBytes, 20, false);
            return true;
        }

        public static bool IsPngIndexedColor(IReadOnlyList<byte> imageBytes)
        {
            if (imageBytes.Count < 29)
                return false;

            // The color type is stored in the IHDR chunk's data at byte index 9.
            // Since the IHDR data starts at index 16 (8 for signature + 4 for length + 4 for type),
            // the color type is at index 16 + 9 = 25.
            var colorType = imageBytes[25];

            // For PNG files, when the color type equals 3, the image uses an indexed color palette.
            return colorType == 3;
        }

        public static bool IsPngIndexedColor(Stream imageStream)
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The provided stream must be seekable.");

            var originalPosition = imageStream.Position;
            try
            {
                imageStream.Position = 0;

                const int requiredBytes = 29;
                var headerBuffer = new byte[requiredBytes];
                var bytesRead = imageStream.Read(headerBuffer, 0, requiredBytes);
                return bytesRead >= requiredBytes && IsPngIndexedColor(headerBuffer);
            }
            finally
            {
                imageStream.Position = originalPosition;
            }
        }

        static bool TryGetJpegDimensions(IReadOnlyList<byte> imageBytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (imageBytes == null || imageBytes.Count < 2)
                return false;

            // Check JPEG signature
            if (!FileIO.IsJpg(imageBytes))
                return false;

            var offset = 2;

            while (offset < imageBytes.Count - 1)
            {
                // Find the next marker prefixed by 0xFF
                if (imageBytes[offset] != 0xFF)
                {
                    offset++;
                    continue;
                }

                // Skip any padding FF bytes
                while (offset < imageBytes.Count && imageBytes[offset] == 0xFF)
                {
                    offset++;
                }

                if (offset >= imageBytes.Count)
                    break;

                var marker = imageBytes[offset++];

                // Read segment length
                if (offset + 1 >= imageBytes.Count)
                    break;

                var length = ReadInt16(imageBytes, offset, false); // Use the shared method, always big-endian
                offset += 2;

                if (length < 2)
                    break;

                // Check for SOF markers (Start Of Frame)
                if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
                {
                    // Ensure there are enough bytes to read
                    if (offset + length - 2 > imageBytes.Count)
                        break;

                    // The length includes the 2 bytes of the length field, so we subtract 2
                    var segmentEnd = offset + length - 2;

                    // Sample Precision (1 byte)
                    if (offset >= segmentEnd)
                        break;
                    offset++;

                    // Image Height (2 bytes)
                    if (offset + 1 >= segmentEnd)
                        break;
                    height = ReadInt16(imageBytes, offset, false); // Use the shared method, always big-endian
                    offset += 2;

                    // Image Width (2 bytes)
                    if (offset + 1 >= segmentEnd)
                        break;
                    width = ReadInt16(imageBytes, offset, false); // Use the shared method, always big-endian
                    //offset += 2;

                    return true;
                }

                // Skip over other markers
                offset += length - 2;
            }

            return false;
        }

        public static bool IsPng(Stream imageStream)
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The specified stream must be seekable.");

            var originalPosition = imageStream.Position;
            try
            {
                imageStream.Position = 0;
                var headerBytes = new byte[8];
                var bytesRead = imageStream.Read(headerBytes, 0, headerBytes.Length);
                return bytesRead >= 8 && FileIO.IsPng(headerBytes);
            }
            finally
            {
                imageStream.Position = originalPosition;
            }
        }

        public static bool IsJpg(Stream imageStream)
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The specified stream must be seekable.");

            var originalPosition = imageStream.Position;
            try
            {
                imageStream.Position = 0;
                var headerBytes = new byte[8];
                var bytesRead = imageStream.Read(headerBytes, 0, headerBytes.Length);
                return bytesRead >= 8 && FileIO.IsJpg(headerBytes);
            }
            finally
            {
                imageStream.Position = originalPosition;
            }
        }

        public static bool IsExr(Stream imageStream)
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The specified stream must be seekable.");

            var originalPosition = imageStream.Position;
            try
            {
                imageStream.Position = 0;
                var headerBytes = new byte[8];
                var bytesRead = imageStream.Read(headerBytes, 0, headerBytes.Length);
                return bytesRead >= 8 && FileIO.IsExr(headerBytes);
            }
            finally
            {
                imageStream.Position = originalPosition;
            }
        }

        public static bool HasPngAlphaChannel(byte[] headerBytes)
        {
            if (headerBytes == null || headerBytes.Length < 26)
                return true;

            var colorType = headerBytes[25];
            return colorType is 4 or 6;
        }

        // Explanation for the different endianness handling:

        /*
         * The methods handle endianness differently because:
         *
         * 1. TryGetJpegDimensions: Always uses big-endian (false parameter) because the JPEG file format
         *    specification requires that all multi-byte integers in the JPEG header structure are stored
         *    in big-endian (network byte order).
         *
         * 2. HasJpgOrientation: Uses variable endianness for EXIF metadata because the EXIF specification
         *    supports both endianness formats. The EXIF data block begins with either "II" (Intel, little-endian)
         *    or "MM" (Motorola, big-endian) to indicate which byte order is used.
         *
         *    - JPEG structure elements (like segment length) are still read as big-endian
         *    - EXIF data elements are read using the endianness specified in the EXIF header
         */

        public static bool HasJpgOrientation(Stream jpegStream)
        {
            var originalPosition = jpegStream.Position;
            const int headerSize = 1024;

            try
            {
                var buffer = new byte[12];

                _ = jpegStream.Read(buffer, 0, 2);
                if (buffer[0] != 0xFF || buffer[1] != 0xD8)
                    return false;

                long bytesRead = 2;
                while (bytesRead < headerSize && jpegStream.Position < jpegStream.Length)
                {
                    var markerStart = jpegStream.ReadByte();
                    var markerType = jpegStream.ReadByte();
                    bytesRead += 2;

                    if (markerStart != 0xFF)
                        break;

                    if (markerType == 0xE1)
                    {
                        _ = jpegStream.Read(buffer, 0, 2);
                        _ = ReadInt16(buffer, 0, false); // Use helper method consistently, always big-endian
                        bytesRead += 2;

                        _ = jpegStream.Read(buffer, 0, 6);
                        bytesRead += 6;

                        if (buffer[0] == 'E' && buffer[1] == 'x' && buffer[2] == 'i' &&
                            buffer[3] == 'f' && buffer[4] == 0 && buffer[5] == 0)
                        {
                            _ = jpegStream.Read(buffer, 0, 8);
                            bytesRead += 8;

                            var isLittleEndian = (buffer[0] == 'I' && buffer[1] == 'I');
                            var ifdOffset = ReadInt32(buffer, 4, isLittleEndian);

                            jpegStream.Seek(ifdOffset - 8, SeekOrigin.Current);
                            bytesRead += (ifdOffset - 8);

                            _ = jpegStream.Read(buffer, 0, 2);
                            bytesRead += 2;

                            var numEntries = ReadInt16(buffer, 0, isLittleEndian);
                            for (var i = 0; i < numEntries; i++)
                            {
                                _ = jpegStream.Read(buffer, 0, 12);
                                bytesRead += 12;

                                var tagId = ReadInt16(buffer, 0, isLittleEndian);
                                if (tagId != 0x0112)
                                    continue;

                                var orientationValue = ReadInt16(buffer, 8, isLittleEndian);
                                return orientationValue != 1;
                            }

                            return false;
                        }
                    }

                    if (markerType == 0xDA)
                        break;

                    if (markerType is 0xD9 or < 0xE0)
                        continue;

                    _ = jpegStream.Read(buffer, 0, 2);
                    var segmentLength = ReadInt16(buffer, 0, false); // Use helper method consistently, always big-endian
                    bytesRead += 2;

                    jpegStream.Seek(segmentLength - 2, SeekOrigin.Current);
                    bytesRead += (segmentLength - 2);
                }

                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                jpegStream.Position = originalPosition;
            }
        }

        static int ReadInt16<T>(T bytes, int offset, bool isLittleEndian) where T : IReadOnlyList<byte> =>
            isLittleEndian
                ? bytes[offset] | (bytes[offset + 1] << 8)
                : (bytes[offset] << 8) | bytes[offset + 1];

        static int ReadInt32<T>(T bytes, int offset, bool isLittleEndian) where T : IReadOnlyList<byte> =>
            isLittleEndian
                ? bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24)
                : (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

        public static bool TryConvert(byte[] imageBytes, out byte[] destData, string toType = ".png")
        {
            Texture2D texture = null;
            Texture2D destTexture = null;
            RenderTexture renderTexture = null;
            destData = null;

            try
            {
                // Check if the source is a JPEG with EXIF orientation data
                using var imageStream = new MemoryStream(imageBytes);
                if (IsJpg(imageStream) && HasJpgOrientation(imageStream))
                {
                    // For JPEGs with orientation data, use an approach that handles rotation
                    texture = LoadJpegWithExifRotation(imageBytes);
                }
                else
                {
                    texture = new Texture2D(1, 1);
                    texture.LoadImage(imageBytes);
                }

                switch (toType.ToLower())
                {
                    // For non-HDR formats, use the source texture directly
                    case ".png":
                        destData = texture.EncodeToPNG();
                        break;
                    case ".jpg":
                    case ".jpeg":
                        destData = texture.EncodeToJPG(100);
                        break;
                    // For EXR, perform GPU-accelerated sRGB to linear conversion
                    case ".exr":
                    {
                        renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                        Graphics.Blit(texture, renderTexture);
                        destTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBAHalf, false, true);

                        var prevRT = RenderTexture.active;
                        RenderTexture.active = renderTexture;
                        destTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                        RenderTexture.active = prevRT;

                        destData = destTexture.EncodeToEXR(Texture2D.EXRFlags.CompressRLE); // RLE is fastest
                        break;
                    }
                    default:
                        return false;
                }

                return destData != null;
            }
            finally
            {
                if (renderTexture != null)
                    RenderTexture.ReleaseTemporary(renderTexture);

                texture?.SafeDestroy();
                destTexture?.SafeDestroy();
            }
        }

        const string k_ToolkitTemp = "Assets/AI Toolkit/Temp";

        static Texture2D LoadJpegWithExifRotation(byte[] imageBytes)
        {
            // Use Unity's asset import system to properly handle EXIF rotation
            using var tempAsset = TemporaryAssetUtilities.ImportAssets(new[] { ($"{k_ToolkitTemp}/{Guid.NewGuid():N}.jpg", imageBytes) });
            var asset = tempAsset.assets[0].asset;
            var importedTexture = asset.GetObject<Texture2D>();

            // Create properly oriented readable texture
            return TryGetAspectRatio(asset, out var aspect)
                ? MakeTextureReadable(importedTexture, aspect)
                : MakeTextureReadable(importedTexture);
        }

        public static bool TryConvert(Stream imageStream, out Stream destStream, string toType = ".png")
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The provided stream must be seekable.");

            var normalizedToType = toType.ToLowerInvariant();
            var originalPosition = imageStream.Position;
            try
            {
                imageStream.Position = 0;
                var formatMatches = TryGetImageExtension(imageStream, out var extension) &&
                    extension.Equals(normalizedToType, StringComparison.OrdinalIgnoreCase);
                if (formatMatches)
                {
                    imageStream.Position = 0;
                    destStream = imageStream;
                    return true;
                }

                imageStream.Position = 0;
                var imageBytes = imageStream.ReadFully();
                var success = TryConvert(imageBytes, out var destData, toType);
                destStream = success ? new MemoryStream(destData) : null;
                return success;
            }
            finally
            {
                if (imageStream.CanSeek)
                    imageStream.Position = originalPosition;
            }
        }

        public static byte[] CheckImageSize(byte[] imageBytes, int minimumSize = 32, int maximumSize = 8192)
        {
            if (!TryGetImageDimensions(imageBytes, out var width, out var height))
                return imageBytes;

            if (width < minimumSize || height < minimumSize)
            {
                var widthScale = minimumSize / (float)width;
                var heightScale = minimumSize / (float)height;
                var scale = Mathf.Max(widthScale, heightScale);

                var outputWidth = Mathf.RoundToInt(width * scale);
                var outputHeight = Mathf.RoundToInt(height * scale);

                return Resize(imageBytes, outputWidth, outputHeight);
            }

            if (width > maximumSize || height > maximumSize)
                throw new ArgumentOutOfRangeException(nameof(maximumSize), $"Image size must be less than or equal to {maximumSize}x{maximumSize}. Actual: {width}x{height}.");

            return imageBytes;
        }

        public static Stream CheckImageSize(Stream imageStream, int minimumSize = 32, int maximumSize = 8192)
        {
            if (imageStream == null)
                throw new ArgumentOutOfRangeException(nameof(minimumSize), $"Image size must be at least 2x2.");

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The provided stream must be seekable.");

            if (!TryGetImageDimensions(imageStream, out var width, out var height))
                return imageStream;

            if (width < minimumSize || height < minimumSize)
            {
                var widthScale = minimumSize / (float)width;
                var heightScale = minimumSize / (float)height;
                var scale = Mathf.Max(widthScale, heightScale);

                var outputWidth = Mathf.RoundToInt(width * scale);
                var outputHeight = Mathf.RoundToInt(height * scale);

                var imageBytes = imageStream.ReadFully();
                imageBytes = Resize(imageBytes, outputWidth, outputHeight);
                imageStream.Dispose();
                return new MemoryStream(imageBytes);
            }

            if (width > maximumSize || height > maximumSize)
                throw new ArgumentOutOfRangeException(nameof(maximumSize), $"Image size must be less than or equal to {maximumSize}x{maximumSize}. Actual: {width}x{height}.");

            return imageStream;
        }

        /// <summary>
        /// Determines if a stream contains an image format that can be loaded at runtime (PNG, JPG, or EXR).
        /// </summary>
        /// <param name="stream">The stream to check.</param>
        /// <returns>True if the stream contains a PNG, JPG, or EXR image; otherwise, false.</returns>
        public static bool IsRuntimeLoadSupported(Stream stream)
        {
            if (stream == null || stream.Length == 0 || !stream.CanRead)
                return false;

            long originalPosition = 0;
            if (stream.CanSeek)
                originalPosition = stream.Position;

            try
            {
                stream.Position = 0;

                // Read enough bytes to determine the file format (16 bytes should be sufficient)
                var headerBytes = new byte[16];
                var bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

                if (bytesRead < 4) // Need at least 4 bytes to identify any format
                    return false;

                // Check for PNG signature
                if (FileIO.IsPng(headerBytes))
                    return true;

                // Check for JPG signature
                if (FileIO.IsJpg(headerBytes))
                    return true;

                // Check for EXR signature
                if (FileIO.IsExr(headerBytes))
                    return true;

                // Not a supported format
                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                // Restore the original position if possible
                if (stream.CanSeek)
                {
                    try { stream.Position = originalPosition; }
                    catch { /* Ignore positioning errors */ }
                }
            }
        }

        /// <summary>
        /// Convert any texture into a readable Texture2D, optionally correcting the aspect if the texture is not already within tolerance of the given aspect (and the given aspect is > 0)
        /// by stretching to restore the original aspect ratio.
        /// </summary>
        public static Texture2D MakeTextureReadable(Texture sourceTexture, float aspect = -1)
        {
            const float aspectTolerance = 0.001f;

            if (!sourceTexture)
                return null;

            // Early return if texture is already readable AND (no aspect correction needed OR aspect already matches)
            if (sourceTexture is Texture2D { isReadable: true } texture2D)
            {
                var currentAspect = (float)texture2D.width / texture2D.height;
                if (aspect <= 0 || Mathf.Abs(currentAspect - aspect) < aspectTolerance)
                    return texture2D;
            }

            var previousActive = RenderTexture.active;
            RenderTexture rt = null;

            try
            {
                var outputWidth = sourceTexture.width;
                var outputHeight = sourceTexture.height;

                var currentAspect = (float)sourceTexture.width / sourceTexture.height;
                // Calculate new dimensions if aspect ratio correction is needed
                if (aspect > 0 && Mathf.Abs(currentAspect - aspect) >= aspectTolerance)
                {
                    // Determine new dimensions while maintaining approximately the same pixel count
                    float pixelCount = sourceTexture.width * sourceTexture.height;
                    outputHeight = Mathf.RoundToInt(Mathf.Sqrt(pixelCount / aspect));
                    outputWidth = Mathf.RoundToInt(outputHeight * aspect);
                }

                rt = RenderTexture.GetTemporary(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(sourceTexture, rt);
                RenderTexture.active = rt;

                // Create new readable texture
                var readableTexture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readableTexture.Apply();

                return readableTexture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rt)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static byte[] Resize(byte[] imageBytes, int width, int height)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return imageBytes;

            var previousActive = RenderTexture.active;
            Texture2D sourceTexture = null;
            RenderTexture rt = null;

            try
            {
                sourceTexture = new Texture2D(2, 2);
                sourceTexture.LoadImage(imageBytes);

                rt = RenderTexture.GetTemporary(width, height);

                Graphics.Blit(sourceTexture, rt);

                var resultTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                resultTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resultTexture.Apply();

                return resultTexture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rt)
                    RenderTexture.ReleaseTemporary(rt);
                sourceTexture?.SafeDestroy();
            }
        }

        public static readonly string[] knownExtensions = { ".png", ".jpg", "jpeg", ".exr" };

        public static async Task<(Texture2D texture, long timestamp)> GetCompatibleImageTextureAsync(Uri uri, bool linear = false)
        {
            var timestamp = GetLastModifiedUtcTime(uri);

            if (!uri.IsFile)
            {
                using var httpClientLease = HttpClientManager.instance.AcquireLease();

                var data = await DownloadImageWithFallback(uri, httpClientLease.client);
                await using var candidateStream = new MemoryStream(data);
                return (await CompatibleImageTexture(candidateStream), timestamp);
            }

            if (File.Exists(uri.GetLocalPath()))
            {
                await using Stream candidateStream = await FileIO.OpenReadWithRetryAsync(uri.GetLocalPath(), CancellationToken.None);
                return (await CompatibleImageTexture(candidateStream), timestamp);
            }

            return (null, timestamp);

            async Task<Texture2D> CompatibleImageTexture(Stream stream)
            {
                // check if the reference image is a jpg and has exif data, if so, it may be rotated and we should go
                // through the Unity asset importer (at the cost of performance and sending a potentially downsized image)
                // exr also doesn't seem to be supported by the backend
                if (IsPng(stream) || (IsJpg(stream) && !HasJpgOrientation(stream)))
                {
                    var loaded = new Texture2D(1, 1, TextureFormat.RGBA32, false, linear) { hideFlags = HideFlags.HideAndDontSave };
                    loaded.LoadImage(await stream.ReadFullyAsync());
                    return loaded;
                }

                using var temporaryAsset = await TemporaryAssetUtilities.ImportAssetsAsync(new[] { (uri.GetLocalPath(), stream) });

                var asset = temporaryAsset.assets[0].asset;
                var referenceTexture = asset.GetObject<Texture2D>();
                var readableTexture = TryGetAspectRatio(asset, out var aspect)
                    ? MakeTextureReadable(referenceTexture, aspect)
                    : MakeTextureReadable(referenceTexture);

                return readableTexture;
            }
        }

        public static (Texture2D texture, long timestamp) GetCompatibleImageTexture(Uri uri, bool linear = false)
        {
            var timestamp = GetLastModifiedUtcTime(uri);

            if (!uri.IsFile)
                return (null, timestamp);

            if (File.Exists(uri.GetLocalPath()))
            {
                using Stream candidateStream = FileIO.OpenReadAsync(uri.GetLocalPath());
                return (CompatibleImageTexture(candidateStream), timestamp);
            }

            return (null, timestamp);

            Texture2D CompatibleImageTexture(Stream stream)
            {
                // check if the reference image is a jpg and has exif data, if so, it may be rotated and we should go
                // through the Unity asset importer (at the cost of performance and sending a potentially downsized image)
                // exr also doesn't seem to be supported by the backend
                if (IsPng(stream) || (IsJpg(stream) && !HasJpgOrientation(stream)))
                {
                    var loaded = new Texture2D(1, 1, TextureFormat.RGBA32, false, linear) { hideFlags = HideFlags.HideAndDontSave };
                    loaded.LoadImage(stream.ReadFully());
                    return loaded;
                }

                using var temporaryAsset = TemporaryAssetUtilities.ImportAssets(new[] { (uri.GetLocalPath(), stream) });

                var asset = temporaryAsset.assets[0].asset;
                var referenceTexture = asset.GetObject<Texture2D>();
                var readableTexture = TryGetAspectRatio(asset, out var aspect)
                    ? MakeTextureReadable(referenceTexture, aspect)
                    : MakeTextureReadable(referenceTexture);

                return readableTexture;
            }
        }

        public static Stream GetCompatibleImageStream(Uri uri)
        {
            if (!uri.IsFile || !File.Exists(uri.GetLocalPath()))
                return null;

            Stream candidateStream = FileIO.OpenReadAsync(uri.GetLocalPath());
            return CompatibleImageStream(candidateStream, uri.GetLocalPath());

            Stream CompatibleImageStream(Stream stream, string filePath)
            {
                // Check if the reference image is a jpg and has exif data, if so, it may be rotated and we should go
                // through the Unity asset importer (at the cost of performance and sending a potentially downsized image)
                // exr also doesn't seem to be supported by the backend
                if (IsPng(stream) || (IsJpg(stream) && !HasJpgOrientation(stream)))
                    return stream;

                using var temporaryAsset = TemporaryAssetUtilities.ImportAssets(new[] { filePath });

                var asset = temporaryAsset.assets[0].asset;
                var referenceTexture = asset.GetObject<Texture2D>();
                var readableTexture = TryGetAspectRatio(asset, out var aspect)
                    ? MakeTextureReadable(referenceTexture, aspect)
                    : MakeTextureReadable(referenceTexture);

                var bytes = readableTexture.EncodeToPNG();
                stream.Dispose();
                stream = new MemoryStream(bytes);

                if (readableTexture != referenceTexture)
                    readableTexture.SafeDestroy();

                return stream;
            }
        }

        public static async Task<Stream> GetCompatibleImageStreamAsync(Uri uri)
        {
            if (!uri.IsFile || !File.Exists(uri.GetLocalPath()))
                return null;

            Stream candidateStream = await FileIO.OpenReadWithRetryAsync(uri.GetLocalPath(), CancellationToken.None);
            return await CompatibleImageStream(candidateStream, uri.GetLocalPath());

            async Task<Stream> CompatibleImageStream(Stream stream, string filePath)
            {
                // Check if the reference image is a jpg and has exif data, if so, it may be rotated and we should go
                // through the Unity asset importer (at the cost of performance and sending a potentially downsized image)
                // exr also doesn't seem to be supported by the backend
                if (IsPng(stream) || (IsJpg(stream) && !HasJpgOrientation(stream)))
                    return stream;

                using var temporaryAsset = await TemporaryAssetUtilities.ImportAssetsAsync(new[] { filePath });

                var asset = temporaryAsset.assets[0].asset;
                var referenceTexture = asset.GetObject<Texture2D>();
                var readableTexture = TryGetAspectRatio(asset, out var aspect)
                    ? MakeTextureReadable(referenceTexture, aspect)
                    : MakeTextureReadable(referenceTexture);

                var bytes = readableTexture.EncodeToPNG();
                await stream.DisposeAsync();
                stream = new MemoryStream(bytes);

                if (readableTexture != referenceTexture)
                    readableTexture.SafeDestroy();

                return stream;
            }
        }

        static T GetObject<T>(this AssetReference asset) where T : Object
        {
            var path = asset.GetPath();
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static byte[] s_FailImageBytes = null;
#if NEW_CODE
        static async Task<byte[]> DownloadImageWithFallback(Uri uri, System.Net.Http.HttpClient httpClient)
        {
            try
            {
                var tempDir = FileUtil.GetUniqueTempPathInProject();
                var downloadedUri = await UriExtensions.DownloadFile(uri, tempDir, httpClient);
                var data = await FileIO.ReadAllBytesAsync(downloadedUri.GetLocalPath());

                // Clean up temp file
                try
                {
                    File.Delete(downloadedUri.GetLocalPath());
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                return data;
            }
            catch
            {
                s_FailImageBytes ??= await FileIO.ReadAllBytesAsync("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/Fail.png");
                return s_FailImageBytes;
            }
        }
#else
        static async Task<byte[]> DownloadImageWithFallback(Uri uri, System.Net.Http.HttpClient _)
        {
            using var uwr = UnityWebRequest.Get(uri.AbsoluteUri);
            await uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                s_FailImageBytes ??= await FileIO.ReadAllBytesAsync("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/Fail.png");
                return s_FailImageBytes;
            }

            return uwr.downloadHandler.data;
        }
#endif

        static bool TryGetAspectRatio(string assetPath, out float aspect)
        {
            aspect = 1.0f;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (!importer)
                return false;

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            if (width <= 0 || height <= 0)
                return false;

            aspect = (float)width / height;
            return true;
        }

        public static bool TryGetAspectRatio(AssetReference asset, out float aspect) => TryGetAspectRatio(asset.GetPath(), out aspect);

        public static bool TryGetAspectRatio(Texture asset, out float aspect) => TryGetAspectRatio(AssetDatabase.GetAssetPath(asset), out aspect);
    }
}
