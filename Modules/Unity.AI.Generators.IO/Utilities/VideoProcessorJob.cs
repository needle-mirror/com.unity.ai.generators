using System;
using System.Threading.Tasks;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Video;

namespace Unity.AI.Generators.IO.Utilities
{
    /// <summary>
    /// Abstract base class to manage a video processing job using the direct MediaDecoder API.
    /// This implementation is simpler and more robust than the previous VideoManager-based approach.
    /// </summary>
    abstract class VideoProcessorJob<T>
    {
        public enum FrameSelectionMode { Sequential, Distributed }

        protected readonly VideoClip m_Clip;
        protected readonly TaskCompletionSource<T> m_Tcs;
        protected readonly double m_StartTime;
        protected readonly double m_EndTime;
        protected readonly Action<float> m_ProgressCallback;
        protected readonly FrameSelectionMode m_FrameSelection;

        MediaDecoderReflected m_Decoder;
        Texture2D m_FrameBuffer;

        protected long m_StartFrame;
        protected long m_EndFrame;
        protected long m_CurrentFrame;
        protected int m_ProcessedDistributedFrames;

        protected VideoProcessorJob(VideoClip clip, TaskCompletionSource<T> tcs, double startTime, double endTime, Action<float> progressCallback, FrameSelectionMode frameSelection = FrameSelectionMode.Sequential)
        {
            m_Clip = clip;
            m_Tcs = tcs;
            m_StartTime = startTime;
            m_EndTime = endTime;
            m_ProgressCallback = progressCallback;
            m_FrameSelection = frameSelection;
        }

        public void Start()
        {
            try
            {
                if (m_Clip == null) throw new ArgumentNullException(nameof(m_Clip));
                if (m_Clip.width == 0 || m_Clip.height == 0) throw new InvalidOperationException("VideoClip has invalid dimensions (width or height is zero).");
                if (m_Clip.frameRate <= 0) throw new InvalidOperationException("VideoClip has no frame rate information.");

                var finalEndTime = (m_EndTime < 0 || m_EndTime > m_Clip.length) ? m_Clip.length : m_EndTime;
                if (m_StartTime < 0 || m_StartTime >= finalEndTime) throw new ArgumentOutOfRangeException(nameof(m_StartTime), "Invalid time range specified.");

                m_StartFrame = (long)(m_StartTime * m_Clip.frameRate);
                m_EndFrame = (long)(finalEndTime * m_Clip.frameRate);
                m_CurrentFrame = m_StartFrame;

                m_Decoder = new MediaDecoderReflected(m_Clip);
                m_FrameBuffer = new Texture2D((int)m_Clip.width, (int)m_Clip.height, TextureFormat.RGBA32, false);

                // Seek to the starting position before the update loop begins to start at the correct time.
                const uint rate = 10000;
                var count = (long)(m_StartTime * rate);
                if (!m_Decoder.SetPosition(new MediaTime(count, rate)))
                {
                    throw new InvalidOperationException($"Failed to seek video '{m_Clip.name}' to start time {m_StartTime:F2}s.");
                }

                InitializeProcessing();
                ReportProgress(0.05f);

                EditorApplication.update += Update;
            }
            catch (Exception e)
            {
                Cleanup();
                m_Tcs.TrySetException(e);
            }
        }

        void Update()
        {
            if (m_FrameSelection == FrameSelectionMode.Distributed)
                UpdateDistributed();
            else
                UpdateSequential();
        }

        void UpdateDistributed()
        {
            try
            {
                var totalFramesToProcess = GetTotalFramesToProcess();
                if (totalFramesToProcess <= 0 || m_ProcessedDistributedFrames >= totalFramesToProcess)
                {
                    Finish();
                    return;
                }

                var totalFramesInRange = m_EndFrame - m_StartFrame;
                if (totalFramesInRange < 0)
                {
                    Finish();
                    return;
                }

                long sourceFrameIndex;
                if (totalFramesToProcess <= 1)
                {
                    sourceFrameIndex = m_StartFrame;
                }
                else
                {
                    var progress = (double)m_ProcessedDistributedFrames / (totalFramesToProcess - 1);
                    sourceFrameIndex = m_StartFrame + (long)Math.Round(progress * (totalFramesInRange - 1));
                }

                var seekTime = sourceFrameIndex / m_Clip.frameRate;
                const uint rate = 10000;
                var count = (long)(seekTime * rate);

                if (!m_Decoder.SetPosition(new MediaTime(count, rate)))
                {
                    Debug.LogWarning($"Failed to seek to frame {sourceFrameIndex} ({seekTime:F2}s). Finishing job.");
                    Finish();
                    return;
                }

                var success = m_Decoder.GetNextFrame(m_FrameBuffer, out var time);
                if (success)
                {
                    m_FrameBuffer.Apply();
                    ProcessFrame(m_FrameBuffer, time);
                    m_ProcessedDistributedFrames++;
                    ReportProgress();
                }
                else
                {
                    Finish();
                }
            }
            catch (Exception e)
            {
                Cleanup();
                m_Tcs.TrySetException(e);
            }
        }

