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
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Transform;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Transform.OperationSubTypes;
using AiEditorToolsSdk.Components.Modalities.Image.Responses;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using Unity.AI.Toolkit.Accounts;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Connect;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Debug = UnityEngine.Debug;
using Logger = Unity.AI.Generators.Sdk.Logger;

namespace Unity.AI.Image.Services.Stores.Actions.Backend
{
    static class Generation
    {
        public static readonly AsyncThunkCreatorWithArg<GenerateImagesData> generateImages = new($"{GenerationResultsActions.slice}/generateImagesSuperProxy",
            GenerateImagesAsync);

        static async Task GenerateImagesAsync(GenerateImagesData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Generating images.");

            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = generationSetting.SelectVariationCount();
            var refinementMode = generationSetting.SelectRefinementMode();
            variations = refinementMode is RefinementMode.RemoveBackground or RefinementMode.Upscale or RefinementMode.Recolor or RefinementMode.Pixelate
                ? 1
                : variations;

            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons,
                new(arg.asset, Enumerable.Range(0, variations).Select(i => new TextureSkeleton(arg.progressTaskId, i)).ToList()));

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
            var upscaleFactor = generationSetting.SelectUpscaleFactor();
            var cost = 0;

            var imageReferences = generationSetting.SelectImageReferencesByRefinement();
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();

            var pixelateTargetSize = generationSetting.pixelateSettings.targetSize;
            var pixelateResizeToTargetSize = !generationSetting.pixelateSettings.keepImageSize;
            var pixelatePixelBlockSize = generationSetting.pixelateSettings.pixelBlockSize;
            var pixelateMode = (int)generationSetting.pixelateSettings.mode;
            var pixelateOutlineThickness = generationSetting.SelectPixelateOutlineThickness();

            Guid.TryParse(modelID, out var generativeModelID);
            Guid.TryParse(api.State.SelectRecolorModel(), out var recolorModelID);

            var ids = new List<Guid>();
            int[] customSeeds = { };

            try
            {
                UploadReferencesData uploadReferences;

                using var progressTokenSource1 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.15f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Uploading references."), 1, progressTokenSource1.Token);

                    uploadReferences = await UploadReferencesAsync(asset, refinementMode, imageReferences, api);
                }
                catch (HandledFailureException)
                {
                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));

