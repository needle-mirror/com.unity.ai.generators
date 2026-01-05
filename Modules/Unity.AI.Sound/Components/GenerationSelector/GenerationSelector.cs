using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Selectors;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Sound.Windows;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    [UxmlElement]
    partial class GenerationSelector : VisualElement
    {
        readonly GridView m_GridView;
        readonly Button m_OpenGeneratorsWindowButton;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Sound/Components/GenerationSelector/GenerationSelector.uxml";

        GenerationFileSystemWatcher m_GenerationFileSystemWatcher;
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

            this.SetupInfoIcon();

            m_GridView = this.Q<GridView>();
            m_GridView.BindTo<AudioClipResult>(m_TilePool, () => true);
            m_GridView.MakeOscillogramGrid(GetHorizontalItemCount);
            this.UseAsset(SetAsset);
            this.Use(state => state.SelectPreviewSizeFactor(this), OnPreviewSizeChanged);
            this.Use(state => state.CalculateSelectorHash(this), OnGeneratedAudioClipsChanged);

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

            m_OpenGeneratorsWindowButton = this.Q<Button>("open-generator-window");
            m_OpenGeneratorsWindowButton.clicked += () =>
            {
                var asset = this.GetAsset();
                if (!asset.IsValid())
                    return;
                SoundGeneratorWindow.Display(asset.GetPath());
            };
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

        void OnItemViewMaxCountChanged(int count)
        {
            var asset = this.GetAsset();
            if (!asset.IsValid())
                return;
            this.Dispatch(GenerationResultsActions.setGeneratedResultVisibleCount,
                new(asset, m_ElementID, m_GridView.IsElementShown() ? count : 0));
        }

        void OnGeneratedAudioClipsChanged(int _) => UpdateItems(this.GetState().SelectGeneratedAudioClipsAndSkeletons(this));

        void UpdateItems(IEnumerable<AudioClipResult> audioClips)
        {
            ((BindingList<AudioClipResult>)m_GridView.itemsSource).ReplaceRangeUnique(audioClips, result => result is AudioClipSkeleton);
            m_GridView.Rebuild();

            var asset = this.GetAsset();
            if (!asset.IsValid())
                return;
            if (assetMonitor)
                this.Dispatch(Generators.UI.Actions.GenerationActions.pruneFulfilledSkeletons, new(asset));
        }

        void SetAsset(AssetReference asset)
        {
            OnItemViewMaxCountChanged(this.GetOscillogramGridMaxItemsInElement(m_GridView.fixedItemHeight, GetHorizontalItemCount()));

            this.RemoveManipulator(m_GenerationFileSystemWatcher);
            m_GenerationFileSystemWatcher = null;

            UpdateItems(this.GetState().SelectGeneratedAudioClipsAndSkeletons(this));

            if (!asset.IsValid() || !assetMonitor)
                return;

            m_GenerationFileSystemWatcher = new GenerationFileSystemWatcher(asset, AssetUtils.knownExtensions,
                files =>
                {
                    if (this.SelectWindowSettingsDisablePrecaching())
                        this.Dispatch(GenerationResultsActions.setGeneratedAudioClips,
                            new(asset, files.Select(AudioClipResult.FromPath).ToList()));
                    else
                        this.GetStoreApi().Dispatch(GenerationResultsActions.setGeneratedAudioClipsAsync,
                            new(asset, files.Select(AudioClipResult.FromPath).ToList()));
                });
            this.AddManipulator(m_GenerationFileSystemWatcher);
        }
    }
}