        void UpdateSequential()
        {
            try
            {
                if (m_CurrentFrame >= m_EndFrame)
                {
                    Finish();
                    return;
                }

                var success = m_Decoder.GetNextFrame(m_FrameBuffer, out var time);

                if (success)
                {
                    m_FrameBuffer.Apply();
                    ProcessFrame(m_FrameBuffer, time);
                    m_CurrentFrame++;
                    ReportProgress();
                }
                else
                {
                    Finish();
                }
            }
            catch (Exception e)
            {
                Cleanup();
                m_Tcs.TrySetException(e);
            }
        }


        void Finish()
        {
            try
            {
                ReportProgress(1f);
                FinalizeProcessing();
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
            EditorApplication.update -= Update;

            m_Decoder?.Dispose();
            m_Decoder = null;

            if (m_FrameBuffer != null)
            {
                m_FrameBuffer.SafeDestroy();
                m_FrameBuffer = null;
            }

            CleanupProcessing();
            EditorUtility.ClearProgressBar();
        }

        void ReportProgress()
        {
            float progress;
            if (m_FrameSelection == FrameSelectionMode.Distributed)
            {
                var totalFrames = GetTotalFramesToProcess();
                if (totalFrames <= 0) return;
                progress = (m_ProcessedDistributedFrames / (float)totalFrames);
            }
            else
            {
                var totalFrames = m_EndFrame - m_StartFrame;
                if (totalFrames <= 0) return;
                var processedFrames = m_CurrentFrame - m_StartFrame;
                progress = (processedFrames / (float)totalFrames);
            }

            ReportProgress(Mathf.Clamp01(0.05f + progress * 0.95f));
        }

        void ReportProgress(float value)
        {
            m_ProgressCallback?.Invoke(value);

            string message;
            if (m_FrameSelection == FrameSelectionMode.Distributed)
            {
                var totalFrames = GetTotalFramesToProcess();
                message = $"Processing frame {m_ProcessedDistributedFrames} of {totalFrames}...";
            }
            else
            {
                var totalFrames = m_EndFrame - m_StartFrame;
                var processedFrames = m_CurrentFrame - m_StartFrame;
                message = $"Processing frame {processedFrames} of {totalFrames}...";
            }
            EditorAsyncKeepAliveScope.ShowProgressOrCancelIfUnfocused("Processing Video", message, value);
        }

        protected virtual int GetTotalFramesToProcess() { return 0; }
        protected abstract void InitializeProcessing();
        protected abstract void ProcessFrame(Texture2D frameTexture, MediaTime frameTime);
        protected abstract void FinalizeProcessing();
        protected abstract void CleanupProcessing();
    }

    class FirstFrameCaptureJob : VideoProcessorJob<Texture2D>
    {
        Texture2D m_CapturedTexture;

        public FirstFrameCaptureJob(VideoClip clip, TaskCompletionSource<Texture2D> tcs)
            : base(clip, tcs, 0, 1.0d / clip.frameRate, null, FrameSelectionMode.Sequential) { }

        protected override void InitializeProcessing() { }

        protected override void ProcessFrame(Texture2D frameTexture, MediaTime frameTime)
        {
            m_CapturedTexture = new Texture2D(frameTexture.width, frameTexture.height, frameTexture.format, false) { hideFlags = HideFlags.HideAndDontSave };
            m_CapturedTexture.LoadRawTextureData(frameTexture.GetRawTextureData());
            m_CapturedTexture.Apply();

            m_Tcs.TrySetResult(m_CapturedTexture);
            m_CurrentFrame = m_EndFrame;
        }

        protected override void FinalizeProcessing() => m_Tcs.TrySetResult(null);

        protected override void CleanupProcessing()
        {
            if ((!m_Tcs.Task.IsCanceled && !m_Tcs.Task.IsFaulted) || m_CapturedTexture == null)
                return;

            m_CapturedTexture.SafeDestroy();
            m_CapturedTexture = null;
        }
    }
}
