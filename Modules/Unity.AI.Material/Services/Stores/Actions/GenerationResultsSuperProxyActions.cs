﻿using System;
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
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Generators.Sdk.Logger;
using Random = UnityEngine.Random;

namespace Unity.AI.Material.Services.Stores.Actions
{
    static class GenerationResultsSuperProxyActions
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();

        public static readonly AsyncThunkCreatorWithArg<QuoteMaterialsData> quoteMaterials = new($"{GenerationResultsActions.slice}/quoteMaterialsSuperProxy", async (arg, api) =>
        {
            if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var existingTokenSource))
            {
                existingTokenSource.Cancel();
                existingTokenSource.Dispose();
            }
            k_QuoteCancellationTokenSources[arg.asset] = arg.cancellationTokenSource;

            try
            {
                SendValidatingMessage();

                var success = await WebUtilities.WaitForCloudProjectSettings(arg.asset);

                if (arg.cancellationTokenSource.IsCancellationRequested)
                {
                    SendValidatingMessage();
                    return;
                }

                if (!success)
                {
                    var messages = new[] { $"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\"." };
                    api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(false, AiResultErrorEnum.Unknown, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var asset = new AssetReference { guid = arg.asset.guid };

                if (arg.cancellationTokenSource.IsCancellationRequested)
                {
                    SendValidatingMessage();
                    return;
                }

                if (!asset.Exists())
                {
                    var messages = new[] { $"Error reason is 'Invalid Asset'." };
                    api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(false, AiResultErrorEnum.Unknown, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                using var httpClientLease = HttpClientManager.instance.AcquireLease();
                var generationSetting = arg.generationSetting;

                var variations = arg.generationSetting.SelectVariationCount();
                var refinementMode = generationSetting.SelectRefinementMode();
                if (refinementMode is RefinementMode.Upscale or RefinementMode.Pbr)
                    variations = 1;
                var prompt = generationSetting.SelectPrompt();
                var negativePrompt = generationSetting.SelectNegativePrompt();
                var modelID = api.State.SelectSelectedModelID(asset);
                var dimensions = generationSetting.SelectImageDimensionsVector2();
                var patternImageReference = generationSetting.SelectPatternImageReference();
                var seed = Random.Range(0, int.MaxValue - variations);
                Guid.TryParse(modelID, out var generativeModelID);
                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);
                var imageComponent = builder.ImageComponent();

                var assetGuid = Guid.NewGuid();

                OperationResult<QuoteResponse> quoteResults = null;

                switch (refinementMode)
                {
                    case RefinementMode.Upscale:
                    {
                        var materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var request = ImageTransformRequestBuilder.Initialize();
                        var requests = materialGenerations.Select(m => request.Upscale(new(m[MapType.Preview], 2, null, null))).ToList();
                        quoteResults = await imageComponent.TransformQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Pbr:
                    {
                        var materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var requests = materialGenerations.Select(m => new Texture2DPbrRequest(generativeModelID, m[MapType.Preview])).ToList();
                        quoteResults = await imageComponent.GeneratePbrQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        var patternGuid = Guid.Empty;
                        if (patternImageReference.asset.IsValid())
                            patternGuid = Guid.NewGuid();

                        var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, seed)
                            .GenerateWithReference(new TextPrompt(prompt, negativePrompt),
                                new CompositionReference(patternGuid, patternImageReference.strength));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await imageComponent.GenerateQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                }

                if (arg.cancellationTokenSource.IsCancellationRequested)
                {
                    SendValidatingMessage();
                    return;
                }

                if (quoteResults == null)
                    return;

                if (!quoteResults.Result.IsSuccessful)
                {
                    var messages = quoteResults.Result.Error.Errors.Count == 0
                        ? new[] { $"Error reason is '{quoteResults.Result.Error.AiResponseError.ToString()}' and no additional error information was provided ({WebUtils.selectedEnvironment})." }
                        : quoteResults.Result.Error.Errors.Distinct().Select(m => $"{quoteResults.Result.Error.AiResponseError.ToString()}: {m}").ToArray();
                    api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(quoteResults.Result.IsSuccessful, quoteResults.Result.Error.AiResponseError, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }
                api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(quoteResults.Result.IsSuccessful,
                            !quoteResults.Result.IsSuccessful ? quoteResults.Result.Error.AiResponseError : AiResultErrorEnum.Unknown,
                            quoteResults.Result.Value.PointsCost, new List<GenerationFeedbackData>())));
            }
            finally
            {
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == arg.cancellationTokenSource)
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                arg.cancellationTokenSource.Dispose();
            }

            void SendValidatingMessage()
            {
                var messages = new[] { "Validating generation inputs..." };
                api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(false, AiResultErrorEnum.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
            }
        });

        public static readonly AsyncThunkCreatorWithArg<GenerateMaterialsData> generateMaterialsSuperProxy = new($"{GenerationResultsActions.slice}/generateMaterialSuperProxy", async (arg, api) =>
        {
            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = arg.generationSetting.SelectVariationCount();
            var refinementMode = generationSetting.SelectRefinementMode();
            if (refinementMode is RefinementMode.Upscale or RefinementMode.Pbr)
                variations = 1;

            DispatchSkeletons(variations);

            var progress = new GenerationProgressData(arg.taskID, variations, 0f);
            SetProgress(progress with { progress = 0.0f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                LogInvalidCloudProjectSettings();
                return;
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            SetProgress(progress with { progress = 0.01f }, "Preparing request.");

            var prompt = generationSetting.SelectPrompt();
            var negativePrompt = generationSetting.SelectNegativePrompt();
            var modelID = api.State.SelectSelectedModelID(asset);
            var dimensions = generationSetting.SelectImageDimensionsVector2();
            var patternImageReference = generationSetting.SelectPatternImageReference();
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();
            // clamping is important as the backend will increment the value
            var seed = useCustomSeed ? Math.Clamp(customSeed, 0, int.MaxValue - variations) : Random.Range(0, int.MaxValue - variations);
            var cost = 0;

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
            var imageComponent = builder.ImageComponent();
            var assetComponent = builder.AssetComponent();

            Guid.TryParse(modelID, out var generativeModelID);

            var materialGenerations = new List<Dictionary<MapType, Guid>>();
            int[] customSeeds = {};

            using var progressTokenSource0 = new CancellationTokenSource();
            try
            {
                switch (refinementMode)
                {
                    case RefinementMode.Upscale:
                    {
                        _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.25f,
                            value => SetProgress(progress with { progress = value }, "Sending request for upscale."),
                            1, progressTokenSource0.Token);

                        await using var assetStream = await ReferenceAssetStream(api.State, asset);
                        if (!FinalizeStoreAsset(await assetComponent.StoreAssetWithResult(assetStream, httpClientLease.client), out var assetGuid))
                        {
                            AbortCleanup(materialGenerations);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var request = ImageTransformRequestBuilder.Initialize();
                        var requests = materialGenerations.Select(m => request.Upscale(new (m[MapType.Preview], 2, null, null))).ToList();
                        var upscaleResults = await imageComponent.Transform(requests);

                        if (!upscaleResults.Batch.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedBatchResult(upscaleResults);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        foreach (var upscaleResult in upscaleResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedResult(upscaleResult.Error);

                            // we can simply return because the error is already logged and we rely on finally statements for cleanup
                            return;
                        }

                        cost = upscaleResults.Batch.Value.Sum(itemResult => itemResult.Value.PointsCost);
                        materialGenerations = upscaleResults.Batch.Value.Select(r => r.Value.JobId)
                            .Select(id => new Dictionary<MapType, Guid> { [MapType.Preview] = id }).ToList();
                        break;
                    }
                    case RefinementMode.Pbr:
                    {
                        _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.25f,
                            value => SetProgress(progress with { progress = value }, "Sending request for pbr."),
                            1, progressTokenSource0.Token);

                        await using var assetStream = await PromptAssetStream(api.State, asset);
                        var result = await assetComponent.StoreAssetWithResultPreservingStream(assetStream, httpClientLease.client);
                        if (!result.Result.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedResult(result.Result.Error);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        var assetGuid = result.Result.Value.AssetId;
                        await GenerationRecoveryUtils.AddCachedDownload(assetStream, assetGuid.ToString());

                        materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var requests = materialGenerations.Select(m => new Texture2DPbrRequest(generativeModelID, m[MapType.Preview])).ToList();
                        var pbrResults = await imageComponent.GeneratePbr(requests);

                        if (!pbrResults.Batch.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedBatchResult(pbrResults);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        foreach (var pbrResult in pbrResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedResult(pbrResult.Error);

                            // we can simply return because the error is already logged and we rely on finally statements for cleanup
                            return;
                        }

                        cost = pbrResults.Batch.Value.Sum(itemResult => itemResult.Value.PointsCost);
                        pbrResults.Batch.Value
                            .SelectMany(batchItemResult => batchItemResult.Value.MapResults,
                                (batchItemResult, itemResult) => new { batchItemResult, itemResult })
                            .ToList()
                            .ForEach(x => materialGenerations.Find(d => d[MapType.Preview] == x.batchItemResult.Value.Request.ReferenceAsset)
                                .Add(MapTypeUtils.Parse(x.itemResult.MapType), x.itemResult.JobId));
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.25f,
                            value => SetProgress(progress with { progress = value }, "Sending request for albedo."),
                            1, progressTokenSource0.Token);

                        // Generate
                        var patternGuid = Guid.Empty;
                        if (patternImageReference.asset.IsValid())
                        {
                            await using var patternStream = await PatternAssetStream(api.State, asset);
                            if (!FinalizeStoreAsset(await assetComponent.StoreAssetWithResult(patternStream, httpClientLease.client), out patternGuid))
                            {
                                AbortCleanup(materialGenerations);

                                // we can simply return without throwing or additional logging because the error is already logged
                                return;
                            }
                        }

                        var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, seed)
                            .GenerateWithReference(new TextPrompt(prompt, negativePrompt),
                                new CompositionReference(patternGuid, patternImageReference.strength));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        var generateResults = await imageComponent.Generate(requests);

                        if (!generateResults.Batch.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedBatchResult(generateResults);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        foreach (var generateResult in generateResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            AbortCleanup(materialGenerations);
                            LogFailedResult(generateResult.Error);

                            // we can simply return because the error is already logged and we rely on finally statements for cleanup
                            return;
                        }

                        cost = generateResults.Batch.Value.Sum(itemResult => itemResult.Value.PointsCost);
                        materialGenerations = generateResults.Batch.Value.Select(r => r.Value.JobId)
                            .Select(id => new Dictionary<MapType, Guid> { [MapType.Preview] = id }).ToList();
                        customSeeds = generateResults.Batch.Value.Select(r => r.Value.Request.Seed ?? -1).ToArray();
                        break;
                    }
                }
            }
            catch
            {
                AbortCleanup(materialGenerations);

                SetProgress(progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                ReenableGenerateButton(); // after validation
                progressTokenSource0.Cancel();
            }

            /*
             * If you got here, points were consumed so a restore point is saved
             */

            AIToolbarButton.ShowPointsCostNotification(cost);

            var downloadMaterialsData = new DownloadMaterialsData(asset, materialGenerations, customSeeds, arg.taskID, generationMetadata, false);
            GenerationRecoveryUtils.AddInterruptedDownload(downloadMaterialsData); // 'potentially' interrupted

            if (WebUtilities.simulateClientSideFailures)
                throw new Exception("Some simulated client side failure.");

            await api.Dispatch(downloadMaterialsSuperProxy, downloadMaterialsData, CancellationToken.None);
            return;

            void SetProgress(GenerationProgressData payload, string description)
            {
                if (payload.taskID > 0)
                    Progress.Report(payload.taskID, payload.progress, description);
                api.Dispatch(GenerationResultsActions.setGenerationProgress, new GenerationsProgressData(arg.asset, payload));
            }

            void AbortCleanup(List<Dictionary<MapType, Guid>> canceledMaterialGenerations)
            {
                foreach (var generatedMaterial in canceledMaterialGenerations)
                    GenerationRecoveryUtils.RemoveCachedDownload(generatedMaterial[MapType.Preview].ToString());
            }

            void LogInvalidCloudProjectSettings()
            {
                api.Dispatch(GenerationResultsActions.setGenerationAllowed, new(arg.asset, true));
                var messages = new[] { $"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\"." };
                foreach (var message in messages)
                {
                    Debug.Log(message);
                    api.Dispatch(GenerationResultsActions.addGenerationFeedback, new GenerationsFeedbackData(arg.asset, new GenerationFeedbackData(message)));
                }
            }

            void LogFailedBatchResult<T>(BatchOperationResult<T> results) where T : class
            {
                LogFailedResult(results.Batch.Error);
                Debug.Log($"Trace Id {results.SdkTraceId} => {results.W3CTraceId}");
            }

            void LogFailedResult(AiOperationFailedResult result)
            {
                api.Dispatch(GenerationResultsActions.setGenerationAllowed, new(arg.asset, true));
                var messages = result.Errors.Count == 0
                    ? new[] { $"Error reason is '{result.AiResponseError.ToString()}' and no additional error information was provided ({WebUtils.selectedEnvironment})." }
                    : result.Errors.Distinct().Select(m => $"{result.AiResponseError.ToString()}: {m}").ToArray();
                foreach (var message in messages)
                {
                    Debug.Log(message);
                    api.Dispatch(GenerationResultsActions.addGenerationFeedback, new GenerationsFeedbackData(arg.asset, new GenerationFeedbackData(message)));
                }
            }

            bool FinalizeStoreAsset(OperationResult<BlobAssetResult> assetResults, out Guid assetGuid)
            {
                assetGuid = Guid.Empty;
                if (!assetResults.Result.IsSuccessful)
                {
                    LogFailedResult(assetResults.Result.Error);
                    Debug.Log($"Trace Id {assetResults.SdkTraceId} => {assetResults.W3CTraceId}");

                    // caller can simply return without throwing or additional logging because the error is already logged and we rely on 'finally' statements for cleanup
                    return false;
                }
                assetGuid = assetResults.Result.Value.AssetId;
                if (LoggerUtilities.sdkLogLevel == 0)
                    return true;
                if (assetResults.Result.Value.Ttl.HasValue)
                    Debug.Log($"Asset {assetGuid} has ttl {assetResults.Result.Value.Ttl}");
                return true;
            }

            void ReenableGenerateButton() => api.Dispatch(GenerationResultsActions.setGenerationAllowed, new(arg.asset, true));

            void DispatchSkeletons(int count)
            {
                var skeletons = Enumerable.Range(0, count).Select(i => new MaterialSkeleton(arg.taskID, i)).ToList();
                api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));
            }
        });

        class HandledFailureException : Exception { }

        public static readonly AsyncThunkCreatorWithArg<DownloadMaterialsData> downloadMaterialsSuperProxy = new($"{GenerationResultsActions.slice}/downloadMaterialsSuperProxy", async (arg, api) =>
        {
            var variations = arg.ids.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new MaterialSkeleton(arg.taskID, i)).ToList();
            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));

            var progress = new GenerationProgressData(arg.taskID, variations, 0.25f);

            SetProgress(progress with { progress = 0.25f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                LogInvalidCloudProjectSettings();
                return;
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            SetProgress(progress with { progress = 0.25f }, "Waiting for server.");

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true, defaultOperationTimeout: Constants.noTimeout);
            var assetComponent = builder.AssetComponent();

            Dictionary<Guid, Dictionary<MapType, TextureResult>> generatedTextures;

            using var progressTokenSource3 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.26f, 0.75f,
                    value => SetProgress(progress with { progress = 0.25f }, "Waiting for server."),
                    variations, progressTokenSource3.Token);

                var urlTasks = arg.ids.ToDictionary(m => m[MapType.Preview],
                    m => m.ToDictionary(kvp => kvp.Key, async kvp =>
                    {
                        var jobId = kvp.Value;
                        if (GenerationRecoveryUtils.HasCachedDownload(jobId.ToString()))
                            return TextureResult.FromUrl(GenerationRecoveryUtils.GetCachedDownloadUrl(jobId.ToString()).GetAbsolutePath());
                        var result = await assetComponent.CreateAssetDownloadUrl(jobId, Constants.noTimeout);

                        if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                            return TextureResult.FromUrl(result.Result.Value.AssetUrl.Url);

                        if (result.Result.IsSuccessful)
                            LogFailedDownload(new AiOperationFailedResult(AiResultErrorEnum.Unknown, new List<string> { "Simulated server timeout" }));
                        else
                            LogFailedDownloadResult(result);
                        throw new HandledFailureException();
                    }));
                await Task.WhenAll(urlTasks.Values.SelectMany(kvp => kvp.Values));
                generatedTextures = urlTasks.ToDictionary(m => m.Key,
                    outerPair => outerPair.Value.ToDictionary(kvp => kvp.Key, innerPair => innerPair.Value.Result));
            }
            catch (HandledFailureException)
            {
                // we can simply return without throwing or additional logging because the error is already logged
                return;
            }
            catch
            {
                SetProgress(progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                progressTokenSource3.Cancel();
            }

            if (api.CancellationToken.IsCancellationRequested)
            {
                Debug.Log($"Download canceled.");
                api.Cancel();
            }

            // initial 'backup'
            var backupSuccess = true;
            var assetWasBlank = await arg.asset.IsBlank();
            if (!api.State.HasHistory(arg.asset) && !assetWasBlank)
                backupSuccess = await arg.asset.SaveToGeneratedAssets();

            List<MaterialResult> generatedMaterials;

            // cache
            using var progressTokenSource4 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.99f,
                    value => SetProgress(progress with { progress = value }, "Downloading results."),
                    1, progressTokenSource4.Token);

                var generativePath = arg.asset.GetGeneratedAssetsPath();
                var generatedMaterialsWithName = generatedTextures.Select(pair => (name: pair.Key,
                    material: new MaterialResult
                        { textures = new SerializableDictionary<MapType, TextureResult>(pair.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)) })).ToList();

