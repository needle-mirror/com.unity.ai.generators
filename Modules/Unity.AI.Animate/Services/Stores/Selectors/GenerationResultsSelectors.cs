using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Undo;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Services.Stores.Selectors
{
    static partial class Selectors
    {
        public static GenerationResults SelectGenerationResults(this IState state) => state.Get<GenerationResults>(GenerationResultsActions.slice);
        public static GenerationResult SelectGenerationResult(this IState state, VisualElement element) => state.SelectGenerationResult(element.GetAsset());
        public static GenerationResult SelectGenerationResult(this IState state, AssetReference asset)
        {
            if (state == null)
                return new GenerationResult();
            var results = state.SelectGenerationResults().generationResults;
            return results.Ensure(asset);
        }
        public static bool SelectGenerationAllowed(this IState state, VisualElement element)
        {
            var results = state.SelectGenerationResult(element);
            return results.generationAllowed && results.generationValidation.success;
        }

        public static List<GenerationProgressData> SelectGenerationProgress(this IState state, VisualElement element) => state.SelectGenerationResult(element).generationProgress;
        public static GenerationProgressData SelectGenerationProgress(this IState state, VisualElement element, AnimationClipResult result)
        {
            if (result is TextureSkeleton textureSkeleton)
            {
                var progressReports = state.SelectGenerationResult(element).generationProgress;
                var progressReport = progressReports.FirstOrDefault(d => d.taskID == textureSkeleton.taskID);
                if (progressReport != null)
                    return progressReport;
            }

            return new GenerationProgressData(-1, 1, 1);
        }
        public static List<GenerationFeedbackData> SelectGenerationFeedback(this IState state, VisualElement element) => state.SelectGenerationResult(element).generationFeedback;
        public static GenerationValidationResult SelectGenerationValidationResult(this IState state, VisualElement element) => state.SelectGenerationResult(element).generationValidation;

        public static int SelectGeneratedResultVisibleCount(this IState state, VisualElement element) => state.SelectGenerationResult(element)
            .generatedResultSelectorSettings.Values.Select(hints => hints.itemCountHint).DefaultIfEmpty(0).Max();
        public static int SelectGeneratedResultVisibleCount(this IState state, AssetReference asset) => state.SelectGenerationResult(asset)
            .generatedResultSelectorSettings.Values.Select(hints => hints.itemCountHint).DefaultIfEmpty(0).Max();

        public static List<AnimationClipResult> SelectGeneratedAnimations(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedAnimations;
        public static List<TextureSkeleton> SelectGeneratedSkeletons(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedSkeletons;
        public static List<TextureSkeleton> SelectGeneratedSkeletons(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedSkeletons;
        public static IEnumerable<AnimationClipResult> SelectGeneratedAnimationsAndSkeletons(this IState state, VisualElement element)
        {
            var generationResults = state.SelectGenerationResult(element);
            var animations = generationResults.generatedAnimations;
            var skeletons = generationResults.generatedSkeletons;
            var fulfilledSkeletons = generationResults.fulfilledSkeletons;

            // Create a HashSet of result URIs for O(1) lookups
            var animationUris = new HashSet<string>(
                animations
                    .Where(animation => animation.uri != null)
                    .Select(animation => animation.uri.GetAbsolutePath())
            );

            // Find skeletons that have been fulfilled and have matching animation results
            var skeletonsToExclude = new HashSet<int>();

            foreach (var fulfilled in fulfilledSkeletons)
            {
                // Check if this fulfilled skeleton has a matching animation result using O(1) lookup
                if (animationUris.Contains(fulfilled.resultUri))
                {
                    skeletonsToExclude.Add(fulfilled.progressTaskID);
                }
            }

            // Filter skeletons to include only those not in the exclude list
            var filteredSkeletons = skeletons.Where(skeleton => !skeletonsToExclude.Contains(skeleton.taskID));

            // Return all animation results plus the filtered skeletons
            return filteredSkeletons.Concat(animations);
        }

        public static bool HasHistory(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedAnimations.Count > 0;
        public static AnimationClipResult SelectSelectedGeneration(this IState state, VisualElement element) => state.SelectGenerationResult(element).selectedGeneration;
        public static AnimationClipResult SelectSelectedGeneration(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).selectedGeneration;
        public static AssetUndoManager SelectAssetUndoManager(this IState state, VisualElement element) => state.SelectGenerationResult(element).assetUndoManager;
        public static AssetUndoManager SelectAssetUndoManager(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).assetUndoManager;
        public static bool SelectReplaceWithoutConfirmationEnabled(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).replaceWithoutConfirmation;
    }
}
