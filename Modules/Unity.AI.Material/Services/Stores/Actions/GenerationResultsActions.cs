using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Undo;
using Unity.AI.Material.Services.Utilities;
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
using FileUtilities = Unity.AI.Material.Services.Utilities.FileUtilities;
using MapType = Unity.AI.Material.Services.Stores.States.MapType;

namespace Unity.AI.Material.Services.Stores.Actions
{
    static class GenerationResultsActions
    {
        public static readonly string slice = "generationResults";
        public static Creator<GenerationMaterials> setGeneratedMaterials => new($"{slice}/setGeneratedMaterials");

        static readonly HashSet<string> k_ActiveDownloads = new();
        static readonly SemaphoreSlim k_SetGeneratedMaterialsAsyncSemaphore = new(1, 1);
        public static readonly AsyncThunkCreatorWithArg<GenerationMaterials> setGeneratedMaterialsAsync = new($"{slice}/setGeneratedMaterialsAsync",
            async (payload, api) =>
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Caching generated materials.");

            // Create a 30-second timeout token
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var timeoutToken = cancellationTokenSource.Token;

            var semaphoreAcquired = false;

            var taskID = 0;
            try
            {
                taskID = Progress.Start("Precaching generations.");

                // Wait to acquire the semaphore
                await k_SetGeneratedMaterialsAsyncSemaphore.WaitAsync(timeoutToken).ConfigureAwaitMainThread();
                semaphoreAcquired = true;

                using var _ = new EditorAsyncKeepAliveScope("Caching generated materials : finished waiting for semaphore.");
                await EditorTask.RunOnMainThread(() => PreCacheGeneratedMaterials(payload, taskID, api, timeoutToken), timeoutToken);
            }
            finally
            {
                try
                {
                    api.Dispatch(setGeneratedMaterials, payload);
                }
                finally
                {
                    if (semaphoreAcquired)
                        k_SetGeneratedMaterialsAsyncSemaphore.Release();
                    if (Progress.Exists(taskID))
                        Progress.Finish(taskID);
                }
            }
        });

