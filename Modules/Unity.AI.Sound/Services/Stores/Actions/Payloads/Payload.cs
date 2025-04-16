using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Undo;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Services.Stores.Actions.Payloads
{
    record AsssetContext(AssetReference asset);
    record QuoteAudioData(AssetReference asset, GenerationSetting generationSetting) : AsssetContext(asset);
    record GenerateAudioData(AssetReference asset, GenerationSetting generationSetting, int taskID) : AsssetContext(asset);
    record DownloadAudioData(AssetReference asset, List<Guid> ids, int taskID, GenerationMetadata generationMetadata, int[] customSeeds, bool autoApply = false) : AsssetContext(asset);
    record GenerationProgressData(int taskID, int count, float progress);
    record GenerationsProgressData(AssetReference asset, GenerationProgressData progress) : AsssetContext(asset);
    record GenerationFeedbackData(string message);
    record GenerationsFeedbackData(AssetReference asset, GenerationFeedbackData feedback) : AsssetContext(asset);
    record GenerationValidationSettings(AssetReference asset, bool valid, bool prompt, bool negativePrompt, string model, int roundedFrameDuration, int variations, int referenceCount) : AsssetContext(asset);
    record GenerationValidationResult(bool success, AiResultErrorEnum error, int cost, List<GenerationFeedbackData> feedback);
    record GenerationsValidationResult(AssetReference asset, GenerationValidationResult result) : AsssetContext(asset);
    record GenerationResultData(AssetReference asset, GenerationResult result) : AsssetContext(asset);
    record GenerationDataWindowArgs(AssetReference asset, VisualElement element, AudioClipResult result) : AsssetContext(asset);
    record GenerationAllowedData(AssetReference asset, bool allowed) : AsssetContext(asset);
    record GeneratedResultVisibleData(AssetReference asset, string elementID, int count) : AsssetContext(asset);
    record GenerationAudioClips(AssetReference asset, List<AudioClipResult> audioClips) : AsssetContext(asset);
    record GenerationSkeletons(AssetReference asset, List<TextureSkeleton> skeletons) : AsssetContext(asset);
    record RemoveGenerationSkeletonsData(AssetReference asset, int taskID) : AsssetContext(asset);
    record SelectGenerationData(AssetReference asset, AudioClipResult result, bool replaceAsset, bool askForConfirmation) : AsssetContext(asset);
    record SelectedGenerationData(AssetReference asset, AudioClipResult result) : AsssetContext(asset);
    record AssetUndoData(AssetReference asset, AssetUndoManager undoManager) : AsssetContext(asset);
    record ReplaceWithoutConfirmationData(AssetReference asset, bool withoutConfirmation) : AsssetContext(asset);
}
