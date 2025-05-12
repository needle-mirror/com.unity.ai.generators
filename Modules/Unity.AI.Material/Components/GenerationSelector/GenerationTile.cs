using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class GenerationTile : VisualElement
    {
        public VisualElement image { get; }
        public VisualElement progress { get; }

        public MaterialResult materialResult { get; private set; }

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/GenerationSelector/GenerationTile.uxml";

        readonly DragExternalFileManipulator m_DragManipulator;
        readonly SpinnerManipulator m_SpinnerManipulator;
        readonly ContextualMenuManipulator m_ContextualMenu;
        readonly ContextualMenuManipulator m_FailedContextualMenu;

        bool m_IsHovered;
        RenderTexture m_SkeletonTexture;
        readonly Label m_Label;
        readonly Label m_Type;
        GenerationMetadata m_Metadata;
        bool m_InvalidateCache;

        public GenerationTile()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Type = this.Q<Label>("type");
            m_Label = this.Q<Label>("label");
            image = this.Q<VisualElement>("border");
            RegisterCallback<AttachToPanelEvent>(_ => {
                if (m_SkeletonTexture)
                    progress.style.backgroundImage = Background.FromRenderTexture(m_SkeletonTexture); });
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<MouseOverEvent>(_ => UpdateTooltip());

            m_ContextualMenu = new ContextualMenuManipulator(BuildContextualMenu);
            m_FailedContextualMenu = new ContextualMenuManipulator(BuildFailedContextualMenu);
            m_DragManipulator = new DragExternalFileManipulator
            {
                newFileName = AssetUtils.defaultNewAssetName + Path.GetExtension(this.GetAsset().GetPath()), // we need to force the extension because our artifacts are lists of images
                copyFunction = data => SessionActions.promoteGenerationUnsafe((new DragAndDropGenerationData(this.GetAsset(),
                    MaterialResult.FromPath(data.sourcePath), data.destinationPath), this.GetStoreApi())).GetPath(),
                moveDependencies = data => SessionActions.moveAssetDependencies((new DragAndDropFinalizeData(this.GetAsset(),
                    data.sourcePath, data.destinationPath), this.GetStoreApi())),
                compareFunction = data => {
                    var result = MaterialResult.FromPath(data.sourcePath);
                    var asset = this.GetAsset();
                    var store = this.GetStoreApi();
                    var materialMapping = store.State.SelectGeneratedMaterialMapping(asset);
                    var cachedAsset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(data.cachedAssetPath) };
                    return FileIO.AreFilesIdentical(data.cachedAssetPath, data.sourcePath) ||
                        result.AreMapsIdentical(cachedAsset, materialMapping);
                }
            };

            progress = this.Q<VisualElement>("progress");
            progress.AddManipulator(m_SpinnerManipulator = new SpinnerManipulator());
        }

        public void SetGenerationProgress(IEnumerable<GenerationProgressData> data)
        {
            if (materialResult is not MaterialSkeleton)
                return;

            var progressReport = data.FirstOrDefault(d => d.taskID == ((MaterialSkeleton)materialResult).taskID);
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

        public void SetSelectedGeneration(MaterialResult result) => this.SetSelected(materialResult != null && result != null && result.IsValid() && materialResult.uri == result.uri);

        void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Clear();
            if (materialResult is MaterialSkeleton)
                return;

            evt.menu.AppendAction("Select", async _ =>
            {
                if (this.GetAsset() == null || materialResult == null)
                    return;
                var asset = this.GetAsset();
                var store = this.GetStoreApi(); // fixme: this is weird, otherwise promoteFocusedGeneration doesn't work
                await store.Dispatch(GenerationResultsActions.selectGeneration, new(asset, materialResult, true, true));
            }, DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction("Refresh", _ =>
            {
                m_InvalidateCache = true;
                OnGeometryChanged(null);
            }, DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction("Promote to new asset", _ =>
            {
                var asset = this.GetAsset();
                var store = this.GetStoreApi(); // fixme: this is weird, otherwise promoteFocusedGeneration doesn't work
                store.Dispatch(SessionActions.promoteGeneration, new (asset, materialResult));
            }, DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Show in Explorer" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Reveal in Finder" : "Show in File Manager", _ =>
            {
                EditorUtility.RevealInFinder(File.Exists(materialResult.uri.GetLocalPath())
                    ? materialResult.uri.GetLocalPath()
                    : Path.GetDirectoryName(materialResult.uri.GetLocalPath()));
            }, DropdownMenuAction.AlwaysEnabled);

            var showGenerationDataStatus = DropdownMenuAction.Status.Normal;
            if (this.GetAsset() == null || materialResult == null || !File.Exists(materialResult.uri.GetLocalPath()))
            {
                showGenerationDataStatus = DropdownMenuAction.Status.Disabled;
            }
            evt.menu.AppendAction("Show Generation Data", async _ =>
            {
                if (this.GetAsset() == null || materialResult == null)
                    return;
                var asset = this.GetAsset();
                var store = this.GetStore();
                await store.Dispatch(GenerationSettingsActions.openGenerationDataWindow, new GenerationDataWindowArgs(asset, this, materialResult));
            }, showGenerationDataStatus);
        }

        void BuildFailedContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Clear();
            if (materialResult is MaterialSkeleton)
                return;

            evt.menu.AppendAction(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Show in Explorer" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Reveal in Finder" : "Show in File Manager", _ =>
                {
                    EditorUtility.RevealInFinder(File.Exists(materialResult.uri.GetLocalPath())
                        ? materialResult.uri.GetLocalPath()
                        : Path.GetDirectoryName(materialResult.uri.GetLocalPath()));
                }, DropdownMenuAction.AlwaysEnabled);

            var showGenerationDataStatus = DropdownMenuAction.Status.Normal;
            if (this.GetAsset() == null || materialResult == null || !File.Exists(materialResult.uri.GetLocalPath()))
            {
                showGenerationDataStatus = DropdownMenuAction.Status.Disabled;
            }
            evt.menu.AppendAction("Show Generation Data", async _ =>
            {
                if (this.GetAsset() == null || materialResult == null)
                    return;

                var asset = this.GetAsset();
                var store = this.GetStore();

                await store.Dispatch(GenerationSettingsActions.openGenerationDataWindow, new GenerationDataWindowArgs(asset, this, materialResult));
            }, showGenerationDataStatus);
        }

        async void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (materialResult is MaterialSkeleton)
                return;

            if (materialResult == null)
            {
                image.style.backgroundImage = null;
                return;
            }

            var width = resolvedStyle.width;
            var height = resolvedStyle.height;

            if (width is <= 0 or float.NaN)
                return;

            var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1.0f;

            RenderTexture rt;

            if (!materialResult.IsFailed())
            {
                rt = materialResult.GetPreview(null, (int)width, (int)height, screenScaleFactor, m_InvalidateCache);
                image.style.backgroundImage = Background.FromRenderTexture(rt);
                image.MarkDirtyRepaint();
            }
            else
            {
                rt = await TextureCache.GetPreview(new Uri(Path.GetFullPath(ImageFileUtilities.failedDownloadIcon), UriKind.Absolute), (int)(width * screenScaleFactor));
                image.style.backgroundImage = Background.FromRenderTexture(rt);
            }

            m_InvalidateCache = false;

            if (!rt)
                return;

            image.EnableInClassList("image-scale-to-fit", resolvedStyle.width <= rt.width || resolvedStyle.height <= rt.height);
            image.EnableInClassList("image-scale-initial", resolvedStyle.width > rt.width && resolvedStyle.height > rt.height);

            progress.EnableInClassList("image-scale-to-fit", resolvedStyle.width <= rt.width || resolvedStyle.height <= rt.height);
            progress.EnableInClassList("image-scale-initial", resolvedStyle.width > rt.width && resolvedStyle.height > rt.height);
        }

        public void OnScreenScaleFactorChanged(ScreenScaleFactor _) => OnGeometryChanged(null);

        public void SetGeneration(MaterialResult result)
        {
            if (materialResult == result)
            {
                SetSelectedGeneration(this.GetState().SelectSelectedGeneration(this));
                return;
            }

            m_SpinnerManipulator.Stop();

            m_Label.SetShown(result is MaterialSkeleton);
            m_Type.SetShown(result.IsPbr() || result.IsMat());
            m_Type.text = result.IsPbr() ? "PBR" : result.IsMat() ? "MAT" : "";

            image.style.backgroundImage = null;

            materialResult = result;

            m_DragManipulator.externalFilePath = materialResult?.uri.GetLocalPath();
            m_DragManipulator.newFileName = Path.GetFileNameWithoutExtension(this.GetAsset().GetPath()) + Path.GetExtension(this.GetAsset().GetPath()); // we need to force the extension because our artifacts are lists of images

            if (materialResult.IsFailed())
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

            if (result is MaterialSkeleton)
            {
                SetGenerationProgress(new [] { this.GetState().SelectGenerationProgress(this, result) });
                return;
            }

            OnGeometryChanged(null);
        }

        async void UpdateTooltip()
        {
            tooltip = this.GetState()?.SelectTooltipModelSettings(null);
            if (materialResult is null or MaterialSkeleton)
                return;

            m_Metadata = await materialResult.GetMetadata();
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
