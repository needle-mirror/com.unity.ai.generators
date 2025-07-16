using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Video;

namespace Unity.AI.Animate.Services.Utilities
{
    /// <summary>
    /// Provides video conversion extension methods for VideoClip.
    /// </summary>
    static class VideoClipExtensions
    {
        public enum Format
        {
            MP4,
            WEBM,
        }

        /// <summary>
        /// Converts the video clip into a different format for the specified time range,
        /// returning the conversion as a byte array.
        /// If <paramref name="endTime"/> is -1 (the default), the entire clip is converted.
        /// </summary>
        /// <param name="clip">The video clip to convert.</param>
        /// <param name="startTime">The start time (in seconds). Defaults to 0.</param>
        /// <param name="endTime">The end time (in seconds). Defaults to -1, which means the full video.</param>
        /// <param name="outputFormat">The output video format. Defaults to MP4.</param>
        /// <param name="deleteOutputOnClose">Delete the file on stream close</param>
        /// <param name="progressCallback">Optional callback for reporting progress between 0.0 and 1.0</param>
        /// <returns>A Task that resolves with the converted video as a byte array.</returns>
        public static async Task<Stream> ConvertAsync(this VideoClip clip,
            double startTime = 0.0,
            double endTime = -1.0,
            Format outputFormat = Format.MP4,
            bool deleteOutputOnClose = true,
            Action<float> progressCallback = null)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            // Report initial progress
            progressCallback?.Invoke(0.01f);

            // If endTime is the default (-1), convert the entire video clip.
            if (endTime < 0 || endTime > clip.length)
            {
                endTime = clip.length;
            }

            if (startTime < 0 || startTime >= endTime)
            {
                throw new ArgumentOutOfRangeException(nameof(startTime), "Invalid time range specified.");
            }

            var previousRunInBackground = Application.runInBackground;
            try
            {
                Application.runInBackground = true;

                // Create a temporary GameObject with a VideoPlayer.
                var go = new GameObject("VideoClipConversionPlayer");
                var videoPlayer = go.AddComponent<VideoPlayer>();

                progressCallback?.Invoke(0.02f);

                // Configure the VideoPlayer.
                videoPlayer.playOnAwake = false;
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.clip = clip;
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                videoPlayer.sendFrameReadyEvents = true;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.isLooping = false;
                videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                videoPlayer.skipOnDrop = false;
                videoPlayer.playbackSpeed = 10f;

                progressCallback?.Invoke(0.03f);

                // Determine original and output resolutions.
                // Note that VideoClip does not directly expose width/height in runtime,
                // so here we assume the clip's imported width and height are available.
                var originalSize = new Vector2Int((int)clip.width, (int)clip.height);
                var maxRes = new Vector2Int(1920, 1080);
                var outputSize = GetScaledResolution(originalSize, maxRes);

                progressCallback?.Invoke(0.04f);

                // Create a render texture for rendering the clip.
                var renderTexture = RenderTexture.GetTemporary(outputSize.x, outputSize.y, 0, RenderTextureFormat.ARGB32);
                videoPlayer.targetTexture = renderTexture;

                progressCallback?.Invoke(0.05f);

                // Start preparing the video.
                videoPlayer.Prepare();
                var prepareProgress = 0.05f;
                while (!videoPlayer.isPrepared)
                {
                    // this progress bar forces the VideoPlayer to work even when the Editor is out of focus
                    prepareProgress = Mathf.Min(0.09f, prepareProgress + 0.005f);
                    progressCallback?.Invoke(prepareProgress);

                    if (EditorFocusScope.ShowProgressOrCancelIfUnfocused("Preparing video converter", "Preparing video...", prepareProgress))
                        throw new OperationCanceledException();

                    if (!Application.isPlaying)
                        EditorApplication.QueuePlayerLoopUpdate();

                    await EditorTask.Yield();
                }

                progressCallback?.Invoke(0.1f);

                // Determine the desired frame range.
                // (Assumes clip.frameRate is the effective rate.)
                var frameRate = clip.frameRate;
                var startFrame = (long)(startTime * frameRate);
                var endFrame = (long)(endTime * frameRate);

                // Clamp the end frame ... (for a safety measure, using frameCount if available)
                if (videoPlayer.frameCount > 0)
                    endFrame = Math.Min(endFrame, (long)videoPlayer.frameCount);

                progressCallback?.Invoke(0.11f);

                // Configure the video track attributes for the encoder.
                var videoTrackAttributes = outputFormat switch
                {
                    Format.MP4 => new VideoTrackEncoderAttributes(new H264EncoderAttributes
                    {
                        gopSize = 25,
                        numConsecutiveBFrames = 2,
                        profile = VideoEncodingProfile.H264High
                    }),
                    Format.WEBM => new VideoTrackEncoderAttributes(new VP8EncoderAttributes
                    {
                        keyframeDistance = 25
                    }),
                    _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, "Unsupported codec")
                };

