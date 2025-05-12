using System;
using System.Linq;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.ModelSelector.Services.Stores.Slices
{
    static class ModelSelectorSlice
    {
        public static void Create(Store store) =>
            store.CreateSlice(ModelSelectorActions.slice, new States.ModelSelector(),
                reducers => reducers
                    .AddCase(ModelSelectorSuperProxyActions.fetchModels.Fulfilled, (state, action) => {
                        if (action.payload is { Count: > 0 })
                            state.settings.models = action.payload; })
                    .Add(ModelSelectorActions.setEnvironment, (state, payload) => state.settings.environment = payload)
                    .Add(ModelSelectorActions.setLastSelectedModelID, (state, payload) => state.lastSelectedModelID = payload)
                    .Add(ModelSelectorActions.setLastSelectedModalities, (state, payload) => state.lastSelectedModalities = payload)
                    .Add(ModelSelectorActions.setLastOperationSubTypes, (state, payload) => state.lastSelectedOperations = payload)
                    .Add(ModelSelectorActions.setLastUsedSelectedModelID, (state, payload) =>
                    {
                        if (string.IsNullOrEmpty(payload))
                            return;
                        state.lastUsedModels[payload] = DateTime.Now.ToString();
                        // We increment popularity score locally, but this data will come from the server in the future and will be global.
                        state.modelPopularityScore[payload] = state.modelPopularityScore.TryGetValue(payload, out var score) ? score + 1 : 1;
                    })
                    .Add(ModelSelectorActions.setLastModelDiscoveryTimestamp, (state, payload) => state.settings.lastModelDiscoveryTimestamp = payload),
                extraReducers => extraReducers
                    .AddCase(ModelSelectorActions.init).With((_, payload) => payload.payload.modelSelectorSlice with { }),
                state => state with {
                    settings = state.settings with {
                        models = state.settings.models,
                        environment = state.settings.environment,
                        lastModelDiscoveryTimestamp = state.settings.lastModelDiscoveryTimestamp
                    },
                    lastSelectedModelID = state.lastSelectedModelID,
                    lastSelectedModalities = state.lastSelectedModalities,
                    lastSelectedOperations = state.lastSelectedOperations.ToArray(),
                    lastUsedModels = new SerializableDictionary<string, string>(state.lastUsedModels),
                    modelPopularityScore = new SerializableDictionary<string, int>(state.modelPopularityScore)
                });
    }
}
