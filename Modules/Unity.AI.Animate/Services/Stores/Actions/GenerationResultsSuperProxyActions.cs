﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using AiEditorToolsSdk.Components.Modalities.Animation.Requests.Generate;
using AiEditorToolsSdk.Components.Modalities.Animation.Responses;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using Task = System.Threading.Tasks.Task;
using Unity.AI.Toolkit.Accounts;
using Unity.AI.Generators.Sdk;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Generators.Sdk.Logger;

namespace Unity.AI.Animate.Services.Stores.Actions
{
    static class GenerationResultsSuperProxyActions
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();

        public static readonly AsyncThunkCreatorWithArg<QuoteAnimationsData> quoteAnimations = new($"{GenerationResultsActions.slice}/quoteAnimationsSuperProxy", async (arg, api) =>
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

                var prompt = generationSetting.SelectPrompt();
                var modelID = generationSetting.SelectSelectedModelID();
                var roundedFrameDuration = generationSetting.SelectRoundedFrameDuration();
                var variations = generationSetting.SelectVariationCount();
                var seed = Random.Range(0, int.MaxValue - variations);
                var refinementMode = generationSetting.SelectRefinementMode();

                Guid.TryParse(modelID, out var generativeModelID);

                var referenceVideoGuid = generationSetting.SelectVideoReference().asset.IsValid() ? Guid.NewGuid() : Guid.Empty;

                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);

                var animationComponent = builder.AnimationComponent();

