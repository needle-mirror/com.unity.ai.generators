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

        public static readonly AsyncThunkCreatorWithArg<VisualElement> openSelectModelPanel = new($"{slice}/{nameof(openSelectModelPanel)}", async (element, api) =>
        {
            var selectedModelID = api.State.SelectSelectedModelID(element);
            var operations = api.State.SelectRefinementOperations(element);
            // the model selector is modal (in the common sense) and it is shared by all modalities (in the generative sense)
            selectedModelID = await ModelSelectorWindow.Open(element, selectedModelID, ModalityEnum.Texture2d, operations.ToArray());
            element.Dispatch(setSelectedModelID, (api.State.SelectRefinementMode(element), selectedModelID));
        });

        public static readonly AsyncThunkCreatorWithArg<GenerationDataWindowArgs> openGenerationDataWindow = new($"{slice}/openGenerationDataWindow",
            async (args, api) => await GenerationMetadataWindow.Open(args.element.GetStore(), args.asset, args.element, args.result));

        public static readonly AssetActionCreator<float> setHistoryDrawerHeight = new($"{slice}/setHistoryDrawerHeight");
    }
}
