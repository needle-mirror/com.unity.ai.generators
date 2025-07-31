using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate.OperationSubTypes;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Pbr;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Transform;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using MapType = Unity.AI.Material.Services.Stores.States.MapType;
using Unity.AI.Toolkit.Accounts;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Toolkit;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Generators.Sdk.Logger;
using Random = UnityEngine.Random;

namespace Unity.AI.Material.Services.Stores.Actions.Backend
{
    static class Generation
    {
        public static readonly AsyncThunkCreatorWithArg<GenerateMaterialsData> generateMaterials =
            new($"{GenerationResultsActions.slice}/generateMaterialSuperProxy", GenerateMaterialsAsync);

        static async Task GenerateMaterialsAsync(GenerateMaterialsData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Generating materials.");

            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = arg.generationSetting.SelectVariationCount();
            var refinementMode = generationSetting.SelectRefinementMode();
            if (refinementMode is RefinementMode.Upscale or RefinementMode.Pbr)
            {
                variations = 1;
            }

            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons,
                new(arg.asset, Enumerable.Range(0, variations).Select(i => new MaterialSkeleton(arg.progressTaskId, i)).ToList()));

            var progress = new GenerationProgressData(arg.progressTaskId, variations, 0f);
            api.DispatchProgress(arg.asset, progress with { progress = 0.0f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                api.DispatchInvalidCloudProjectMessage(arg.asset);
                return;
            }

            api.DispatchProgress(arg.asset, progress with { progress = 0.01f }, "Preparing request.");

            var prompt = generationSetting.SelectPrompt();
            var negativePrompt = generationSetting.SelectNegativePrompt();
            var modelID = api.State.SelectSelectedModelID(asset);
            var dimensions = generationSetting.SelectImageDimensionsVector2();
            var patternImageReference = generationSetting.SelectPatternImageReference();
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();

            // clamping is important as the backend will increment the value
            var seed = useCustomSeed ? Math.Clamp(customSeed, 0, int.MaxValue - variations) : Random.Range(0, int.MaxValue - variations);
            var cost = 0;

            Guid.TryParse(modelID, out var generativeModelID);

            var ids = new List<Guid>();
            var materialGenerations = new List<Dictionary<MapType, Guid>>();
            int[] customSeeds = { };

            try
            {
                UploadReferencesData uploadReferences;

                using var progressTokenSource0 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.15f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Uploading references."), 1,
                        progressTokenSource0.Token);

                    uploadReferences = await UploadReferencesAsync(asset, refinementMode, patternImageReference, api);
                }
                catch (HandledFailureException)
                {
                    AbortCleanup(materialGenerations);

                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));

