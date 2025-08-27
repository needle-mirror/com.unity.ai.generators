using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Undo;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using FileUtilities = Unity.AI.Animate.Services.Utilities.FileUtilities;

namespace Unity.AI.Animate.Services.Stores.Actions
{
    static class GenerationResultsActions
    {
        public static readonly string slice = "generationResults";
        public static Creator<GenerationAnimations> setGeneratedAnimations => new($"{slice}/setGeneratedAnimations");

        static readonly HashSet<string> k_ActiveDownloads = new();
        static readonly SemaphoreSlim k_SetGeneratedAnimationsAsyncSemaphore = new(1, 1);
        public static readonly AsyncThunkCreatorWithArg<GenerationAnimations> setGeneratedAnimationsAsync = new($"{slice}/setGeneratedAnimationsAsync",
            async (payload, api) =>
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Caching generated animations.");

            // Create a 30-second timeout token
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var timeoutToken = cancellationTokenSource.Token;

            var semaphoreAcquired = false;

            int taskID = 0;
            try
            {
                taskID = Progress.Start("Precaching generations.");

                // Wait to acquire the semaphore
                await k_SetGeneratedAnimationsAsyncSemaphore.WaitAsync(timeoutToken).ConfigureAwaitMainThread();
                semaphoreAcquired = true;

                using var _ = new EditorAsyncKeepAliveScope("Caching generated animations : finished waiting for semaphore.");
                await EditorTask.RunOnMainThread(() => PreCacheGeneratedAnimations(payload, taskID, api, timeoutToken), timeoutToken);
            }
            finally
            {
                try
                {
                    api.Dispatch(setGeneratedAnimations, payload);
                }
                finally
                {
                    if (semaphoreAcquired)
                        k_SetGeneratedAnimationsAsyncSemaphore.Release();

                    if (Progress.Exists(taskID))
                        Progress.Finish(taskID);
                }
            }
        });

        static async Task PreCacheGeneratedAnimations(GenerationAnimations payload, int taskID, AsyncThunkApi<bool> api, CancellationToken timeoutToken)
        {
            var timer = Stopwatch.StartNew();
            const float timeoutInSeconds = 2.0f;
            const int minPrecache = 8;
            const int maxInFlight = 4;
            var processedAnimations = 0;
            var inFlightTasks = new List<Task>();

            // Iterate over all animations (assuming payload.animations is ordered by last write time)
            foreach (var animation in payload.animations)
            {
                // Check for timeout cancellation
                timeoutToken.ThrowIfCancellationRequested();

                // After minPrecache is reached, wait until the state indicates a user visible count.
                int precacheCount;
                if (processedAnimations < minPrecache)
                    precacheCount = minPrecache;
                else
                {
                    // Even if we returned with a visible item count we still want to check the count in case the user closed the UI or resized it smaller; to early out.
                    var visibleCount = await WaitForVisibleCount();
                    precacheCount = Math.Max(minPrecache, visibleCount);
                }

                // If we've already processed as many animations as desired by the current target, stop processing.
                if (processedAnimations >= precacheCount)
                    break;

                processedAnimations++;

                // Report progress with current target count.
                precacheCount = Math.Min(payload.animations.Count, precacheCount);
                Progress.Report(taskID, processedAnimations, precacheCount, $"Precaching {precacheCount} generations");

                // Skip animation if it is already cached.
                if (AnimationClipCache.Peek(animation.uri))
                    continue;

                var loadTask = LoadTaskAsync();
                inFlightTasks.Add(loadTask);

                if (inFlightTasks.Count >= maxInFlight)
                {
                    await Task.WhenAny(inFlightTasks);
                    inFlightTasks.RemoveAll(t => t.IsCompleted);
                }

                continue;

                async Task LoadTaskAsync()
                {
                    await EditorTask.Yield();
                    // GetAnimationClip is synchronous when it hits our database, so we yield to let the UI update.
                    _ = await animation.GetAnimationClip();
                }
            }

            if (inFlightTasks.Count > 0)
                await Task.WhenAll(inFlightTasks);

            // Helper function: Wait for up to 2 seconds until UI visible count is > 0.
            async Task<int> WaitForVisibleCount()
            {
                int visible;
                while ((visible = api.State.SelectGeneratedResultVisibleCount(payload.asset)) <= 0 && timer.Elapsed.TotalSeconds < timeoutInSeconds)
                    await EditorTask.Yield();
                return visible;
            }
        }