        static async Task PreCacheGeneratedMaterials(GenerationMaterials payload, int taskID, AsyncThunkApi<bool> api, CancellationToken timeoutToken)
        {
            var timer = Stopwatch.StartNew();
            const float timeoutInSeconds = 2.0f;
            const int minPrecache = 4; // material import is a bit heavy
            const int maxInFlight = 4;
            var processedMaterials = 0;
            var inFlightTasks = new List<Task>();

            // Iterate over all materials (assuming payload.materials is ordered by last write time)
            foreach (var material in payload.materials)
            {
                // Check for timeout cancellation
                timeoutToken.ThrowIfCancellationRequested();

                // After minPrecache is reached, wait until the state indicates a user visible count.
                int precacheCount;
                if (processedMaterials < minPrecache)
                    precacheCount = minPrecache;
                else
                {
                    // Even if we returned with a visible item count we still want to check the count in case the user closed the UI or resized it smaller; to early out.
                    var visibleCount = await WaitForVisibleCount();
                    precacheCount = Math.Max(minPrecache, visibleCount);
                }

                // If we've already processed as many materials as desired by the current target, stop processing.
                if (processedMaterials >= precacheCount)
                    break;

                processedMaterials++;

                // Report progress with current target count.
                precacheCount = Math.Min(payload.materials.Count, precacheCount);
                Progress.Report(taskID, processedMaterials, precacheCount, $"Precaching {precacheCount} generations");

                // Skip material if it is already cached.
                if (MaterialCacheHelper.Peek(material))
                    continue;

                var loadTask = MaterialCacheHelper.Precache(material);
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
        public static Creator<AsssetContext> pruneFulfilledSkeletons => new($"{slice}/pruneFulfilledSkeletons");
        public static Creator<PromotedGenerationData> setSelectedGeneration => new($"{slice}/setSelectedGeneration");
        public static Creator<GenerationMaterialMappingData> setGeneratedMaterialMapping => new($"{slice}/setGeneratedMaterialMapping");
        public static Creator<AssetUndoData> setAssetUndoManager => new($"{slice}/setAssetUndoManager");
        public static Creator<ReplaceWithoutConfirmationData> setReplaceWithoutConfirmation => new($"{slice}/setReplaceWithoutConfirmation");

        public static async Task<bool> CopyToAsync(IState state, MaterialResult generatedMaterial, AssetReference asset,
            Dictionary<MapType, string> generatedMaterialMapping)
        {
            var sourceFileName = generatedMaterial.uri.GetLocalPath();
            if (!File.Exists(sourceFileName))
                return false;

            var destFileName = asset.GetPath();
            if (!Path.GetExtension(destFileName).Equals(Path.GetExtension(sourceFileName), StringComparison.OrdinalIgnoreCase))
            {
                var destMaterial = asset.GetMaterialAdapter();
                if (await generatedMaterial.CopyToAsync(destMaterial, state, generatedMaterialMapping))
                    destMaterial.AsObject.SafeCall(AssetDatabase.SaveAssetIfDirty);
            }
            else
            {
                await FileIO.CopyFileAsync(sourceFileName, destFileName, true);
                AssetDatabase.ImportAsset(asset.GetPath(), ImportAssetOptions.ForceUpdate);
                asset.FixObjectName();
            }
            asset.EnableGenerationLabel();

            return true;
        }

        public static bool CopyTo(IState state, MaterialResult generatedMaterial, AssetReference asset, Dictionary<MapType, string> generatedMaterialMapping)
        {
            var sourceFileName = generatedMaterial.uri.GetLocalPath();
            if (!File.Exists(sourceFileName))
                return false;

            var destFileName = asset.GetPath();
            if (!Path.GetExtension(destFileName).Equals(Path.GetExtension(sourceFileName), StringComparison.OrdinalIgnoreCase))
            {
                var destMaterial = asset.GetMaterialAdapter();
                if (generatedMaterial.CopyTo(destMaterial, state, generatedMaterialMapping))
                    destMaterial.AsObject.SafeCall(AssetDatabase.SaveAssetIfDirty);
            }
            else
            {
                FileIO.CopyFile(sourceFileName, destFileName, true);
                AssetDatabase.ImportAsset(asset.GetPath(), ImportAssetOptions.ForceUpdate);
                asset.FixObjectName();
            }
            asset.EnableGenerationLabel();

            return true;
        }

        public static async Task<bool> ReplaceAsync(IState state, AssetReference asset, MaterialResult generatedMaterial,
            Dictionary<MapType, string> generatedMaterialMapping)
        {
            if (await CopyToAsync(state, generatedMaterial, asset, generatedMaterialMapping))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

        public static bool Replace(IState state, AssetReference asset, MaterialResult generatedMaterial, Dictionary<MapType, string> generatedMaterialMapping)
        {
            if (CopyTo(state, generatedMaterial, asset, generatedMaterialMapping))
            {
                asset.EnableGenerationLabel();
                return true;
            }

            return false;
        }

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

            var materialMapping = api.State.SelectGeneratedMaterialMapping(payload.asset);
            if (!FileIO.AreFilesIdentical(payload.asset.GetPath(), payload.result.uri.LocalPath) &&
                !payload.result.AreMapsIdentical(payload.asset, materialMapping))
            {
                var replaceWithoutConfirmation = api.State.SelectReplaceWithoutConfirmationEnabled(payload.asset);
                if (replaceAsset && (!payload.askForConfirmation || await DialogUtilities.ConfirmReplaceAsset(payload.asset, replaceWithoutConfirmation,
                        b => replaceWithoutConfirmation = b, payload.result.uri.LocalPath)))
                {
                    Debug.Assert(assetUndoManager != null);
                    assetUndoManager.BeginRecord(payload.asset);
                    if (await ReplaceAsync(api.api.State, payload.asset, payload.result, materialMapping))
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

        public static readonly AsyncThunkCreatorWithArg<AutodetectMaterialMappingData> autodetectMaterialMapping = new($"{slice}/autodetectMaterialMapping", (payload, api) =>
        {
            foreach (MapType mapType in Enum.GetValues(typeof(MapType)))
            {
                if (mapType == MapType.Preview)
                    continue;
                var (found, materialProperty) = api.api.State.GetTexturePropertyName(payload.asset, mapType, payload.force);
                api.Dispatch(setGeneratedMaterialMapping,
                    new GenerationMaterialMappingData(payload.asset, mapType, found ? materialProperty : GenerationResult.noneMapping));
            }

            return Task.CompletedTask;
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
                                await api.Dispatch(downloadMaterialsMain,
                                    new DownloadMaterialsData(data.asset,
                                        data.ids.ConvertIds(),
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
                        foreach (var dict in data.ids)
                        {
                            if (!dict.TryGetValue((int)MapType.Preview, out var jobId))
                            {
                                Debug.LogError($"Unable to find preview image for material '{string.Join(",", dict.Keys)}'.");
                                continue;
                            }

                            var generationResult = MaterialResult.FromPreview(TextureResult.FromUrl(FileUtilities.GetFailedImageUrl(jobId)));
                            await generationResult.CopyToProject(jobId, data.generationMetadata, generativePath);
                        }
                        GenerationRecovery.RemoveInterruptedDownload(data);
                    }
                    break;
                case 2: // "Skip" selected
                    // Do nothing
                    break;
            }
        });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> quoteMaterialsMain = new($"{slice}/quoteMaterialsMain",
            async (asset, api) =>
            {
                try { await api.Dispatch(Backend.Quote.quoteMaterials, new(asset, api.State.SelectGenerationSetting(asset))); }
                catch (OperationCanceledException) { /* ignored */ }
            });

        public static readonly AsyncThunkCreatorWithArg<AssetReference> generateMaterialsMain = new($"{slice}/generateMaterialsMain", async (asset, api) =>
        {
            var label = api.State.SelectGenerationSetting(asset).prompt;
            if (string.IsNullOrEmpty(label))
                label = "reference";
            var progressTaskId = Progress.Start($"Generating with {label}.");
            SkeletonExtensions.Acquire(progressTaskId);
            var uniqueTaskId = Guid.NewGuid();
            var uniqueTaskIdString = uniqueTaskId.ToString();
            k_ActiveDownloads.Add(uniqueTaskIdString);
            try
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(asset, false));
                var generationSetting = api.State.SelectGenerationSetting(asset);
                api.Dispatch(ModelSelectorActions.setLastUsedSelectedModelID, generationSetting.SelectSelectedModelID());
                await api.Dispatch(Backend.Generation.generateMaterials,
                    new(asset, generationSetting, progressTaskId, uniqueTaskId: uniqueTaskId), CancellationToken.None);
            }
            finally
            {
                k_ActiveDownloads.Remove(uniqueTaskIdString);
                Progress.Finish(progressTaskId);
                SkeletonExtensions.Release(progressTaskId);
            }
        });

        public static readonly AsyncThunkCreatorWithArg<DownloadMaterialsData> downloadMaterialsMain = new($"{slice}/downloadMaterialsMain", async (arg, api) =>
        {
            var progressTaskId = Progress.Exists(arg.progressTaskId) ? arg.progressTaskId : Progress.Start($"Resuming download for asset {arg.asset.GetPath()}.");
            SkeletonExtensions.Acquire(progressTaskId);
            try
            {
                await api.Dispatch(Backend.Generation.downloadMaterials, arg with { progressTaskId = progressTaskId }, CancellationToken.None);
            }
            finally
            {
                Progress.Finish(progressTaskId);
                SkeletonExtensions.Release(progressTaskId);
            }
        });
    }
}
