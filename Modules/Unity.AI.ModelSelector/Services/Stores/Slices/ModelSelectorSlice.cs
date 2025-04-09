using System;
using System.Linq;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.Generators.Redux;

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
                    .Add(ModelSelectorActions.setLastSelectedModality, (state, payload) => state.lastSelectedModality = payload)
                    .Add(ModelSelectorActions.setLastOperationSubTypes, (state, payload) => state.lastSelectedOperations = payload),
                extraReducers => extraReducers
                    .AddCase(ModelSelectorActions.init).With((_, payload) => payload.payload.modelSelectorSlice with { }),
                state => state with
                {
                    settings = state.settings with
                    {
                        models = state.settings.models,
                        environment = state.settings.environment
                    },
                    lastSelectedModelID = state.lastSelectedModelID,
                    lastSelectedModality = state.lastSelectedModality,
                    lastSelectedOperations = state.lastSelectedOperations.ToArray()
                });
    }
}