                videoTrackAttributes.frameRate = GetRationalFrameRate(frameRate);
                videoTrackAttributes.width = (uint)outputSize.x;
                videoTrackAttributes.height = (uint)outputSize.y;
                videoTrackAttributes.includeAlpha = false;
                videoTrackAttributes.bitRateMode = VideoBitrateMode.High;
                videoTrackAttributes.targetBitRate = (uint)(8f * 1_000_000);

                progressCallback?.Invoke(0.12f);

                // Get a temporary file path for the output.
                var tempOutputPath = Path.GetFullPath(FileUtil.GetUniqueTempPathInProject());
                tempOutputPath = Path.ChangeExtension(tempOutputPath, outputFormat == Format.MP4 ? ".mp4" : ".webm");

                {
                    // Create the encoder.
                    using var encoder = new MediaEncoder(tempOutputPath, videoTrackAttributes);
                    var tempTex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

                    progressCallback?.Invoke(0.13f);

                    // Loop from startFrame to endFrame (exclusive) and add each frame.
                    var totalFrames = (int)(endFrame - startFrame - 1);
                    for (var frame = startFrame; frame < endFrame - 1; frame++)
                    {
                        // Calculate encoding progress between 0.13 and 0.93
                        var frameProgress = (frame - startFrame) / (float)totalFrames;
                        progressCallback?.Invoke(0.13f + frameProgress * 0.8f);

                        videoPlayer.frame = frame;

                        var timeWaiting = 0f;
                        var timeCheckInterval = 0.1f;

                        while (videoPlayer.frame < frame)
                        {
                            // this progress bar forces the VideoPlayer to work even when the Editor is out of focus
                            if (EditorFocusScope.ShowProgressOrCancelIfUnfocused("Converting video", $"Converting {frame}/{endFrame} ({videoPlayer.frame})",
                                    frame / (float)endFrame))
                                throw new OperationCanceledException();

                            videoPlayer.Play();

                            if (!Application.isPlaying)
                                EditorApplication.QueuePlayerLoopUpdate();
                            await EditorTask.Yield();

                            videoPlayer.Pause();

                            // Add time tracking
                            timeWaiting += timeCheckInterval;

                            // Check for timeout (10 seconds)
                            if (timeWaiting >= 10.0f)
                            {
                                Debug.LogWarning($"Frame {frame} processing timed out after 10 seconds. Skipping to next frame.");
                                break;
                            }
                        }

                        RenderTexture.active = renderTexture;
                        tempTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                        tempTex.Apply();

                        RenderTexture.active = null;

                        encoder.AddFrame(tempTex);

                        if (!Application.isPlaying)
                            EditorApplication.QueuePlayerLoopUpdate();
                        await EditorTask.Yield();
                    }

                    progressCallback?.Invoke(0.94f);

                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(renderTexture);
                    tempTex.SafeDestroy();
                    go.SafeDestroy();
                }

                progressCallback?.Invoke(0.95f);

                return FileIO.OpenFileStream(tempOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096,
                    (deleteOutputOnClose ? FileOptions.DeleteOnClose : FileOptions.None) | FileOptions.Asynchronous);
            }
            finally
            {
                Application.runInBackground = previousRunInBackground;
            }
        }

        /// <summary>
        /// Returns a scaled resolution that fits within the given maximum dimensions.
        /// </summary>
        static Vector2Int GetScaledResolution(Vector2Int original, Vector2Int max)
        {
            var aspect = (float)original.x / original.y;
            // Ensure max resolution is properly oriented.
            if (aspect < 1f && max.x > max.y)
            {
                max = new Vector2Int(max.y, max.x);
            }
            if (original.x <= max.x && original.y <= max.y)
            {
                return original;
            }
            var scale = Mathf.Min(max.x / (float)original.x, max.y / (float)original.y);
            return Vector2Int.RoundToInt(new Vector2(original.x, original.y) * scale);
        }

        /// <summary>
        /// Creates a MediaRational from a float frame rate.
        /// </summary>
        static MediaRational GetRationalFrameRate(double value)
        {
            var numerator = Mathf.RoundToInt((float)(value * 1000));
            return new MediaRational(numerator, 1000);
        }
    }
}
