using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.SessionPersistence;
using Unity.AI.ModelSelector.Services.Stores.Actions.Payloads;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Stores.Actions
{
    static class ModelSelectorActions
    {
        public const string slice = "modelSelector";
        internal static Creator<AppData> init => new($"{slice}/init");
        public static Creator<string> setEnvironment => new($"{slice}/setEnvironment");
        public static Creator<string> setLastSelectedModelID => new($"{slice}/setLastSelectedModelID");
        public static Creator<ModalityEnum> setLastSelectedModality => new($"{slice}/setLastSelectedModality");
        public static Creator<OperationSubTypeEnum[]> setLastOperationSubTypes => new($"{slice}/setLastOperationSubTypes");

        public static readonly AsyncThunkCreatorWithArg<DiscoverModelsData> discoverModels = new($"{slice}/openSelectModelPanel", async (data, api) =>
        {
            WebUtils.selectedEnvironment = data.environment;

            if (s_FetchingBool || (api.State.SelectModelSelectorSettingsReady() && WebUtils.selectedEnvironment == api.State.SelectEnvironment()))
                return;

            s_FetchingBool = true;
            try
            {
                await api.Dispatch(ModelSelectorSuperProxyActions.fetchModels);
                api.Dispatch(setEnvironment, WebUtils.selectedEnvironment);
            }
            finally
            {
                s_FetchingBool = false;
            }
        });

        static bool s_FetchingBool = false;
    }
}
