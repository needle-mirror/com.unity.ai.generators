using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class GenerationTile : VisualElement
    {
        public VisualElement image { get; }
        public VisualElement progress { get; }

        public TextureResult textureResult { get; private set; }

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/GenerationSelector/GenerationTile.uxml";

        readonly DragExternalFileManipulator m_DragManipulator;
        readonly SpinnerManipulator m_SpinnerManipulator;
        readonly ContextualMenuManipulator m_ContextualMenu;
        readonly ContextualMenuManipulator m_FailedContextualMenu;

        RenderTexture m_SkeletonTexture;
        readonly Label m_Label;
        GenerationMetadata m_Metadata;

        public GenerationTile()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Label = this.Q<Label>("label");
            image = this.Q<VisualElement>("image");
            RegisterCallback<AttachToPanelEvent>(_ => {
                if (m_SkeletonTexture)
                    progress.style.backgroundImage = Background.FromRenderTexture(m_SkeletonTexture); });
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<MouseOverEvent>(_ => UpdateTooltip());

            m_ContextualMenu = new ContextualMenuManipulator(BuildContextualMenu);
            m_FailedContextualMenu = new ContextualMenuManipulator(BuildFailedContextualMenu);
            m_DragManipulator = new DragExternalFileManipulator();

            progress = this.Q<VisualElement>("progress");
            progress.AddManipulator(m_SpinnerManipulator = new SpinnerManipulator());
        }

        public void SetGenerationProgress(IEnumerable<GenerationProgressData> data)
        {
            if (textureResult is not TextureSkeleton)
                return;

            var progressReport = data.FirstOrDefault(d => d.taskID == ((TextureSkeleton)textureResult).taskID);
            if (progressReport == null)
                return;

            m_SpinnerManipulator.Start();

            m_Label.text = $"{progressReport.progress * 100:0} %";

            var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1f;
            var previousSkeletonTexture = m_SkeletonTexture;
            var width = float.IsNaN(resolvedStyle.width) ? (int)TextureSizeHint.Carousel : (int)resolvedStyle.width;
            m_SkeletonTexture = SkeletonRenderingUtils.GetTemporary(progressReport.progress, width, width, screenScaleFactor);
            if (previousSkeletonTexture == m_SkeletonTexture)
                return;

            progress.style.backgroundImage = Background.FromRenderTexture(m_SkeletonTexture);
            MarkDirtyRepaint();
        }

        public void SetSelectedGeneration(TextureResult result) => this.SetSelected(this.textureResult != null && result != null && this.textureResult.uri == result.uri);

        void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Clear();
            if (textureResult is TextureSkeleton)
                return;

            evt.menu.AppendAction("Select", async _ =>
            {
                if (this.GetAsset() == null || textureResult == null)
                    return;
                var asset = this.GetAsset();
                var store = this.GetStoreApi(); // fixme: this is weird, otherwise promoteFocusedGeneration doesn't work
                await store.Dispatch(GenerationResultsActions.selectGeneration, new(asset, textureResult, false, false));
            }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Promote to current asset", async _ =>
            {
                var asset = this.GetAsset();
                var store = this.GetStoreApi(); // fixme: this is weird, otherwise promoteFocusedGeneration doesn't work
                await store.Dispatch(GenerationResultsActions.selectGeneration, new(asset, textureResult, true, false));
            }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Promote to new asset", _ =>
            {
                var asset = this.GetAsset();
                var store = this.GetStoreApi(); // fixme: this is weird, otherwise promoteFocusedGeneration doesn't work
                store.Dispatch(SessionActions.promoteFocusedGeneration, new (asset, textureResult));
            }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Show in Explorer" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Reveal in Finder" : "Show in File Manager", _ =>
            {
                EditorUtility.RevealInFinder(File.Exists(textureResult.uri.GetLocalPath())
                    ? textureResult.uri.GetLocalPath()
                    : Path.GetDirectoryName(textureResult.uri.GetLocalPath()));
            }, DropdownMenuAction.AlwaysEnabled);

            var showGenerationDataStatus = DropdownMenuAction.Status.Normal;
            if (this.GetAsset() == null || textureResult == null || !File.Exists(textureResult.uri.GetLocalPath()))
            {
                showGenerationDataStatus = DropdownMenuAction.Status.Disabled;
            }
            evt.menu.AppendAction("Show Generation Data", async _ =>
            {
                if (this.GetAsset() == null || textureResult == null)
                    return;

                var asset = this.GetAsset();
                var store = this.GetStore();

                await store.Dispatch(GenerationSettingsActions.openGenerationDataWindow, new GenerationDataWindowArgs(asset, this, textureResult));
            }, showGenerationDataStatus);
        }

        void BuildFailedContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Clear();
            if (textureResult is TextureSkeleton)
                return;

            evt.menu.AppendAction(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Show in Explorer" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Reveal in Finder" : "Show in File Manager", _ =>
            {
                EditorUtility.RevealInFinder(File.Exists(textureResult.uri.GetLocalPath())
                    ? textureResult.uri.GetLocalPath()
                    : Path.GetDirectoryName(textureResult.uri.GetLocalPath()));
            }, DropdownMenuAction.AlwaysEnabled);

            var showGenerationDataStatus = DropdownMenuAction.Status.Normal;
            if (this.GetAsset() == null || textureResult == null || !File.Exists(textureResult.uri.GetLocalPath()))
            {
                showGenerationDataStatus = DropdownMenuAction.Status.Disabled;
            }
            evt.menu.AppendAction("Show Generation Data", async _ =>
            {
                if (this.GetAsset() == null || textureResult == null)
                    return;

                var asset = this.GetAsset();
                var store = this.GetStore();

                await store.Dispatch(GenerationSettingsActions.openGenerationDataWindow, new GenerationDataWindowArgs(asset, this, textureResult));
            }, showGenerationDataStatus);
        }

        async void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (textureResult is TextureSkeleton)
                return;

            if (textureResult == null)
            {
                image.style.backgroundImage = null;
                return;
            }

            var width = resolvedStyle.width;
            if (width is <= 0 or float.NaN)
                return;

            RenderTexture rt;

            var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1f;
            if (!textureResult.IsFailed())
            {
                rt = await TextureCache.GetPreview(textureResult.uri, Mathf.RoundToInt(width * screenScaleFactor));
                image.style.backgroundImage = Background.FromRenderTexture(rt);
                image.MarkDirtyRepaint();
            }
            else
            {
                rt = await TextureCache.GetPreview(new Uri(Path.GetFullPath(ImageFileUtilities.failedDownloadIcon), UriKind.Absolute), (int)(width * screenScaleFactor));
                image.style.backgroundImage = Background.FromRenderTexture(rt);
            }

            if (!rt)
                return;

            image.EnableInClassList("image-scale-to-fit", resolvedStyle.width <= rt.width || resolvedStyle.height <= rt.height);
            image.EnableInClassList("image-scale-initial", resolvedStyle.width > rt.width && resolvedStyle.height > rt.height);

            progress.EnableInClassList("image-scale-to-fit", resolvedStyle.width <= rt.width || resolvedStyle.height <= rt.height);
            progress.EnableInClassList("image-scale-initial", resolvedStyle.width > rt.width && resolvedStyle.height > rt.height);
        }

        public void OnScreenScaleFactorChanged(ScreenScaleFactor _) => OnGeometryChanged(null);

        public void SetGeneration(TextureResult result)
        {
            if (textureResult == result)
            {
                SetSelectedGeneration(this.GetState().SelectSelectedGeneration(this));
                return;
            }

            m_SpinnerManipulator.Stop();

            m_Label.SetShown(result is TextureSkeleton);

            image.style.backgroundImage = null;

            textureResult = result;

            m_DragManipulator.externalFilePath = textureResult?.uri.GetLocalPath();

            if (textureResult.IsFailed())
            {
                this.RemoveManipulator(m_ContextualMenu);
                this.RemoveManipulator(m_DragManipulator);
                this.AddManipulator(m_FailedContextualMenu);
            }
            else
            {
                this.RemoveManipulator(m_FailedContextualMenu);
                this.AddManipulator(m_ContextualMenu);
                this.AddManipulator(m_DragManipulator);
            }

            SetSelectedGeneration(this.GetState().SelectSelectedGeneration(this));

            if (textureResult is TextureSkeleton)
            {
                SetGenerationProgress(new [] { this.GetState().SelectGenerationProgress(this, textureResult) });
                return;
            }

            OnGeometryChanged(null);
        }

        async void UpdateTooltip()
        {
            tooltip = this.GetState()?.SelectTooltipModelSettings(null);
            if (textureResult is null or TextureSkeleton)
                return;

            m_Metadata = await textureResult.GetMetadata();
            tooltip = this.GetState()?.SelectTooltipModelSettings(m_Metadata);
        }
    }

    class GenerationTileItem : VisualElement
    {
        GenerationTile m_Tile;

        public GenerationTile tile
        {
            get => m_Tile;
            set
            {
                if (m_Tile != null)
                    Remove(m_Tile);
                if (value != null)
                    Add(value);
                m_Tile = value;
            }
        }
    }
}
