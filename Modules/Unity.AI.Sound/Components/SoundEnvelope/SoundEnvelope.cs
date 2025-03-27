using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Undo;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    class SoundEnvelope : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Sound/Components/SoundEnvelope/SoundEnvelope.uxml";

        AudioClip m_Asset;
        readonly VisualElement m_TimeRuler;
        readonly Label m_TimeRulerStart;
        readonly Label m_TimeRulerMid;
        readonly Label m_TimeRulerEnd;
        readonly Button m_PlayButton;
        readonly Button m_PointModeButton;
        readonly Button m_MarkerModeButton;
        readonly SoundEnvelopeZoomButton m_ZoomButton;
        readonly Button m_LoopModeButton;
        readonly PlayManipulator m_PlayManipulator;

        RenderTexture m_CompositeTexture;
        RenderTexture m_WaveformTexture;

        public Image waveformImage { get; }

        public SoundEnvelopeUndoManager undoManager { get; }

        public float currentTime { get; set; }
        public float totalTime => m_Asset != null ? m_Asset.length : 1;

        public SoundEnvelope()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            undoManager = ScriptableObject.CreateInstance<SoundEnvelopeUndoManager>();
            undoManager.OnPanOrZoomChanged += UpdateShaderProperties;
            undoManager.OnPanOrZoomChanged += UpdateTimeMarkerPositions;
            undoManager.OnMarkerPositionChanged += UpdateShaderProperties;
            undoManager.OnControlPointDataChanged += UpdateShaderProperties;

            waveformImage = this.Q<Image>(classes: "waveform");
            m_TimeRuler = this.Q<VisualElement>(classes: "time-ruler");
            m_TimeRulerStart = this.Q<Label>(classes: "time-ruler-start");
            m_TimeRulerMid = this.Q<Label>(classes: "time-ruler-mid");
            m_TimeRulerEnd = this.Q<Label>(classes: "time-ruler-end");

            m_LoopModeButton = this.Q<Button>(classes: "loop-button");
            m_LoopModeButton.clickable = new Clickable(handler => ((VisualElement)handler.target).ToggleSelected());
            m_PlayButton = this.Q<Button>(classes: "play-button");
            m_PlayButton.clickable = m_PlayManipulator = new PlayManipulator(() => undoManager.envelopeSettings, () => m_Asset, () => m_LoopModeButton.IsSelected()) {
                timeUpdate = time => {
                    currentTime = time;
                    UpdateShaderProperties();
                }
            };
            var saveButton = this.Q<Button>(classes: "save-button");
            saveButton.clickable = new Clickable(async () =>
            {
                await Save();
                SetAsset(this.GetAsset());
            });
            m_ZoomButton = this.Q<SoundEnvelopeZoomButton>();
            m_PointModeButton = this.Q<Button>(classes: "envelope-button");
            m_PointModeButton.clickable = new Clickable(ExclusiveClickHandler);
            m_MarkerModeButton = this.Q<Button>(classes: "trim-button");
            m_MarkerModeButton.clickable = new Clickable(ExclusiveClickHandler);
            m_MarkerModeButton.SetSelected();
            UpdateMarkerMode();

            this.UseAsset(SetAsset);

            this.UseContext<ScreenScaleFactor>(OnScreenScaleFactorChanged, false);

            m_ZoomButton.onZoomChanged += zoomValue => {
                undoManager.CurrentZoomLevel = zoomValue;
                UpdateTimeMarkerPositions();
            };

            RegisterCallback<GeometryChangedEvent>(_ => UpdateShaderProperties());
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (m_WaveformTexture)
                    RenderTexture.ReleaseTemporary(m_WaveformTexture);
                if (m_CompositeTexture)
                    RenderTexture.ReleaseTemporary(m_CompositeTexture);
            });

            this.AddManipulator(new SoundEnvelopeManipulator());
            return;

            void ExclusiveClickHandler(EventBase handler) {
                var handlerTarget = (VisualElement)handler.target;
                var wasSelected = handlerTarget.IsSelected();
                m_PointModeButton.SetSelected(false);
                m_MarkerModeButton.SetSelected(false);
                if (!wasSelected)
                    handlerTarget.ToggleSelected();
                UpdateMarkerMode();
            }
        }

        async Task Save()
        {
            if (undoManager.envelopeSettings == new SoundEnvelopeSettings())
                return;

            var path = AssetDatabase.GetAssetPath(m_Asset);
            path = Path.ChangeExtension(path, ".wav");

            {
                await using var fileStream = FileIO.OpenFileStream(path, FileMode.Create);
                m_Asset.EncodeToWav(fileStream, undoManager.envelopeSettings);
            }

            undoManager.envelopeSettings = new SoundEnvelopeSettings();
            AssetDatabase.Refresh();
        }

        public void SaveOnClose()
        {
            if (undoManager.envelopeSettings != new SoundEnvelopeSettings() &&
                EditorUtility.DisplayDialog("Warning", "You are about to lose your changes. Do you want to save them?", "Yes", "No"))
                _ = Save();
        }

        void OnAssetChanged()
        {
            SaveOnClose();

            m_PlayManipulator?.Cancel();
        }

        void OnScreenScaleFactorChanged(ScreenScaleFactor screenScaleFactor)
        {
            UpdateShaderProperties();
        }

        void SetAsset(AssetReference assetReference)
        {
            OnAssetChanged();

            m_Asset = AssetDatabase.LoadAssetAtPath<AudioClip>(assetReference.GetPath());
            undoManager.envelopeSettings = new SoundEnvelopeSettings();
            UpdateTimeMarkerPositions();
            UpdateShaderProperties();
        }

        void UpdateShaderProperties()
        {
            if (m_Asset == null)
            {
                waveformImage.image = null;
                return;
            }

            var width = waveformImage.resolvedStyle.width;
            var height = waveformImage.resolvedStyle.height;
            if (width is <= 0 or float.NaN)
                return;

            // Generate reference texture using utility
            var sampleReferenceTexture = m_Asset.MakeSampleReference((int)width, undoManager.CurrentZoomLevel, undoManager.TotalPanOffset);
            if (sampleReferenceTexture == null)
                return;

            var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1.0f;
            var markerSettings = new SoundEnvelopeMarkerSettings
            {
                envelopeSettings = undoManager.envelopeSettings,
                panOffset = undoManager.TotalPanOffset,
                padding = 16,
                playbackPosition = currentTime / Mathf.Max(totalTime, 0.01f),
                selectedPointIndex = undoManager.SelectedPointIndex,
                showControlLines = undoManager.CurrentMarkerMode == MarkerMode.Point,
                showControlPoints = undoManager.CurrentMarkerMode == MarkerMode.Point,
                showCursor = currentTime != 0,
                showMarker = undoManager.CurrentMarkerMode == MarkerMode.Marker,
                zoomScale = m_ZoomButton.zoomValue,
                width = width,
                height = height,
                screenScaleFactor = screenScaleFactor,
            };

            m_WaveformTexture = AudioClipOscillogramRenderingUtils.GetTemporary(sampleReferenceTexture, markerSettings, m_WaveformTexture);
            if (m_WaveformTexture == null)
                return;

            m_CompositeTexture = AudioClipMarkerRenderingUtils.GetTemporary(m_WaveformTexture, markerSettings, m_CompositeTexture);

            // Set the result to the image
            waveformImage.image = m_CompositeTexture;
            waveformImage.MarkDirtyRepaint();
        }

        static string FormatTime(float totalSeconds) => TimeSpan.FromSeconds(totalSeconds).ToString(@"mm\:ss\.fff");

        void UpdateTimeMarkerPositions()
        {
            var halfDuration = totalTime / 2;
            var rulerWidth = m_TimeRuler.resolvedStyle.width;
            var zoomedRulerWidth = rulerWidth / undoManager.CurrentZoomLevel;
            var adjustedLeftPosition = (rulerWidth - zoomedRulerWidth) / 2 - undoManager.TotalPanOffset * zoomedRulerWidth;
            var adjustedRightPosition = adjustedLeftPosition + waveformImage.resolvedStyle.width / undoManager.CurrentZoomLevel;
            var midDuration = undoManager.TotalPanOffset * totalTime + halfDuration;
            m_TimeRulerStart.text = FormatTime(adjustedLeftPosition >= 0 && adjustedLeftPosition <= rulerWidth ? 0 : midDuration - halfDuration * undoManager.CurrentZoomLevel);
            m_TimeRulerEnd.text = FormatTime(adjustedRightPosition >= 0 && adjustedRightPosition <= rulerWidth ? totalTime : midDuration + halfDuration * undoManager.CurrentZoomLevel);
            m_TimeRulerMid.text = FormatTime(Mathf.Clamp(midDuration, 0, totalTime));
        }

        void UpdateMarkerMode()
        {
            undoManager.CurrentMarkerMode = m_PointModeButton.IsSelected() ? MarkerMode.Point : m_MarkerModeButton.IsSelected() ? MarkerMode.Marker : MarkerMode.None;
            undoManager.SelectedPointIndex = undoManager.CurrentMarkerMode == MarkerMode.Point ? undoManager.SelectedPointIndex : -1;

            UpdateShaderProperties();
        }
    }
}
