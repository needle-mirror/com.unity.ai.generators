using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    static class ImageFileUtilities
    {
        public const string failedDownloadIcon = "Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/Warning.png";

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
            width = ReadInt32BigEndian(imageBytes, 16);
            height = ReadInt32BigEndian(imageBytes, 20);
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

                var length = ReadInt16BigEndian(imageBytes, offset);
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
                    height = ReadInt16BigEndian(imageBytes, offset);
                    offset += 2;

                    // Image Width (2 bytes)
                    if (offset + 1 >= segmentEnd)
                        break;
                    width = ReadInt16BigEndian(imageBytes, offset);
                    //offset += 2;

                    return true;
                }

                // Skip over other markers
                offset += length - 2;
            }

            return false;
        }

        static int ReadInt32BigEndian(IReadOnlyList<byte> bytes, int offset)
        {
            return (bytes[offset] << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3];
        }

        static int ReadInt16BigEndian(IReadOnlyList<byte> bytes, int offset)
        {
            return (bytes[offset] << 8) | bytes[offset + 1];
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

        public static bool HasPngAlphaChannel(byte[] headerBytes)
        {
            if (headerBytes == null || headerBytes.Length < 26)
                return true;

            var colorType = headerBytes[25];

            return colorType == 4 || colorType == 6;
        }

        public static bool HasJpgExifMetadata(Stream jpegStream)
        {
            var originalPosition = jpegStream.Position;
            const int headerSize = 512;

            try
            {
                var buffer = new byte[8];

                // Check for JPEG header (FF D8)
                _ = jpegStream.Read(buffer, 0, 2);
                if (buffer[0] != 0xFF || buffer[1] != 0xD8)
                    return false; // Not a valid JPEG file

                long bytesRead = 2;

                // Search only through the header area
                while (bytesRead < headerSize && jpegStream.Position < jpegStream.Length)
                {
                    var markerStart = jpegStream.ReadByte();
                    var markerType = jpegStream.ReadByte();
                    bytesRead += 2; // Increment for marker bytes

                    // Valid marker must start with 0xFF
                    if (markerStart != 0xFF)
                        break;

                    // Found EXIF marker (0xE1)
                    if (markerType == 0xE1)
                    {
                        // Read segment length
                        _ = jpegStream.Read(buffer, 0, 2);
                        bytesRead += 2;

                        // Verify this is EXIF by checking for "Exif\0\0" header
                        _ = jpegStream.Read(buffer, 0, 6);
                        bytesRead += 6;

                        if (buffer[0] == 'E' && buffer[1] == 'x' && buffer[2] == 'i' &&
                            buffer[3] == 'f' && buffer[4] == 0 && buffer[5] == 0)
                        {
                            return true; // Valid EXIF data found
                        }
                    }

                    // Stop at SOS marker, as all metadata appears before image data
                    if (markerType == 0xDA)
                        break;

                    // Skip segments with variable length
                    if (markerType != 0xD9 && markerType >= 0xE0)
                    {
                        _ = jpegStream.Read(buffer, 0, 2);
                        var segmentLength = (buffer[0] << 8) | buffer[1];
                        bytesRead += 2;

                        jpegStream.Seek(segmentLength - 2, SeekOrigin.Current);
                        bytesRead += (segmentLength - 2);
                    }
                }

                return false; // No EXIF data found
            }
            catch
            {
                return false; // Error reading the file
            }
            finally
            {
                jpegStream.Position = originalPosition;
            }
        }

        public static bool TryConvert(byte[] imageBytes, out byte[] destData, string toType = ".png")
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(1, 1);
                texture.LoadImage(imageBytes);
                destData = null;
                switch (toType.ToLower())
                {
                    case ".png":
                        destData = texture.EncodeToPNG();
                        break;
                    case ".jpg":
                    case ".jpeg":
                        destData = texture.EncodeToJPG();
                        break;
                    case ".exr":
                        destData = texture.EncodeToEXR();
                        break;
                    default:
                        texture.SafeDestroy();
                        return false;
                }
            }
            finally
            {
                texture?.SafeDestroy();
            }

            return true;
        }

        public static bool TryConvert(Stream imageStream, out Stream destStream, string toType = ".png")
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            var imageBytes = imageStream.ReadFully();
            var success = TryConvert(imageBytes, out var destData, toType);
            destStream = success ? new MemoryStream(destData) : null;

            return success;
        }

        public static byte[] CheckImageSize(byte[] imageBytes, int minimumSize = 33, int maximumSize = 8192)
        {
            if (!TryGetImageDimensions(imageBytes, out var width, out var height))
                return imageBytes;

            if (width < minimumSize || height < minimumSize)
            {
                var e = new ArgumentOutOfRangeException(nameof(minimumSize), $"Image size must be at least {minimumSize}x{minimumSize}.");
                Debug.LogException(e);
                throw e;
            }

            if (width > maximumSize || height > maximumSize)
            {
                var e = new ArgumentOutOfRangeException(nameof(minimumSize), $"Image size must be less than or equal to {maximumSize}x{maximumSize}.");
                Debug.LogException(e);
                throw e;
            }

            return imageBytes;
        }

        public static Stream CheckImageSize(Stream imageStream, int minimumSize = 33, int maximumSize = 8192)
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (!imageStream.CanSeek)
                throw new NotSupportedException("The provided stream must be seekable.");

            var originalPosition = imageStream.Position;
            imageStream.Position = 0;

            const int headerSize = 1024;
            var headerBuffer = new byte[headerSize];
            var bytesRead = imageStream.Read(headerBuffer, 0, headerSize);

            imageStream.Position = originalPosition;
            var headerBytes = headerBuffer.Take(bytesRead).ToArray();

            if (!TryGetImageDimensions(headerBytes, out var width, out var height))
                return imageStream;

            if (width < minimumSize || height < minimumSize)
            {
                var e = new ArgumentOutOfRangeException(nameof(minimumSize),
                    $"Image size must be at least {minimumSize}x{minimumSize}. Actual: {width}x{height}.");
                Debug.LogException(e);
                throw e;
            }

            if (width > maximumSize || height > maximumSize)
            {
                var e = new ArgumentOutOfRangeException(nameof(maximumSize),
                    $"Image size must be less than or equal to {maximumSize}x{maximumSize}. Actual: {width}x{height}.");
                Debug.LogException(e);
                throw e;
            }

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

        public static Texture2D MakeTextureReadable(Texture sourceTexture)
        {
            // Early return if texture is already readable
            if (sourceTexture is Texture2D { isReadable: true } texture2D)
                return texture2D;

            var previousActive = RenderTexture.active;
            RenderTexture rt = null;

            try
            {
                // Create render texture with same dimensions as source
                rt = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);

                // Blit texture to render texture
                Graphics.Blit(sourceTexture, rt);
                RenderTexture.active = rt;

                // Create new readable texture
                var readableTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);

                // Read pixels from render texture to new texture
                readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readableTexture.Apply();

                return readableTexture;
            }
            finally
            {
                // Clean up
                RenderTexture.active = previousActive;
                if (rt)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static readonly string[] knownExtensions = { ".png", ".jpg", "jpeg", ".exr" };
    }
}