                    // we can simply return without throwing or additional logging because the error is already logged
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                    return;
                }
                finally
                {
                    progressTokenSource1.Cancel();
                }

                using var progressTokenSource2 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.15f, 0.24f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request."), 1, progressTokenSource2.Token);

                    using var httpClientLease = HttpClientManager.instance.AcquireLease();

                    var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                        projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                    var imageComponent = builder.ImageComponent();

                    BatchOperationResult<ImageGenerateResult> generateResults = null;
                    BatchOperationResult<ImageTransformResult> transformResults = null;

                    using var sdkTimeoutTokenSource = new CancellationTokenSource(Constants.generateTimeout);

                    switch (refinementMode)
                    {
                        case RefinementMode.Recolor:
                        {
                            var request = ImageGenerateRequestBuilder.Initialize(recolorModelID, dimensions.x, dimensions.y, useCustomSeed ? customSeed : null)
                                .Recolor(new Recolor(uploadReferences.assetGuid, uploadReferences.paletteAssetGuid));
                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            generateResults = await EditorTask.Run(() => imageComponent.Generate(requests, cancellationToken: sdkTimeoutTokenSource.Token), sdkTimeoutTokenSource.Token);
                            break;
                        }
                        case RefinementMode.Pixelate:
                        {
                            var requests = ImageTransformRequestBuilder.Initialize()
                                .Pixelate(new Pixelate(uploadReferences.assetGuid, pixelateResizeToTargetSize, pixelateTargetSize, pixelatePixelBlockSize, pixelateMode,
                                    pixelateOutlineThickness))
                                .AsSingleInAList();
                            transformResults = await EditorTask.Run(() => imageComponent.Transform(requests, cancellationToken: sdkTimeoutTokenSource.Token), sdkTimeoutTokenSource.Token);
                            break;
                        }
                        case RefinementMode.Inpaint:
                        {
                            var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, useCustomSeed ? customSeed : null)
                                .GenerateWithMaskReference(new TextPrompt(prompt, negativePrompt),
                                    new MaskReference(uploadReferences.assetGuid, uploadReferences.maskGuid, uploadReferences.inpaintMaskImageReference.strength));
                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            generateResults = await EditorTask.Run(() => imageComponent.Generate(requests, cancellationToken: sdkTimeoutTokenSource.Token), sdkTimeoutTokenSource.Token);
                            break;
                        }
                        case RefinementMode.RemoveBackground:
                        {
                            var request = ImageTransformRequestBuilder.Initialize().RemoveBackground(new RemoveBackground(uploadReferences.assetGuid));
                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            transformResults = await EditorTask.Run(() => imageComponent.Transform(requests, cancellationToken: sdkTimeoutTokenSource.Token), sdkTimeoutTokenSource.Token);
                            break;
                        }
                        case RefinementMode.Upscale:
                        {
                            var request = ImageTransformRequestBuilder.Initialize().Upscale(new(uploadReferences.assetGuid, upscaleFactor, null, null));
                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            transformResults = await EditorTask.Run(() => imageComponent.Transform(requests, cancellationToken: sdkTimeoutTokenSource.Token), sdkTimeoutTokenSource.Token);
                            break;
                        }
                        case RefinementMode.Generation:
                        {
                            var requestBuilder =
                                ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, useCustomSeed ? customSeed : null);
                            var textPrompt = new TextPrompt(prompt, negativePrompt);

                            try
                            {
                                var request = requestBuilder.GenerateWithReferences(textPrompt,
                                    uploadReferences.referenceGuids[ImageReferenceType.PromptImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.PromptImage],
                                            imageReferences[refinementMode][ImageReferenceType.PromptImage].strength)
                                        : null,
                                    uploadReferences.referenceGuids[ImageReferenceType.StyleImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.StyleImage],
                                            imageReferences[refinementMode][ImageReferenceType.StyleImage].strength)
                                        : null,
                                    uploadReferences.referenceGuids[ImageReferenceType.CompositionImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.CompositionImage],
                                            imageReferences[refinementMode][ImageReferenceType.CompositionImage].strength)
                                        : null,
                                    uploadReferences.referenceGuids[ImageReferenceType.PoseImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.PoseImage], imageReferences[refinementMode][ImageReferenceType.PoseImage].strength)
                                        : null,
                                    uploadReferences.referenceGuids[ImageReferenceType.DepthImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.DepthImage],
                                            imageReferences[refinementMode][ImageReferenceType.DepthImage].strength)
                                        : null,
                                    uploadReferences.referenceGuids[ImageReferenceType.LineArtImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.LineArtImage],
                                            imageReferences[refinementMode][ImageReferenceType.LineArtImage].strength)
                                        : null,
                                    uploadReferences.referenceGuids[ImageReferenceType.FeatureImage] != Guid.Empty
                                        ? new(uploadReferences.referenceGuids[ImageReferenceType.FeatureImage],
                                            imageReferences[refinementMode][ImageReferenceType.FeatureImage].strength)
                                        : null);

                                var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                                generateResults = await EditorTask.Run(() => imageComponent.Generate(requests, cancellationToken: sdkTimeoutTokenSource.Token), sdkTimeoutTokenSource.Token);
                            }
                            catch (UnhandledReferenceCombinationException e)
                            {
                                var messages = new[] { $"{e.responseError.ToString()}: {e.Message}" };
                                api.Dispatch(GenerationActions.setGenerationValidationResult,
                                    new(arg.asset, new(false, e.responseError, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                                return;
                            }

                            break;
                        }
                    }

                    if (generateResults != null)
                    {
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

                        cost = generateResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                        ids = generateResults.Batch.Value.Where(v => v.IsSuccessful).Select(itemResult => itemResult.Value.JobId).ToList();
                        customSeeds = generateResults.Batch.Value.Where(v => v.IsSuccessful).Select(result => result.Value.Request.Seed ?? -1).ToArray();
                        generationMetadata.w3CTraceId = generateResults.W3CTraceId;
                    }

                    if (transformResults != null)
                    {
                        if (!transformResults.Batch.IsSuccessful)
                        {
                            api.DispatchFailedBatchMessage(arg.asset, transformResults);

                            // we can simply return because the error is already logged and we rely on finally statements for cleanup
                            return;
                        }

                        var once = false;
                        foreach (var transformResult in transformResults.Batch.Value.Where(v => !v.IsSuccessful))
                        {
                            if (!once)
                            {
                                api.DispatchFailedBatchMessage(arg.asset, transformResults);
                            }

                            once = true;

                            api.DispatchFailedMessage(arg.asset, transformResult.Error);
                        }

                        cost = transformResults.Batch.Value.Where(v => v.IsSuccessful).Sum(itemResult => itemResult.Value.PointsCost);
                        ids = transformResults.Batch.Value.Where(v => v.IsSuccessful).Select(itemResult => itemResult.Value.JobId).ToList();
                        generationMetadata.w3CTraceId = transformResults.W3CTraceId;
                    }

                    if (ids.Count == 0)
                    {
                        throw new HandledFailureException();
                    }
                }
                catch
                {
                    api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Failed.");
                    throw;
                }
                finally
                {
                    progressTokenSource2.Cancel();
                }
            }
            catch (HandledFailureException)
            {
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));

                // we can simply return without throwing or additional logging because the error is already logged
                return;
            }
            catch (OperationCanceledException)
            {
                api.DispatchGenerationRequestFailedMessage(asset);

                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.progressTaskId));
                return;
            }
            finally
            {
                api.Dispatch(GenerationActions.setGenerationAllowed, new(arg.asset, true)); // after validation
            }

            /*
             * If you got here, points were consumed so a restore point is saved
             */

            AIToolbarButton.ShowPointsCostNotification(cost);

            // Generate a unique task ID for download recovery
            var downloadImagesData = new DownloadImagesData(asset, ids, arg.progressTaskId, Guid.NewGuid(), generationMetadata, customSeeds,
                refinementMode is RefinementMode.RemoveBackground or RefinementMode.Pixelate or RefinementMode.Upscale or RefinementMode.Recolor,
                generationSetting.replaceBlankAsset, generationSetting.replaceRefinementAsset, false);

            GenerationRecovery.AddInterruptedDownload(downloadImagesData); // 'potentially' interrupted

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
                    downloadImagesData = downloadImagesData with { retryable = retryCount < maxRetries };
                    await DownloadImagesAsync(downloadImagesData, api);
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
        }

        record UploadReferencesData(Guid assetGuid, Guid maskGuid, Guid paletteAssetGuid, Dictionary<ImageReferenceType, Guid> referenceGuids, ImageReferenceSettings inpaintMaskImageReference);

        static async Task<UploadReferencesData> UploadReferencesAsync(AssetReference asset, RefinementMode refinementMode,
            Dictionary<RefinementMode, Dictionary<ImageReferenceType, ImageReferenceSettings>> imageReferences,
            AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Uploading image references.");

            var assetGuid = Guid.Empty;
            var maskGuid = Guid.Empty;
            var paletteAssetGuid = Guid.Empty;
            var referenceGuids = new Dictionary<ImageReferenceType, Guid>();
            ImageReferenceSettings inpaintMaskImageReference = null;

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                defaultOperationTimeout: Constants.referenceUploadCreateUrlTimeout);
            var assetComponent = builder.AssetComponent();

            var refineAsset = refinementMode is RefinementMode.RemoveBackground or RefinementMode.Upscale or RefinementMode.Recolor
                or RefinementMode.Pixelate or RefinementMode.Inpaint;

            // main asset is only uploaded when refining
            if (refineAsset)
            {
                var streamsToDispose = new List<Stream>();
                try
                {
                    var mainAssetStream = await UnsavedAssetStream(api.State, asset);
                    streamsToDispose.Add(mainAssetStream);
                    var streamToStore = mainAssetStream;

                    // the current Pixelate model doesn't support indexed color pngs so we need to check that
                    if (refinementMode == RefinementMode.Pixelate && ImageFileUtilities.IsPng(mainAssetStream) &&
                        ImageFileUtilities.IsPngIndexedColor(mainAssetStream) && ImageFileUtilities.TryConvert(mainAssetStream, out var convertedStream))
                    {
                        streamsToDispose.Add(convertedStream);
                        streamToStore = convertedStream;
                    }

                    using var sdkTimeoutTokenSource = new CancellationTokenSource(Constants.referenceUploadCreateUrlTimeout);

                    var mainAssetWithResult = await assetComponent.StoreAssetWithResult(streamToStore, httpClientLease.client, sdkTimeoutTokenSource.Token, CancellationToken.None);
                    if (!api.DispatchStoreAssetMessage(asset, mainAssetWithResult, out assetGuid))
                    {
                        throw new HandledFailureException();
                    }
                }
                catch (OperationCanceledException)
                {
                    api.DispatchReferenceUploadFailedMessage(asset);
                    throw new HandledFailureException();
                }
                finally
                {
                    foreach (var stream in streamsToDispose) _ = stream?.DisposeAsync();
                }
            }

            switch (refinementMode)
            {
                case RefinementMode.Recolor:
                {
                    paletteAssetGuid = Guid.Empty;

                    var paletteImageReference = imageReferences[refinementMode][ImageReferenceType.PaletteImage];
                    if (paletteImageReference.SelectImageReferenceIsValid())
                    {
                        try
                        {
                            await using var paletteAsset = await paletteImageReference.SelectImageReferenceStream();

                            // 2x3 pixels expected from CreatePaletteApproximation
                            await using var paletteApproximation = await TextureUtils.CreatePaletteApproximation(paletteAsset);

                            using var sdkTimeoutTokenSource = new CancellationTokenSource(Constants.referenceUploadCreateUrlTimeout);

                            var assetWithResult = await assetComponent.StoreAssetWithResult(paletteApproximation, httpClientLease.client, sdkTimeoutTokenSource.Token, CancellationToken.None);
                            if (!api.DispatchStoreAssetMessage(asset, assetWithResult, out paletteAssetGuid))
                            {
                                throw new HandledFailureException();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            api.DispatchReferenceUploadFailedMessage(asset);
                            throw new HandledFailureException();
                        }
                    }

                    break;
                }
                case RefinementMode.Pixelate:
                case RefinementMode.RemoveBackground:
                case RefinementMode.Upscale:
                {
                    break;
                }
                case RefinementMode.Inpaint:
                {
                    maskGuid = Guid.Empty;

                    inpaintMaskImageReference = imageReferences[refinementMode][ImageReferenceType.InPaintMaskImage];
                    if (inpaintMaskImageReference.SelectImageReferenceIsValid())
                    {
                        try
                        {
                            await using var maskAsset = ImageFileUtilities.CheckImageSize(await inpaintMaskImageReference.SelectImageReferenceStream());

                            using var sdkTimeoutTokenSource = new CancellationTokenSource(Constants.referenceUploadCreateUrlTimeout);

                            var assetWithResult = await assetComponent.StoreAssetWithResult(maskAsset, httpClientLease.client, sdkTimeoutTokenSource.Token, CancellationToken.None);
                            if (!api.DispatchStoreAssetMessage(asset, assetWithResult, out maskGuid))
                            {
                                throw new HandledFailureException();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            api.DispatchReferenceUploadFailedMessage(asset);
                            throw new HandledFailureException();
                        }
                    }

                    break;
                }
                case RefinementMode.Generation:
                {
                    var streamsToDispose = new List<Stream>();
                    try
                    {
                        using var sdkTimeoutTokenSource = new CancellationTokenSource(Constants.referenceUploadCreateUrlTimeout);

                        Dictionary<ImageReferenceType, Task<OperationResult<BlobAssetResult>>> referenceAssetTasks = new();
                        foreach (var (imageReferenceType, imageReference) in imageReferences[refinementMode])
                        {
                            if (!imageReference.SelectImageReferenceIsValid())
                            {
                                continue;
                            }

                            var referenceAsset = ImageFileUtilities.CheckImageSize(await imageReference.SelectImageReferenceStream());
                            streamsToDispose.Add(referenceAsset);
                            referenceAssetTasks.Add(imageReferenceType, assetComponent.StoreAssetWithResult(referenceAsset, httpClientLease.client, sdkTimeoutTokenSource.Token, CancellationToken.None));
                        }

                        // await as late as possible as we want to upload everything in parallel
                        foreach (var uploadTask in referenceAssetTasks.Values)
                        {
                            await uploadTask;
                        }

                        referenceGuids = imageReferences[refinementMode].ToDictionary(kvp => kvp.Key, _ => Guid.Empty);
                        foreach (var (imageReferenceType, referenceAssetTask) in referenceAssetTasks)
                        {
                            if (!api.DispatchStoreAssetMessage(asset, await referenceAssetTask, out var referenceGuid))
                            {
                                throw new HandledFailureException();
                            }

                            referenceGuids[imageReferenceType] = referenceGuid;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        api.DispatchReferenceUploadFailedMessage(asset);
                        throw new HandledFailureException();
                    }
                    finally
                    {
                        foreach (var stream in streamsToDispose) _ = stream?.DisposeAsync();
                    }

                    break;
                }
            }

            return new(assetGuid, maskGuid, paletteAssetGuid, referenceGuids, inpaintMaskImageReference);
        }

        public static readonly AsyncThunkCreatorWithArg<DownloadImagesData> downloadImages = new($"{GenerationResultsActions.slice}/downloadImagesSuperProxy",
            DownloadImagesAsync);

        static async Task DownloadImagesAsync(DownloadImagesData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Downloading images.");

            var variations = arg.jobIds.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new TextureSkeleton(arg.progressTaskId, i)).ToList();
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

            var retryTimeout = arg.retryable ? Constants.imageDownloadCreateUrlRetryTimeout : Constants.noTimeout;

            using var retryTokenSource = new CancellationTokenSource(retryTimeout);

            List<TextureResult> generatedImages;

            using var progressTokenSource2 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.25f, 0.75f,
                    _ => api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server."), variations, progressTokenSource2.Token);

                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true,
                    defaultOperationTimeout: retryTimeout);
                var assetComponent = builder.AssetComponent();

                var assetResults = new List<(Guid jobId, OperationResult<BlobAssetResult>)>();
                foreach (var jobId in arg.jobIds)
                {
                    // need to be very careful, we're taking each in turn to guarantee paused play mode support
                    // there's not much drawback as the generations are started way before
                    var url = await EditorTask.Run(() =>
                        assetComponent.CreateAssetDownloadUrl(jobId, retryTimeout, api.DispatchJobUpdates, retryTokenSource.Token), retryTokenSource.Token);
                    if (retryTokenSource.IsCancellationRequested)
                        throw new OperationCanceledException();
                    assetResults.Add((jobId, url));
                }

                generatedImages = assetResults.Select(pair =>
                    {
                        var (_, result) = pair;
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
                    })
                    .ToList();
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
                progressTokenSource2.Cancel();
            }

            // initial 'backup'
            var backupSuccess = true;
            var assetWasBlank = false;
            if (!api.State.HasHistory(arg.asset))
            {
                assetWasBlank = await arg.asset.IsBlank();
                if (!assetWasBlank)
                {
                    if (!await arg.asset.SaveToGeneratedAssets())
                    {
                        backupSuccess = false;
                    }
                }
            }

            // cache
            using var progressTokenSource4 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.99f,
                    value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Downloading results."), 1, progressTokenSource4.Token);

                var generativePath = arg.asset.GetGeneratedAssetsPath();
                var metadata = arg.generationMetadata;
                var saveTasks = generatedImages.Select((result, index) =>
                    {
                        var metadataCopy = metadata with { };
                        if (arg.customSeeds.Length > 0 && generatedImages.Count == arg.customSeeds.Length)
                        {
                            metadataCopy.customSeed = arg.customSeeds[index];
                        }

                        return result.DownloadToProject(metadataCopy, generativePath, httpClientLease.client);
                    })
                    .ToList();

                foreach (var saveTask in saveTasks)
                {
                    await saveTask; // saves to project and is picked up by GenerationFileSystemWatcherManipulator
                }

                // the ui handles results, skeletons, and fulfilled skeletons to determine which tiles are ready for display (see Selectors.SelectGeneratedTexturesAndSkeletons)
                api.Dispatch(GenerationResultsActions.setFulfilledSkeletons,
                    new(arg.asset, generatedImages.Select(res => new FulfilledSkeleton(arg.progressTaskId, res.uri.GetAbsolutePath())).ToList()));
            }
            finally
            {
                progressTokenSource4.Cancel();
            }

            // auto-apply if blank or if RefinementMode
            if (generatedImages.Count > 0 && ((assetWasBlank && arg.replaceBlankAsset) || (arg.isRefinement && arg.replaceRefinementAsset)))
            {
                await api.Dispatch(GenerationResultsActions.selectGeneration, new(arg.asset, generatedImages[0], backupSuccess, !assetWasBlank));
                if (assetWasBlank)
                {
                    api.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(arg.asset, true));
                }
            }

            api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Done.");

            // if you got here, no need to keep the potentially interrupted download
            GenerationRecovery.RemoveInterruptedDownload(arg);
        }

        public static async Task<Stream> UnsavedAssetStream(IState state, AssetReference asset) =>
            ImageFileUtilities.CheckImageSize(await state.SelectUnsavedAssetStreamWithFallback(asset));
    }
}
