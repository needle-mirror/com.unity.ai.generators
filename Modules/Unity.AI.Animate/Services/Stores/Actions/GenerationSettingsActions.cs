using System;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Animate.Services.Stores.Actions.Creators;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Windows.GenerationMetadataWindow;
using Unity.AI.ModelSelector.Windows;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Services.Stores.Actions
{
    static class GenerationSettingsActions
    {
        public static readonly string slice = "generationSettings";

        public static AssetActionCreator<float> setLastModelDiscoveryTime => new($"{slice}/{nameof(setLastModelDiscoveryTime)}");
        public static AssetActionCreator<(RefinementMode mode, string modelID)> setSelectedModelID => new($"{slice}/{nameof(setSelectedModelID)}");
        public static AssetActionCreator<string> setPrompt => new($"{slice}/{nameof(setPrompt)}");
        public static AssetActionCreator<string> setNegativePrompt => new($"{slice}/{nameof(setNegativePrompt)}");
        public static AssetActionCreator<int> setVariationCount => new($"{slice}/{nameof(setVariationCount)}");
        public static AssetActionCreator<float> setDuration => new($"{slice}/{nameof(setDuration)}");
        public static AssetActionCreator<bool> setUseCustomSeed => new($"{slice}/{nameof(setUseCustomSeed)}");
        public static AssetActionCreator<int> setCustomSeed => new($"{slice}/{nameof(setCustomSeed)}");
        public static AssetActionCreator<RefinementMode> setRefinementMode => new($"{slice}/setRefinementMode");

        public static AssetActionCreator<AssetReference> setVideoInputReferenceAsset => new($"{slice}/{nameof(setVideoInputReferenceAsset)}");
        public static AssetActionCreator<VideoInputReference> setVideoInputReference => new($"{slice}/{nameof(setVideoInputReference)}");

        public static readonly AsyncThunkCreatorWithArg<(VisualElement element, RefinementMode mode)> openSelectModelPanel = new($"{slice}/{nameof(openSelectModelPanel)}", async (payload, api) =>
        {
            // the model selector is modal (in the common sense) and it is shared by all modalities (in the generative sense)
            // its model selection is transient and needs to be exchanged with the current modality's slice
            var element = payload.element;
            var selectedModelID = api.State.SelectSelectedModelID(element);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastSelectedModelID, selectedModelID);
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastSelectedModality, ModalityEnum.Animate); //Animate
            element.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.setLastOperationSubTypes, payload.mode == RefinementMode.VideoToMotion
                    ? new [] { OperationSubTypeEnum.ReferencePrompt }
                    : new [] { OperationSubTypeEnum.TextPrompt });
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
