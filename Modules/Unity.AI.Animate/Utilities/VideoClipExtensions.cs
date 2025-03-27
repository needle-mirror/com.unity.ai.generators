using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Generators.UI.Utilities;
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
        /// <returns>A Task that resolves with the converted video as a byte array.</returns>
        public static async Task<Stream> ConvertAsync(this VideoClip clip,
            double startTime = 0.0,
            double endTime = -1.0,
            Format outputFormat = Format.MP4,
            bool deleteOutputOnClose = true)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            // If endTime is the default (-1), convert the entire video clip.
            if (endTime < 0 || endTime > clip.length)
            {
                endTime = clip.length;
            }

            if (startTime < 0 || startTime >= endTime)
            {
                throw new ArgumentOutOfRangeException(nameof(startTime), "Invalid time range specified.");
            }

            // Create a temporary GameObject with a VideoPlayer.
            var go = new GameObject("VideoClipConversionPlayer");
            var videoPlayer = go.AddComponent<VideoPlayer>();

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

            // Determine original and output resolutions.
            // Note that VideoClip does not directly expose width/height in runtime,
            // so here we assume the clip’s imported width and height are available.
            var originalSize = new Vector2Int((int)clip.width, (int)clip.height);
            var maxRes = new Vector2Int(1920, 1080);
            var outputSize = GetScaledResolution(originalSize, maxRes);

            // Create a render texture for rendering the clip.
            var renderTexture = RenderTexture.GetTemporary(outputSize.x, outputSize.y, 0, RenderTextureFormat.ARGB32);
            videoPlayer.targetTexture = renderTexture;

            var prepareProgress = 1f;

            var isFocused = true;

            void OnFocusChanged(bool focus)
            {
                isFocused = focus;
                if (isFocused)
                    EditorUtility.ClearProgressBar();
            }

            EditorApplication.focusChanged += OnFocusChanged;

            // Start preparing the video.
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
            {
                // this progress bar forces the VideoPlayer to work even when the Editor is out of focus
                if (!isFocused && EditorUtility.DisplayCancelableProgressBar("Preparing video converter", "Preparing video...", 1 - (prepareProgress /= 2)))
                    throw new OperationCanceledException();

                if (!Application.isPlaying)
                    EditorApplication.QueuePlayerLoopUpdate();
                await Task.Yield();
            }

            // Determine the desired frame range.
            // (Assumes clip.frameRate is the effective rate.)
            var frameRate = clip.frameRate;
            var startFrame = (long)(startTime * frameRate);
            var endFrame = (long)(endTime * frameRate);

            // Clamp the end frame ... (for a safety measure, using frameCount if available)
            if (videoPlayer.frameCount > 0)
                endFrame = Math.Min(endFrame, (long)videoPlayer.frameCount);

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

            // Get a temporary file path for the output.
            var tempOutputPath = Path.GetFullPath(FileUtil.GetUniqueTempPathInProject());
            tempOutputPath = Path.ChangeExtension(tempOutputPath, outputFormat == Format.MP4 ? ".mp4" : ".webm");

            var previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            try
            {
                // Create the encoder.
                using var encoder = new MediaEncoder(tempOutputPath, videoTrackAttributes);

                var tempTex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

                // Loop from startFrame to endFrame (exclusive) and add each frame.
                for (var frame = startFrame; frame < endFrame - 1; frame++)
                {
                    videoPlayer.frame = frame;

                    while (videoPlayer.frame < frame)
                    {
                        // this progress bar forces the VideoPlayer to work even when the Editor is out of focus
                        if (!isFocused && EditorUtility.DisplayCancelableProgressBar("Converting video", $"Converting {frame}/{endFrame} ({videoPlayer.frame})", frame / (float)endFrame))
                            throw new OperationCanceledException();

                        videoPlayer.Play();
                        videoPlayer.Pause();

                        if (!Application.isPlaying)
                            EditorApplication.QueuePlayerLoopUpdate();
                        await Task.Yield();
                    }

                    RenderTexture.active = renderTexture;
                    tempTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    tempTex.Apply();

                    RenderTexture.active = null;

                    encoder.AddFrame(tempTex);

                    if (!Application.isPlaying)
                        EditorApplication.QueuePlayerLoopUpdate();
                    await Task.Yield();
                }

                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
                tempTex.SafeDestroy();
                go.SafeDestroy();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Application.runInBackground = previousRunInBackground;
                EditorApplication.focusChanged -= OnFocusChanged;
            }

            return FileIO.OpenFileStream(tempOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096,
                (deleteOutputOnClose ? FileOptions.DeleteOnClose : FileOptions.None) | FileOptions.Asynchronous);
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
