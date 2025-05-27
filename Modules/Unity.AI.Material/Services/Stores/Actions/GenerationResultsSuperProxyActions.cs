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
using AiEditorToolsSdk.Components.Modalities.Image.Responses;
using AiEditorToolsSdk.Components.Modalities.Image.Responses.Pbr;
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
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Toolkit;
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
            var cancellationTokenSource = new CancellationTokenSource();
            k_QuoteCancellationTokenSources[arg.asset] = cancellationTokenSource;

            try
            {
                SendValidatingMessage();

                var success = await WebUtilities.WaitForCloudProjectSettings(arg.asset);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    SendValidatingMessage();
                    return;
                }

                if (!success)
                {
                    var messages = new[] { $"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\"." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(false, AiResultErrorEnum.Unknown, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var asset = new AssetReference { guid = arg.asset.guid };

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    SendValidatingMessage();
                    return;
                }

                if (!asset.Exists())
                {
                    var messages = new[] { $"Error reason is 'Invalid Asset'." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
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
                        quoteResults = await EditorTask.Run(() => imageComponent.TransformQuote(requests, Constants.realtimeTimeout, cancellationTokenSource.Token));
                        break;
                    }
                    case RefinementMode.Pbr:
                    {
                        if (generativeModelID == Guid.Empty)
                        {
                            var messages = new[] { $"Error reason is 'Invalid Model'." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset,
                                    new(false, AiResultErrorEnum.UnknownModel, 0,
                                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var requests = materialGenerations.Select(m => new Texture2DPbrRequest(generativeModelID, m[MapType.Preview])).ToList();
                        quoteResults = await EditorTask.Run(() => imageComponent.GeneratePbrQuote(requests, Constants.realtimeTimeout, cancellationTokenSource.Token));
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        if (generativeModelID == Guid.Empty)
                        {
                            var messages = new[] { $"Error reason is 'Invalid Model'." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset,
                                    new(false, AiResultErrorEnum.UnknownModel, 0,
                                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var patternGuid = Guid.Empty;
                        if (patternImageReference.asset.IsValid())
                            patternGuid = Guid.NewGuid();

                        var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, seed)
                            .GenerateWithReference(new TextPrompt(prompt, negativePrompt),
                                new CompositionReference(patternGuid, patternImageReference.strength));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await EditorTask.Run(() => imageComponent.GenerateQuote(requests, Constants.realtimeTimeout, cancellationTokenSource.Token));
                        break;
                    }
                }

                if (cancellationTokenSource.IsCancellationRequested)
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
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(quoteResults.Result.IsSuccessful, quoteResults.Result.Error.AiResponseError, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }
                api.Dispatch(GenerationActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(quoteResults.Result.IsSuccessful,
                            (!quoteResults.Result.IsSuccessful ? quoteResults.Result.Error.AiResponseError : AiResultErrorEnum.Unknown),
                            quoteResults.Result.Value.PointsCost, new List<GenerationFeedbackData>())));
            }
            finally
            {
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                cancellationTokenSource.Dispose();
            }

            void SendValidatingMessage()
            {
                var messages = new[] { "Validating generation inputs..." };
                api.Dispatch(GenerationActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(false, AiResultErrorEnum.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
            }
        });

        public static readonly AsyncThunkCreatorWithArg<GenerateMaterialsData> generateMaterialsSuperProxy = new($"{GenerationResultsActions.slice}/generateMaterialSuperProxy", async (arg, api) =>
        {
            using var editorFocus = new EditorFocusScope(onlyWhenPlayingPaused: true);

            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = arg.generationSetting.SelectVariationCount();
            var refinementMode = generationSetting.SelectRefinementMode();
            if (refinementMode is RefinementMode.Upscale or RefinementMode.Pbr)
                variations = 1;

            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, Enumerable.Range(0, variations).Select(i => new MaterialSkeleton(arg.taskID, i)).ToList()));

            var progress = new GenerationProgressData(arg.taskID, variations, 0f);
            api.DispatchProgress(arg.asset, progress with { progress = 0.0f }, "Authenticating with UnityConnect.", editorFocus);

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                api.DispatchInvalidCloudProjectMessage(arg.asset);
                return;
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            api.DispatchProgress(arg.asset, progress with { progress = 0.01f }, "Preparing request.", editorFocus);

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
                            value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request for upscale.", editorFocus),
                            1, progressTokenSource0.Token);

                        await using var assetStream = await ReferenceAssetStream(api.State, asset);
                        if (!api.DispatchStoreAssetMessage(arg.asset, await assetComponent.StoreAssetWithResult(assetStream, httpClientLease.client), out var assetGuid))
                        {
                            AbortCleanup(materialGenerations);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var request = ImageTransformRequestBuilder.Initialize();
                        var requests = materialGenerations.Select(m => request.Upscale(new (m[MapType.Preview], 2, null, null))).ToList();
                        var upscaleResults = await EditorTask.Run(() => imageComponent.Transform(requests));

                        if (!upscaleResults.Batch.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            api.DispatchFailedBatchMessage(arg.asset, upscaleResults);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        var once = false;
                        foreach (var upscaleResult in upscaleResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            if (!once)
                                api.DispatchFailedBatchMessage(arg.asset, upscaleResults);
                            once = true;

                            api.DispatchFailedMessage(arg.asset, upscaleResult.Error);
                        }

                        cost = upscaleResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                        materialGenerations = upscaleResults.Batch.Value.Where(v => v.IsSuccessful).Select(r => r.Value.JobId)
                            .Select(id => new Dictionary<MapType, Guid> { [MapType.Preview] = id }).ToList();
                        generationMetadata.w3CTraceId = upscaleResults.W3CTraceId;
                        break;
                    }
                    case RefinementMode.Pbr:
                    {
                        _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.25f,
                            value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request for pbr.", editorFocus),
                            1, progressTokenSource0.Token);

                        await using var assetStream = await PromptAssetStream(api.State, asset);
                        var result = await assetComponent.StoreAssetWithResultPreservingStream(assetStream, httpClientLease.client);
                        if (!result.Result.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            api.DispatchFailedMessage(arg.asset, result.Result.Error);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        var assetGuid = result.Result.Value.AssetId;
                        await GenerationRecoveryUtils.AddCachedDownload(assetStream, assetGuid.ToString());

                        materialGenerations = new List<Dictionary<MapType, Guid>>
                            { new() { [MapType.Preview] = assetGuid } };

                        var requests = materialGenerations.Select(m => new Texture2DPbrRequest(generativeModelID, m[MapType.Preview])).ToList();
                        var pbrResults = await EditorTask.Run(() => imageComponent.GeneratePbr(requests));

                        if (!pbrResults.Batch.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            api.DispatchFailedBatchMessage(arg.asset, pbrResults);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        var once = false;
                        foreach (var pbrResult in pbrResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            if (!once)
                                api.DispatchFailedBatchMessage(arg.asset, pbrResults);
                            once = true;

                            api.DispatchFailedMessage(arg.asset, pbrResult.Error);
                        }

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
                        _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.25f,
                            value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request for albedo.", editorFocus),
                            1, progressTokenSource0.Token);

                        // Generate
                        var patternGuid = Guid.Empty;
                        if (patternImageReference.asset.IsValid())
                        {
                            await using var patternStream = await PatternAssetStream(api.State, asset);
                            if (!api.DispatchStoreAssetMessage(arg.asset, await assetComponent.StoreAssetWithResult(patternStream, httpClientLease.client), out patternGuid))
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
                        var generateResults = await EditorTask.Run(() => imageComponent.Generate(requests));

                        if (!generateResults.Batch.IsSuccessful)
                        {
                            AbortCleanup(materialGenerations);
                            api.DispatchFailedBatchMessage(arg.asset, generateResults);

                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        var once = false;
                        foreach (var generateResult in generateResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            if (!once)
                                api.DispatchFailedBatchMessage(arg.asset, generateResults);
                            once = true;

                            api.DispatchFailedMessage(arg.asset, generateResult.Error);
                        }

                        cost = generateResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                        materialGenerations = generateResults.Batch.Value.Where(v => v.IsSuccessful).Select(r => r.Value.JobId)
                            .Select(id => new Dictionary<MapType, Guid> { [MapType.Preview] = id }).ToList();
                        customSeeds = generateResults.Batch.Value.Where(v => v.IsSuccessful).Select(r => r.Value.Request.Seed ?? -1).ToArray();
                        generationMetadata.w3CTraceId = generateResults.W3CTraceId;
                        break;
                    }
                }
            }
            catch
            {
                AbortCleanup(materialGenerations);

                api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Failed.", editorFocus);
                throw;
            }
            finally
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(arg.asset, true)); // after validation
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

            void AbortCleanup(List<Dictionary<MapType, Guid>> canceledMaterialGenerations)
            {
                foreach (var generatedMaterial in canceledMaterialGenerations)
                    GenerationRecoveryUtils.RemoveCachedDownload(generatedMaterial[MapType.Preview].ToString());
            }
        });

        class HandledFailureException : Exception { }

        public static readonly AsyncThunkCreatorWithArg<DownloadMaterialsData> downloadMaterialsSuperProxy = new($"{GenerationResultsActions.slice}/downloadMaterialsSuperProxy", async (arg, api) =>
        {
            using var editorFocus = new EditorFocusScope(onlyWhenPlayingPaused: true);

            var variations = arg.ids.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new MaterialSkeleton(arg.taskID, i)).ToList();
            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));

            var progress = new GenerationProgressData(arg.taskID, variations, 0.25f);

            api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Authenticating with UnityConnect.", editorFocus);

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                api.DispatchInvalidCloudProjectMessage(arg.asset);
                return;
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server.", editorFocus);

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true, defaultOperationTimeout: Constants.noTimeout);
            var assetComponent = builder.AssetComponent();

            Dictionary<Guid, Dictionary<MapType, TextureResult>> generatedTextures;

            _ = EditorFocus.UpdateEditorAsync("Waiting for server...", TimeSpan.FromMilliseconds(50));

            using var progressTokenSource3 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.25f, 0.75f,
                    _ => api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server.", editorFocus),
                    variations, progressTokenSource3.Token);

                var jobIdList = arg.ids
                    .SelectMany(dictionary => dictionary.Values) // Get all JobId values
                    .Where(jobId => !GenerationRecoveryUtils.HasCachedDownload(jobId.ToString())) // Filter out the ones that are already cached
                    .ToList();

                var assetResults = new Dictionary<Guid, OperationResult<BlobAssetResult>>();
                foreach (var guid in jobIdList)
                {
                    // need to be very careful, we're taking each in turn to guarantee paused play mode support
                    // there's not much drawback as the generations are started way before
                    var url = await EditorTask.Run(() => assetComponent.CreateAssetDownloadUrl(guid, Constants.noTimeout));
                    assetResults.Add(guid, url);
                }

                var urls = arg.ids.ToDictionary(m => m[MapType.Preview],
                    m => m.ToDictionary(kvp => kvp.Key, kvp =>
                    {
                        var jobId = kvp.Value;
                        if (GenerationRecoveryUtils.HasCachedDownload(jobId.ToString()))
                            return TextureResult.FromUrl(GenerationRecoveryUtils.GetCachedDownloadUrl(jobId.ToString()).GetAbsolutePath());
                        var result = assetResults[jobId];

                        if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                            return TextureResult.FromUrl(result.Result.Value.AssetUrl.Url);

                        if (result.Result.IsSuccessful)
                            api.DispatchFailedDownloadMessage(arg.asset, new AiOperationFailedResult(AiResultErrorEnum.Unknown, new List<string> { "Simulated server timeout" }));
                        else
                            api.DispatchFailedDownloadMessage(arg.asset, result);
                        throw new HandledFailureException();
                    }));
                generatedTextures = urls.ToDictionary(m => m.Key, outerPair => outerPair.Value.ToDictionary(kvp => kvp.Key, innerPair => innerPair.Value));
            }
            catch (HandledFailureException)
            {
                // we can simply return without throwing or additional logging because the error is already logged
                return;
            }
            catch
            {
                api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Failed.", editorFocus);
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

            _ = EditorFocus.UpdateEditorAsync("Downloading results...", TimeSpan.FromMilliseconds(50));

            // cache
            using var progressTokenSource4 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.99f,
                    value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Downloading results.", editorFocus),
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

            api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Done.", editorFocus);

            // if you got here, no need to keep the potentially interrupted download
            foreach (var generatedMaterial in arg.ids)
                GenerationRecoveryUtils.RemoveCachedDownload(generatedMaterial[MapType.Preview].ToString());
            GenerationRecoveryUtils.RemoveInterruptedDownload(arg);

            // ensure the file system watcher looks at the generations
            GenerationFileSystemWatcher.EnsureFocus();
        });

        public static async Task<Stream> ReferenceAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectReferenceAssetStreamWithFallback(asset));

        public static async Task<Stream> PromptAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectPromptAssetBytesWithFallback(asset));

        public static async Task<Stream> PatternAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectPatternImageReferenceAssetStream(asset));
    }
}
