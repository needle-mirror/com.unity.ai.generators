using System;
using System.Collections.Generic;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Undo;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Payloads;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Services.Stores.Actions.Payloads
{
    record QuoteAnimationsData(AssetReference asset, GenerationSetting generationSetting) : AsssetContext(asset);
    record GenerateAnimationsData(AssetReference asset, GenerationSetting generationSetting, int taskID) : AsssetContext(asset);
    record DownloadAnimationsData(AssetReference asset, List<Guid> ids, int[] customSeeds, int taskID = 0, GenerationMetadata generationMetadata = null, bool autoApply = false) : AsssetContext(asset);
    record GenerationValidationSettings(AssetReference asset, bool valid, bool prompt, string model, int roundedFrameDuration, int variations, RefinementMode mode, int referenceCount) : AsssetContext(asset);
    record GenerationDataWindowArgs(AssetReference asset, VisualElement element, AnimationClipResult result) : AsssetContext(asset);
    record GenerationAnimations(AssetReference asset, List<AnimationClipResult> animations) : AsssetContext(asset);
    record GenerationSkeletons(AssetReference asset, List<TextureSkeleton> skeletons) : AsssetContext(asset);
    record RemoveGenerationSkeletonsData(AssetReference asset, int taskID) : AsssetContext(asset);
    record SelectGenerationData(AssetReference asset, AnimationClipResult result, bool replaceAsset, bool askForConfirmation) : AsssetContext(asset);
    record PromotedGenerationData(AssetReference asset, AnimationClipResult result) : AsssetContext(asset);
    record DragAndDropGenerationData(AssetReference asset, AnimationClipResult result, string newAssetPath) : PromotedGenerationData(asset, result);
    record AssetUndoData(AssetReference asset, AssetUndoManager undoManager) : AsssetContext(asset);
    record ReplaceWithoutConfirmationData(AssetReference asset, bool withoutConfirmation) : AsssetContext(asset);
}
