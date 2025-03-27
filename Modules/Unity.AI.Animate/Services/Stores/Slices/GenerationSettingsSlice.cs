using System;
using System.Linq;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.Animate.Services.Stores.Slices
{
    static class GenerationSettingsSlice
    {
        public static void Create(Store store) => store.CreateSlice(
            GenerationSettingsActions.slice,
            new GenerationSettings(),
            reducers => reducers
                .Slice<GenerationSetting, IContext<AssetContext>>(
                    (state, action, slice) =>
                    {
                        if (action.context.asset == null) return;
                        var subState = state.generationSettings.Ensure(action.context.asset).EnsureSelectedModelID(store.State);
                        state.generationSettings[action.context.asset] = slice(subState);
                    },
                    reducers => reducers
                        .Add(GenerationSettingsActions.setHistoryDrawerHeight, (state, payload) => state.historyDrawerHeight = payload)
                        .Add(GenerationSettingsActions.setLastModelDiscoveryTime, (state, payload) => state.lastModelDiscoveryTime = payload)
                        .Add(GenerationSettingsActions.setSelectedModelID, (state, payload) => state.selectedModels.Ensure(payload.mode).modelID = payload.modelID)
                        .Add(GenerationSettingsActions.setPrompt, (state, payload) => state.prompt = payload)
                        .Add(GenerationSettingsActions.setNegativePrompt, (state, payload) => state.negativePrompt = payload)
                        .Add(GenerationSettingsActions.setVariationCount, (state, payload) => state.variationCount = payload)
                        .Add(GenerationSettingsActions.setDuration, (state, payload) => state.duration = payload)
                        .Add(GenerationSettingsActions.setUseCustomSeed, (state, payload) => state.useCustomSeed = payload)
                        .Add(GenerationSettingsActions.setCustomSeed, (state, payload) => state.customSeed = Math.Max(0, payload))
                        .Add(GenerationSettingsActions.setRefinementMode, (state, payload) => state.refinementMode = payload)
                        .Add(GenerationSettingsActions.setVideoInputReferenceAsset, (state, payload) => state.videoReference.asset = payload)
                        .Add(GenerationSettingsActions.setVideoInputReference, (state, payload) => state.videoReference = payload)
                ),
            extraReducers => extraReducers
                .AddCase(AppActions.init).With((state, payload) => payload.payload.generationSettingsSlice with { })
                .AddCase(AppActions.deleteAsset).With((state, payload) =>
                {
                    if (state.generationSettings.ContainsKey(payload.payload))
                        state.generationSettings.Remove(payload.payload);
                    return state with { };
                }),
            state => state with {
                generationSettings = new SerializableDictionary<AssetReference, GenerationSetting>(
                    state.generationSettings.ToDictionary(kvp => kvp.Key, entry => entry.Value with {
                        lastModelDiscoveryTime = entry.Value.lastModelDiscoveryTime,
                        selectedModels = new SerializableDictionary<RefinementMode, ModelSelection>(
                            entry.Value.selectedModels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with {
                                modelID = kvp.Value.modelID
                            })),
                        prompt = entry.Value.prompt,
                        negativePrompt = entry.Value.negativePrompt,
                        variationCount = entry.Value.variationCount,
                        duration = entry.Value.duration,
                        useCustomSeed = entry.Value.useCustomSeed,
                        customSeed = entry.Value.customSeed,
                        refinementMode = entry.Value.refinementMode,
                        videoReference = entry.Value.videoReference with {
                            asset = entry.Value.videoReference.asset
                        }
                    })
                )
            });
    }
}