                generatedMaterials = generatedMaterialsWithName.Select(tuple => tuple.material).ToList();
                var smoothnessTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateSmoothnessFromRoughness);
                var metallicSmoothnessTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateMetallicSmoothnessFromMetallicAndRoughness);
                var nonMetallicSmoothnessTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateNonMetallicSmoothnessFromRoughness);
                var aoTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateAOFromHeight);
                var postProcessTasks = smoothnessTasks.Concat(metallicSmoothnessTasks).Concat(nonMetallicSmoothnessTasks).Concat(aoTasks).ToList();

                await Task.WhenAll(postProcessTasks);

                var maskMapTasks = generatedMaterials.Select(MaterialResultExtensions.GenerateMaskMapFromAOAndMetallicAndRoughness); // ao needs to be completed first
                await Task.WhenAll(maskMapTasks);

                // gather temporary files
                var temporaryFiles = generatedMaterials.SelectMany(m => m.textures.Values.Select(r => r.uri)).Where(uri => uri.IsFile).ToList();

                var metadata = arg.generationMetadata;
                var saveTasks = generatedMaterialsWithName.Select((result, index) =>
                {
                    var metadataCopy = metadata with { };
                    if (arg.customSeeds.Length > 0 && generatedMaterialsWithName.Count == arg.customSeeds.Length)
                        metadataCopy.customSeed = arg.customSeeds[index];

                    return result.material.DownloadToProject($"{result.name}", metadataCopy, generativePath, httpClientLease.client);
                }).ToList();

                await Task.WhenAll(saveTasks); // saves to project and is picked up by GenerationFileSystemWatcherManipulator

                // cleanup temporary files
                try
                {
                    foreach (var temporaryFile in temporaryFiles)
                    {
                        var path = temporaryFile.GetLocalPath();
                        Debug.Assert(!FileIO.IsFileDirectChildOfFolder(generativePath, path));
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                }
                catch
                {
                    // ignored
                }

                // the ui defers the removal of the skeletons a little bit so we can call this pretty early
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.taskID));
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
                    api.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(arg.asset, true));
            }

            SetProgress(progress with { progress = 1f }, "Done.");

            // if you got here, no need to keep the potentially interrupted download
            foreach (var generatedMaterial in arg.ids)
                GenerationRecoveryUtils.RemoveCachedDownload(generatedMaterial[MapType.Preview].ToString());
            GenerationRecoveryUtils.RemoveInterruptedDownload(arg);
            return;

            void SetProgress(GenerationProgressData payload, string description)
            {
                if (payload.taskID > 0)
                    Progress.Report(payload.taskID, payload.progress, description);
                api.Dispatch(GenerationResultsActions.setGenerationProgress, new GenerationsProgressData(arg.asset, payload));
            }

            void LogInvalidCloudProjectSettings()
            {
                api.Dispatch(GenerationResultsActions.setGenerationAllowed, new(arg.asset, true));
                var messages = new[] { $"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\"." };
                foreach (var message in messages)
                {
                    Debug.Log(message);
                    api.Dispatch(GenerationResultsActions.addGenerationFeedback, new GenerationsFeedbackData(arg.asset, new GenerationFeedbackData(message)));
                }
            }

            void LogFailedDownloadResult<T>(OperationResult<T> result) where T : class
            {
                LogFailedDownload(result.Result.Error);
                Debug.Log($"Trace Id {result.SdkTraceId} => {result.W3CTraceId}");
            }

            void LogFailedDownload(AiOperationFailedResult result)
            {
                var messages = result.Errors.Count == 0
                    ? new[] { $"Error reason is '{result.AiResponseError.ToString()}' and no additional error information was provided ({WebUtils.selectedEnvironment})." }
                    : result.Errors.Distinct().Select(m => $"{result.AiResponseError.ToString()}: {m}").ToArray();
                foreach (var message in messages)
                {
                    Debug.Log(message);
                    api.Dispatch(GenerationResultsActions.addGenerationFeedback, new GenerationsFeedbackData(arg.asset, new GenerationFeedbackData(message)));
                }
            }
        });

        public static async Task<Stream> ReferenceAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectReferenceAssetStreamWithFallback(asset));

        public static async Task<Stream> PromptAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectPromptAssetBytesWithFallback(asset));

        public static async Task<Stream> PatternAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectPatternImageReferenceAssetStream(asset));
    }
}
