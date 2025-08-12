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
    /// This implementation uses the internal VideoUtil preview system for robust conversion
    /// across all editor states (Edit, Play, and Paused Play modes).
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
        /// </summary>
        /// <param name="clip">The video clip to convert.</param>
        /// <param name="startTime">The start time (in seconds). Defaults to 0.</param>
        /// <param name="endTime">The end time (in seconds). Defaults to -1, which means the full video.</param>
        /// <param name="outputFormat">The output video format. Defaults to MP4.</param>
        /// <param name="deleteOutputOnClose">Delete the file on stream close</param>
        /// <param name="progressCallback">Optional callback for reporting progress between 0.0 and 1.0</param>
        /// <returns>A Task that resolves with a Stream of the converted video.</returns>
        public static Task<Stream> ConvertAsync(this VideoClip clip,
            double startTime = 0.0,
            double endTime = -1.0,
            Format outputFormat = Format.MP4,
            bool deleteOutputOnClose = true,
            Action<float> progressCallback = null)
        {
            // Use a custom TaskCompletionSource to manage the async operation.
            // This allows us to control the task's completion from the EditorApplication.update loop.
            var tcs = new TaskCompletionSource<Stream>();

            // Create an instance of our conversion job class.
            var converter = new VideoConverterJob(
                clip, tcs, startTime, endTime, outputFormat, deleteOutputOnClose, progressCallback);

            // Start the job.
            converter.Start();

            // Return the task that will be completed by the job.
            return tcs.Task;
        }

        /// <summary>
        /// Private class to manage the state of a single conversion job.
        /// </summary>
        class VideoConverterJob
        {
            // Add a new state for our state machine
            private enum State { Processing, WaitingForFrame }
            private State m_JobState = State.Processing;

            // Input parameters
            readonly VideoClip m_Clip;
            readonly TaskCompletionSource<Stream> m_Tcs;
            readonly double m_StartTime;
            readonly double m_EndTime;
            readonly Format m_OutputFormat;
            readonly bool m_DeleteOutputOnClose;
            readonly Action<float> m_ProgressCallback;

            // State variables
            GUID m_PreviewID;
            MediaEncoder m_Encoder;
            Texture2D m_TempTex;
            string m_TempOutputPath;

            long m_StartFrame;
            long m_EndFrame;
            long m_CurrentFrame;

            // ... (Constructor and Start() method remain the same) ...

            public VideoConverterJob(VideoClip clip, TaskCompletionSource<Stream> tcs, double startTime, double endTime,
                Format outputFormat, bool deleteOutputOnClose, Action<float> progressCallback)
            {
                m_Clip = clip;
                m_Tcs = tcs;
                m_StartTime = startTime;
                m_EndTime = endTime;
                m_OutputFormat = outputFormat;
                m_DeleteOutputOnClose = deleteOutputOnClose;
                m_ProgressCallback = progressCallback;
            }

            public void Start()
            {
                try
                {
                    // --- 1. Validation and Setup ---
                    if (m_Clip == null)
                        throw new ArgumentNullException(nameof(m_Clip));

                    if (m_Clip.frameRate <= 0)
                        throw new InvalidOperationException("VideoClip has no frame rate information.");

                    var finalEndTime = (m_EndTime < 0 || m_EndTime > m_Clip.length) ? m_Clip.length : m_EndTime;
                    if (m_StartTime < 0 || m_StartTime >= finalEndTime)
                        throw new ArgumentOutOfRangeException(nameof(m_StartTime), "Invalid time range specified.");

                    ReportProgress(0.01f);

                    m_StartFrame = (long)(m_StartTime * m_Clip.frameRate);
                    m_EndFrame = (long)(finalEndTime * m_Clip.frameRate);
                    m_CurrentFrame = m_StartFrame;

                    // --- 2. Start the VideoUtil Preview ---
                    m_PreviewID = VideoUtilReflected.StartPreview(m_Clip);
                    if (m_PreviewID.Empty())
                        throw new InvalidOperationException("Failed to start internal video preview system.");

                    // Immediately pause it after starting. We will control playback manually.
                    VideoUtilReflected.PausePreview(m_PreviewID);

                    ReportProgress(0.05f);

                    // --- 3. Prepare the MediaEncoder ---
                    var outputSize = new Vector2Int((int)m_Clip.width, (int)m_Clip.height);
                    var videoAttrs = CreateVideoTrackAttributes(outputSize, m_Clip.frameRate, m_OutputFormat);

                    m_TempOutputPath = Path.GetFullPath(FileUtil.GetUniqueTempPathInProject());
                    m_TempOutputPath = Path.ChangeExtension(m_TempOutputPath, m_OutputFormat == Format.MP4 ? ".mp4" : ".webm");

                    m_Encoder = new MediaEncoder(m_TempOutputPath, videoAttrs);
                    m_TempTex = new Texture2D(outputSize.x, outputSize.y, TextureFormat.RGBA32, false);

                    ReportProgress(0.1f);

                    // --- 4. Hook into EditorApplication.update to drive the conversion ---
                    EditorApplication.update += Update;
                }
                catch (Exception e)
                {
                    // If setup fails, clean up and fail the task.
                    Cleanup();
                    m_Tcs.TrySetException(e);
                }
            }

            void Update()
            {
                try
                {
                    // --- State Machine Logic ---
                    if (m_JobState == State.WaitingForFrame)
                    {
                        // We waited one frame, now we can process.
                        m_JobState = State.Processing;
                    }
                    else if (m_JobState == State.Processing)
                    {
                        ProcessCurrentFrame();
                    }
                }
                catch (Exception e)
                {
                    Cleanup();
                    m_Tcs.TrySetException(e);
                }
            }

            void ProcessCurrentFrame()
            {
                // --- Frame Processing Loop ---
                if (m_CurrentFrame >= m_EndFrame)
                {
                    // We've processed all frames, finalize the process.
                    Finish();
                    return;
                }

                // Get the texture from the preview system. It will be null until the first frame is ready.
                var videoTexture = VideoUtilReflected.GetPreviewTexture(m_PreviewID);
                if (videoTexture == null)
                {
                    // The preview isn't ready yet. Tell it to play so it can prepare the first frame.
                    VideoUtilReflected.PlayPreview(m_PreviewID, false);
                    m_JobState = State.WaitingForFrame; // Wait a frame for it to become available.
                    return;
                }

                // --- Add Frame to Encoder ---
                var activeRT = RenderTexture.active;
                RenderTexture.active = (RenderTexture)videoTexture;
                m_TempTex.ReadPixels(new Rect(0, 0, videoTexture.width, videoTexture.height), 0, 0);
                m_TempTex.Apply();
                RenderTexture.active = activeRT;

                m_Encoder.AddFrame(m_TempTex);

                // --- Advance to the next frame and report progress ---
                m_CurrentFrame++;

                // Calculate progress from 0.1 to 0.95
                var frameProgress = (m_CurrentFrame - m_StartFrame) / (float)(m_EndFrame - m_StartFrame);
                ReportProgress(0.1f + frameProgress * 0.85f);

                if (m_CurrentFrame < m_EndFrame)
                {
                    // Tell the player to play to advance to the next frame.
                    VideoUtilReflected.PlayPreview(m_PreviewID, false);
                    // IMPORTANT: Set state to wait for the next frame to be decoded.
                    m_JobState = State.WaitingForFrame;
                }
            }

            void Finish()
            {
                // ... (Finish method remains the same) ...
                try
                {
                    ReportProgress(0.98f);

                    m_Encoder?.Dispose();
                    m_Encoder = null;

                    var stream = FileIO.OpenFileStream(m_TempOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096,
                        (m_DeleteOutputOnClose ? FileOptions.DeleteOnClose : FileOptions.None) | FileOptions.Asynchronous);

                    m_Tcs.TrySetResult(stream);
                }
                catch (Exception e)
                {
                    m_Tcs.TrySetException(e);
                }
                finally
                {
                    Cleanup();
                }
            }

            void Cleanup()
            {
                // ... (Cleanup method remains the same) ...
                EditorApplication.update -= Update;

                m_Encoder?.Dispose();

                if (!m_PreviewID.Empty())
                {
                    VideoUtilReflected.StopPreview(m_PreviewID);
                    m_PreviewID = new GUID();
                }

                if (m_TempTex != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_TempTex);
                    m_TempTex = null;
                }
            }

            void ReportProgress(float value)
            {
                m_ProgressCallback?.Invoke(Mathf.Clamp01(value));
            }
        }

        // --- Helper Methods ---
        // ... (CreateVideoTrackAttributes and GetRationalFrameRate remain the same) ...
        static VideoTrackEncoderAttributes CreateVideoTrackAttributes(Vector2Int size, double frameRate, Format format)
        {
            var videoAttrs = format switch
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
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported codec")
            };

            videoAttrs.frameRate = GetRationalFrameRate(frameRate);
            videoAttrs.width = (uint)size.x;
            videoAttrs.height = (uint)size.y;
            videoAttrs.includeAlpha = false;
            videoAttrs.bitRateMode = VideoBitrateMode.High;

            return videoAttrs;
        }

        static MediaRational GetRationalFrameRate(double value)
        {
            var numerator = Mathf.RoundToInt((float)(value * 1000));
            return new MediaRational(numerator, 1000);
        }
    }
}
