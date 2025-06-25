using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;
using UnityEngine.UIElements;
using AssetReferenceExtensions = Unity.AI.Material.Services.Utilities.AssetReferenceExtensions;

namespace Unity.AI.Material.Services.Stores.Selectors
{
    static partial class Selectors
    {
        public static GenerationSettings SelectGenerationSettings(this IState state) => state.Get<GenerationSettings>(GenerationSettingsActions.slice);

        public static GenerationSetting SelectGenerationSetting(this IState state, AssetReference asset)
        {
            if (state == null)
                return new GenerationSetting();
            var settings = state.SelectGenerationSettings().generationSettings;
            return settings.Ensure(asset);
        }

        public static GenerationSetting SelectGenerationSetting(this IState state, VisualElement element) => state.SelectGenerationSetting(element.GetAsset());

        public static string SelectSelectedModelID(this IState state, VisualElement element) => state.SelectSelectedModelID(element.GetAsset());
        public static string SelectSelectedModelID(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            return state.SelectGenerationSetting(asset).selectedModels.Ensure(mode).modelID;
        }
        public static string SelectSelectedModelID(this GenerationSetting setting)
        {
            var mode = setting.SelectRefinementMode();
            return setting.selectedModels.Ensure(mode).modelID;
        }

        public static string SelectSelectedModelName(this GenerationSetting setting)
        {
            // The model settings are shared between all generation settings. We can use the modelID to find the model.
            // Normally we try to use the store from the window context, but here we have a design flaw and will
            // use the shared store instead of modifying the setting argument which could be risky for serialization and dictionary lookups.
            // Suggestion: we could add an overload to MakeMetadata that takes the store as an argument and passes it here
            var store = SessionPersistence.SharedStore.Store;
            if (store?.State == null)
                return null;

            var modelID = setting.SelectSelectedModelID();
            var modelSettings = store.State.SelectModelSettingsWithModelId(modelID);

            return modelSettings?.name;
        }

        public static List<OperationSubTypeEnum> SelectRefinementOperations(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            var operations = mode switch
            {
                RefinementMode.Generation => new[] { OperationSubTypeEnum.TextPrompt },
                RefinementMode.Upscale => new[] { OperationSubTypeEnum.Upscale },
                RefinementMode.Pbr => new[] { OperationSubTypeEnum.Pbr },
                _ => new[] { OperationSubTypeEnum.TextPrompt }
            };
            return operations.ToList();
        }
        public static List<OperationSubTypeEnum> SelectRefinementOperations(this IState state, VisualElement element) => state.SelectRefinementOperations(element.GetAsset());

        public static (RefinementMode mode, bool should, long timestamp) SelectShouldAutoAssignModel(this IState state, VisualElement element)
        {
            var modalities = GenerationSettingsActions.spriteModelsAsMaterialModelsEnabled
                ? new [] { ModalityEnum.Texture2d, ModalityEnum.Image }
                : new [] { ModalityEnum.Texture2d };
            var mode = state.SelectRefinementMode(element);
            return (mode, ModelSelectorSelectors.SelectShouldAutoAssignModel(state, modalities, state.SelectRefinementOperations(element).ToArray()),
                timestamp: ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state));
        }

        public static ModelSettings SelectAutoAssignModel(this IState state, VisualElement element)
        {
            var modalities = GenerationSettingsActions.spriteModelsAsMaterialModelsEnabled
                ? new [] { ModalityEnum.Texture2d, ModalityEnum.Image }
                : new [] { ModalityEnum.Texture2d };
            return ModelSelectorSelectors.SelectAutoAssignModel(state, modalities, state.SelectRefinementOperations(element).ToArray());
        }

        public static GenerationSetting EnsureSelectedModelID(this GenerationSetting setting, IState state)
        {
            foreach (RefinementMode mode in Enum.GetValues(typeof(RefinementMode)))
            {
                var selection = setting.selectedModels.Ensure(mode);
                selection.modelID = !string.IsNullOrEmpty(selection.modelID) ? selection.modelID : state.SelectSession().settings.lastSelectedModels.Ensure(mode).modelID;
            }
            return setting;
        }

        public static ModelSettings SelectModelSettings(this IState state, GenerationMetadata generationMetadata)
        {
            var models = state.SelectModelSettings().ToList();
            var metadataModel = models.Find(x => x.id == generationMetadata.model);
            return metadataModel;
        }

