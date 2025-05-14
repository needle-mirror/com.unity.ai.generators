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
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Transform;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Transform.OperationSubTypes;
using AiEditorToolsSdk.Components.Modalities.Image.Responses;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.ImageEditor.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using Unity.AI.Toolkit.Accounts;
using Unity.AI.Generators.Sdk;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Debug = UnityEngine.Debug;
using Logger = Unity.AI.Generators.Sdk.Logger;

namespace Unity.AI.Image.Services.Stores.Actions
{
    static class GenerationResultsSuperProxyActions
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();

        public static readonly AsyncThunkCreatorWithArg<QuoteImagesData> quoteImages = new($"{GenerationResultsActions.slice}/quoteImagesSuperProxy", async (arg, api) =>
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

                var variations = generationSetting.SelectVariationCount();
                var refinementMode = generationSetting.SelectRefinementMode();
                if (refinementMode is RefinementMode.RemoveBackground or RefinementMode.Upscale or RefinementMode.Recolor or RefinementMode.Pixelate)
                    variations = 1;

                var prompt = generationSetting.SelectPrompt();
                var negativePrompt = generationSetting.SelectNegativePrompt();
                var modelID = api.State.SelectSelectedModelID(asset);
                var dimensions = generationSetting.SelectImageDimensionsVector2();
                var upscaleFactor = generationSetting.SelectUpscaleFactor();
                var imageReferences = generationSetting.SelectImageReferencesByRefinement();

                var pixelateTargetSize = generationSetting.pixelateSettings.targetSize;
                var pixelateResizeToTargetSize = !generationSetting.pixelateSettings.keepImageSize;
                var pixelatePixelBlockSize = generationSetting.pixelateSettings.pixelBlockSize;
                var pixelateMode = (int)generationSetting.pixelateSettings.mode;
                var pixelateOutlineThickness = generationSetting.SelectPixelateOutlineThickness();

                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);
                var imageComponent = builder.ImageComponent();

                Guid.TryParse(modelID, out var generativeModelID);

                OperationResult<QuoteResponse> quoteResults = null;

                var assetGuid = asset.IsValid() ? Guid.NewGuid() : Guid.Empty;

                switch (refinementMode)
                {
                    case RefinementMode.Recolor:
                    {
                        Guid.TryParse(api.State.SelectRecolorModel(), out var recolorModelID);

                        var paletteImageReference = imageReferences[refinementMode][ImageReferenceType.PaletteImage];
                        var paletteAssetGuid = paletteImageReference.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty;

                        if (paletteAssetGuid == Guid.Empty)
                        {
                            var messages = new[] { $"Error reason is 'Invalid palette'." };
                            api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                                new(arg.asset,
                                    new(false, AiResultErrorEnum.Unknown, 0,
                                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var request = ImageGenerateRequestBuilder.Initialize(recolorModelID, dimensions.x, dimensions.y, null)
                            .Recolor(new Recolor(assetGuid, paletteAssetGuid));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await imageComponent.GenerateQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Pixelate:
                    {
                        var requests = ImageTransformRequestBuilder.Initialize().Pixelate(new Pixelate(assetGuid, pixelateResizeToTargetSize,
                            pixelateTargetSize, pixelatePixelBlockSize, pixelateMode, pixelateOutlineThickness)).AsSingleInAList();
                        quoteResults = await imageComponent.TransformQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Inpaint:
                    {
                        var inpaintMaskImageReference = imageReferences[refinementMode][ImageReferenceType.InPaintMaskImage];
                        var maskGuid = inpaintMaskImageReference.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty;
                        var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, null)
                            .GenerateWithMaskReference(new TextPrompt(prompt, negativePrompt), new MaskReference(assetGuid, maskGuid, inpaintMaskImageReference.strength));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await imageComponent.GenerateQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.RemoveBackground:
                    {
                        var request = ImageTransformRequestBuilder.Initialize().RemoveBackground(new RemoveBackground(assetGuid));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await imageComponent.TransformQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Upscale:
                    {
                        var request = ImageTransformRequestBuilder.Initialize().Upscale(new(assetGuid, upscaleFactor, null, null));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await imageComponent.TransformQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        var requestBuilder = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, null);
                        var textPrompt = new TextPrompt(prompt, negativePrompt);
                        try
                        {
                            var referenceGuids = imageReferences[refinementMode].ToDictionary(kvp => kvp.Key, kvp => kvp.Value.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty);
                            var request = requestBuilder.GenerateWithReferences(textPrompt,
                                referenceGuids[ImageReferenceType.PromptImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.PromptImage], imageReferences[refinementMode][ImageReferenceType.PromptImage].strength) : null,
                                referenceGuids[ImageReferenceType.StyleImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.StyleImage], imageReferences[refinementMode][ImageReferenceType.StyleImage].strength) : null,
                                referenceGuids[ImageReferenceType.CompositionImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.CompositionImage], imageReferences[refinementMode][ImageReferenceType.CompositionImage].strength) : null,
                                referenceGuids[ImageReferenceType.PoseImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.PoseImage], imageReferences[refinementMode][ImageReferenceType.PoseImage].strength) : null,
                                referenceGuids[ImageReferenceType.DepthImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.DepthImage], imageReferences[refinementMode][ImageReferenceType.DepthImage].strength) : null,
                                referenceGuids[ImageReferenceType.LineArtImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.LineArtImage], imageReferences[refinementMode][ImageReferenceType.LineArtImage].strength) : null,
                                referenceGuids[ImageReferenceType.FeatureImage] != Guid.Empty ? new(referenceGuids[ImageReferenceType.FeatureImage], imageReferences[refinementMode][ImageReferenceType.FeatureImage].strength) : null);

                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            quoteResults = await imageComponent.GenerateQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
                        }
                        catch (UnhandledReferenceCombinationException e)
                        {
                            var messages = new[] { $"{e.responseError.ToString()}: {e.Message}" };
                            api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                                new(arg.asset,
                                    new(false, e.responseError, 0,
                                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }
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
                // Only dispose if this is still the current token source for this asset
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == arg.cancellationTokenSource)
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                arg.cancellationTokenSource.Dispose();
            }

            return;

            void SendValidatingMessage()
            {
                var messages = new[] { "Validating generation inputs..." };
                api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(false, AiResultErrorEnum.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
            }
        });

