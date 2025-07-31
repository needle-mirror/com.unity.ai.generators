using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Undo;
using Unity.AI.Image.Services.Utilities;
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
using FileUtilities = Unity.AI.Image.Services.Utilities.FileUtilities;
using Task = System.Threading.Tasks.Task;

namespace Unity.AI.Image.Services.Stores.Actions
{
    static class GenerationResultsActions
    {
        public static readonly string slice = "generationResults";
        public static Creator<GenerationTextures> setGeneratedTextures => new($"{slice}/setGeneratedTextures");

        static readonly SemaphoreSlim k_SetGeneratedTexturesAsyncSemaphore = new(1, 1);
        public static readonly AsyncThunkCreatorWithArg<GenerationTextures> setGeneratedTexturesAsync = new($"{slice}/setGeneratedTexturesAsync",
            async (payload, api) =>
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Caching generated images.");

            // Create a 30-second timeout token
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var timeoutToken = cancellationTokenSource.Token;

            int taskID = 0;
            try
            {
                taskID = Progress.Start("Precaching generations.");

                // Wait to acquire the semaphore
                await k_SetGeneratedTexturesAsyncSemaphore.WaitAsync();
                await EditorTask.RunOnMainThread(() => PreCacheGeneratedTextures(payload, taskID, api, timeoutToken), timeoutToken);
            }
            finally
            {
                try
                {
                    api.Dispatch(setGeneratedTextures, payload);
                }
                finally
                {
                    k_SetGeneratedTexturesAsyncSemaphore.Release();
                    if (Progress.Exists(taskID))
                        Progress.Finish(taskID);
                }
            }
        });

