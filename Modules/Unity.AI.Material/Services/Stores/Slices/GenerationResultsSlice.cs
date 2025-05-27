using System;
using System.Linq;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Actions;
using UnityEngine;

namespace Unity.AI.Material.Services.Stores.Slices
{
    static class GenerationResultsSlice
    {
        public static void Create(Store store) => store.CreateSlice(
            GenerationResultsActions.slice,
            new GenerationResults(),
            reducers => reducers
                .Add(GenerationActions.setGenerationAllowed, (state, payload) => state.generationResults.Ensure(payload.asset).generationAllowed = payload.allowed)
                .Add(GenerationActions.setGenerationProgress, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generationProgress = new[] { payload.progress }.Concat(results.generationProgress)
                        .GroupBy(tr => tr.taskID)
                        .Select(group => group.First())
                        .ToList();
                })
                .Add(GenerationActions.addGenerationFeedback, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generationFeedback = results.generationFeedback.Append(payload.feedback).ToList();
                })
                .Add(GenerationActions.removeGenerationFeedback, (state, asset) => {
                    var results = state.generationResults.Ensure(asset);
                    results.generationFeedback = results.generationFeedback.Skip(1).ToList();
                })
                .Add(GenerationActions.setGenerationValidationResult, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generationValidation = payload.result;
                })
                .Add(GenerationResultsActions.setGeneratedMaterials, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generatedMaterials = payload.materials.ToList();
                })
                .Add(GenerationResultsActions.setGeneratedSkeletons, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generatedSkeletons = results.generatedSkeletons.Union(payload.skeletons).ToList();
                })
                .Add(GenerationResultsActions.removeGeneratedSkeletons, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generatedSkeletons = results.generatedSkeletons.Where(s => s.taskID != payload.taskID).ToList();
                })
                .Add(GenerationResultsActions.setSelectedGeneration, (state, payload) => state.generationResults.Ensure(payload.asset).selectedGeneration = payload.result with { })
                .Add(GenerationResultsActions.setGeneratedMaterialMapping, (state, payload) => state.generationResults.Ensure(payload.asset).generatedMaterialMapping[payload.mapType] = payload.materialProperty)
                .Add(GenerationResultsActions.setAssetUndoManager, (state, payload) => state.generationResults.Ensure(payload.asset).assetUndoManager = payload.undoManager)
                .Add(GenerationResultsActions.setReplaceWithoutConfirmation, (state, payload) => state.generationResults.Ensure(payload.asset).replaceWithoutConfirmation = payload.withoutConfirmation)
                .Add(GenerationResultsActions.setGeneratedResultVisibleCount, (state, payload) => {
                    var results = state.generationResults.Ensure(payload.asset);
                    results.generatedResultSelectorSettings.Ensure(payload.elementID).itemCountHint = payload.count;
                }),
        extraReducers => extraReducers
                .AddCase(AppActions.init).With((state, payload) => payload.payload.generationResultsSlice with {})
                .AddCase(AppActions.deleteAsset).With((state, payload) =>
                {
                    if (state.generationResults.ContainsKey(payload.payload))
                        state.generationResults.Remove(payload.payload);
                    return state with { };
                }),
            state => state with {
                generationResults = new SerializableDictionary<AssetReference, GenerationResult>(
                    state.generationResults.ToDictionary(kvp => kvp.Key, entry => entry.Value with {
                        generatedMaterials = entry.Value.generatedMaterials,
                        generatedSkeletons = entry.Value.generatedSkeletons,
                        generationAllowed = entry.Value.generationAllowed,
                        generationProgress = entry.Value.generationProgress,
                        generationFeedback = entry.Value.generationFeedback,
                        selectedGeneration = entry.Value.selectedGeneration with { },
                        generatedMaterialMapping = new SerializableDictionary<MapType, string>(entry.Value.generatedMaterialMapping),
                        assetUndoManager = entry.Value.assetUndoManager,
                        replaceWithoutConfirmation = entry.Value.replaceWithoutConfirmation,
                        generatedResultSelectorSettings = new SerializableDictionary<string, GeneratedResultSelectorSettings>(
                            entry.Value.generatedResultSelectorSettings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with {
                                itemCountHint = kvp.Value.itemCountHint
                            })),
                        generationValidation = entry.Value.generationValidation with { }
                    })
                )
            });
    }
}
