using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    /// <summary>
    /// Options for the file comparison
    /// </summary>
    /// <param name="getBytes1">Retrieves the bytes from path1.</param>
    /// <param name="getBytes2">Retrieves the bytes from path2.</param>
    record FileComparisonOptions(string path1, string path2, bool getBytes1 = false, bool getBytes2 = false)
    {
        public byte[] bytes1;   // Bytes that were read from path1 while comparing (if any)
        public byte[] bytes2;   // Bytes that were read from path2 while comparing (if any)
    }

    static class FileIO
    {
        public static bool AreFilesIdentical(string path1, string path2) => AreFilesIdentical(new(path1, path2));
        public static bool AreFilesIdentical(FileComparisonOptions options)
        {
            if (string.IsNullOrEmpty(options.path1) || string.IsNullOrEmpty(options.path2))
                return false;

            var fileInfo1 = new FileInfo(options.path1);
            var fileInfo2 = new FileInfo(options.path2);

            if (!fileInfo1.Exists || !fileInfo2.Exists)
                return false;

            if (fileInfo1.Length != fileInfo2.Length)
                return false;

            using Stream fileStream1 = OpenReadAsync(options.path1);
            using Stream fileStream2 = OpenReadAsync(options.path2);

            using Stream readStream1 = options.getBytes1 ? new MemoryStream(options.bytes1 = fileStream1.ReadFully()) : null;
            using Stream readStream2 = options.getBytes2 ? new MemoryStream(options.bytes2 = fileStream2.ReadFully()) : null;

            using var sha256 = SHA256.Create();
            var hash1 = sha256.ComputeHash(readStream1 ?? fileStream1);
            var hash2 = sha256.ComputeHash(readStream2 ?? fileStream2);
            return StructuralComparisons.StructuralEqualityComparer.Equals(hash1, hash2);
        }

        public static bool IsFileDirectChildOfFolder(string folderPath, string filePath)
        {
            folderPath = Path.GetFullPath(folderPath);
            filePath = Path.GetFullPath(filePath);

            var fileParentDirectory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(fileParentDirectory))
                return false;

            fileParentDirectory = Path.GetFullPath(fileParentDirectory);

            return string.Equals(folderPath, fileParentDirectory, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetFileExtension(Stream stream)
        {
            if (stream is not { CanSeek: true })
                throw new ArgumentException("Stream must be non-null and seekable", nameof(stream));

            var originalPosition = stream.Position;

            try
            {
                stream.Position = 0;

                // Read a buffer large enough for all signature checks (1024 should be more than enough)
                var buffer = new byte[1024];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                var headerBytes = new byte[bytesRead];
                Array.Copy(buffer, headerBytes, bytesRead);

                if (ImageFileUtilities.TryGetImageExtension(headerBytes, out var imageExt))
                    return imageExt;

                // Check for image types
                if (IsPng(headerBytes))
                    return ".png";

                if (IsJpg(headerBytes))
                    return ".jpg";

                if (IsExr(headerBytes))
                    return ".exr";

                // Check for audio types
                if (IsWav(headerBytes))
                    return ".wav";

                // Check for FBX
                if (IsBinaryFbx(headerBytes))
                    return ".fbx";

                // Check for JSON
                if (IsJson(headerBytes))
                {
                    // Check for our animation format
                    if (IsJsonPose(headerBytes))
                        return ".pose.json";

                    return ".json";
                }

                // Unknown file type
                return ".bin";
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        static bool IsJson(IReadOnlyList<byte> headerBytes)
        {
            // Skip any leading whitespace
            var i = 0;
            while (i < headerBytes.Count && (headerBytes[i] == ' ' || headerBytes[i] == '\t' || headerBytes[i] == '\n' || headerBytes[i] == '\r'))
                i++;

            // Check if the first non-whitespace character indicates JSON
            if (i >= headerBytes.Count || (headerBytes[i] != '{' && headerBytes[i] != '['))
                return false;

            // Basic JSON validation: try to read a bit further to make sure it's not just a lone bracket
            var hasContent = false;
            for (var j = i + 1; j < headerBytes.Count && !hasContent; j++)
            {
                var c = (char)headerBytes[j];
                if (c == '"' || c == 't' || c == 'f' || c == 'n' || (c >= '0' && c <= '9') || c == '-' || c == '{' || c == '[')
                    hasContent = true;
                else if (!(c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == ':' || c == ','))
                    break;
            }
            return hasContent;
        }

        static bool IsJsonPose(IReadOnlyList<byte> headerBytes)
        {
            // First check if it looks like JSON (this is a quick pre-check)
            if (!IsJson(headerBytes))
                return false;

            try
            {
                // Convert just enough bytes to check for the frames property
                // No need for the full 1KB, we just need the beginning
                var maxLength = Math.Min(headerBytes.Count, 256);
                var jsonStart = Encoding.UTF8.GetString(headerBytes as byte[] ?? headerBytes.ToArray(), 0, maxLength);

                using var reader = new JsonTextReader(new StringReader(jsonStart));
                reader.DateParseHandling = DateParseHandling.None;

                // We only need to verify the beginning structure, not the entire document
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    return false;

                if (!reader.Read() || reader.TokenType != JsonToken.PropertyName)
                    return false;

                var propertyName = reader.Value?.ToString();
                if (propertyName != "frames")
                    return false;

                if (!reader.Read() || reader.TokenType != JsonToken.StartArray)
                    return false;

                // We've found {"frames":[, which is enough to identify this as a pose JSON
                return true;
            }
            catch
            {
                // If we can't parse even this much, it's not what we're looking for
                return false;
            }
        }

        public static bool IsWav(IReadOnlyList<byte> data) =>
            data is { Count: >= 12 } &&
            data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' &&
            data[8] == (byte)'W' && data[9] == (byte)'A' && data[10] == (byte)'V' && data[11] == (byte)'E';

        public static bool IsPng(IReadOnlyList<byte> imageBytes) =>
            imageBytes.Count >= 8 &&
            imageBytes[0] == 0x89 &&
            imageBytes[1] == 0x50 &&
            imageBytes[2] == 0x4E &&
            imageBytes[3] == 0x47 &&
            imageBytes[4] == 0x0D &&
            imageBytes[5] == 0x0A &&
            imageBytes[6] == 0x1A &&
            imageBytes[7] == 0x0A;

        public static bool IsJpg(IReadOnlyList<byte> imageBytes) => imageBytes[0] == 0xFF && imageBytes[1] == 0xD8;

        public static bool IsExr(IReadOnlyList<byte> imageBytes) => imageBytes.Count >= 4 && imageBytes[0] == 0x76 && imageBytes[1] == 0x2F &&
            imageBytes[2] == 0x31 && imageBytes[3] == 0x01;

        const string k_FbxHeader = "Kaydara FBX Binary";

        public static bool IsBinaryFbx(IReadOnlyList<byte> data) =>
            data != null && data.Count >= k_FbxHeader.Length &&
            Encoding.ASCII.GetString(data.ToArray(), 0, k_FbxHeader.Length).Equals(k_FbxHeader, StringComparison.Ordinal);

        const string k_ExtendedPathPrefix = @"\\?\";

        static string GetFullPathWithExtendedPrefix(string path)
        {
            if (path.StartsWith(k_ExtendedPathPrefix))
                return path;

            var fullPath = Path.GetFullPath(path);
            if (fullPath.Length >= 260 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return k_ExtendedPathPrefix + fullPath;

            return fullPath;
        }

        public static byte[] ReadAllBytes(string path)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds if file exists and on Windows
                if (!File.Exists(path) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    return File.ReadAllBytes(extendedPath);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static async Task<byte[]> ReadAllBytesAsync(string path)
        {
            try
            {
                return await File.ReadAllBytesAsync(path);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds if file exists and on Windows
                if (!File.Exists(path) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    return await File.ReadAllBytesAsync(extendedPath);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static string ReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds if file exists and on Windows
                if (!File.Exists(path) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    return File.ReadAllText(extendedPath);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static async Task<string> ReadAllTextAsync(string path)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds if file exists and on Windows
                if (!File.Exists(path) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    return await File.ReadAllTextAsync(extendedPath);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static void WriteAllBytes(string path, byte[] bytes)
        {
            try
            {
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    File.WriteAllBytes(extendedPath, bytes);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            try
            {
                await File.WriteAllBytesAsync(path, bytes);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    await File.WriteAllBytesAsync(extendedPath, bytes);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static void WriteAllText(string path, string contents)
        {
            try
            {
                File.WriteAllText(path, contents);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    File.WriteAllText(extendedPath, contents);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static async Task WriteAllTextAsync(string path, string contents)
        {
            try
            {
                await File.WriteAllTextAsync(path, contents);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    await File.WriteAllTextAsync(extendedPath, contents);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static void WriteAllBytes(string path, Stream inputStream)
        {
            long originalPosition = 0;
            var canSeek = inputStream.CanSeek;
            if (canSeek)
                originalPosition = inputStream.Position;

            try
            {
                if (canSeek)
                    inputStream.Position = 0;

                using var fileStream = OpenFileStreamInternal(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, false ? FileOptions.Asynchronous : FileOptions.None);
                inputStream.CopyTo(fileStream);
            }
            finally
            {
                if (canSeek)
                {
                    try { inputStream.Position = originalPosition; }
                    catch { /* ignored */ }
                }
            }
        }

        public static async Task WriteAllBytesAsync(string path, Stream inputStream)
        {
            long originalPosition = 0;
            var canSeek = inputStream.CanSeek;
            if (canSeek)
                originalPosition = inputStream.Position;

            try
            {
                if (canSeek)
                    inputStream.Position = 0;

                await using var fileStream = OpenFileStreamInternal(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                await inputStream.CopyToAsync(fileStream);
            }
            finally
            {
                if (canSeek)
                {
                    try { inputStream.Position = originalPosition; }
                    catch { /* ignored */ }
                }
            }
        }

        static FileStream OpenFileStreamInternal(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int bufferSize, FileOptions options)
        {
            try
            {
                return new FileStream(path, fileMode, fileAccess, fileShare, bufferSize, options);
            }
            catch (Exception originalException)
            {
                // Only apply Windows-specific workarounds if we're on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw;

                // Don't retry paths that already have the prefix
                if (path.StartsWith(k_ExtendedPathPrefix))
                    throw;

                try
                {
                    // Convert to full path and apply the prefix
                    var extendedPath = GetFullPathWithExtendedPrefix(path);
                    if (extendedPath == path) // No change needed
                        throw;

                    return new FileStream(extendedPath, fileMode, fileAccess, fileShare, bufferSize, options);
                }
                catch (Exception)
                {
                    // If our recovery attempt failed, throw the original exception
                    // to preserve the root cause
                    throw originalException;
                }
            }
        }

        public static FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) =>
            OpenFileStreamInternal(path, mode, access, share, bufferSize, options);

        public static FileStream OpenRead(string path) =>
            OpenFileStreamInternal(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);

        public static FileStream OpenReadAsync(string path) =>
            OpenFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);

        public static FileStream OpenWrite(string path) =>
            OpenFileStreamInternal(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None);

        public static FileStream OpenWriteAsync(string path) =>
            OpenFileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
    }
}