        static async Task PreCacheGeneratedTextures(GenerationTextures payload, int taskID, AsyncThunkApi<bool> api, CancellationToken timeoutToken)
        {
            var timer = Stopwatch.StartNew();
            const float timeoutInSeconds = 2.0f;
            const int minPrecache = 8;
            const int maxInFlight = 4;
            var processedTextures = 0;
            var inFlightTasks = new List<Task>();

            // Iterate over all textures (assuming payload.textures is ordered by last write time)
            foreach (var texture in payload.textures)
            {
                // Check for timeout cancellation
                timeoutToken.ThrowIfCancellationRequested();

                // After minPrecache is reached, wait until the state indicates a user visible count.
                int precacheCount;
                if (processedTextures < minPrecache)
                    precacheCount = minPrecache;
                else
                {
                    // Even if we returned with a visible item count we still want to check the count in case the user closed the UI or resized it smaller; to early out.
                    var visibleCount = await WaitForVisibleCount();
                    precacheCount = Math.Max(minPrecache, visibleCount);
                }

                // If we've already processed as many textures as desired by the current target, stop processing.
                if (processedTextures >= precacheCount)
                    break;

                processedTextures++;

                // Report progress with current target count.
                precacheCount = Math.Min(payload.textures.Count, precacheCount);
                Progress.Report(taskID, processedTextures, precacheCount, $"Precaching {precacheCount} generations");

                // Skip texture if it is already cached.
                if (TextureCache.Peek(texture.uri))
                    continue;

                var loadTask = TextureCache.GetTexture(texture.uri);
                inFlightTasks.Add(loadTask);

                if (inFlightTasks.Count >= maxInFlight)
                {
                    await Task.WhenAny(inFlightTasks);
                    inFlightTasks.RemoveAll(t => t.IsCompleted);
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
        public static Creator<SelectedGenerationData> setSelectedGeneration => new($"{slice}/setSelectedGeneration");
        public static Creator<AssetUndoData> setAssetUndoManager => new($"{slice}/setAssetUndoManager");
        public static readonly Creator<AssetReference> incrementGenerationCount = new($"{slice}/incrementGenerationCount");
        public static Creator<ReplaceWithoutConfirmationData> setReplaceWithoutConfirmation => new($"{slice}/setReplaceWithoutConfirmation");
        public static Creator<PromoteNewAssetPostActionData> setPromoteNewAssetPostAction => new($"{slice}/setPromoteNewAssetPostAction");

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
                    api.Dispatch(setAssetUndoManager, new (payload.asset, assetUndoManager));
                    assetUndoManager.EndRecord(payload.asset, api.State.SelectSelectedGeneration(payload.asset), true); // record initial
                }
            }

            var result = false;

            var options = new FileComparisonOptions(payload.result.uri.LocalPath, payload.asset.GetPath(), getBytes1: true);
            if (!FileIO.AreFilesIdentical(options))
            {
                var replaceWithoutConfirmation = api.State.SelectReplaceWithoutConfirmationEnabled(payload.asset);
                if (replaceAsset && (!payload.askForConfirmation || await DialogUtilities.ConfirmReplaceAsset(payload.asset, replaceWithoutConfirmation,
                        b => replaceWithoutConfirmation = b, payload.result.uri.LocalPath)))
                {
                    Debug.Assert(assetUndoManager != null);
                    assetUndoManager.BeginRecord(payload.asset);
                    if (await payload.asset.Replace(payload.result))
                    {
                        AssetDatabase.ImportAsset(payload.asset.GetPath(), ImportAssetOptions.ForceUpdate);
                        api.Dispatch(setReplaceWithoutConfirmation, new (payload.asset, replaceWithoutConfirmation));
                        assetUndoManager.EndRecord(payload.asset, payload.result);
                        result = true;
                    }
                }
            }

            // run detached because await GetFile is likely to block the main thread when application is out of focus
            var unsavedBytes = options.bytes1;
            if (unsavedBytes != null)
                api.Dispatch(GenerationSettingsActions.setUnsavedAssetBytes, new(payload.asset, unsavedBytes, payload.result));
            else
            {
                async Task FireAndForget()
                {
                    unsavedBytes = await payload.result.GetFile();
                    api.Dispatch(GenerationSettingsActions.setUnsavedAssetBytes, new(payload.asset, unsavedBytes, payload.result));
                }

                _ = FireAndForget();
            }

            // set late because asset import clears the selection
            api.Dispatch(setSelectedGeneration, new (payload.asset, payload.result));

            return result;
        });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> checkDownloadRecovery = new($"{slice}/checkDownloadRecovery", async (asset, api) =>
        {
            var option = 0;

            var interruptedDownloads = GenerationRecovery.GetInterruptedDownloads(asset);
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

                        _ = api.Dispatch(downloadImagesMain,
                            new DownloadImagesData(
                                data.asset,
                                data.ids.Select(Guid.Parse).ToList(),
                                data.sessionId == GenerationRecoveryUtils.sessionId ? data.taskId : -1,
                                string.IsNullOrEmpty(data.uniqueTaskId) ? Guid.Empty : Guid.Parse(data.uniqueTaskId),
                                data.generationMetadata,
                                data.customSeeds.ToArray(),
                                false,
                                false,
                                false, false),
                            CancellationToken.None);
                    }
                    break;
                case 1: // "Delete" selected
                    var generativePath = asset.GetGeneratedAssetsPath();
                    foreach (var data in interruptedDownloads)
                    {
                        foreach (var jobId in data.ids)
                        {
                            var generationResult = TextureResult.FromUrl(FileUtilities.GetFailedImageUrl(jobId));
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

        public static readonly AsyncThunkCreatorWithArg<AssetReference> quoteImagesMain = new($"{slice}/quoteImagesMain",
            async (asset, api) =>
            {
                try { await api.Dispatch(Backend.Quote.quoteImages, new(asset, api.State.SelectGenerationSetting(asset))); }
                catch (OperationCanceledException) { /* ignored */ }
            });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> generateImagesMain = new($"{slice}/generateImagesMain", async (asset, api) =>
        {
            var progressTaskId = Progress.Start($"{api.State.SelectProgressLabel(asset)}.");
            SkeletonExtensions.Acquire(progressTaskId);
            try
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(asset, false));
                var generationSetting = api.State.SelectGenerationSetting(asset);
                api.Dispatch(ModelSelectorActions.setLastUsedSelectedModelID, generationSetting.SelectSelectedModelID());
                await api.Dispatch(Backend.Generation.generateImages, new(asset, generationSetting, progressTaskId), CancellationToken.None);
            }
            finally
            {
                Progress.Finish(progressTaskId);
                SkeletonExtensions.Release(progressTaskId);
            }
        });

        public static readonly AsyncThunkCreatorWithArg<DownloadImagesData> downloadImagesMain = new($"{slice}/downloadImagesMain", async (arg, api) =>
        {
            var progressTaskId = Progress.Exists(arg.progressTaskId) ? arg.progressTaskId : Progress.Start($"Resuming download for asset {arg.asset.GetPath()}.");
            SkeletonExtensions.Acquire(progressTaskId);
            try
            {
                await api.Dispatch(Backend.Generation.downloadImages, arg with { progressTaskId = progressTaskId }, CancellationToken.None);
            }
            finally
            {
                Progress.Finish(progressTaskId);
                SkeletonExtensions.Release(progressTaskId);
            }
        });
    }
}
