using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Services.Stores.Selectors
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

        public static string SelectSelectedModelID(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).selectedModelID;

        public static bool SelectShouldAutoAssignModel(this IState state, VisualElement element) =>
            ModelSelectorSelectors.SelectShouldAutoAssignModel(state, new[] { ModalityEnum.Sound }, null);
        public static ModelSettings SelectAutoAssignModel(this IState state, VisualElement element) =>
            ModelSelectorSelectors.SelectAutoAssignModel(state, new[] { ModalityEnum.Sound }, null);

        public static GenerationSetting EnsureSelectedModelID(this GenerationSetting setting, IState state)
        {
            setting.selectedModelID = !string.IsNullOrEmpty(setting.selectedModelID) ? setting.selectedModelID : state.SelectSession().settings.lastSelectedModelID;
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
        public static float SelectDuration(this GenerationSetting setting)
        {
            var duration = setting.duration;
            var clip = (AudioClip)SelectSoundReference(setting).asset.GetObject();
            if (clip)
                duration = clip.length;
            return duration;
        }
        public static int SelectRoundedFrameDuration(this IState state, AssetReference asset)
        {
            var settings = state.SelectGenerationSetting(asset);
            return settings.SelectRoundedFrameDuration();
        }
        public static int SelectRoundedFrameDuration(this GenerationSetting setting) => Mathf.RoundToInt(setting.SelectDuration() * 30 / 8) * 8;

        const float k_TrainingSetDuration = 10;
        public static float SelectTrainingSetDuration(this GenerationSetting _) => k_TrainingSetDuration;

        public static float SelectGenerableDuration(this GenerationSetting setting) => Mathf.Max(setting.SelectDuration(), setting.SelectTrainingSetDuration());

        public static bool SelectShouldAutoTrim(this GenerationSetting setting) => setting.SelectDuration() < setting.SelectGenerableDuration() - float.Epsilon;

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useCustomSeed, settings.customSeed);
        }

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this GenerationSetting setting) =>
            (setting.useCustomSeed, setting.customSeed);

        public static string SelectProgressLabel(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectProgressLabel();

        public static string SelectProgressLabel(this GenerationSetting _) => "Generating";

        public static SoundReferenceState SelectSoundReference(this GenerationSetting setting) => setting.soundReference;
        public static AssetReference SelectSoundReferenceAsset(this IState state, VisualElement element) => state.SelectGenerationSetting(element).soundReference.asset;
        public static byte[] SelectSoundReferenceRecording(this IState state, VisualElement element) => state.SelectGenerationSetting(element).soundReference.recording;
        public static float SelectSoundReferenceStrength(this IState state, VisualElement element) => state.SelectGenerationSetting(element).soundReference.strength;
        public static bool SelectSoundReferenceIsValid(this IState state, VisualElement element) => state.SelectSoundReferenceAsset(element).IsValid();
        public static bool SelectOverwriteSoundReferenceAsset(this IState state, VisualElement element) => state.SelectGenerationSetting(element).soundReference.overwriteSoundReferenceAsset;

        public static async Task<Stream> SelectReferenceAssetStream(this GenerationSetting setting)
        {
            var soundReference = setting.SelectSoundReference();
            if (!soundReference.asset.IsValid())
                return null;

            var referenceClip = (AudioClip)soundReference.asset.GetObject();

            // input sounds shorter than the training set duration are padded with silence, input sounds longer than the maximum duration are trimmed
            var referenceStream = new MemoryStream();
            await referenceClip.EncodeToWavUnclampedAsync(referenceStream, 0, referenceClip.GetNormalizedPositionAtTimeUnclamped(setting.SelectTrainingSetDuration()));
            referenceStream.Position = 0;

            return referenceStream;
        }
        public static Task<Stream> SelectReferenceAssetStream(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectReferenceAssetStream();

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
            var soundReference = generationSetting.SelectSoundReference();
            // fixme: if overwriteSoundReferenceAsset is false AND we have a recording I don't think this works, should work more like a doodle
            if (soundReference.asset.IsValid())
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
            var duration = settings.SelectRoundedFrameDuration();
            var variations = settings.SelectVariationCount();
            var referenceCount = state.SelectActiveReferencesCount(element);
            return new GenerationValidationSettings(asset, asset.Exists(), prompt, negativePrompt, model, duration, variations, referenceCount);
        }

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;
    }
}
