using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Undo;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Asset;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Services.Stores.Actions.Payloads
{
    record AsssetContext(AssetReference asset);
    record QuoteMaterialsData(AssetReference asset, GenerationSetting generationSetting) : AsssetContext(asset);
    record GenerateMaterialsData(AssetReference asset, GenerationSetting generationSetting, int taskID) : AsssetContext(asset);
    record DownloadMaterialsData(AssetReference asset, List<Dictionary<MapType, Guid>> ids, int[] customSeeds, int taskID = 0, GenerationMetadata generationMetadata = null, bool autoApply = false) : AsssetContext(asset);
    record GenerationProgressData(int taskID, int count, float progress);
    record GenerationsProgressData(AssetReference asset, GenerationProgressData progress) : AsssetContext(asset);
    record GenerationFeedbackData(string message);
    record GenerationsFeedbackData(AssetReference asset, GenerationFeedbackData feedback) : AsssetContext(asset);
    record GenerationValidationSettings(AssetReference asset, bool prompt, bool negativePrompt, string model, int variations, RefinementMode mode, int referenceCount) : AsssetContext(asset);
    record GenerationValidationResult(bool success, AiResultErrorEnum error, int cost, List<GenerationFeedbackData> feedback);
    record GenerationsValidationResult(AssetReference asset, GenerationValidationResult result) : AsssetContext(asset);
    record GenerationResultData(AssetReference asset, GenerationResult result) : AsssetContext(asset);
    record GenerationDataWindowArgs(AssetReference asset, VisualElement element, MaterialResult result) : AsssetContext(asset);
    record GenerationAllowedData(AssetReference asset, bool allowed) : AsssetContext(asset);
    record GeneratedResultVisibleData(AssetReference asset, string elementID, int count) : AsssetContext(asset);
    record GenerationMaterials(AssetReference asset, List<MaterialResult> materials) : AsssetContext(asset);
    record GenerationSkeletons(AssetReference asset, List<MaterialSkeleton> skeletons) : AsssetContext(asset);
    record RemoveGenerationSkeletonsData(AssetReference asset, int taskID) : AsssetContext(asset);
    record SelectGenerationData(AssetReference asset, MaterialResult result, bool replaceAsset, bool askForConfirmation) : AsssetContext(asset);
    record PromotedGenerationData(AssetReference asset, MaterialResult result) : AsssetContext(asset);
    record DragAndDropGenerationData(AssetReference asset, MaterialResult result, string newAssetPath) : PromotedGenerationData(asset, result);
    record DragAndDropFinalizeData(AssetReference asset, string tempNewAssetPath, string newAssetPath) : AsssetContext(asset);
    record GenerationMaterialMappingData(AssetReference asset, MapType mapType, string materialProperty) : AsssetContext(asset);
    record AssetUndoData(AssetReference asset, AssetUndoManager undoManager) : AsssetContext(asset);
    record ReplaceWithoutConfirmationData(AssetReference asset, bool withoutConfirmation) : AsssetContext(asset);
}
