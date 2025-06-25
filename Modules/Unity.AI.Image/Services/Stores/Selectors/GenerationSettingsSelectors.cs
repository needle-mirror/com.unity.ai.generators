using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiEditorToolsSdk.Components.Common.Enums;
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
using Unity.AI.ModelSelector.Services.Stores.Selectors;
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

        public static ModelSettings SelectSelectedModel(this IState state, VisualElement element) => state.SelectSelectedModel(element.GetAsset());
        public static ModelSettings SelectSelectedModel(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            var modelID = state.SelectGenerationSetting(asset).selectedModels.Ensure(mode).modelID;
            var model = ModelSelectorSelectors.SelectModelSettings(state).FirstOrDefault(s => s.id == modelID);
            return model;
        }

        public static List<OperationSubTypeEnum> SelectRefinementOperations(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            var operations = mode switch
            {
                RefinementMode.Generation => new [] { OperationSubTypeEnum.TextPrompt },
                RefinementMode.Upscale => new [] { OperationSubTypeEnum.Upscale },
                RefinementMode.Pixelate => new [] { OperationSubTypeEnum.Pixelate },
                RefinementMode.Recolor => new [] { OperationSubTypeEnum.RecolorReference },
                RefinementMode.Inpaint => new [] { OperationSubTypeEnum.MaskReference },
                _ => new [] { OperationSubTypeEnum.TextPrompt }
            };
            return operations.ToList();
        }
        public static List<OperationSubTypeEnum> SelectRefinementOperations(this IState state, VisualElement element) => state.SelectRefinementOperations(element.GetAsset());

        public static (RefinementMode mode, bool should, long timestamp) SelectShouldAutoAssignModel(this IState state, VisualElement element)
        {
            var mode = state.SelectRefinementMode(element);
            return (mode, ModelSelectorSelectors.SelectShouldAutoAssignModel(state, new[] { ModalityEnum.Image, ModalityEnum.Texture2d },
                state.SelectRefinementOperations(element).ToArray()), timestamp: ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state));
        }

        public static ModelSettings SelectAutoAssignModel(this IState state, VisualElement element) =>
            ModelSelectorSelectors.SelectAutoAssignModel(state, new[] { ModalityEnum.Image, ModalityEnum.Texture2d },
                state.SelectRefinementOperations(element).ToArray());

        public static GenerationSetting EnsureSelectedModelID(this GenerationSetting setting, IState state)
        {
            foreach (RefinementMode mode in Enum.GetValues(typeof(RefinementMode)))
            {
                var selection = setting.selectedModels.Ensure(mode);
                if (!string.IsNullOrEmpty(selection.modelID))
                    continue;
                var session = state.SelectSession();
                if (session == null)
                    continue;
                selection.modelID = state.SelectSession().settings.lastSelectedModels.Ensure(mode).modelID;
            }
            return setting;
        }

        public static ModelSettings SelectModelSettings(this IState state, GenerationMetadata generationMetadata)
        {
            var modelID = generationMetadata.model;
            var model = ModelSelectorSelectors.SelectModelSettings(state).FirstOrDefault(s => s.id == modelID);
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

            if (string.IsNullOrEmpty(text))
                text = noDataFoundString;

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

        public static async Task<Stream> SelectPaletteImageStream(this GenerationSetting setting)
        {
            var paletteImageReference = setting.SelectImageReference(ImageReferenceType.PaletteImage);
            return await paletteImageReference.SelectImageReferenceStream();
        }
        public static async Task<Stream> SelectPaletteImageStream(this IState state, VisualElement element)
        {
            var setting = state.SelectGenerationSetting(element);
            return await setting.SelectPaletteImageStream();
        }

        public static byte[] SelectUnsavedAssetBytes(this GenerationSetting setting) => setting.unsavedAssetBytes.data;
        public static byte[] SelectUnsavedAssetBytes(this IState state, VisualElement element) => state.SelectGenerationSetting(element).unsavedAssetBytes.data;
        public static UnsavedAssetBytesSettings SelectUnsavedAssetBytesSettings(this IState state, VisualElement element) => state.SelectGenerationSetting(element).unsavedAssetBytes;

        public static async Task<bool> SelectHasAssetToRefine(this IState state, VisualElement element) => await state.SelectHasAssetToRefine(element.GetAsset());
        public static async Task<bool> SelectHasAssetToRefine(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();
            if (unsavedAssetBytes is { Length: > 0 })
                return true;

            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);
            if (generations.Contains(currentSelection) && currentSelection.IsValid())
                return true;

            return asset.IsValid() && !await asset.IsOneByOnePixelOrLikelyBlank();
        }

        public static async Task<Stream> SelectUnsavedAssetStreamWithFallback(this IState state, VisualElement element) => await state.SelectUnsavedAssetStreamWithFallback(element.GetAsset());
        public static async Task<Stream> SelectUnsavedAssetStreamWithFallback(this IState state, AssetReference asset)
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
            return currentSelection.IsValid() ? await currentSelection.GetCompatibleImageStreamAsync() : await asset.GetCompatibleImageStreamAsync();
        }

        public static Stream SelectUnsavedAssetBytesStream(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();

            // use unsaved asset bytes if available
            return unsavedAssetBytes is { Length: > 0 } ? new MemoryStream(unsavedAssetBytes) : null;
        }

        public static async Task<RenderTexture> SelectBaseAssetPreviewTexture(this IState state, AssetReference asset, int sizeHint)
        {
            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            // selection, or asset
            return currentSelection.IsValid()
                ? await TextureCache.GetPreview(currentSelection.uri, sizeHint)
                : await TextureCache.GetPreview(asset.GetUri(), sizeHint);
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
            if (!asset.IsValid())
                return new(0);

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
            !state.SelectGenerationSetting(element).imageReferences[(int)type].asset.IsValid() &&
            state.SelectImageReferenceDoodle(element, type) is not { Length: not 0 };

        public static bool SelectImageReferenceIsValid(this IState state, VisualElement element, ImageReferenceType type) =>
            state.SelectGenerationSetting(element).SelectImageReference(type).SelectImageReferenceIsValid();
        public static bool SelectImageReferenceIsValid(this ImageReferenceSettings imageReference) => imageReference.isActive &&
            (imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid() || imageReference.mode == ImageReferenceMode.Doodle);
        public static async Task<Stream> SelectImageReferenceStream(this ImageReferenceSettings imageReference) =>
            imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid()
                ? await imageReference.asset.GetCompatibleImageStreamAsync()
                : new MemoryStream(imageReference.doodle ?? Array.Empty<byte>());
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

        static readonly ImmutableArray<ImageDimensions> k_DefaultModelSettingsResolutions = new(new []{ new ImageDimensions { width = 1024, height = 1024 } });

        public static IEnumerable<string> SelectModelSettingsResolutions(this IState state, VisualElement element)
        {
            var imageSizes = state.SelectSelectedModel(element)?.imageSizes;
            if (imageSizes == null || !imageSizes.Any())
                imageSizes = new List<ImageDimensions>(k_DefaultModelSettingsResolutions);
            return imageSizes.Select(size => $"{size.width} x {size.height}");
        }

        public static string SelectImageDimensions(this IState state, VisualElement element)
        {
            var dimension = state.SelectGenerationSetting(element).imageDimensions;
            var resolutions = state.SelectModelSettingsResolutions(element)?.ToList();
            if (resolutions == null || resolutions.Count == 0)
                return $"{k_DefaultModelSettingsResolutions[0].width} x {k_DefaultModelSettingsResolutions[0].height}";
            return resolutions.Contains(dimension) ? dimension : resolutions[0];
        }

        public static Vector2Int SelectImageDimensionsVector2(this GenerationSetting setting)
        {
            if (string.IsNullOrEmpty(setting.imageDimensions))
                return new Vector2Int(k_DefaultModelSettingsResolutions[0].width, k_DefaultModelSettingsResolutions[0].height);

            var dimensionsSplit = setting.imageDimensions.Split(" x ");

            int.TryParse(dimensionsSplit[0], out var width);
            int.TryParse(dimensionsSplit[1], out var height);

            if (width == 0 || height == 0)
            {
                width = k_DefaultModelSettingsResolutions[0].width;
                height = k_DefaultModelSettingsResolutions[0].height;
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

        /// <summary>
        /// Returns a bit mask representing active reference types.
        /// Each bit position corresponds to the integer value of the ImageReferenceType enum.
        /// </summary>
        public static int SelectActiveReferencesBitMask(this IState state, AssetReference asset)
        {
            var bitMask = 0;
            var generationSetting = state.SelectGenerationSetting(asset);

            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var typeValue = (int)type;
                if (typeValue >= 32)
                    throw new InvalidOperationException($"ImageReferenceType value {typeValue} ({type}) exceeds the maximum bit position (31) " + "that can be stored in an Int32. Consider using a long (64-bit) instead.");
                var imageReference = generationSetting.SelectImageReference(type);
                var isActiveReference = imageReference.SelectImageReferenceIsActive();
                if (isActiveReference)
                    bitMask |= 1 << typeValue;
            }

            return bitMask;
        }
        public static int SelectActiveReferencesBitMask(this IState state, VisualElement element) => state.SelectActiveReferencesBitMask(element.GetAsset());

        /// <summary>
        /// Returns a bit mask representing valid reference types with valid content.
        /// Each bit position corresponds to the integer value of the ImageReferenceType enum.
        /// </summary>
        public static int SelectValidReferencesBitMask(this IState state, AssetReference asset)
        {
            var bitMask = 0;
            var generationSetting = state.SelectGenerationSetting(asset);

            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var typeValue = (int)type;
                if (typeValue >= 32)
                    throw new InvalidOperationException($"ImageReferenceType value {typeValue} ({type}) exceeds the maximum bit position (31) " + "that can be stored in an Int32. Consider using a long (64-bit) instead.");
                var imageReference = generationSetting.SelectImageReference(type);
                var isValidReference = imageReference.SelectImageReferenceIsValid();
                if (isValidReference)
                    bitMask |= 1 << typeValue;
            }

            return bitMask;
        }
        public static int SelectValidReferencesBitMask(this IState state, VisualElement element) => state.SelectValidReferencesBitMask(element.GetAsset());

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
            var activeReferencesBitMask = state.SelectActiveReferencesBitMask(element);
            var validReferencesBitMask = state.SelectValidReferencesBitMask(element);
            var baseImageBytesTimeStamp = state.SelectBaseImageBytesTimestamp(element);
            var modelsTimeStamp = ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state);
            return new GenerationValidationSettings(asset, asset.Exists(), prompt, negativePrompt, model, variations, mode, activeReferencesBitMask,
                validReferencesBitMask, baseImageBytesTimeStamp.lastWriteTimeUtcTicks, modelsTimeStamp);
        }

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;

        public static float SelectGenerationPaneWidth(this IState state, VisualElement element) => state.SelectGenerationSetting(element).generationPaneWidth;
    }
}
