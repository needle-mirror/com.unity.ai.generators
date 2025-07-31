using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Undo;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Services.Stores.Selectors
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
        public static GenerationProgressData SelectGenerationProgress(this IState state, VisualElement element, AudioClipResult result)
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

        public static IEnumerable<AudioClipResult> SelectGeneratedAudioClips(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedAudioClips;
        public static IEnumerable<TextureSkeleton> SelectGeneratedSkeletons(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedSkeletons;
        public static IEnumerable<TextureSkeleton> SelectGeneratedSkeletons(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedSkeletons;

        /// <summary>
        /// Returns a combined list of generated textures and skeletons for an element.
        ///
        /// This method intelligently filters out skeletons that have already been fulfilled
        /// with a corresponding TextureResult. The logic is as follows:
        ///
        /// 1. All texture results are included (completed generations)
        /// 2. Skeletons are included only if:
        ///    - They don't have a corresponding entry in fulfilledSkeletons, OR
        ///    - Their corresponding fulfilledSkeleton doesn't yet have a matching TextureResult
        ///
        /// This ensures we don't show duplicate items for both the skeleton and its result.
        /// </summary>
        /// <param name="state">The state to select from</param>
        /// <param name="element">The visual element associated with the asset</param>
        /// <returns>Combined collection of TextureResults and TextureSkeletons</returns>
        public static IEnumerable<AudioClipResult> SelectGeneratedAudioClipsAndSkeletons(this IState state, VisualElement element)
        {
            var generationResults = state.SelectGenerationResult(element);
            var audioClips = generationResults.generatedAudioClips;
            var skeletons = generationResults.generatedSkeletons;
            var fulfilledSkeletons = generationResults.fulfilledSkeletons;

            // Create a HashSet of result URIs for O(1) lookups
            var audioClipUris = new HashSet<string>(
                audioClips
                    .Where(audioClip => audioClip.uri != null)
                    .Select(audioClip => audioClip.uri.GetAbsolutePath())
            );

            // Find skeletons that have been fulfilled and have matching audio clip results
            var skeletonsToExclude = new HashSet<int>();

            foreach (var fulfilled in fulfilledSkeletons)
            {
                // Check if this fulfilled skeleton has a matching audio clip result using O(1) lookup
                if (audioClipUris.Contains(fulfilled.resultUri))
                {
                    skeletonsToExclude.Add(fulfilled.progressTaskID);
                }
            }

            // Filter skeletons to include only those not in the exclude list
            var filteredSkeletons = skeletons.Where(skeleton => !skeletonsToExclude.Contains(skeleton.taskID));

            // Return all audio clip results plus the filtered skeletons
            return filteredSkeletons.Concat(audioClips);
        }

        public static bool HasHistory(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedAudioClips.Count > 0;
        public static AudioClipResult SelectSelectedGeneration(this IState state, VisualElement element) => state.SelectGenerationResult(element).selectedGeneration;
        public static AudioClipResult SelectSelectedGeneration(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).selectedGeneration;
        public static AssetUndoManager SelectAssetUndoManager(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).assetUndoManager;
        public static bool SelectReplaceWithoutConfirmationEnabled(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).replaceWithoutConfirmation;
    }
}