                    // we can simply return without throwing or additional logging because the error is already logged
                    return;
                }
                catch (Exception e)
                {
                    AbortCleanup(materialGenerations);

                    Debug.LogException(e);

                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                    return;
                }
                finally
                {
                    progressTokenSource0.Cancel();
                }

                using var progressTokenSource1 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.15f, 0.25f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request."), 1,
                        progressTokenSource1.Token);

                    using var httpClientLease = HttpClientManager.instance.AcquireLease();

                    var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                        projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                    var imageComponent = builder.ImageComponent();

                    switch (refinementMode)
                    {
                        case RefinementMode.Upscale:
                        {
                            materialGenerations = new List<Dictionary<MapType, Guid>> { new() { [MapType.Preview] = uploadReferences.assetGuid } };

                            var request = ImageTransformRequestBuilder.Initialize();
                            var requests = materialGenerations.Select(m => request.Upscale(new(m[MapType.Preview], 2, null, null))).ToList();
                            var upscaleResults = await EditorTask.Run(() => imageComponent.Transform(requests));

                            if (!upscaleResults.Batch.IsSuccessful)
                            {
                                api.DispatchFailedBatchMessage(arg.asset, upscaleResults);

                                throw new HandledFailureException();
                            }

                            var once = false;
                            foreach (var upscaleResult in upscaleResults.Batch.Value.Where(v => !v.IsSuccessful))
                            {
                                if (!once)
                                {
                                    api.DispatchFailedBatchMessage(arg.asset, upscaleResults);
                                }

                                once = true;

                                api.DispatchFailedMessage(arg.asset, upscaleResult.Error);
                            }

                            ids = upscaleResults.Batch.Value.Where(v => v.IsSuccessful)
                                .Select(r => r.Value.JobId)
                                .ToList();
                            cost = upscaleResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                            materialGenerations = upscaleResults.Batch.Value.Where(v => v.IsSuccessful)
                                .Select(r => r.Value.JobId)
                                .Select(id => new Dictionary<MapType, Guid> { [MapType.Preview] = id })
                                .ToList();
                            generationMetadata.w3CTraceId = upscaleResults.W3CTraceId;
                            break;
                        }
                        case RefinementMode.Pbr:
                        {
                            materialGenerations = new List<Dictionary<MapType, Guid>> { new() { [MapType.Preview] = uploadReferences.assetGuid } };

                            var requests = materialGenerations.Select(m => new Texture2DPbrRequest(generativeModelID, m[MapType.Preview])).ToList();
                            var pbrResults = await EditorTask.Run(() => imageComponent.GeneratePbr(requests));

                            if (!pbrResults.Batch.IsSuccessful)
                            {
                                api.DispatchFailedBatchMessage(arg.asset, pbrResults);

                                throw new HandledFailureException();
                            }

                            var once = false;
                            foreach (var pbrResult in pbrResults.Batch.Value.Where(v => !v.IsSuccessful))
                            {
                                if (!once)
                                {
                                    api.DispatchFailedBatchMessage(arg.asset, pbrResults);
                                }

                                once = true;

                                api.DispatchFailedMessage(arg.asset, pbrResult.Error);
                            }

                            ids = pbrResults.Batch.Value.Where(v => v.IsSuccessful)
                                .SelectMany(r => r.Value.MapResults.Select(mr => mr.JobId))
                                .ToList();
                            cost = pbrResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                            pbrResults.Batch.Value.Where(v => v.IsSuccessful)
                                .SelectMany(batchItemResult => batchItemResult.Value.MapResults,
                                    (batchItemResult, itemResult) => new { batchItemResult, itemResult })
                                .ToList()
                                .ForEach(x => materialGenerations.Find(d => d[MapType.Preview] == x.batchItemResult.Value.Request.ReferenceAsset)
                                    .Add(MapTypeUtils.Parse(x.itemResult.MapType), x.itemResult.JobId));
                            generationMetadata.w3CTraceId = pbrResults.W3CTraceId;
                            break;
                        }
                        case RefinementMode.Generation:
                        {
                            var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, seed)
                                .GenerateWithReference(new TextPrompt(prompt, negativePrompt),
                                    new CompositionReference(uploadReferences.patternGuid, patternImageReference.strength));
                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            var generateResults = await EditorTask.Run(() => imageComponent.Generate(requests));

                            if (!generateResults.Batch.IsSuccessful)
                            {
                                api.DispatchFailedBatchMessage(arg.asset, generateResults);

                                throw new HandledFailureException();
                            }

                            var once = false;
                            foreach (var generateResult in generateResults.Batch.Value.Where(v => !v.IsSuccessful))
                            {
                                if (!once)
                                {
                                    api.DispatchFailedBatchMessage(arg.asset, generateResults);
                                }

                                once = true;

                                api.DispatchFailedMessage(arg.asset, generateResult.Error);
                            }

                            ids = generateResults.Batch.Value.Where(v => v.IsSuccessful)
                                .Select(r => r.Value.JobId)
                                .ToList();
                            cost = generateResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                            materialGenerations = generateResults.Batch.Value.Where(v => v.IsSuccessful)
                                .Select(r => r.Value.JobId)
                                .Select(id => new Dictionary<MapType, Guid> { [MapType.Preview] = id })
                                .ToList();
                            customSeeds = generateResults.Batch.Value.Where(v => v.IsSuccessful).Select(r => r.Value.Request.Seed ?? -1).ToArray();
                            generationMetadata.w3CTraceId = generateResults.W3CTraceId;
                            break;
                        }
                    }

                    if (ids.Count == 0)
                    {
                        throw new HandledFailureException();
                    }
                }
                catch (HandledFailureException)
                {
                    AbortCleanup(materialGenerations);

                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));

                    // we can simply return without throwing or additional logging because the error is already logged
                    return;
                }
                catch (Exception e)
                {
                    AbortCleanup(materialGenerations);

                    Debug.LogException(e);

                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                    return;
                }
                finally
                {
                    progressTokenSource1.Cancel();
                }
            }
            finally
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(arg.asset, true)); // after validation
            }

            /*
             * If you got here, points were consumed so a restore point is saved
             */

            AIToolbarButton.ShowPointsCostNotification(cost);

            var downloadMaterialsData =
                new DownloadMaterialsData(asset, materialGenerations, arg.progressTaskId, Guid.NewGuid(), generationMetadata, customSeeds, false, false);
            GenerationRecovery.AddInterruptedDownload(downloadMaterialsData); // 'potentially' interrupted

            if (WebUtilities.simulateClientSideFailures)
            {
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                throw new Exception("Some simulated client side failure.");
            }

            /* Retry loop. On the last try, retryable is false and we never timeout
               Each download attempt has a reasonable timeout (90 seconds)
               The operation retries up to 6 times on timeout
               The final attempt uses a very long timeout to ensure completion
               If all attempts fail, appropriate error handling occurs
            */
            const int maxRetries = Constants.retryCount;
            var retryCount = 0;
            while (true)
            {
                try
                {
                    downloadMaterialsData = downloadMaterialsData with { retryable = retryCount < maxRetries };
                    await DownloadMaterialsAsync(downloadMaterialsData, api);
                    break;
                }
                catch (DownloadTimeoutException)
                {
                    if (++retryCount > maxRetries)
                    {
                        throw new NotImplementedException(
                            $"The last download attempt ({retryCount}/{maxRetries}) is never supposed to timeout. This is a bug in the code, please report it.");
                    }

                    Debug.Log($"Download timed out. Retrying ({retryCount}/{maxRetries})...");
                }
                catch
                {
                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                    throw;
                }
            }

            return;

            void AbortCleanup(List<Dictionary<MapType, Guid>> canceledMaterialGenerations)
            {
                foreach (var generatedMaterial in canceledMaterialGenerations)
                    GenerationRecovery.RemoveCachedDownload(generatedMaterial[MapType.Preview].ToString());
            }
        }

        record UploadReferencesData(Guid assetGuid, Guid patternGuid);

        static async Task<UploadReferencesData> UploadReferencesAsync(AssetReference asset, RefinementMode refinementMode,
            PatternImageReference patternImageReference, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Uploading references for material.");

            var assetGuid = Guid.Empty;
            var patternGuid = Guid.Empty;

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                defaultOperationTimeout: Constants.noTimeout);
            var assetComponent = builder.AssetComponent();

            switch (refinementMode)
            {
                case RefinementMode.Upscale:
                {
                    await using var assetStream = await ReferenceAssetStream(api.State, asset);
                    var assetStreamWithResult = await assetComponent.StoreAssetWithResult(assetStream, httpClientLease.client);
                    if (!api.DispatchStoreAssetMessage(asset, assetStreamWithResult, out assetGuid))
                    {
                        throw new HandledFailureException();
                    }

                    break;
                }
                case RefinementMode.Pbr:
                {
                    await using var assetStream = await PromptAssetStream(api.State, asset);
                    var assetStreamWithResult = await assetComponent.StoreAssetWithResultPreservingStream(assetStream, httpClientLease.client);
                    if (!api.DispatchStoreAssetMessage(asset, assetStreamWithResult, out assetGuid))
                    {
                        throw new HandledFailureException();
                    }

                    await GenerationRecovery.AddCachedDownload(assetStream, assetGuid.ToString());

                    break;
                }
                case RefinementMode.Generation:
                {
                    if (patternImageReference.asset.IsValid())
                    {
                        var patternStream = await PatternAssetStream(api.State, asset);
                        var patternStreamWithResult = await assetComponent.StoreAssetWithResult(patternStream, httpClientLease.client);
                        if (!api.DispatchStoreAssetMessage(asset, patternStreamWithResult, out patternGuid))
                        {
                            throw new HandledFailureException();
                        }
                    }

                    break;
                }
            }

            return new(assetGuid, patternGuid);
        }

        class HandledFailureException : Exception { }

        class DownloadTimeoutException : Exception { }

        public static readonly AsyncThunkCreatorWithArg<DownloadMaterialsData> downloadMaterials =
            new($"{GenerationResultsActions.slice}/downloadMaterialsSuperProxy", DownloadMaterialsAsync);

        static async Task DownloadMaterialsAsync(DownloadMaterialsData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Downloading materials.");

            var variations = arg.jobIds.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new MaterialSkeleton(arg.progressTaskId, i)).ToList();
            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));

            var progress = new GenerationProgressData(arg.progressTaskId, variations, 0.25f);

            api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                api.DispatchInvalidCloudProjectMessage(arg.asset);
                throw new HandledFailureException();
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server.");

            var retryTimeout = arg.retryable ? Constants.imageRetryTimeout : Constants.noTimeout;

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true,
                defaultOperationTimeout: retryTimeout);
            var assetComponent = builder.AssetComponent();

            using var retryTokenSource = new CancellationTokenSource(retryTimeout);

            Dictionary<Guid, Dictionary<MapType, TextureResult>> generatedTextures;

            using var progressTokenSource3 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.25f, 0.75f,
                    _ => api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server."), variations, progressTokenSource3.Token);

                var jobIdList = arg.jobIds.SelectMany(dictionary => dictionary.Values) // Get all JobId values
                    .Where(jobId => !GenerationRecovery.HasCachedDownload(jobId.ToString())) // Filter out the ones that are already cached
                    .ToList();

                var assetResults = new Dictionary<Guid, OperationResult<BlobAssetResult>>();
                foreach (var guid in jobIdList)
                {
                    // need to be very careful, we're taking each in turn to guarantee paused play mode support
                    // there's not much drawback as the generations are started way before
                    var url = await EditorTask.Run(() =>
                        assetComponent.CreateAssetDownloadUrl(guid, retryTimeout, cancellationToken: retryTokenSource.Token), retryTokenSource.Token);
                    if (retryTokenSource.IsCancellationRequested)
                        throw new OperationCanceledException();
                    assetResults.Add(guid, url);
                }

                var urls = arg.jobIds.ToDictionary(m => m[MapType.Preview], m => m.ToDictionary(kvp => kvp.Key, kvp =>
                {
                    var jobId = kvp.Value;
                    if (GenerationRecovery.HasCachedDownload(jobId.ToString()))
                    {
                        return TextureResult.FromUrl(GenerationRecovery.GetCachedDownloadUrl(jobId.ToString()).GetAbsolutePath());
                    }

                    var result = assetResults[jobId];

                    if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                    {
                        return TextureResult.FromUrl(result.Result.Value.AssetUrl.Url);
                    }

                    if (result.Result.IsSuccessful)
                    {
                        api.DispatchFailedDownloadMessage(arg.asset,
                            new AiOperationFailedResult(AiResultErrorEnum.Unknown, new List<string> { "Simulated server timeout" }));
                    }
                    else
                    {
                        // Add check for cancellation/timeout
                        if (retryTokenSource.IsCancellationRequested && arg.retryable)
                        {
                            throw new DownloadTimeoutException();
                        }

                        api.DispatchFailedDownloadMessage(arg.asset, result, arg.generationMetadata.w3CTraceId);
                    }

                    throw new HandledFailureException();
                }));
                generatedTextures = urls.ToDictionary(m => m.Key, outerPair => outerPair.Value.ToDictionary(kvp => kvp.Key, innerPair => innerPair.Value));
            }
            catch (OperationCanceledException)
            {
                // don't remove skeletons, we will retry
                throw new DownloadTimeoutException();
            }
            catch (DownloadTimeoutException)
            {
                // don't remove skeletons, we will retry
                throw;
            }
            catch (HandledFailureException)
            {
                // we can simply return without throwing or additional logging because the error is already logged
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                return;
            }
            catch
            {
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                progressTokenSource3.Cancel();
            }

            // initial 'backup'
            var backupSuccess = true;
            var assetWasBlank = await arg.asset.IsBlank();
            if (!api.State.HasHistory(arg.asset) && !assetWasBlank)
            {
                backupSuccess = await arg.asset.SaveToGeneratedAssets();
            }

            List<MaterialResult> generatedMaterials;

            // cache
            using var progressTokenSource4 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.99f,
                    value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Downloading results."), 1, progressTokenSource4.Token);

                var generativePath = arg.asset.GetGeneratedAssetsPath();
                var generatedMaterialsWithName = generatedTextures.Select(pair => (name: pair.Key,
                        material: new MaterialResult
                        {
                            textures = new SerializableDictionary<MapType, TextureResult>(pair.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                        }))
                    .ToList();

                generatedMaterials = generatedMaterialsWithName.Select(tuple => tuple.material).ToList();
                var smoothnessTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateSmoothnessFromRoughness);
                var metallicSmoothnessTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateMetallicSmoothnessFromMetallicAndRoughness);
                var nonMetallicSmoothnessTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateNonMetallicSmoothnessFromRoughness);
                var aoTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateAOFromHeight);
                var postProcessTasks = smoothnessTasks.Concat(metallicSmoothnessTasks).Concat(nonMetallicSmoothnessTasks).Concat(aoTasks).ToList();

                await Task.WhenAll(postProcessTasks);

                var maskMapTasks =
                    generatedMaterials.Select(MaterialResultExtensions.GenerateMaskMapFromAOAndMetallicAndRoughness); // ao needs to be completed first
                await Task.WhenAll(maskMapTasks);

                // gather temporary files
                var temporaryFiles = generatedMaterials.SelectMany(m => m.textures.Values.Select(r => r.uri)).Where(uri => uri.IsFile).ToList();

                var metadata = arg.generationMetadata;
                var saveTasks = generatedMaterialsWithName.Select((result, index) =>
                    {
                        var metadataCopy = metadata with { };
                        if (arg.customSeeds.Length > 0 && generatedMaterialsWithName.Count == arg.customSeeds.Length)
                        {
                            metadataCopy.customSeed = arg.customSeeds[index];
                        }

                        return result.material.DownloadToProject($"{result.name}", metadataCopy, generativePath, httpClientLease.client);
                    })
                    .ToList();

                await Task.WhenAll(saveTasks); // saves to project and is picked up by GenerationFileSystemWatcherManipulator

                // cleanup temporary files
                try
                {
                    foreach (var temporaryFile in temporaryFiles)
                    {
                        var path = temporaryFile.GetLocalPath();
                        Debug.Assert(!FileIO.IsFileDirectChildOfFolder(generativePath, path));
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                }
                catch
                {
                    // ignored
                }

                // the ui handles results, skeletons, and fulfilled skeletons to determine which tiles are ready for display (see Selectors.SelectGeneratedTexturesAndSkeletons)
                api.Dispatch(GenerationResultsActions.setFulfilledSkeletons,
                    new(arg.asset, generatedMaterials.Select(res => new FulfilledSkeleton(arg.progressTaskId, res.uri.GetAbsolutePath())).ToList()));
            }
            finally
            {
                progressTokenSource4.Cancel();
            }

            // auto-apply if blank or if it's a PBR
            if (generatedMaterials[0].IsValid() && (assetWasBlank || generatedMaterials[0].IsPbr() || arg.autoApply))
            {
                await api.Dispatch(GenerationResultsActions.selectGeneration, new(arg.asset, generatedMaterials[0], backupSuccess, !assetWasBlank));
                if (assetWasBlank)
                {
                    api.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(arg.asset, true));
                }
            }

            api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Done.");

            // if you got here, no need to keep the potentially interrupted download
            foreach (var generatedMaterial in arg.jobIds) GenerationRecovery.RemoveCachedDownload(generatedMaterial[MapType.Preview].ToString());
            GenerationRecovery.RemoveInterruptedDownload(arg);
        }

        public static async Task<Stream> ReferenceAssetStream(IState state, AssetReference asset) =>
            ImageFileUtilities.CheckImageSize(await state.SelectReferenceAssetStreamWithFallback(asset));

        public static async Task<Stream> PromptAssetStream(IState state, AssetReference asset) =>
            ImageFileUtilities.CheckImageSize(await state.SelectPromptAssetBytesWithFallback(asset));

        public static async Task<Stream> PatternAssetStream(IState state, AssetReference asset) =>
            ImageFileUtilities.CheckImageSize(await state.SelectPatternImageReferenceAssetStream(asset));
    }
}
