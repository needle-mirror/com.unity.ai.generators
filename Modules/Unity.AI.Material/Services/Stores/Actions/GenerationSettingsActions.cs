using System;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Material.Services.Stores.Actions.Creators;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Windows.GenerationMetadataWindow;
using Unity.AI.ModelSelector.Windows;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Services.Stores.Actions
{
    static class GenerationSettingsActions
    {
        public static readonly string slice = "generationSettings";

        public static AssetActionCreator<float> setLastModelDiscoveryTime => new($"{slice}/{nameof(setLastModelDiscoveryTime)}");
        public static AssetActionCreator<(RefinementMode mode, string modelID)> setSelectedModelID => new($"{slice}/{nameof(setSelectedModelID)}");
        public static AssetActionCreator<string> setPrompt => new($"{slice}/{nameof(setPrompt)}");
        public static AssetActionCreator<string> setNegativePrompt => new($"{slice}/{nameof(setNegativePrompt)}");
        public static AssetActionCreator<int> setVariationCount => new($"{slice}/{nameof(setVariationCount)}");
        public static AssetActionCreator<bool> setUseCustomSeed => new($"{slice}/{nameof(setUseCustomSeed)}");
        public static AssetActionCreator<int> setCustomSeed => new($"{slice}/{nameof(setCustomSeed)}");
        public static AssetActionCreator<RefinementMode> setRefinementMode => new($"{slice}/setRefinementMode");
        public static AssetActionCreator<string> setImageDimensions => new($"{slice}/setImageDimensions");

        public static AssetActionCreator<AssetReference> setPromptImageReferenceAsset => new($"{slice}/setPromptImageReferenceAsset");
        public static AssetActionCreator<PromptImageReference> setPromptImageReference => new($"{slice}/setPromptImageReference");

        public static AssetActionCreator<AssetReference> setPatternImageReferenceAsset => new($"{slice}/setPatternImageReferenceAsset");
        public static AssetActionCreator<float> setPatternImageReferenceStrength => new($"{slice}/setPatternImageReferenceStrength");
        public static AssetActionCreator<PatternImageReference> setPatternImageReference => new($"{slice}/setPatternImageReference");

        public static readonly AsyncThunkCreatorWithArg<(VisualElement element, RefinementMode mode)> openSelectModelPanel = new($"{slice}/{nameof(openSelectModelPanel)}", async (payload, api) =>
        {
            // the model selector is modal (in the common sense) and it is shared by all modalities (in the generative sense)
            // its model selection is transient and needs to be exchanged with the current modality's slice
            var element = payload.element;
            var selectedModelID = api.State.SelectSelectedModelID(element);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastSelectedModelID, selectedModelID);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastSelectedModality, ModalityEnum.Texture2d);
            var operations = payload.mode switch
            {
                RefinementMode.Generation => new[] { OperationSubTypeEnum.TextPrompt },
                RefinementMode.Upscale => new[] { OperationSubTypeEnum.Upscale },
                RefinementMode.Pbr => new[] { OperationSubTypeEnum.Pbr },
                _ => new[] { OperationSubTypeEnum.TextPrompt }
            };
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastOperationSubTypes, operations);
            await ModelSelectorWindow.Open(element.GetStore());
            selectedModelID = ModelSelector.Services.Stores.Selectors.ModelSelectorSelectors.SelectLastSelectedModelID(api.State);
            element.Dispatch(setSelectedModelID, (api.State.SelectRefinementMode(element), selectedModelID));
        });

        public static readonly AsyncThunkCreatorWithArg<GenerationDataWindowArgs> openGenerationDataWindow = new($"{slice}/openGenerationDataWindow", async (args, api) =>
        {
            await GenerationMetadataWindow.Open(args.element.GetStore(), args.asset, args.element, args.result);
        });

        public static readonly AssetActionCreator<float> setHistoryDrawerHeight = new($"{slice}/setHistoryDrawerHeight");
    }
}
