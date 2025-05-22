using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Services.Stores.Selectors
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

        public static List<OperationSubTypeEnum> SelectRefinementOperations(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            var operations = mode switch
            {
                RefinementMode.TextToMotion => new[] { OperationSubTypeEnum.TextPrompt },
                RefinementMode.VideoToMotion => new[] { OperationSubTypeEnum.ReferencePrompt },
                _ => new[] { OperationSubTypeEnum.TextPrompt }
            };
            return operations.ToList();
        }
        public static List<OperationSubTypeEnum> SelectRefinementOperations(this IState state, VisualElement element) => state.SelectRefinementOperations(element.GetAsset());

        public static (RefinementMode mode, bool should, long timestamp) SelectShouldAutoAssignModel(this IState state, VisualElement element)
        {
            var mode = state.SelectRefinementMode(element);
            return (mode, ModelSelectorSelectors.SelectShouldAutoAssignModel(state, new[] { ModalityEnum.Animate },
                state.SelectRefinementOperations(element).ToArray()), timestamp: ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state));
        }

        public static ModelSettings SelectAutoAssignModel(this IState state, VisualElement element) =>
            ModelSelectorSelectors.SelectAutoAssignModel(state, new[] { ModalityEnum.Animate },
                state.SelectRefinementOperations(element).ToArray());

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

            if (!string.IsNullOrEmpty(generationMetadata.model))
            {
                var modelSettings = state.SelectModelSettings(generationMetadata);
                if (!string.IsNullOrEmpty(modelSettings?.name))
                {
                    text += $"Model: {modelSettings.name}\n";
                }
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
        public static int SelectVariationCount(this GenerationSetting setting) => setting.variationCount;

        public static float SelectDuration(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectDuration();
        public static float SelectDuration(this GenerationSetting setting) => setting.duration;
        public static int SelectRoundedFrameDuration(this IState state, AssetReference asset)
        {
            var settings = state.SelectGenerationSetting(asset);
            return settings.SelectRoundedFrameDuration();
        }
        public static int SelectRoundedFrameDuration(this GenerationSetting setting) => Mathf.RoundToInt(setting.SelectDuration() * 30 / 8) * 8;

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useCustomSeed, settings.customSeed);
        }

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this GenerationSetting setting) => (setting.useCustomSeed, setting.customSeed);

        public static RefinementMode SelectRefinementMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).refinementMode;
        public static RefinementMode SelectRefinementMode(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).refinementMode;
        public static RefinementMode SelectRefinementMode(this GenerationSetting setting) => setting.refinementMode;

        public static VideoInputReference SelectVideoReference(this GenerationSetting setting) => setting.videoReference;

        public static AssetReference SelectVideoReferenceAsset(this IState state, VisualElement element) => state.SelectGenerationSetting(element).videoReference.asset;

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
            var videoReference = generationSetting.SelectVideoReference();
            if (videoReference.asset.IsValid())
                count++;

            return count;
        }

        public static GenerationValidationSettings SelectGenerationValidationSettings(this IState state, VisualElement element)
        {
            var asset = element.GetAsset();
            var settings = state.SelectGenerationSetting(asset);
            var prompt = string.IsNullOrWhiteSpace(settings.SelectPrompt());
            var model = state.SelectSelectedModelID(asset);
            var duration = settings.SelectRoundedFrameDuration();
            var variations = settings.SelectVariationCount();
            var mode = settings.SelectRefinementMode();
            var referenceCount = state.SelectActiveReferencesCount(element);
            return new GenerationValidationSettings(asset, asset.Exists(), prompt, model, duration, variations, mode, referenceCount);
        }

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;
        public static float SelectGenerationPaneWidth(this IState state, VisualElement element) => state.SelectGenerationSetting(element).generationPaneWidth;
    }
}
