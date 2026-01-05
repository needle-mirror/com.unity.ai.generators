using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Toolkit.Asset;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Unity.AI.Image.Services.Utilities
{
    /// <summary>
    /// A static class to create and manage on-disk caches of frames from video-type TextureResults.
    /// </summary>
    static class VideoClipFrameCache
    {
        public const int frameRate = 24;

        static readonly string k_CacheRootPath = Path.Combine(TempUtilities.projectRootPath, "Temp", Unity.AI.Generators.Asset.AssetReferenceExtensions.GetGeneratedAssetsRoot(), "Video");
        static readonly Dictionary<string, Task<VideoClipCacheHandle>> k_ActiveTasks = new();

        static VideoClipFrameCache()
        {
            if (!Directory.Exists(k_CacheRootPath))
                Directory.CreateDirectory(k_CacheRootPath);
        }

        /// <summary>
        /// Generates a unique key for a TextureResult based on its file metadata.
        /// </summary>
        static string GetCacheKey(TextureResult result)
        {
            var path = result.uri.GetLocalPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                return $"videoclip_{Path.GetFileNameWithoutExtension(path)}_{fileInfo.Length}_{fileInfo.LastWriteTime.Ticks}";
            }

            return $"videoclip_{result.uri.GetHashCode()}";
        }

        /// <summary>
        /// Creates a frame cache for the given video TextureResult or retrieves an existing one.
        /// </summary>
        /// <param name="result">The TextureResult of type video to cache.</param>
        /// <returns>A disposable handle to manage the cache's lifetime.</returns>
        public static Task<VideoClipCacheHandle> GetOrCreateCacheAsync(TextureResult result)
        {
            if (result == null || !result.IsVideoClip())
                return Task.FromResult<VideoClipCacheHandle>(null);

            var cacheKey = GetCacheKey(result);
            if (k_ActiveTasks.TryGetValue(cacheKey, out var existingTask))
                return existingTask;

            var newTask = CreateCacheInternalAsync(result, cacheKey);
            k_ActiveTasks.Add(cacheKey, newTask);
            return newTask;
        }

        static async Task<VideoClipCacheHandle> CreateCacheInternalAsync(TextureResult result, string cacheKey)
        {
            var (clip, scope) = await result.GetVideoClipWithScope();
            try
            {
                if (clip == null)
                {
                    Debug.LogError($"[VideoFrameCache] Could not import VideoClip from TextureResult '{result.uri}'.");
                    return null;
                }

                var handle = new VideoClipCacheHandle(cacheKey);
                var cachePath = Path.Combine(k_CacheRootPath, cacheKey);
                Directory.CreateDirectory(cachePath);

                var frameCount = (int)Math.Ceiling(clip.length * frameRate);
                var frameFiles = Directory.GetFiles(cachePath, "frame_*.raw").OrderBy(f => f).ToArray();
                if (frameFiles.Length != frameCount)
                {
                    if (frameFiles.Length > 0)
                    {
                        Directory.Delete(cachePath, true);
                        Directory.CreateDirectory(cachePath);
                    }
                    await RenderAndSaveFramesAsync(clip, cachePath, frameCount);
                }

                handle.Frames = await LoadFramesFromDiskAsync(cachePath, (int)clip.width / 4, (int)clip.height / 4);
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoFrameCache] Failed to create cache for '{result.uri}': {e}");
                ClearCache(cacheKey);
                return null;
            }
            finally
            {
                scope?.Dispose();
                k_ActiveTasks.Remove(cacheKey);
            }
        }

        static Task RenderAndSaveFramesAsync(VideoClip clip, string cachePath, int frameCount)
        {
            var tcs = new TaskCompletionSource<bool>();
            var targetWidth = (int)clip.width / 4;
            var targetHeight = (int)clip.height / 4;

            var job = new VideoClipCacheRenderJob(clip, tcs, cachePath, targetWidth, targetHeight, frameCount);
            job.Start();

            return tcs.Task;
        }

        static async Task<List<RenderTexture>> LoadFramesFromDiskAsync(string cachePath, int width, int height)
        {
            var frameFiles = Directory.GetFiles(cachePath, "frame_*.raw").OrderBy(f => f).ToArray();
            var loadedData = await Task.WhenAll(frameFiles.Select(FileIO.ReadAllBytesAsync));

            var frames = new List<RenderTexture>();
            var tempTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            foreach (var data in loadedData)
            {
                if (data == null || data.Length == 0)
                    continue;

                var renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();

                tempTexture.LoadRawTextureData(data);
                tempTexture.Apply();

                FrameCacheUtils.SafeBlit(tempTexture, renderTexture);
                frames.Add(renderTexture);
            }

            tempTexture.SafeDestroy();
            return frames;
        }

        static void ClearCache(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
                return;

            var cachePath = Path.Combine(k_CacheRootPath, cacheKey);
            if (Directory.Exists(cachePath))
                Directory.Delete(cachePath, true);
        }
    }

    /// <summary>
    /// A disposable handle that manages the lifetime of a video clip's frame cache.
    /// When disposed, it removes the cached frames from memory.
    /// </summary>
    sealed class VideoClipCacheHandle : IDisposable
    {
        /// <summary>
        /// The unique identifier for this cache instance.
        /// </summary>
        public string CacheKey { get; }

        /// <summary>
        /// The list of frames loaded into memory from the cache.
        /// </summary>
        public IReadOnlyList<RenderTexture> Frames { get; internal set; }

        bool m_IsDisposed;

        internal VideoClipCacheHandle(string cacheKey)
        {
            CacheKey = cacheKey;
            m_IsDisposed = false;
        }

        /// <summary>
        /// Disposes the handle, releasing GPU memory.
        /// </summary>
        public void Dispose()
        {
            if (m_IsDisposed)
                return;

            m_IsDisposed = true;

            if (Frames == null)
                return;

            foreach (var frame in Frames)
            {
                if (frame == null)
                    continue;

                frame.Release();
                frame.SafeDestroy();
            }
            Frames = null;
        }
    }

    /// <summary>
    /// Provides extension methods for the VideoClipCacheHandle.
    /// </summary>
    static class VideoClipCacheHandleExtensions
    {
        /// <summary>
        /// Retrieves a cached frame that corresponds to a specific time in the video.
        /// </summary>
        /// <param name="handle">The cache handle.</param>
        /// <param name="timeInSeconds">The time in the video for which to retrieve the frame.</param>
        /// <returns>The cached RenderTexture for the specified time, or null if invalid.</returns>
        public static RenderTexture GetFrameAtTime(this VideoClipCacheHandle handle, float timeInSeconds)
        {
            if (handle?.Frames == null || handle.Frames.Count == 0)
                return null;

            var frameIndex = (int)(timeInSeconds * VideoClipFrameCache.frameRate);
            frameIndex = Mathf.Clamp(frameIndex, 0, handle.Frames.Count - 1);

            var frame = handle.Frames[frameIndex];
            return frame.IsValid() ? frame : null;
        }
    }

    /// <summary>
    /// A video processor job that renders frames of a VideoClip to disk at a specific resolution and frame rate.
    /// </summary>
    sealed class VideoClipCacheRenderJob : VideoProcessorJob<bool>
    {
        readonly string m_CachePath;
        readonly int m_TargetWidth;
        readonly int m_TargetHeight;
        readonly int m_FrameCount;
        RenderTexture m_ResizedFrame;

        public VideoClipCacheRenderJob(VideoClip clip, TaskCompletionSource<bool> tcs, string cachePath, int width, int height, int frameCount)
            : base(clip, tcs, 0, -1, null, FrameSelectionMode.Distributed)
        {
            m_CachePath = cachePath;
            m_TargetWidth = width;
            m_TargetHeight = height;
            m_FrameCount = frameCount;
        }

        protected override int GetTotalFramesToProcess() => m_FrameCount;

        protected override void InitializeProcessing()
        {
            m_ResizedFrame = new RenderTexture(m_TargetWidth, m_TargetHeight, 0, RenderTextureFormat.ARGB32);
            m_ResizedFrame.Create();
        }

        protected override void ProcessFrame(Texture2D frameTexture, MediaTime frameTime)
        {
            FrameCacheUtils.SafeBlit(frameTexture, m_ResizedFrame);

            var frameIndex = m_ProcessedDistributedFrames - 1;
            var rawPath = Path.Combine(m_CachePath, $"frame_{frameIndex:D4}.raw");
            _ = ReadAndSaveFrameAsync(m_ResizedFrame, rawPath);
        }

        static async Task ReadAndSaveFrameAsync(RenderTexture rt, string rawPath)
        {
            var request = await AsyncGPUReadback.RequestAsync(rt, 0, TextureFormat.RGBA32);
            if (request.hasError)
            {
                Debug.LogError("[VideoClipCacheRenderJob] GPU readback error.");
                return;
            }

            var rawData = request.GetData<byte>().ToArray();
            await FileIO.WriteAllBytesAsync(rawPath, rawData);
        }

        protected override void FinalizeProcessing() => m_Tcs.TrySetResult(true);

        protected override void CleanupProcessing()
        {
            if (m_ResizedFrame == null)
                return;

            m_ResizedFrame.Release();
            m_ResizedFrame.SafeDestroy();
            m_ResizedFrame = null;
        }
    }
}
