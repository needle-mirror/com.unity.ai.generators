﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class GenerationSelector : VisualElement
    {
        readonly GridView m_GridView;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Animate/Components/GenerationSelector/GenerationSelector.uxml";
        const int k_RemovalDelayMS = 2000;

        GenerationFileSystemWatcher m_GenerationFileSystemWatcher;
        CancellationTokenSource m_ItemsRemovalCancellationTokenSource;
        float m_PreviewSizeFactor = 1;

        float GetPreviewSize() => Mathf.NextPowerOfTwo(127) * m_PreviewSizeFactor;

        [UxmlAttribute]
        public bool assetMonitor { get; set; } = true;

        readonly List<GenerationTile> m_TilePool = new();

        string m_ElementID;

        public GenerationSelector()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_GridView = this.Q<GridView>();
            m_GridView.BindTo<AnimationClipResult>(m_TilePool, () => true);
            m_GridView.MakeTileGrid(GetPreviewSize);
            this.UseAsset(SetAsset);
            this.Use(state => state.SelectPreviewSizeFactor(), OnPreviewSizeChanged);
            this.UseArray(state => state.SelectGeneratedAnimationsAndSkeletons(this), OnGeneratedTexturesChanged);

            this.Use(state => state.SelectSelectedGeneration(this), OnGenerationSelected);
            this.UseArray(state => state.SelectGenerationProgress(this), OnGenerationProgressChanged);
            this.UseContext<ScreenScaleFactor>(OnScreenScaleFactorChanged, false);

            RegisterCallback<GeometryChangedEvent>(_ => OnItemViewMaxCountChanged(this.GetTileGridMaxItemsInElement(GetPreviewSize())));
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

        void OnGenerationSelected(AnimationClipResult result)
        {
            var tiles = this.Query<GenerationTile>();
            tiles.ForEach(tile => tile.SetSelectedGeneration(result));
        }

        void OnPreviewSizeChanged(float sizeFactor)
        {
            m_PreviewSizeFactor = sizeFactor;
            m_GridView.TileSizeChanged(GetPreviewSize());
            OnItemViewMaxCountChanged(this.GetTileGridMaxItemsInElement(GetPreviewSize()));
        }

        void OnItemViewMaxCountChanged(int count) => this.Dispatch(GenerationResultsActions.setGeneratedResultVisibleCount,
            new(this.GetAsset(), m_ElementID, m_GridView.IsElementShown() ? count : 0));

        void OnGeneratedTexturesChanged(List<AnimationClipResult> animations)
        {
            var currentItemCount = m_GridView.itemsSource.Count;
            var newItemCount = animations.Count;

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

            UpdateItems(animations);
        }

        async void UpdateItemsAfterDelay(CancellationToken token)
        {
            try
            {
                await EditorTask.Delay(k_RemovalDelayMS, token);
            }
            catch (TaskCanceledException)
            {
                // If canceled, do not proceed
                return;
            }

            UpdateItems(this.GetState().SelectGeneratedAnimationsAndSkeletons(this));
        }

        void UpdateItems(IEnumerable<AnimationClipResult> textures)
        {
            ((BindingList<AnimationClipResult>)m_GridView.itemsSource).ReplaceRangeUnique(textures, result => result is TextureSkeleton);
            m_GridView.Rebuild();
        }

        void SetAsset(AssetReference asset)
        {
            OnItemViewMaxCountChanged(this.GetTileGridMaxItemsInElement(GetPreviewSize()));

            this.RemoveManipulator(m_GenerationFileSystemWatcher);
            m_GenerationFileSystemWatcher = null;

            UpdateItems(this.GetState().SelectGeneratedAnimationsAndSkeletons(this));

            if (!asset.IsValid() || !assetMonitor)
                return;

            m_GenerationFileSystemWatcher = new GenerationFileSystemWatcher(asset,
                new[] { AssetUtils.poseAssetExtension, AssetUtils.defaultAssetExtension, AssetUtils.fbxAssetExtension },
                files => this.GetStoreApi().Dispatch(GenerationResultsActions.setGeneratedAnimationsAsync,
                    new(asset, files.Select(AnimationClipResult.FromPath).ToList())));
            this.AddManipulator(m_GenerationFileSystemWatcher);
        }
    }
}
