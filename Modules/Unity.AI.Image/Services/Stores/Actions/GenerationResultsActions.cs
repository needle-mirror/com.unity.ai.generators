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
using Unity.AI.ImageEditor.Services.Utilities;
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

        static bool s_SetGeneratedTexturesAsyncMutex = false;
        public static readonly AsyncThunkCreatorWithArg<GenerationTextures> setGeneratedTexturesAsync = new($"{slice}/setGeneratedTexturesAsync",
            async (payload, api) =>
        {
            // Wait if another invocation is already running.
            while (s_SetGeneratedTexturesAsyncMutex)
                await EditorTask.Yield();

            s_SetGeneratedTexturesAsyncMutex = true;
            var taskID = Progress.Start("Precaching generations.");
            try
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
            finally
            {
                try
                {
                    api.Dispatch(setGeneratedTextures, payload);
                }
                finally
                {
                    s_SetGeneratedTexturesAsyncMutex = false;
                    Progress.Finish(taskID);
                }
            }
        });
        public static Creator<GeneratedResultVisibleData> setGeneratedResultVisibleCount => new($"{slice}/setGeneratedResultVisibleCount");

        public static Creator<GenerationSkeletons> setGeneratedSkeletons => new($"{slice}/setGeneratedSkeletons");
        public static Creator<RemoveGenerationSkeletonsData> removeGeneratedSkeletons => new($"{slice}/removeGeneratedSkeletons");
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

            // set late because asset import clears the selection
            api.Dispatch(GenerationSettingsActions.setUnsavedAssetBytes, new (payload.asset, options.bytes1 ?? await payload.result.GetFile(), payload.result));
            api.Dispatch(setSelectedGeneration, new (payload.asset, payload.result));

            return result;
        });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> checkDownloadRecovery = new($"{slice}/checkDownloadRecovery", async (asset, api) =>
        {
            var option = 0;

            var interruptedDownloads = GenerationRecoveryUtils.GetInterruptedDownloads(asset);
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

                        await api.Dispatch(downloadImagesMain,
                            new DownloadImagesData(data.asset, data.ids.Select(Guid.Parse).ToList(), data.taskId, data.generationMetadata, false, false, false, data.customSeeds.ToArray()), CancellationToken.None);
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
                        GenerationRecoveryUtils.RemoveInterruptedDownload(data);
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
                try { await api.Dispatch(GenerationResultsSuperProxyActions.quoteImages, new(asset, api.State.SelectGenerationSetting(asset))); }
                catch (OperationCanceledException) { /* ignored */ }
            });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> generateImagesMain = new($"{slice}/generateImagesMain", async (asset, api) =>
        {
            var taskID = Progress.Start($"{api.State.SelectProgressLabel(asset)}.");
            SkeletonExtensions.Acquire(taskID);
            try
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(asset, false));
                var generationSetting = api.State.SelectGenerationSetting(asset);
                api.Dispatch(ModelSelectorActions.setLastUsedSelectedModelID, generationSetting.SelectSelectedModelID());
                await api.Dispatch(GenerationResultsSuperProxyActions.generateImages, new(asset, generationSetting, taskID), CancellationToken.None);
            }
            finally
            {
                Progress.Finish(taskID);
                SkeletonExtensions.Release(taskID);

                api.Dispatch(removeGeneratedSkeletons, new(asset, taskID));
            }
        });

        public static readonly AsyncThunkCreatorWithArg<DownloadImagesData> downloadImagesMain = new($"{slice}/downloadImagesMain", async (arg, api) =>
        {
            var taskID = Progress.Exists(arg.taskID) ? arg.taskID : Progress.Start($"Resuming download for asset {arg.asset.GetPath()}.");
            SkeletonExtensions.Acquire(taskID);
            try
            {
                await api.Dispatch(GenerationResultsSuperProxyActions.downloadImages, new DownloadImagesData(arg.asset, arg.ids, taskID, arg.generationMetadata, false, false, false, arg.customSeeds), CancellationToken.None);
            }
            finally
            {
                Progress.Finish(taskID);
                SkeletonExtensions.Release(taskID);

                api.Dispatch(removeGeneratedSkeletons, new(arg.asset, taskID));
            }
        });
    }
}
