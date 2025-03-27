using System;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Image.Services.Stores.Actions.Creators;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Utilities;
using Unity.AI.Image.Windows;
using Unity.AI.ModelSelector.Windows;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Services.Stores.Actions
{
    static class GenerationSettingsActions
    {
        public static readonly string slice = "generationSettings";

        public static AssetActionCreator<float> setLastModelDiscoveryTime => new($"{slice}/setLastModelDiscoveryTime");
        public static AssetActionCreator<(RefinementMode mode, string modelID)> setSelectedModelID => new($"{slice}/{nameof(setSelectedModelID)}");
        public static AssetActionCreator<string> setPrompt => new($"{slice}/setPrompt");
        public static AssetActionCreator<string> setNegativePrompt => new($"{slice}/setNegativePrompt");
        public static AssetActionCreator<int> setVariationCount => new($"{slice}/setVariationCount");
        public static AssetActionCreator<bool> setUseCustomSeed => new($"{slice}/setUseCustomSeed");
        public static AssetActionCreator<int> setCustomSeed => new($"{slice}/setCustomSeed");
        public static AssetActionCreator<RefinementMode> setRefinementMode => new($"{slice}/setRefinementMode");
        public static AssetActionCreator<string> setImageDimensions => new($"{slice}/setImageDimensions");
        public static AssetActionCreator<bool> setReplaceBlankAsset => new($"{slice}/setReplaceBlankAsset");
        public static AssetActionCreator<bool> setReplaceRefinementAsset => new($"{slice}/setReplaceRefinementAsset");
        public static AssetActionCreator<UnsavedAssetBytesData> setUnsavedAssetBytes => new($"{slice}/setUnsavedAssetBytes");
        public static AssetActionCreator<int> setUpscaleFactor => new($"{slice}/setUpscaleFactor");

        public static AssetActionCreator<ImageReferenceAssetData> setImageReferenceAsset => new($"{slice}/setImageReferenceAsset");
        public static AssetActionCreator<ImageReferenceDoodleData> setImageReferenceDoodle => new($"{slice}/setImageReferenceDoodle");
        public static AssetActionCreator<ImageReferenceModeData> setImageReferenceMode => new($"{slice}/setImageReferenceMode");
        public static AssetActionCreator<ImageReferenceStrengthData> setImageReferenceStrength => new($"{slice}/setImageReferenceStrength");
        public static AssetActionCreator<ImageReferenceActiveData> setImageReferenceActive => new($"{slice}/setImageReferenceActive");
        public static AssetActionCreator<ImageReferenceSettingsData> setImageReferenceSettings => new($"{slice}/setImageReferenceSettings");

        public static AssetActionCreator<PixelateSettings> setPixelateSettings => new($"{slice}/setPixelateSettings");
        public static AssetActionCreator<int> setPixelateTargetSize => new($"{slice}/setPixelateTargetSize");
        public static AssetActionCreator<bool> setPixelateKeepImageSize => new($"{slice}/setPixelateKeepImageSize");
        public static AssetActionCreator<int> setPixelatePixelBlockSize => new($"{slice}/setPixelatePixelBlockSize");
        public static AssetActionCreator<PixelateMode> setPixelateMode => new($"{slice}/setPixelateMode");
        public static AssetActionCreator<int> setPixelateOutlineThickness => new($"{slice}/setPixelateOutlineThickness");
        public static AssetActionCreator<string> setPendingPing => new($"{slice}/setPendingPing");
        public static AssetActionCreator<(ImageReferenceType type, byte[] data)> applyEditedImageReferenceDoodle => new($"{slice}/applyEditedImageReferenceDoodle");

        public static readonly AsyncThunkCreatorWithArg<(VisualElement element, RefinementMode mode)> openSelectModelPanel = new($"{slice}/openSelectModelPanel", async (payload, api) =>
        {
            // the model selector is modal (in the common sense) and it is shared by all modalities (in the generative sense)
            // its model selection is transient and needs to be exchanged with the current modality's slice
            var element = payload.element;
            var selectedModelID = api.State.SelectSelectedModelID(element);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastSelectedModelID, selectedModelID);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastSelectedModality, ModalityEnum.Image);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastOperationSubTypes, payload.mode switch
            {
                RefinementMode.Generation => new [] { OperationSubTypeEnum.TextPrompt },
                RefinementMode.Upscale => new [] { OperationSubTypeEnum.Upscale },
                RefinementMode.Pixelate => new [] { OperationSubTypeEnum.Pixelate },
                RefinementMode.Recolor => new [] { OperationSubTypeEnum.RecolorReference },
                RefinementMode.Inpaint => new [] { OperationSubTypeEnum.MaskReference },
                _ => new [] { OperationSubTypeEnum.TextPrompt }
            });
            await ModelSelectorWindow.Open(element.GetStore());
            selectedModelID = ModelSelector.Services.Stores.Selectors.ModelSelectorSelectors.SelectLastSelectedModelID(api.State);
            element.Dispatch(setSelectedModelID, (api.State.SelectRefinementMode(element), selectedModelID));
        });

        public static readonly AsyncThunkCreatorWithArg<GenerationDataWindowArgs> openGenerationDataWindow = new($"{slice}/openGenerationDataWindow",
            async (args, api) => await GenerationMetadataWindow.Open(args.element.GetStore(), args.asset, args.element, args.result));

        public static readonly AssetActionCreator<float> setHistoryDrawerHeight = new($"{slice}/setHistoryDrawerHeight");
    }
}