                var requests = new List<AnimationGenerateRequest>();
                switch (refinementMode)
                {
                    case RefinementMode.VideoToMotion:
                    {
                        var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID).GenerateWithReference(referenceVideoGuid);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        break;
                    }
                    case RefinementMode.TextToMotion:
                    {
                        const float defaultTemperature = 0;
                        var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID).Generate(AnimationClipUtilities.bipedVersion.ToString(), prompt,
                            roundedFrameDuration, seed, defaultTemperature);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        break;
                    }
                }
                var quoteResults = await animationComponent.GenerateAnimationQuote(requests, Constants.realtimeTimeout, arg.cancellationTokenSource.Token);
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

            void SendValidatingMessage()
            {
                var messages = new[] { "Validating generation inputs..." };
                api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(false, AiResultErrorEnum.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
            }
        });

        public static readonly AsyncThunkCreatorWithArg<GenerateAnimationsData> generateAnimations = new($"{GenerationResultsActions.slice}/generateAnimationsSuperProxy", async (arg, api) =>
        {
            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var prompt = generationSetting.SelectPrompt();
            var modelID = api.State.SelectSelectedModelID(asset);
            var videoReference = generationSetting.SelectVideoReference();
            var roundedFrameDuration = generationSetting.SelectRoundedFrameDuration();
            var variations = generationSetting.SelectVariationCount();
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();
            var seed = useCustomSeed ? Math.Clamp(customSeed, 0, int.MaxValue - variations) : Random.Range(0, int.MaxValue - variations);
            var refinementMode = generationSetting.SelectRefinementMode();
            var cost = 0;

            Guid.TryParse(modelID, out var generativeModelID);

            DispatchSkeletons(variations);

            var progress = new GenerationProgressData(arg.taskID, variations, 0f);
            SetProgress(progress with { progress = 0.0f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                LogInvalidCloudProjectSettings();
                return;
            }

            SetProgress(progress with { progress = 0.01f }, "Preparing request.");

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            var referenceVideoGuid = Guid.Empty;
            if (refinementMode == RefinementMode.VideoToMotion)
            {
                if (videoReference.asset.IsValid())
                {
                    using var progressTokenSource0 = new CancellationTokenSource();
                    try
                    {
                        _ = ProgressUtils.RunFuzzyProgress(0.01f, 0.15f,
                            value => SetProgress(progress with { progress = value }, "Converting video."),
                            1, progressTokenSource0.Token);

                        var videoClip = videoReference.asset.GetObject<VideoClip>();
                        await using var uploadStream = !Path.GetExtension(videoReference.asset.GetPath()).Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                            videoClip.length > 10
                                ? await videoClip.ConvertAsync(0, 10)
                                : FileIO.OpenReadAsync(videoReference.asset.GetPath());

                        var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                            projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                            unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                        var assetComponent = builder.AssetComponent();

                        if (!FinalizeStoreAsset(await assetComponent.StoreAssetWithResult(uploadStream, httpClientLease.client), out referenceVideoGuid))
                            return;
                    }
                    catch
                    {
                        SetProgress(progress with { progress = 1f }, "Failed.");
                        throw;
                    }
                    finally
                    {
                        progressTokenSource0.Cancel();
                    }
                }
            }

            var ids = new List<Guid>();
            int[] customSeeds = {};
            using var progressTokenSource2 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.15f, 0.25f,
                    value => SetProgress(progress with { progress = value }, "Sending request for motion."),
                    1, progressTokenSource2.Token);

                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true );
                var animationComponent = builder.AnimationComponent();

                var requests = new List<AnimationGenerateRequest>();
                switch (refinementMode)
                {
                    case RefinementMode.VideoToMotion:
                    {
                        var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID).GenerateWithReference(referenceVideoGuid);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        break;
                    }
                    case RefinementMode.TextToMotion:
                    {
                        const float defaultTemperature = 0;
                        var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID).Generate(AnimationClipUtilities.bipedVersion.ToString(), prompt,
                            roundedFrameDuration, seed, defaultTemperature);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        break;
                    }
                }
                var generateResults = await animationComponent.GenerateAnimation(requests);
                if (!generateResults.Batch.IsSuccessful)
                {
                    LogFailedBatchResult(generateResults);
                    // we can simply return without throwing or additional logging because the error is already logged
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
            catch
            {
                SetProgress(progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                ReenableGenerateButton(); // after validation
                progressTokenSource2.Cancel();
            }

            /*
             * If you got here, points were consumed so a restore point is saved
             */

            AIToolbarButton.ShowPointsCostNotification(cost);

            var downloadAnimationData = new DownloadAnimationsData(asset, ids, customSeeds, arg.taskID, generationMetadata);
            GenerationRecoveryUtils.AddInterruptedDownload(downloadAnimationData); // 'potentially' interrupted

            if (WebUtilities.simulateClientSideFailures)
                throw new Exception("Some simulated client side failure.");

            await api.Dispatch(downloadAnimationClips, downloadAnimationData, CancellationToken.None);
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

            void LogFailedBatchResult(BatchOperationResult<AnimationGenerateResult> results)
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

        class HandledFailureException : Exception { }

        public static readonly AsyncThunkCreatorWithArg<DownloadAnimationsData> downloadAnimationClips = new($"{GenerationResultsActions.slice}/downloadAnimationsSuperProxy", async (arg, api) =>
        {
            var variations = arg.ids.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new TextureSkeleton(arg.taskID, i)).ToList();
            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons)) ;

            var progress = new GenerationProgressData(arg.taskID, variations, 0.25f);

            SetProgress(progress with { progress = 0.25f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                LogInvalidCloudProjectSettings();
                return;
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            SetProgress(progress with { progress = 0.25f }, "Waiting for server.");

            List<AnimationClipResult> generatedAnimationClips;

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true, defaultOperationTimeout: Constants.noTimeout);
            var assetComponent = builder.AssetComponent();

            using var progressTokenSource2 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.26f, 0.75f,
                    value => SetProgress(progress with { progress = 0.25f }, "Waiting for server."),
                    variations, progressTokenSource2.Token);

                var tasks = arg.ids.Select(async jobId => (jobId, await assetComponent.CreateAssetDownloadUrl(jobId, Constants.noTimeout)));
                var assetResults = await Task.WhenAll(tasks);
                generatedAnimationClips = assetResults.Select(pair =>
                {
                    var (_, result) = pair;
                    if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                        return AnimationClipResult.FromUrl(result.Result.Value.AssetUrl.Url);

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

            if (api.CancellationToken.IsCancellationRequested)
            {
                Debug.Log($"Download canceled.");
                api.Cancel();
            }

            // initial 'backup'
            var backupSuccess = true;
            var assetWasBlank = false;
            if (!api.State.HasHistory(arg.asset))
            {
                assetWasBlank = await arg.asset.IsBlank();
                if (!assetWasBlank)
                    backupSuccess = await arg.asset.SaveToGeneratedAssets();
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
                var saveTasks = generatedAnimationClips.Select((t, index) =>
                {
                    var metadataCopy = metadata with { };
                    if (arg.customSeeds.Length > 0 && generatedAnimationClips.Count == arg.customSeeds.Length)
                        metadataCopy.customSeed = arg.customSeeds[index];
                    return t.DownloadToProject(metadataCopy, generativePath, httpClientLease.client);
                }).ToList();
                await Task.WhenAll(saveTasks); // saves to project and is picked up by GenerationFileSystemWatcherManipulator

                // the ui defers the removal of the skeletons a little bit so we can call this pretty early
                api.Dispatch(GenerationResultsActions.removeGeneratedSkeletons, new(arg.asset, arg.taskID));
            }
            catch
            {
                SetProgress(progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                progressTokenSource4.Cancel();
            }

            // auto-apply if blank or if RefinementMode
            if (generatedAnimationClips.Count > 0 && (assetWasBlank || arg.autoApply))
            {
                await api.Dispatch(GenerationResultsActions.selectGeneration, new(arg.asset, generatedAnimationClips[0], backupSuccess, !assetWasBlank));
                AssetDatabase.ImportAsset(arg.asset.GetPath(), ImportAssetOptions.ForceUpdate);
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
        });
    }
}