        public static readonly AsyncThunkCreatorWithArg<GenerateImagesData> generateImages = new($"{GenerationResultsActions.slice}/generateImagesSuperProxy", async (arg, api) =>
        {
            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = generationSetting.SelectVariationCount();
            var refinementMode = generationSetting.SelectRefinementMode();
            variations = refinementMode
                is RefinementMode.RemoveBackground or RefinementMode.Upscale
                or RefinementMode.Recolor or RefinementMode.Pixelate ? 1 : variations;

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
            var upscaleFactor = generationSetting.SelectUpscaleFactor();
            var cost = 0;

            var imageReferences = generationSetting.SelectImageReferencesByRefinement();
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();

            var pixelateTargetSize = generationSetting.pixelateSettings.targetSize;
            var pixelateResizeToTargetSize = !generationSetting.pixelateSettings.keepImageSize;
            var pixelatePixelBlockSize = generationSetting.pixelateSettings.pixelBlockSize;
            var pixelateMode = (int)generationSetting.pixelateSettings.mode;
            var pixelateOutlineThickness = generationSetting.SelectPixelateOutlineThickness();

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
            var imageComponent = builder.ImageComponent();
            var assetComponent = builder.AssetComponent();

            Guid.TryParse(modelID, out var generativeModelID);
            Guid.TryParse(api.State.SelectRecolorModel(), out var recolorModelID);

            var ids = new List<Guid>();
            int[] customSeeds = {};

            List<Stream> streamsToDispose = new();

            using var progressTokenSource1 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.25f,
                    value => SetProgress(progress with { progress = value }, "Sending request."),
                    1, progressTokenSource1.Token);

                BatchOperationResult<ImageGenerateResult> generateResults = null;
                BatchOperationResult<ImageTransformResult> transformResults = null;

                var storeMainAssetTask = Task.FromResult<OperationResult<BlobAssetResult>>(null);

                var refineAsset = refinementMode
                    is RefinementMode.RemoveBackground or RefinementMode.Upscale
                    or RefinementMode.Recolor or RefinementMode.Pixelate
                    or RefinementMode.Inpaint;

                // main asset is only uploaded when refining
                if (refineAsset)
                {
                    var mainAssetStream = await UnsavedAssetStream(api.State, asset);
                    streamsToDispose.Add(mainAssetStream); // in use until FinalizeStoreAsset is called
                    var streamToStore = mainAssetStream;

                    // the current Pixelate model doesn't support indexed color pngs so we need to check that
                    if (refinementMode == RefinementMode.Pixelate && ImageFileUtilities.IsPng(mainAssetStream) &&
                        ImageFileUtilities.IsPngIndexedColor(mainAssetStream) && ImageFileUtilities.TryConvert(mainAssetStream, out var convertedStream))
                    {
                        streamsToDispose.Add(convertedStream);
                        streamToStore = convertedStream;
                    }

                    storeMainAssetTask = assetComponent.StoreAssetWithResult(streamToStore, httpClientLease.client);
                }

                switch (refinementMode)
                {
                    case RefinementMode.Recolor:
                    {
                        var paletteAssetGuid = Guid.Empty;

                        var paletteImageReference = imageReferences[refinementMode][ImageReferenceType.PaletteImage];
                        if (paletteImageReference.SelectImageReferenceIsValid())
                        {
                            await using var paletteAsset = await paletteImageReference.SelectImageReferenceStream();

                            // 2x3 pixels expected from CreatePaletteApproximation
                            await using var paletteApproximation = await TextureUtils.CreatePaletteApproximation(paletteAsset);
                            if (!FinalizeStoreAsset(await assetComponent.StoreAssetWithResult(paletteApproximation, httpClientLease.client), out paletteAssetGuid))
                                return;
                        }

                        // await as late as possible as the reference asset might be quite large to upload and we want this done in parallel to the palette
                        if (!FinalizeStoreAsset(await storeMainAssetTask, out var assetGuid))
                            return;

                        var request = ImageGenerateRequestBuilder.Initialize(recolorModelID, dimensions.x, dimensions.y, useCustomSeed ? customSeed : null)
                            .Recolor(new Recolor(assetGuid, paletteAssetGuid));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        generateResults = await imageComponent.Generate(requests);
                        break;
                    }
                    case RefinementMode.Pixelate:
                    {
                        if (!FinalizeStoreAsset(await storeMainAssetTask, out var assetGuid))
                            return;

                        var requests = ImageTransformRequestBuilder.Initialize().Pixelate(new Pixelate(assetGuid, pixelateResizeToTargetSize,
                            pixelateTargetSize, pixelatePixelBlockSize, pixelateMode, pixelateOutlineThickness)).AsSingleInAList();
                        transformResults = await imageComponent.Transform(requests);
                        break;
                    }
                    case RefinementMode.Inpaint:
                    {
                        var maskGuid = Guid.Empty;

                        var inpaintMaskImageReference = imageReferences[refinementMode][ImageReferenceType.InPaintMaskImage];
                        if (inpaintMaskImageReference.SelectImageReferenceIsValid())
                        {
                            await using var maskAsset = ImageFileUtilities.CheckImageSize(await inpaintMaskImageReference.SelectImageReferenceStream());
                            if (!FinalizeStoreAsset(await assetComponent.StoreAssetWithResult(maskAsset, httpClientLease.client), out maskGuid))
                                return;
                        }

                        // await as late as possible as the reference asset might be quite large to upload
                        if (!FinalizeStoreAsset(await storeMainAssetTask, out var assetGuid))
                            return;

                        var request = ImageGenerateRequestBuilder
                            .Initialize(generativeModelID, dimensions.x, dimensions.y, useCustomSeed ? customSeed : null)
                            .GenerateWithMaskReference(new TextPrompt(prompt, negativePrompt),
                                new MaskReference(assetGuid, maskGuid, inpaintMaskImageReference.strength));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        generateResults = await imageComponent.Generate(requests);
                        break;
                    }
                    case RefinementMode.RemoveBackground:
                    {
                        if (!FinalizeStoreAsset(await storeMainAssetTask, out var assetGuid))
                            return;

                        var request = ImageTransformRequestBuilder.Initialize().RemoveBackground(new RemoveBackground(assetGuid));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        transformResults = await imageComponent.Transform(requests);
                        break;
                    }
                    case RefinementMode.Upscale:
                    {
                        if (!FinalizeStoreAsset(await storeMainAssetTask, out var assetGuid))
                            return;

                        var request = ImageTransformRequestBuilder.Initialize().Upscale(new (assetGuid, upscaleFactor, null, null));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        transformResults = await imageComponent.Transform(requests);
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        var requestBuilder =
                            ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, useCustomSeed ? customSeed : null);
                        var textPrompt = new TextPrompt(prompt, negativePrompt);

                        Dictionary<ImageReferenceType, Task<OperationResult<BlobAssetResult>>> referenceAssetTasks = new();
                        foreach (var (imageReferenceType, imageReference) in imageReferences[refinementMode])
                        {
                            if (!imageReference.SelectImageReferenceIsValid())
                                continue;

                            var referenceAsset = ImageFileUtilities.CheckImageSize(await imageReference.SelectImageReferenceStream());
                            streamsToDispose.Add(referenceAsset);
                            referenceAssetTasks.Add(imageReferenceType, assetComponent.StoreAssetWithResult(referenceAsset, httpClientLease.client));
                        }

                        // await as late as possible as we want to upload everything in parallel
                        await Task.WhenAll(referenceAssetTasks.Values);

                        var referenceGuids = imageReferences[refinementMode].ToDictionary(kvp => kvp.Key, _ => Guid.Empty);
                        foreach (var (imageReferenceType, referenceAssetTask) in referenceAssetTasks)
                        {
                            if (!FinalizeStoreAsset(await referenceAssetTask, out var referenceGuid))
                                return;
                            referenceGuids[imageReferenceType] = referenceGuid;
                        }

                        try
                        {
                            var request = requestBuilder.GenerateWithReferences(textPrompt,
                                referenceGuids[ImageReferenceType.PromptImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.PromptImage], imageReferences[refinementMode][ImageReferenceType.PromptImage].strength) : null,
                                referenceGuids[ImageReferenceType.StyleImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.StyleImage], imageReferences[refinementMode][ImageReferenceType.StyleImage].strength) : null,
                                referenceGuids[ImageReferenceType.CompositionImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.CompositionImage], imageReferences[refinementMode][ImageReferenceType.CompositionImage].strength) : null,
                                referenceGuids[ImageReferenceType.PoseImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.PoseImage], imageReferences[refinementMode][ImageReferenceType.PoseImage].strength) : null,
                                referenceGuids[ImageReferenceType.DepthImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.DepthImage], imageReferences[refinementMode][ImageReferenceType.DepthImage].strength) : null,
                                referenceGuids[ImageReferenceType.LineArtImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.LineArtImage], imageReferences[refinementMode][ImageReferenceType.LineArtImage].strength) : null,
                                referenceGuids[ImageReferenceType.FeatureImage] != Guid.Empty ? new (referenceGuids[ImageReferenceType.FeatureImage], imageReferences[refinementMode][ImageReferenceType.FeatureImage].strength) : null);

                            var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            generateResults = await imageComponent.Generate(requests);
                        }
                        catch (UnhandledReferenceCombinationException e)
                        {
                            var messages = new[] { $"{e.responseError.ToString()}: {e.Message}" };
                            api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                                new(arg.asset,
                                    new(false, e.responseError, 0,
                                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }
                        break;
                    }
                }

                if (generateResults != null)
                {
                    if (!generateResults.Batch.IsSuccessful)
                    {
                        LogFailedBatchResult(generateResults);
                        // we can simply return because the error is already logged and we rely on finally statements for cleanup
                        return;
                    }

                    foreach (var generateResult in generateResults.Batch.Value.Where(v => !v.IsSuccessful))
                    {
                        LogFailedResult(generateResult.Error);
                        // we can simply return because the error is already logged and we rely on finally statements for cleanup
                        return;
                    }

                    cost = generateResults.Batch.Value.Sum(itemResult => itemResult.Value.PointsCost);
                    ids = generateResults.Batch.Value.Select(itemResult => itemResult.Value.JobId).ToList();
                    customSeeds = generateResults.Batch.Value.Select(result => result.Value.Request.Seed ?? -1).ToArray();
                }

                if (transformResults != null)
                {
                    if (!transformResults.Batch.IsSuccessful)
                    {
                        LogFailedBatchResult(transformResults);
                        // we can simply return because the error is already logged and we rely on finally statements for cleanup
                        return;
                    }

                    foreach (var transformResult in transformResults.Batch.Value.Where(v => !v.IsSuccessful))
                    {
                        LogFailedResult(transformResult.Error);
                        // we can simply return because the error is already logged and we rely on finally statements for cleanup
                        return;
                    }

                    cost = transformResults.Batch.Value.Sum(itemResult => itemResult.Value.PointsCost);
                    ids = transformResults.Batch.Value.Select(itemResult => itemResult.Value.JobId).ToList();
                }
            }
            catch
            {
                SetProgress(progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                foreach (var stream in streamsToDispose)
                    _ = stream?.DisposeAsync();
                ReenableGenerateButton(); // after validation
                progressTokenSource1.Cancel();
            }

            /*
             * If you got here, points were consumed so a restore point is saved
             */

            AIToolbarButton.ShowPointsCostNotification(cost);

            var downloadImagesData = new DownloadImagesData(asset, ids, arg.taskID, generationMetadata,
                refinementMode is RefinementMode.RemoveBackground or RefinementMode.Pixelate or RefinementMode.Upscale or RefinementMode.Recolor,
                generationSetting.replaceBlankAsset, generationSetting.replaceRefinementAsset, customSeeds);
            GenerationRecoveryUtils.AddInterruptedDownload(downloadImagesData); // 'potentially' interrupted

            if (WebUtilities.simulateClientSideFailures)
                throw new Exception("Some simulated client side failure.");

            await api.Dispatch(downloadImages, downloadImagesData, CancellationToken.None);
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
                var skeletons = Enumerable.Range(0, count).Select(i => new TextureSkeleton(arg.taskID, i)).ToList();
                api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));
            }
        });

        public static readonly AsyncThunkCreatorWithArg<DownloadImagesData> downloadImages = new($"{GenerationResultsActions.slice}/downloadImagesSuperProxy", async (arg, api) =>
        {
            var variations = arg.ids.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new TextureSkeleton(arg.taskID, i)).ToList();
            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));

            var progress = new GenerationProgressData(arg.taskID, variations, 0.25f );

            SetProgress(progress with { progress = 0.25f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                LogInvalidCloudProjectSettings();
                return;
            }
            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            SetProgress(progress with { progress = 0.25f }, "Waiting for server.");

            List<TextureResult> generatedImages;

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset) ,enableDebugLogging: true, defaultOperationTimeout: Constants.noTimeout);
            var assetComponent = builder.AssetComponent();

            using var progressTokenSource2 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.26f, 0.75f,
                    value => SetProgress(progress with { progress = 0.25f }, "Waiting for server."),
                    variations, progressTokenSource2.Token);

                var tasks = arg.ids.Select(async jobId => (jobId, await assetComponent.CreateAssetDownloadUrl(jobId, Constants.noTimeout, LogJobUpdates)));
                var assetResults = await Task.WhenAll(tasks);
                generatedImages = assetResults.Select(pair =>
                {
                    var (_, result) = pair;
                    if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                        return TextureResult.FromUrl(result.Result.Value.AssetUrl.Url);

                    if (result.Result.IsSuccessful)
                        LogFailedDownload(new AiOperationFailedResult(AiResultErrorEnum.Unknown, new List<string> { "Simulated server timeout" }));
                    else
                        LogFailedDownloadResult(result);
                    throw new HandledFailureException();
                }).ToList();
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
                        backupSuccess = false;
                }
            }

            // cache
            using var progressTokenSource4 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.99f,
                    value => SetProgress(progress with { progress = value }, "Downloading results."),
                    1, progressTokenSource4.Token);

                var generativePath = arg.asset.GetGeneratedAssetsPath();
                var metadata = arg.generationMetadata;
                var saveTasks = generatedImages.Select((result, index) =>
                {
                    var metadataCopy = metadata with { };
                    if (arg.customSeeds.Length > 0 && generatedImages.Count == arg.customSeeds.Length)
                        metadataCopy.customSeed = arg.customSeeds[index];
                    return result.DownloadToProject(metadataCopy, generativePath, httpClientLease.client);
                }).ToList();

                await Task.WhenAll(saveTasks); // saves to project and is picked up by GenerationFileSystemWatcherManipulator

                // the ui defers the removal of the skeletons a little bit so we can call this pretty early
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.taskID));
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
                    api.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(arg.asset, true));
            }

            SetProgress(progress with { progress = 1f }, "Done.");

            // if you got here, no need to keep the potentially interrupted download
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

            void LogJobUpdates(JobStatusSdkEnum jobStatus)
            {
                if (s_LastJobStatus == jobStatus)
                    return;
                s_LastJobStatus = jobStatus;
                if (LoggerUtilities.sdkLogLevel == 0)
                    return;
                Debug.Log($"Job status: {jobStatus}");
            }
        });

        public static async Task<Stream> UnsavedAssetStream(IState state, AssetReference asset) => ImageFileUtilities.CheckImageSize(await state.SelectUnsavedAssetStreamWithFallback(asset));

        static JobStatusSdkEnum s_LastJobStatus = JobStatusSdkEnum.None;
    }
}
