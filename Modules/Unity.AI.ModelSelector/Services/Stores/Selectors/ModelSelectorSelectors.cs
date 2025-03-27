using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Redux;

namespace Unity.AI.ModelSelector.Services.Stores.Selectors
{
    static partial class ModelSelectorSelectors
    {
        public static States.ModelSelector SelectModels(this IState state) => state.Get<States.ModelSelector>(ModelSelectorActions.slice);

        public static IEnumerable<ModelSettings> SelectModelSettings(this IState state) => state.SelectModels().settings.models;

        public static bool SelectModelSelectorSettingsReady(this IState state) => SelectModelSettings(state).ToList() is { Count: > 0 };

        public static IEnumerable<ModelSettings> SelectUnityModels(this IState state) => state.SelectModelSettings().Where(s => s.provider == ProviderEnum.Unity);

        public static IEnumerable<ModelSettings> SelectPartnersModels(this IState state) => state.SelectModelSettings().Where(s => s.provider != ProviderEnum.Unity);

        public static string SelectRecolorModel(this IState state)
        {
            var modelId = FindFirstModelWithOperatorSubType(state.SelectUnityModels(), OperationSubTypeEnum.RecolorReference);

            if (!string.IsNullOrEmpty(modelId))
                return modelId;

            modelId = FindFirstModelWithOperatorSubType(state.SelectPartnersModels(), OperationSubTypeEnum.RecolorReference);

            return !string.IsNullOrEmpty(modelId) ? modelId : string.Empty;
        }

        static string FindFirstModelWithOperatorSubType(IEnumerable<ModelSettings> models, OperationSubTypeEnum operatorSubType)
        {
            var operatorModels = models.Where(s => s.operations.Contains(operatorSubType)).ToList();
            return operatorModels.Any() ? operatorModels.First().id : string.Empty;
        }
        public static string SelectEnvironment(this IState state) => state.SelectModels().settings.environment;
        public static string SelectLastSelectedModelID(this IState state) => state.SelectModels().lastSelectedModelID;
        public static ModalityEnum SelectLastSelectedModality(this IState state) => state.SelectModels().lastSelectedModality;
        public static OperationSubTypeEnum[] SelectLastSelectedOperations(this IState state) => state.SelectModels().lastSelectedOperations;

        static readonly ModelSettings k_InvalidModelSettings = new();

        public static ModelSettings SelectSelectedModel(this IState state)
        {
            var modelSelector = state.SelectModels();
            var modelID = modelSelector.lastSelectedModelID;
            var models = SelectModelSettings(state);
            var model = models.FirstOrDefault(m => m.id == modelID);
            return model ?? k_InvalidModelSettings;
        }
    }
}
