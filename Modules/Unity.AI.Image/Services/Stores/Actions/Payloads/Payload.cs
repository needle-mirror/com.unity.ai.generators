using System;
using System.Collections.Generic;
using System.Threading;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Undo;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Generators.Asset;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Services.Stores.Actions.Payloads
{
    record AsssetContext(AssetReference asset);
    record QuoteImagesData(AssetReference asset, GenerationSetting generationSetting, CancellationTokenSource cancellationTokenSource) : AsssetContext(asset);
    record GenerateImagesData(AssetReference asset, GenerationSetting generationSetting, int taskID) : AsssetContext(asset);
    record DownloadImagesData(
        AssetReference asset,
        List<Guid> ids,
        int taskID,
        GenerationMetadata generationMetadata,
        bool isRefinement,
        bool replaceBlankAsset,
        bool replaceRefinementAsset,
        int[] customSeeds) : AsssetContext(asset);
    record GenerationProgressData(int taskID, int count, float progress);
    record GenerationsProgressData(AssetReference asset, GenerationProgressData progress) : AsssetContext(asset);
    record GenerationFeedbackData(string message);
    record GenerationsFeedbackData(AssetReference asset, GenerationFeedbackData feedback) : AsssetContext(asset);
    record GenerationValidationSettings(AssetReference asset, bool valid, bool prompt, bool negativePrompt, string model, int variations, RefinementMode mode, int activeReferencesBitmask, int validReferencesBitmask) : AsssetContext(asset);
    record GenerationValidationResult(bool success, AiResultErrorEnum error, int cost, List<GenerationFeedbackData> feedback);
    record GenerationsValidationResult(AssetReference asset, GenerationValidationResult result) : AsssetContext(asset);
    record GenerationResultData(AssetReference asset, GenerationResult result) : AsssetContext(asset);
    record GenerationAllowedData(AssetReference asset, bool allowed) : AsssetContext(asset);
    record GeneratedResultVisibleData(AssetReference asset, string elementID, int count) : AsssetContext(asset);
    record GenerationTextures(AssetReference asset, List<TextureResult> textures) : AsssetContext(asset);
    record GenerationSkeletons(AssetReference asset, List<TextureSkeleton> skeletons) : AsssetContext(asset);
    record RemoveGenerationSkeletonsData(AssetReference asset, int taskID) : AsssetContext(asset);
    record SelectGenerationData(AssetReference asset, TextureResult result, bool replaceAsset, bool askForConfirmation) : AsssetContext(asset);
    record GenerationDataWindowArgs(AssetReference asset, VisualElement element, TextureResult result) : AsssetContext(asset);
    record DoodleWindowArgs(AssetReference asset, ImageReferenceType imageReferenceType, byte[] data, Vector2Int size, bool showBaseImage) : AsssetContext(asset);
    record SelectedGenerationData(AssetReference asset, TextureResult result) : AsssetContext(asset);
    record AssetUndoData(AssetReference asset, AssetUndoManager undoManager) : AsssetContext(asset);
    record ReplaceWithoutConfirmationData(AssetReference asset, bool withoutConfirmation) : AsssetContext(asset);
    record PromoteNewAssetPostActionData(AssetReference asset, Action<AssetReference> postPromoteAction) : AsssetContext(asset);
    record AddImageReferenceTypeData(AssetReference asset, ImageReferenceType[] types) : AsssetContext(asset);
    record ImageReferenceTypeData(ImageReferenceType type);
    record ImageReferenceAssetData(ImageReferenceType type, AssetReference reference) : ImageReferenceTypeData(type);
    record ImageReferenceDoodleData(ImageReferenceType type, byte[] doodle) : ImageReferenceTypeData(type);
    record ImageReferenceModeData(ImageReferenceType type, ImageReferenceMode mode) : ImageReferenceTypeData(type);
    record ImageReferenceStrengthData(ImageReferenceType type, float strength) : ImageReferenceTypeData(type);
    record ImageReferenceActiveData(ImageReferenceType type, bool active) : ImageReferenceTypeData(type);
    record ImageReferenceSettingsData(ImageReferenceType type, ImageReferenceSettings settings) : ImageReferenceTypeData(type);
    record UnsavedAssetBytesData(AssetReference asset, byte[] data, TextureResult result = null) : AsssetContext(asset);
}
