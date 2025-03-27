using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Services.Stores.Selectors
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

        public static ModelSettings SelectSelectedModel(this IState state, VisualElement element) => state.SelectSelectedModel(element.GetAsset());
        public static ModelSettings SelectSelectedModel(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            var modelID = state.SelectGenerationSetting(asset).selectedModels.Ensure(mode).modelID;
            var model = ModelSelector.Services.Stores.Selectors.ModelSelectorSelectors.SelectModelSettings(state).FirstOrDefault(s => s.id == modelID);
            return model;
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
            var modelID = generationMetadata.model;
            var model = ModelSelector.Services.Stores.Selectors.ModelSelectorSelectors.SelectModelSettings(state).FirstOrDefault(s => s.id == modelID);
            return model;
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
            {
                text += $"Operation: {generationMetadata.refinementMode.AddSpaceBeforeCapitalLetters()}\n";
            }

            if (!string.IsNullOrEmpty(generationMetadata.model))
            {
                var modelSettings = state.SelectModelSettings(generationMetadata);
                if (!string.IsNullOrEmpty(modelSettings?.name))
                {
                    text += $"Model: {modelSettings.name}\n";
                }
            }

            text = text.TrimEnd();

            if (string.IsNullOrEmpty(text))
            {
                text = noDataFoundString;
            }

            return text;
        }

        public static string SelectPrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectPrompt();
        public static string SelectPrompt(this GenerationSetting setting) => PromptUtilities.TruncatePrompt(setting.prompt);

        public static string SelectNegativePrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectNegativePrompt();
        public static string SelectNegativePrompt(this GenerationSetting setting) => PromptUtilities.TruncatePrompt(setting.negativePrompt);

        public static int SelectVariationCount(this IState state, VisualElement element) => state.SelectGenerationSetting(element).variationCount;
        public static int SelectVariationCount(this GenerationSetting setting) => setting.variationCount;

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useCustomSeed, settings.customSeed);
        }

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this GenerationSetting setting) =>
            (setting.useCustomSeed, setting.customSeed);

        public static RefinementMode SelectRefinementMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).refinementMode;
        public static RefinementMode SelectRefinementMode(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).refinementMode;
        public static RefinementMode SelectRefinementMode(this GenerationSetting setting) => setting.refinementMode;

        public static int SelectUpscaleFactor(this IState state, VisualElement element) => state.SelectGenerationSetting(element).upscaleFactor;
        public static int SelectUpscaleFactor(this GenerationSetting setting) => setting.upscaleFactor;

        public static string SelectProgressLabel(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectProgressLabel();

        public static string SelectProgressLabel(this GenerationSetting setting) =>
            setting.refinementMode switch
            {
                RefinementMode.Generation => $"Generating with {setting.prompt}",
                RefinementMode.RemoveBackground => "Removing background",
                RefinementMode.Upscale => "Upscaling",
                RefinementMode.Pixelate => "Pixelating",
                RefinementMode.Recolor => "Recoloring",
                RefinementMode.Inpaint => $"Inpainting with {setting.prompt}",
                _ => "Failing"
            };

        public static Timestamp SelectPaletteImageBytesTimeStamp(this IState state, AssetReference asset)
        {
            // UriWithTimestamp is my poor-person's memoizer
            var setting = state.SelectGenerationSetting(asset);
            var paletteImageReference = setting.SelectImageReference(ImageReferenceType.PaletteImage);
            if (paletteImageReference.mode == ImageReferenceMode.Asset && paletteImageReference.asset.IsValid())
            {
                var path = Path.GetFullPath(paletteImageReference.asset.GetPath());
                return new Timestamp(File.GetLastWriteTime(path).ToUniversalTime().Ticks);
            }

            return Timestamp.FromUtcTicks(paletteImageReference.doodleTimestamp);
        }
        public static Timestamp SelectPaletteImageBytesTimeStamp(this IState state, VisualElement element) => state.SelectPaletteImageBytesTimeStamp(element.GetAsset());

        public static Stream SelectPaletteImageStream(this GenerationSetting setting)
        {
            var paletteImageReference = setting.SelectImageReference(ImageReferenceType.PaletteImage);
            return paletteImageReference.SelectImageReferenceStream();
        }
        public static byte[] SelectPaletteImageBytes(this IState state, VisualElement element)
        {
            var setting = state.SelectGenerationSetting(element);
            var paletteImageReference = setting.SelectImageReference(ImageReferenceType.PaletteImage);
            var bytes = paletteImageReference.mode == ImageReferenceMode.Asset && paletteImageReference.asset.IsValid()
                ? paletteImageReference.asset.GetFileSync() : paletteImageReference.doodle;
            return bytes;
        }

        public static byte[] SelectUnsavedAssetBytes(this GenerationSetting setting) => setting.unsavedAssetBytes.data;
        public static byte[] SelectUnsavedAssetBytes(this IState state, VisualElement element) => state.SelectGenerationSetting(element).unsavedAssetBytes.data;
        public static UnsavedAssetBytesSettings SelectUnsavedAssetBytesSettings(this IState state, VisualElement element) => state.SelectGenerationSetting(element).unsavedAssetBytes;

        public static Stream SelectUnsavedAssetStreamWithFallback(this IState state, VisualElement element) => state.SelectUnsavedAssetStreamWithFallback(element.GetAsset());

        public static Stream SelectUnsavedAssetStreamWithFallback(this IState state, AssetReference asset)
        {
            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            // use unsaved asset bytes if available
            if (unsavedAssetBytes is { Length: > 0 })
                return new MemoryStream(unsavedAssetBytes);

            // fallback to selection, or asset
            return currentSelection.IsValid() ? currentSelection.GetFileStream() : asset.GetFileStream();
        }

        public static Timestamp SelectBaseImageBytesTimestamp(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.unsavedAssetBytes;

            // use unsaved asset bytes if available
            if (unsavedAssetBytes.data is { Length: > 0 })
                return Timestamp.FromUtcTicks(setting.unsavedAssetBytes.timeStamp);

            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            // fallback to selection
            if (currentSelection.IsValid())
                return new Timestamp(File.GetLastWriteTime(currentSelection.uri.AbsolutePath).ToUniversalTime().Ticks);

            // fallback to asset
            var path = Path.GetFullPath(asset.GetPath());
            return new Timestamp(File.GetLastWriteTime(path).ToUniversalTime().Ticks);
        }
        public static Timestamp SelectBaseImageBytesTimestamp(this IState state, VisualElement element) => state.SelectBaseImageBytesTimestamp(element.GetAsset());

        public static ImageReferenceSettings SelectImageReference(this GenerationSetting setting, ImageReferenceType type) => setting.imageReferences[(int)type];
        public static AssetReference SelectImageReferenceAsset(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].asset;
        public static byte[] SelectImageReferenceDoodle(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].doodle;
        public static ImageReferenceMode SelectImageReferenceMode(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].mode;
        public static float SelectImageReferenceStrength(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].strength;
        public static bool SelectImageReferenceIsActive(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].isActive;
        public static bool SelectImageReferenceIsActive(this ImageReferenceSettings imageReference) => imageReference.isActive;
        public static bool SelectImageReferenceAllowed(this IState state, VisualElement element, ImageReferenceType type) => true;
        public static bool SelectImageReferenceIsClear(this IState state, VisualElement element, ImageReferenceType type) =>
            !state.SelectGenerationSetting(element).imageReferences[(int)type].asset.IsValid() && state.SelectImageReferenceDoodle(element, type) == null;
        public static bool SelectImageReferenceIsValid(this IState state, VisualElement element, ImageReferenceType type) =>
            state.SelectGenerationSetting(element).SelectImageReference(type).SelectImageReferenceIsValid();
        public static bool SelectImageReferenceIsValid(this ImageReferenceSettings imageReference) => imageReference.isActive &&
            (imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid() || imageReference.mode == ImageReferenceMode.Doodle);
        public static Stream SelectImageReferenceStream(this ImageReferenceSettings imageReference) =>
            imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid()
                ? imageReference.asset.GetFileStream()
                : new MemoryStream(imageReference.doodle);
        public static Dictionary<RefinementMode, Dictionary<ImageReferenceType, ImageReferenceSettings>> SelectImageReferencesByRefinement(this GenerationSetting setting)
        {
            var result = new Dictionary<RefinementMode, Dictionary<ImageReferenceType, ImageReferenceSettings>>();
            foreach (ImageReferenceType type in Enum.GetValues(typeof(ImageReferenceType)))
            {
                var imageReference = setting.SelectImageReference(type);
                var modes = type.GetRefinementModeForType();
                foreach (var mode in modes)
                {
                    if (!result.ContainsKey(mode))
                        result[mode] = new Dictionary<ImageReferenceType, ImageReferenceSettings>();
                    result[mode].Add(type, imageReference);
                }
            }
            return result;
        }

        public static int SelectPixelateTargetSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.targetSize;
        public static bool SelectPixelateKeepImageSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.keepImageSize;
        public static int SelectPixelatePixelBlockSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.pixelBlockSize;
        public static PixelateMode SelectPixelateMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.mode;

        public static int SelectPixelateOutlineThickness(this IState state, VisualElement element)
        {
            return state.SelectGenerationSetting(element).SelectPixelateOutlineThickness();
        }

        public static int SelectPixelateOutlineThickness(this GenerationSetting setting)
        {
            var pixelBlockSize = setting.pixelateSettings.pixelBlockSize;
            if (pixelBlockSize < PixelateSettings.minSamplingSize)
                return 0;
            return setting.pixelateSettings.outlineThickness;
        }

        static readonly ImmutableArray<int[]> k_DefaultModelSettingsResolutions = new( new []{ new[] { 1024, 1024 } });

        public static IEnumerable<string> SelectModelSettingsResolutions(this IState state, VisualElement element)
        {
            var imageSizes = state.SelectSelectedModel(element)?.imageSizes;
            if (imageSizes == null || imageSizes.Length == 0)
                imageSizes = k_DefaultModelSettingsResolutions;
            return imageSizes.Select(size => $"{size[0]} x {size[1]}");
        }

        public static string SelectImageDimensions(this IState state, VisualElement element)
        {
            var dimension = state.SelectGenerationSetting(element).imageDimensions;
            var resolutions = state.SelectModelSettingsResolutions(element)?.ToList();
            if (resolutions == null || resolutions.Count == 0)
                return "1024 x 1024";
            return resolutions.Contains(dimension) ? dimension : resolutions[0];
        }

        public static Vector2Int SelectImageDimensionsVector2(this GenerationSetting setting)
        {
            if (string.IsNullOrEmpty(setting.imageDimensions))
                return new Vector2Int(1024, 1024);

            var dimensionsSplit = setting.imageDimensions.Split(" x ");

            int.TryParse(dimensionsSplit[0], out var width);
            int.TryParse(dimensionsSplit[1], out var height);

            if (width == 0 || height == 0)
            {
                width = 1024;
                height = 1024;
            }

            var dimensions = new Vector2Int(width, height);
            return dimensions;
        }

        public static IEnumerable<ImageReferenceType> SelectActiveReferencesTypes(this IState state, VisualElement element)
        {
            var active = new List<ImageReferenceType>();
            var generationSetting = state.SelectGenerationSetting(element);
            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var imageReference = generationSetting.SelectImageReference(type);
                if (imageReference.isActive)
                    active.Add(type);
            }
            return active;
        }

        public static IEnumerable<string> SelectActiveReferences(this IState state, VisualElement element)
        {
            var active = new List<string>();
            var generationSetting = state.SelectGenerationSetting(element);
            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var imageReference = generationSetting.SelectImageReference(type);
                if (imageReference.isActive)
                    active.Add(type.GetImageReferenceName());
            }
            return active;
        }

        public static int SelectActiveReferencesCount(this IState state, VisualElement element)
        {
            var count = 0;
            var generationSetting = state.SelectGenerationSetting(element);
            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var imageReference = generationSetting.SelectImageReference(type);
                if (imageReference.isActive && (imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid() || imageReference.mode == ImageReferenceMode.Doodle))
                    count++;
            }
            return count;
        }

        public static string SelectPendingPing(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pendingPing;

        public static bool SelectAssetExists(this IState state, AssetReference asset) => asset.Exists();

        public static bool SelectAssetExists(this IState state, VisualElement element) => state.SelectAssetExists(element.GetAsset());

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
            return new GenerationValidationSettings(asset, prompt, negativePrompt, model, variations, mode, referenceCount);
        }

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;
    }
}
