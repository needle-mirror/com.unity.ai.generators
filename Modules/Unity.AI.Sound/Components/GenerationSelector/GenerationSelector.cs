using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    [UxmlElement]
    partial class GenerationSelector : VisualElement
    {
        readonly GridView m_GridView;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Sound/Components/GenerationSelector/GenerationSelector.uxml";
        const int k_RemovalDelayMS = 2000;

        GenerationFileSystemWatcher m_GenerationFileSystemWatcher;
        CancellationTokenSource m_ItemsRemovalCancellationTokenSource;
        float m_PreviewSizeFactor = 1;

        float GetHorizontalItemCount() => 1 / (m_PreviewSizeFactor * m_PreviewSizeFactor);

        [UxmlAttribute]
        public bool assetMonitor { get; set; } = true;

        readonly List<GenerationTile> m_TilePool = new();

        string m_ElementID;

        public GenerationSelector()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_GridView = this.Q<GridView>();
            m_GridView.BindTo<AudioClipResult>(m_TilePool, () => true);
            m_GridView.MakeOscillogramGrid(GetHorizontalItemCount);
            this.UseAsset(SetAsset);
            this.Use(state => state.SelectPreviewSizeFactor(), OnPreviewSizeChanged);
            this.UseArray(state => state.SelectGeneratedAudioClipsAndSkeletons(this), OnGeneratedAudioClipsAndSkeletonsChanged);

            this.Use(state => state.SelectSelectedGeneration(this), OnGenerationSelected);
            this.UseArray(state => state.SelectGenerationProgress(this), OnGenerationProgressChanged);
            this.UseContext<ScreenScaleFactor>(OnScreenScaleFactorChanged, false);

            RegisterCallback<GeometryChangedEvent>(_ => OnItemViewMaxCountChanged(this.GetOscillogramGridMaxItemsInElement(m_GridView.fixedItemHeight, GetHorizontalItemCount())));
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                OnItemViewMaxCountChanged(0);
                this.RemoveManipulator(m_GenerationFileSystemWatcher);
            });
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                this.AddManipulator(m_GenerationFileSystemWatcher);
                if (string.IsNullOrEmpty(m_ElementID))
                    m_ElementID = this.GetElementIdentifier().ToString();
            });
        }

        void OnScreenScaleFactorChanged(ScreenScaleFactor factor)
        {
            var tiles = this.Query<GenerationTile>();
            tiles.ForEach(tile => tile.OnScreenScaleFactorChanged(factor));
        }

        void OnGenerationProgressChanged(List<GenerationProgressData> data)
        {
            var tiles = this.Query<GenerationTile>();
            tiles.ForEach(tile => tile.SetGenerationProgress(data));
        }

        void OnGenerationSelected(AudioClipResult result)
        {
            var tiles = this.Query<GenerationTile>();
            tiles.ForEach(tile => tile.SetSelectedGeneration(result));
        }

        void OnPreviewSizeChanged(float sizeFactor)
        {
            m_PreviewSizeFactor = sizeFactor;
            m_GridView.OscillogramTileSizeChanged(GetHorizontalItemCount());
            OnItemViewMaxCountChanged(this.GetOscillogramGridMaxItemsInElement(m_GridView.fixedItemHeight, GetHorizontalItemCount()));
        }

        void OnItemViewMaxCountChanged(int count) => this.Dispatch(GenerationResultsActions.setGeneratedResultVisibleCount,
            new(this.GetAsset(), m_ElementID, m_GridView.IsElementShown() ? count : 0));

        void OnGeneratedAudioClipsAndSkeletonsChanged(List<AudioClipResult> audioClips)
        {
            var currentItemCount = m_GridView.itemsSource.Count;
            var newItemCount = audioClips.Count;

            m_ItemsRemovalCancellationTokenSource?.Cancel();
            m_ItemsRemovalCancellationTokenSource?.Dispose();
            if (newItemCount < currentItemCount)
            {
                // Items are being removed
                // Schedule update after delay
                m_ItemsRemovalCancellationTokenSource = new CancellationTokenSource();
                UpdateItemsAfterDelay(m_ItemsRemovalCancellationTokenSource.Token);
                return;
            }

            // Items are added or same count
            // Update immediately
            m_ItemsRemovalCancellationTokenSource = null;

            UpdateItems(audioClips);
        }

        async void UpdateItemsAfterDelay(CancellationToken token)
        {
            try
            {
                await Task.Delay(k_RemovalDelayMS, token);
            }
            catch (TaskCanceledException)
            {
                // If canceled, do not proceed
                return;
            }

            UpdateItems(this.GetState().SelectGeneratedAudioClipsAndSkeletons(this));
        }

        void UpdateItems(IEnumerable<AudioClipResult> audioClips)
        {
            ((BindingList<AudioClipResult>)m_GridView.itemsSource).ReplaceRangeUnique(audioClips, result => result is TextureSkeleton);
            m_GridView.Rebuild();
        }

        void SetAsset(AssetReference asset)
        {
            OnItemViewMaxCountChanged(this.GetOscillogramGridMaxItemsInElement(m_GridView.fixedItemHeight, GetHorizontalItemCount()));

            this.RemoveManipulator(m_GenerationFileSystemWatcher);
            m_GenerationFileSystemWatcher = null;

            UpdateItems(new List<AudioClipResult>());

            if (!asset.IsValid() || !assetMonitor)
                return;

            UpdateItems(this.GetState().SelectGeneratedAudioClipsAndSkeletons(this));

            m_GenerationFileSystemWatcher = new GenerationFileSystemWatcher(asset, new[] { ".wav" },
                files => this.GetStoreApi().Dispatch(GenerationResultsActions.setGeneratedAudioClipsAsync,
                    new(asset, files.Select(AudioClipResult.FromPath).ToList())));
            this.AddManipulator(m_GenerationFileSystemWatcher);
        }
    }
}
