using System;
using System.Collections.Generic;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Undo;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Payloads;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Services.Stores.Actions.Payloads
{
    record QuoteAudioData(AssetReference asset, GenerationSetting generationSetting) : AsssetContext(asset);
    record GenerateAudioData(AssetReference asset, GenerationSetting generationSetting, int taskID) : AsssetContext(asset);
    record DownloadAudioData(
        AssetReference asset,
        List<Guid> jobIds,
        int progressTaskId,
        Guid uniqueTaskId,
        GenerationMetadata generationMetadata,
        int[] customSeeds,
        bool autoApply) : AsssetContext(asset);
    record GenerationValidationSettings(
        AssetReference asset,
        bool valid,
        bool prompt,
        bool negativePrompt,
        string model,
        int roundedFrameDuration,
        int variations,
        int referenceCount,
        long modelsSelectorTimeStampUtcTicks) : AsssetContext(asset);
    record GenerationDataWindowArgs(AssetReference asset, VisualElement element, AudioClipResult result) : AsssetContext(asset);
    record GenerationAudioClips(AssetReference asset, List<AudioClipResult> audioClips) : AsssetContext(asset);
    record GenerationSkeletons(AssetReference asset, List<TextureSkeleton> skeletons) : AsssetContext(asset);
    record RemoveGenerationSkeletonsData(AssetReference asset, int taskID) : AsssetContext(asset);
    record SelectGenerationData(AssetReference asset, AudioClipResult result, bool replaceAsset, bool askForConfirmation) : AsssetContext(asset);
    record SelectedGenerationData(AssetReference asset, AudioClipResult result) : AsssetContext(asset);
    record AssetUndoData(AssetReference asset, AssetUndoManager undoManager) : AsssetContext(asset);
    record ReplaceWithoutConfirmationData(AssetReference asset, bool withoutConfirmation) : AsssetContext(asset);
}