        public static string SelectTooltipModelSettings(this IState state, GenerationMetadata generationMetadata)
        {
            const string noDataFoundString = "No generation data found";

            if (generationMetadata == null)
                return noDataFoundString;

            var text = string.Empty;

            if (!string.IsNullOrEmpty(generationMetadata.prompt))
                text += $"Prompt: {generationMetadata.prompt}\n";

            if (!string.IsNullOrEmpty(generationMetadata.negativePrompt))
                text += $"Negative prompt: {generationMetadata.negativePrompt}\n";

            if (!string.IsNullOrEmpty(generationMetadata.refinementMode))
                text += $"Operation: {generationMetadata.refinementMode.AddSpaceBeforeCapitalLetters()}\n";

            var modelSettings = state.SelectModelSettings(generationMetadata);
            if (!string.IsNullOrEmpty(modelSettings?.name))
            {
                text += $"Model: {modelSettings.name}\n";
            }
            else if(!string.IsNullOrEmpty(generationMetadata.modelName))
            {
                text += $"Model: {generationMetadata.modelName}\n";
            }

            text = text.TrimEnd();

            if(string.IsNullOrEmpty(text))
                text = noDataFoundString;

            return text;
        }

        public static string SelectPrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectPrompt();
        public static string SelectPrompt(this GenerationSetting setting) => PromptUtilities.TruncatePrompt(setting.prompt);
        public static string SelectNegativePrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectNegativePrompt();
        public static string SelectNegativePrompt(this GenerationSetting setting) => PromptUtilities.TruncatePrompt(setting.negativePrompt);
        public static int SelectVariationCount(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectVariationCount();
        public static int SelectVariationCount(this GenerationSetting setting) => setting.promptImageReference.asset.IsValid() ? 1 : setting.variationCount;

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useCustomSeed, settings.customSeed);
        }

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this GenerationSetting setting) => (setting.useCustomSeed, setting.customSeed);

        public static RefinementMode SelectRefinementMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).refinementMode;
        public static RefinementMode SelectRefinementMode(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).refinementMode;
        public static RefinementMode SelectRefinementMode(this GenerationSetting setting) => setting.refinementMode;

        public static PromptImageReference SelectPromptImageReference(this GenerationSetting setting) => setting.promptImageReference;

        public static AssetReference SelectPromptImageReferenceAsset(this IState state, VisualElement element) => state.SelectGenerationSetting(element).promptImageReference.asset;
        public static AssetReference SelectPromptImageReferenceAsset(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).promptImageReference.asset;

        public static PatternImageReference SelectPatternImageReference(this GenerationSetting setting) => setting.patternImageReference;

        public static AssetReference SelectPatternImageReferenceAsset(this IState state, VisualElement element) => state.SelectGenerationSetting(element).patternImageReference.asset;
        public static AssetReference SelectPatternImageReferenceAsset(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).patternImageReference.asset;

        public static float SelectPatternImageReferenceStrength(this IState state, VisualElement element) => state.SelectGenerationSetting(element).patternImageReference.strength;

        public static IEnumerable<string> SelectSettingsResolutions(this IState state, VisualElement element) => new ImmutableStringList(new []{"512 x 512", "1024 x 1024", "2048 x 2048"});

        public static string SelectImageDimensions(this IState state, VisualElement element)
        {
            var dimension = state.SelectGenerationSetting(element).imageDimensions;
            var resolutions = state.SelectSettingsResolutions(element)?.ToList();
            if (resolutions == null || resolutions.Count == 0)
                return "512 x 512";

            return resolutions.Contains(dimension) ? dimension : resolutions[0];
        }

        public static Vector2Int SelectImageDimensionsVector2(this GenerationSetting setting)
        {
            var dimensionsSplit = setting.imageDimensions.Split(" x ");

            int.TryParse(dimensionsSplit[0], out var width);
            int.TryParse(dimensionsSplit[1], out var height);

            if (width == 0 || height == 0)
            {
                width = 512;
                height = 512;
            }

            var dimensions = new Vector2Int(width, height);
            return dimensions;
        }

        public static Texture2D SelectBaseImageReferenceBackground(this IState state, VisualElement element)
        {
            var currentSelection = state.SelectSelectedGeneration(element);
            var generations = state.SelectGeneratedMaterials(element);
            if (currentSelection.IsValid() && generations.Contains(currentSelection))
            {
                if (currentSelection.IsMat())
                {
                    var mappings = state.SelectGeneratedMaterialMapping(element);
                    var mapping = mappings[MapType.Delighted];
                    var material = currentSelection.GetTemporary(state);
                    if (material.HasTexture(mapping))
                        return material.GetTexture(mapping) as Texture2D;
                    return null;
                }

                return currentSelection.GetPreview(MapType.Preview).GetTextureUnsafe();
            }

            {
                var mappings = state.SelectGeneratedMaterialMapping(element);
                var mapping = mappings[MapType.Delighted];
                var asset = element.GetAsset();
                if (!asset.Exists())
                    return null;
                var material = asset.GetMaterialAdapter();
                if (material.IsValid && material.HasTexture(mapping))
                    return material.GetTexture(mapping) as Texture2D;
                return null;
            }
        }

        public static Texture2D SelectPromptImageReferenceBackground(this IState state, VisualElement element)
        {
            var promptImageReferenceAsset = state.SelectPromptImageReferenceAsset(element);
            if (promptImageReferenceAsset.IsValid())
                return null; // already shown on top layer

            return SelectBaseImageReferenceBackground(state, element);
        }

        public static async Task<Stream> SelectReferenceAssetStreamWithFallback(this IState state, AssetReference asset)
        {
            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedMaterials(asset);
            var mappings = state.SelectGeneratedMaterialMapping(asset);

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            // Fallback on asset
            Stream candidateStream;
            if (currentSelection.IsValid())
                candidateStream = FileIO.OpenReadAsync(MaterialResultExtensions.GetPreview(currentSelection).uri.GetLocalPath());
            else
            {
                var referenceImage = asset.GetMaterialAdapter().GetTexture(mappings[MapType.Delighted]);
                candidateStream = await AssetReferenceExtensions.FromObject(referenceImage).GetCompatibleImageStreamAsync();
            }

            if (!ImageResizeUtilities.NeedsResize(candidateStream))
                return candidateStream;

            var resized = ImageResizeUtilities.ResizeForPbr(candidateStream);
            if (resized != candidateStream)
                await candidateStream.DisposeAsync();

            return resized;
        }

        public static async Task<Stream> SelectPatternImageReferenceAssetStream(this IState state, AssetReference asset)
        {
            var patternImageReferenceAsset = state.SelectPatternImageReferenceAsset(asset);
            if (!patternImageReferenceAsset.IsValid())
                return null;

            var candidateStream = await patternImageReferenceAsset.GetCompatibleImageStreamAsync();
            if (!ImageResizeUtilities.NeedsResize(candidateStream, true))
                return candidateStream;

            var resized = ImageResizeUtilities.ResizeForPbr(candidateStream, true);
            if (resized != candidateStream)
                await candidateStream.DisposeAsync();

            return resized;
        }

        public static async Task<Stream> SelectPromptImageReferenceAssetStream(this IState state, AssetReference asset)
        {
            var promptImageReferenceAsset = state.SelectPromptImageReferenceAsset(asset);
            if (!promptImageReferenceAsset.IsValid())
                return null;

            var candidateStream = await promptImageReferenceAsset.GetCompatibleImageStreamAsync();
            if (!ImageResizeUtilities.NeedsResize(candidateStream))
                return candidateStream;

            var resized = ImageResizeUtilities.ResizeForPbr(candidateStream);
            if (resized != candidateStream)
                await candidateStream.DisposeAsync();

            return resized;
        }

        public static async Task<Stream> SelectPromptAssetBytesWithFallback(this IState state, AssetReference asset) =>
            state.SelectPromptImageReferenceAsset(asset).IsValid()
                ? await state.SelectPromptImageReferenceAssetStream(asset)
                : await state.SelectReferenceAssetStreamWithFallback(asset);

        public static bool SelectAssetExists(this IState state, AssetReference asset) => asset.Exists();

        public static bool SelectAssetExists(this IState state, VisualElement element)
        {
            var asset = element.GetAsset();
            return state.SelectAssetExists(asset);
        }

        public static int SelectActiveReferencesCount(this IState state, VisualElement element)
        {
            var count = 0;
            var generationSetting = state.SelectGenerationSetting(element);
            var patternReference = generationSetting.SelectPatternImageReference();
            var promptImageReference = generationSetting.SelectPromptImageReference();
            if (patternReference.asset.IsValid())
                count++;
            if (promptImageReference.asset.IsValid())
                count++;

            return count;
        }

        public static GenerationValidationSettings SelectGenerationValidationSettings(this IState state, VisualElement element)
        {
            var asset = element.GetAsset();
            var settings = state.SelectGenerationSetting(asset);
            var prompt = string.IsNullOrWhiteSpace(settings.SelectPrompt());
            var negativePrompt = string.IsNullOrWhiteSpace(settings.SelectNegativePrompt());
            var model = state.SelectSelectedModelID(asset);
            var variations = settings.SelectVariationCount();
            var mode = settings.SelectRefinementMode();
            var referenceCount = state.SelectActiveReferencesCount(element);
            var modelsTimeStamp = ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state);
            return new GenerationValidationSettings(asset, asset.Exists(), prompt, negativePrompt, model, variations, mode, referenceCount, modelsTimeStamp);
        }

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;
        public static float SelectGenerationPaneWidth(this IState state, VisualElement element) => state.SelectGenerationSetting(element).generationPaneWidth;
    }
}