        public static Creator<GeneratedResultVisibleData> setGeneratedResultVisibleCount => new($"{slice}/setGeneratedResultVisibleCount");

        public static Creator<GenerationSkeletons> setGeneratedSkeletons => new($"{slice}/setGeneratedSkeletons");
        public static Creator<RemoveGenerationSkeletonsData> removeGeneratedSkeletons => new($"{slice}/removeGeneratedSkeletons");

        public static Creator<FulfilledSkeletons> setFulfilledSkeletons => new($"{slice}/setFulfilledSkeletons");
        public static Creator<AsssetContext> pruneFulfilledSkeletons => new($"{slice}/pruneFulfilledSkeletons");
        public static Creator<PromotedGenerationData> setSelectedGeneration => new($"{slice}/setSelectedGeneration");
        public static Creator<AssetUndoData> setAssetUndoManager => new($"{slice}/setAssetUndoManager");
        public static Creator<ReplaceWithoutConfirmationData> setReplaceWithoutConfirmation => new($"{slice}/setReplaceWithoutConfirmation");

        public static readonly AsyncThunkCreator<SelectGenerationData, bool> selectGeneration = new($"{slice}/selectGeneration", async (payload, api) =>
        {
            var replaceAsset = payload.replaceAsset && !payload.result.IsFailed();

            AssetUndoManager assetUndoManager = null;
            if (replaceAsset)
            {
                assetUndoManager = api.State.SelectAssetUndoManager(payload.asset);
                if (!assetUndoManager)
                {
                    assetUndoManager = ScriptableObject.CreateInstance<AssetUndoManager>();
                    assetUndoManager.hideFlags = HideFlags.HideAndDontSave;
                    api.Dispatch(setAssetUndoManager, new AssetUndoData(payload.asset, assetUndoManager));
                    assetUndoManager.EndRecord(payload.asset, api.State.SelectSelectedGeneration(payload.asset), true); // record initial
                }
            }

            var result = false;
            if (!FileIO.AreFilesIdentical(payload.asset.GetPath(), payload.result.uri.LocalPath))
            {
                var replaceWithoutConfirmation = api.State.SelectReplaceWithoutConfirmationEnabled(payload.asset);
                if (replaceAsset && (!payload.askForConfirmation || await DialogUtilities.ConfirmReplaceAsset(payload.asset, replaceWithoutConfirmation,
                        b => replaceWithoutConfirmation = b, payload.result.uri.LocalPath)))
                {
                    Debug.Assert(assetUndoManager != null);
                    assetUndoManager.BeginRecord(payload.asset);
                    if (await payload.asset.ReplaceAsync(payload.result))
                    {
                        AssetDatabase.ImportAsset(payload.asset.GetPath(), ImportAssetOptions.ForceUpdate);
                        api.Dispatch(setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(payload.asset, replaceWithoutConfirmation));
                        assetUndoManager.EndRecord(payload.asset, payload.result);
                        result = true;
                    }
                }
            }

            // set late because asset import clears the selection
            api.Dispatch(setSelectedGeneration, new PromotedGenerationData(payload.asset, payload.result));

            return result;
        });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> checkDownloadRecovery = new($"{slice}/checkDownloadRecovery", async (asset, api) =>
        {
            var option = 0;

            var interruptedDownloads = GenerationRecovery.GetInterruptedDownloads(asset)
                .Where(d => string.IsNullOrEmpty(d.uniqueTaskId) || !k_ActiveDownloads.Contains(d.uniqueTaskId))
                .ToList();

            if (!await DialogUtilities.ShowResumeDownloadPopup(interruptedDownloads, op => option = op))
                return;

            switch (option)
            {
                case 0: // "Resume" selected
                    foreach (var data in interruptedDownloads)
                    {
                        if (!data.asset.IsValid())
                        {
                            Debug.LogWarning($"Unable to resume download for asset: {data.asset.GetPath()}");
                            continue;
                        }

                        _ = ResumeDownload();
                        continue;

                        async Task ResumeDownload()
                        {
                            var uniqueTaskId = data.uniqueTaskId;
                            if (!string.IsNullOrEmpty(uniqueTaskId))
                            {
                                if (!k_ActiveDownloads.Add(uniqueTaskId))
                                    return;
                            }

                            try
                            {
                                await api.Dispatch(downloadAnimationsMain,
                                    new DownloadAnimationsData(data.asset,
                                        data.ids.Select(Guid.Parse).ToList(),
                                        data.sessionId == GenerationRecoveryUtils.sessionId ? data.taskId : -1,
                                        string.IsNullOrEmpty(data.uniqueTaskId) ? Guid.Empty : Guid.Parse(data.uniqueTaskId),
                                        data.generationMetadata,
                                        data.customSeeds.ToArray(),
                                        false, false),
                                    CancellationToken.None);
                            }
                            finally
                            {
                                if (!string.IsNullOrEmpty(uniqueTaskId))
                                {
                                    k_ActiveDownloads.Remove(uniqueTaskId);
                                }
                            }
                        }
                    }

                    break;
                case 1: // "Delete" selected
                    var generativePath = asset.GetGeneratedAssetsPath();
                    foreach (var data in interruptedDownloads)
                    {
                        foreach (var jobId in data.ids)
                        {
                            var generationResult = AnimationClipResult.FromUrl(FileUtilities.GetFailedAnimationUrl(jobId));
                            await generationResult.CopyToProject(data.generationMetadata, generativePath);
                        }
                        GenerationRecovery.RemoveInterruptedDownload(data);
                    }

                    break;
                case 2: // "Skip" selected
                    // Do nothing
                    break;
            }
        });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> quoteAnimationsMain = new($"{slice}/quoteAnimationsMain",
            async (asset, api) =>
            {
                try { await api.Dispatch(Backend.Quote.quoteAnimations, new(asset, api.State.SelectGenerationSetting(asset))); }
                catch (OperationCanceledException) { /* ignored */ }
            });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> generateAnimationsMain = new($"{slice}/generateAnimationsMain", async (asset, api) =>
        {
            var label = api.State.SelectGenerationSetting(asset).prompt;
            var mode = api.State.SelectRefinementMode(asset);
            if (string.IsNullOrEmpty(label))
                label = "reference";
            var progressTaskId = Progress.Start($"Generating with {(mode == RefinementMode.TextToMotion ? label : "reference")}.");
            SkeletonExtensions.Acquire(progressTaskId);
            var uniqueTaskId = Guid.NewGuid();
            var uniqueTaskIdString = uniqueTaskId.ToString();
            k_ActiveDownloads.Add(uniqueTaskIdString);
            try
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(asset, false));
                var generationSetting = api.State.SelectGenerationSetting(asset);
                api.Dispatch(ModelSelectorActions.setLastUsedSelectedModelID, generationSetting.SelectSelectedModelID());
                await api.Dispatch(
                    Backend.Generation.generateAnimations, new(asset, generationSetting, progressTaskId, uniqueTaskId: uniqueTaskId),
                    CancellationToken.None);
            }
            finally
            {
                k_ActiveDownloads.Remove(uniqueTaskIdString);
                Progress.Finish(progressTaskId);
                SkeletonExtensions.Release(progressTaskId);
            }
        });

        public static readonly AsyncThunkCreatorWithArg<DownloadAnimationsData> downloadAnimationsMain = new($"{slice}/downloadAnimationsMain", async (arg, api) =>
        {
            var progressTaskId = Progress.Exists(arg.progressTaskId) ? arg.progressTaskId : Progress.Start($"Resuming download for asset {arg.asset.GetPath()}.");
            SkeletonExtensions.Acquire(progressTaskId);
            try
            {
                await api.Dispatch(Backend.Generation.downloadAnimationClips, arg with { progressTaskId = progressTaskId }, CancellationToken.None);
            }
            finally
            {
                Progress.Finish(progressTaskId);
                SkeletonExtensions.Release(progressTaskId);
            }
        });
    }
}
