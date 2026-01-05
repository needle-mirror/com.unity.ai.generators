using System;
using System.Threading.Tasks;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Services.Stores.States;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class VideoPreview : VisualElement
    {
        public event Action<float> currentTimeChanged;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/VideoPreview/VideoPreview.uxml";

        readonly VisualElement m_Image;
        readonly SpinnerManipulator m_SpinnerManipulator;

        TextureResult m_TextureResult;
        VideoClipCacheHandle m_CacheHandle;

        float m_CurrentTime;
        float m_PlaybackStartTime;
        float m_PlaybackEndTime = float.MaxValue;
        double m_LastEditorTime;
        bool m_IsPlaying;

        IVisualElementScheduledItem m_UpdateScheduler;

        public VideoPreview()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Image = this.Q<VisualElement>("border");
            var progress = this.Q<VisualElement>("progress");
            progress.AddManipulator(m_SpinnerManipulator = new SpinnerManipulator());

            RegisterCallback<GeometryChangedEvent>(e => _ = UpdateFrame());
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            this.UseContext<ScreenScaleFactor>(OnScreenScaleFactorChanged, false);

            isPlaying = true;

            this.Use(state => state.SelectSelectedGeneration(this), SetSelectedGeneration);
        }

        void StartSpinner()
        {
            m_SpinnerManipulator.Start();
            m_Image.style.opacity = 0.5f;
        }

        void StopSpinner()
        {
            m_SpinnerManipulator.Stop();
            m_Image.style.opacity = 1.0f;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // Re-acquire the cache handle when the element is re-attached to the UI.
            _ = SetSourceAsync(m_TextureResult, forceReload: true);
            m_UpdateScheduler?.Resume();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            // Dispose of the handle to free resources when the element is removed.
            m_CacheHandle?.Dispose();
            m_CacheHandle = null;
            m_UpdateScheduler?.Pause();
        }

        void OnScreenScaleFactorChanged(ScreenScaleFactor context) => _ = UpdateFrame();

        void UpdatePlaybackTime()
        {
            if (!m_IsPlaying || panel == null || m_CacheHandle == null)
                return;

            var currentEditorTime = EditorApplication.timeSinceStartup;
            var deltaTime = currentEditorTime - m_LastEditorTime;
            m_LastEditorTime = currentEditorTime;

            var newTime = m_CurrentTime + (float)deltaTime;

            var playbackDuration = m_PlaybackEndTime - m_PlaybackStartTime;
            if (playbackDuration > 0.001f)
            {
                if (newTime > m_PlaybackEndTime)
                {
                    newTime = m_PlaybackStartTime + (newTime - m_PlaybackEndTime) % playbackDuration;
                }
            }

            currentTime = newTime;
        }

        async Task UpdateFrame()
        {
            if (panel == null)
                return;

            var width = resolvedStyle.width;
            var height = resolvedStyle.height;

            if (width is <= 0 or float.NaN || height is <= 0 or float.NaN)
                return;

            var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1.0f;

            RenderTexture frameTexture = null;
            if (m_CacheHandle != null)
                frameTexture = m_CacheHandle.GetFrameAtTime(m_CurrentTime);
            if (!frameTexture.IsValid() && m_TextureResult.IsValid())
            {
                // Immediately show the first frame of the video as a static fallback preview.
                frameTexture = await TextureCache.GetPreview(m_TextureResult.uri, (int)(width * screenScaleFactor));
            }
            if (frameTexture != null)
            {
                m_Image.style.backgroundImage = Background.FromRenderTexture(frameTexture);
                m_Image.MarkDirtyRepaint();

                m_Image.EnableInClassList("image-scale-to-fit", resolvedStyle.width <= frameTexture.width || resolvedStyle.height <= frameTexture.height);
                m_Image.EnableInClassList("image-scale-initial", resolvedStyle.width > frameTexture.width && resolvedStyle.height > frameTexture.height);
            }
        }

        async Task UpdateFrameQuickly()
        {
            if (panel == null)
                return;

            var width = resolvedStyle.width;
            var height = resolvedStyle.height;

            if (width is <= 0 or float.NaN || height is <= 0 or float.NaN)
                return;

            var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1.0f;

            RenderTexture frameTexture = null;
            if (m_TextureResult.IsValid())
            {
                // Immediately show the first frame of the video as a static fallback preview.
                frameTexture = await TextureCache.GetPreview(m_TextureResult.uri, (int)(width * screenScaleFactor));
            }
            if (frameTexture != null)
            {
                m_Image.style.backgroundImage = Background.FromRenderTexture(frameTexture);
                m_Image.MarkDirtyRepaint();

                m_Image.EnableInClassList("image-scale-to-fit", resolvedStyle.width <= frameTexture.width || resolvedStyle.height <= frameTexture.height);
                m_Image.EnableInClassList("image-scale-initial", resolvedStyle.width > frameTexture.width && resolvedStyle.height > frameTexture.height);
            }
        }

        public void SetPlaybackRange(float startTime, float endTime)
        {
            m_PlaybackStartTime = startTime;
            m_PlaybackEndTime = endTime;

            if (m_CurrentTime < startTime || m_CurrentTime > endTime)
            {
                currentTime = startTime;
            }
        }

        public bool isPlaying
        {
            get => m_IsPlaying;
            set
            {
                if (m_IsPlaying == value) return;
                m_IsPlaying = value;

                if (m_IsPlaying)
                {
                    m_LastEditorTime = EditorApplication.timeSinceStartup;
                    if (m_UpdateScheduler == null)
                        m_UpdateScheduler = schedule.Execute(UpdatePlaybackTime).Every(20); // ~50 FPS
                    else
                        m_UpdateScheduler.Resume();
                }
                else
                {
                    m_UpdateScheduler?.Pause();
                }
            }
        }

        void SetSelectedGeneration(TextureResult result) => _ = SetSourceAsync(result);

        async Task SetSourceAsync(TextureResult result, bool forceReload = false)
        {
            // Guard against redundant calls unless a reload is forced.
            if (m_TextureResult == result && !forceReload)
                return;

            // Immediately clean up the old state and update the UI to reflect loading.
            m_CacheHandle?.Dispose();
            m_CacheHandle = null;
            m_TextureResult = result;
            m_Image.style.backgroundImage = null;

            if (result == null || !result.IsVideoClip())
            {
                return;
            }

            try
            {
                StartSpinner();
                await UpdateFrameQuickly();

                // Kick off the caching task and await it. Control returns to the caller
                // here, and the rest of the method executes when the task completes.
                var newHandle = await VideoClipFrameCache.GetOrCreateCacheAsync(result);

                // After awaiting, check if the source is still the one we requested.
                // This handles race conditions if the user selects another video quickly.
                if (m_TextureResult != result)
                {
                    newHandle?.Dispose(); // Dispose the handle we no longer need.
                    return;
                }

                if (newHandle == null) return; // Caching failed.

                // If the source is still valid, apply the new handle and update the UI.
                m_CacheHandle = newHandle;
                m_PlaybackStartTime = 0;
                m_PlaybackEndTime = result.GetDuration();
                if (m_PlaybackEndTime <= 0)
                    m_PlaybackEndTime = VideoResultFrameCache.Duration;
                currentTime = 0; // This triggers the first frame update.
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoPreview] Failed to load video cache for '{result?.uri}': {e}");
                if (m_TextureResult == result)
                {
                    m_CacheHandle = null;
                    m_Image.style.backgroundImage = null;
                }
            }
            finally
            {
                StopSpinner();
            }
        }

        public float currentTime
        {
            get => m_CurrentTime;
            set
            {
                var newTime = value;
                if (Mathf.Approximately(m_CurrentTime, newTime)) return;

                m_CurrentTime = newTime;
                currentTimeChanged?.Invoke(m_CurrentTime);
                _ = UpdateFrame();
            }
        }
    }
}
