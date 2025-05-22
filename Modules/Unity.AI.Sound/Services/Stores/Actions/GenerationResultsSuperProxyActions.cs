﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using AiEditorToolsSdk.Components.Modalities.Audio.Requests.Generate;
using AiEditorToolsSdk.Components.Modalities.Audio.Responses;
using Unity.AI.Sound.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using Unity.AI.Toolkit.Accounts;
using Unity.AI.Generators.Sdk;
using Unity.AI.Toolkit;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Generators.Sdk.Logger;
using Random = UnityEngine.Random;

namespace Unity.AI.Sound.Services.Stores.Actions
{
    static class GenerationResultsSuperProxyActions
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();

        public static readonly AsyncThunkCreatorWithArg<QuoteAudioData> quoteAudioClips = new($"{GenerationResultsActions.slice}/quoteAudioClipsSuperProxy", async (arg, api) =>
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
                    api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
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
                    api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(false, AiResultErrorEnum.Unknown, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                using var httpClientLease = HttpClientManager.instance.AcquireLease();
                var generationSetting = arg.generationSetting;

                var variations = generationSetting.SelectVariationCount();
                var duration = generationSetting.SelectGenerableDuration();
                var prompt = generationSetting.SelectPrompt();
                var negativePrompt = generationSetting.SelectNegativePrompt();
                var modelID = api.State.SelectSelectedModelID(asset);
                var soundReference = generationSetting.SelectSoundReference();
                var referenceAudioStrength = soundReference.strength;

                var seed = Random.Range(0, int.MaxValue - variations);
                Guid.TryParse(modelID, out var generativeModelID);

                if (generativeModelID == Guid.Empty)
                {
                    var messages = new[] { $"Error reason is 'Invalid Model'." };
                    api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(false, AiResultErrorEnum.UnknownModel, 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);
                var audioComponent = builder.AudioComponent();

                var referenceAudioGuid = Guid.Empty;
                if (soundReference.asset.IsValid())
                    referenceAudioGuid = Guid.NewGuid();

                List<AudioGenerateRequest> requests;
                if (referenceAudioGuid != Guid.Empty)
                {
                    var request = AudioGenerateRequestBuilder
                        .Initialize(generativeModelID, prompt, duration)
                        .GenerateWithReference(referenceAudioGuid, referenceAudioStrength, negativePrompt, seed);
                    requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                }
                else
                {
                    var request = AudioGenerateRequestBuilder
                        .Initialize(generativeModelID, prompt, duration)
                        .Generate(negativePrompt, seed);
                    requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                }

                var quoteResults = await EditorTask.Run(() => audioComponent.GenerateQuote(requests, Constants.realtimeTimeout, cancellationTokenSource.Token));

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    SendValidatingMessage();
                    return;
                }

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
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                cancellationTokenSource.Dispose();
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

        public static readonly AsyncThunkCreatorWithArg<GenerateAudioData> generateAudioClips = new($"{GenerationResultsActions.slice}/generateAudioClipsSuperProxy", async (arg, api) =>
        {
            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = generationSetting.SelectVariationCount();
            var cost = 0;

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

            var duration = generationSetting.SelectGenerableDuration();
            var prompt = generationSetting.SelectPrompt();
            var negativePrompt = generationSetting.SelectNegativePrompt();
            var modelID = api.State.SelectSelectedModelID(asset);
            var soundReference = generationSetting.SelectSoundReference();
            var referenceAudioStrength = soundReference.strength;
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();
            // clamping is important as the backend will increment the value
            var seed = useCustomSeed ? Math.Clamp(customSeed, 0, int.MaxValue - variations) : Random.Range(0, int.MaxValue - variations);

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
            var audioComponent = builder.AudioComponent();
            var assetComponent = builder.AssetComponent();

            Guid.TryParse(modelID, out var generativeModelID);

            var ids = new List<Guid>();
            int[] customSeeds = {};

            using var progressTokenSource1 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.01f, 0.25f,
                    value => SetProgress(progress with { progress = value }, "Sending request for sound."),
                    1, progressTokenSource1.Token);

                // fixme: if overwriteSoundReferenceAsset is false AND we have a recording I don't think this works, should work more like a doodle
                var referenceAudioGuid = Guid.Empty;
                if (soundReference.asset.IsValid())
                {
                    await using var uploadStream = await generationSetting.SelectReferenceAssetStream();
                    if (!FinalizeStoreAsset(await assetComponent.StoreAssetWithResult(uploadStream, httpClientLease.client), out referenceAudioGuid))
                        return;
                }

                List<AudioGenerateRequest> requests;
                if (referenceAudioGuid != Guid.Empty)
                {
                    var request = AudioGenerateRequestBuilder
                        .Initialize(generativeModelID, prompt, duration)
                        .GenerateWithReference(referenceAudioGuid, referenceAudioStrength, negativePrompt, seed);
                    requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                }
                else
                {
                    var request = AudioGenerateRequestBuilder
                        .Initialize(generativeModelID, prompt, duration)
                        .Generate(negativePrompt, seed);
                    requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                }
                var generateResults = await EditorTask.Run(() => audioComponent.Generate(requests));
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
            }
            catch
            {
                SetProgress(progress with { progress = 1f }, "Failed.");
                throw;
            }
            finally
            {
                ReenableGenerateButton(); // after validation
                progressTokenSource1.Cancel();
            }

            /*
             * If you got here, points were consumed so a restore point is saved
             */

            AIToolbarButton.ShowPointsCostNotification(cost);

            var downloadAudioData = new DownloadAudioData(asset, ids, arg.taskID, generationMetadata, customSeeds);
            GenerationRecoveryUtils.AddInterruptedDownload(downloadAudioData); // 'potentially' interrupted

            if (WebUtilities.simulateClientSideFailures)
                throw new Exception("Some simulated client side failure.");

            await api.Dispatch(downloadAudioClips, downloadAudioData, CancellationToken.None);
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

            void LogFailedBatchResult(BatchOperationResult<AudioGenerateResult> results)
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

        public static readonly AsyncThunkCreatorWithArg<DownloadAudioData> downloadAudioClips = new($"{GenerationResultsActions.slice}/downloadAudioClipsSuperProxy", async (arg, api) =>
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

            List<AudioClipResult> generatedAudioClips;

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

                var assetResults = new List<(Guid jobId, OperationResult<BlobAssetResult>)>();
                foreach (var jobId in arg.ids)
                {
                    // need to be very careful, we're taking each in turn to guarantee paused play mode support
                    // there's not much drawback as the generations are started way before
                    var url = await EditorTask.Run(() => assetComponent.CreateAssetDownloadUrl(jobId, Constants.noTimeout));
                    assetResults.Add((jobId, url));
                }

                generatedAudioClips = assetResults.Select(pair =>
                {
                    var (_, result) = pair;
                    if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                        return AudioClipResult.FromUrl(result.Result.Value.AssetUrl.Url);

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
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.95f,
                    value => SetProgress(progress with { progress = value }, "Downloading results."),
                    1, progressTokenSource4.Token);

                var generativePath = arg.asset.GetGeneratedAssetsPath();
                var metadata = arg.generationMetadata;
                var saveTasks = generatedAudioClips.Select((t, index) =>
                {
                    var metadataCopy = metadata with { };
                    if (arg.customSeeds.Length > 0 && generatedAudioClips.Count == arg.customSeeds.Length)
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

            if (arg.generationMetadata.autoTrim)
            {
                // auto-trim on sounds generated from prompts, crop on sounds generated from sound references
                using var progressTokenSource5 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.95f, 0.99f,
                        value => SetProgress(progress with { progress = value }, "Processing results."),
                        1, progressTokenSource5.Token);

                    var postProcessTasks = arg.generationMetadata.hasReference
                        ? generatedAudioClips.Select(t => t.Crop(arg.generationMetadata.duration)).ToList()
                        : generatedAudioClips.Select(t => t.AutoTrim(arg.generationMetadata.duration)).ToList();
                    await Task.WhenAll(postProcessTasks);
                }
                finally
                {
                    progressTokenSource5.Cancel();
                }
            }

            // auto-apply if blank or if RefinementMode
            if (generatedAudioClips.Count > 0 && (assetWasBlank || arg.autoApply))
            {
                await api.Dispatch(GenerationResultsActions.selectGeneration, new(arg.asset, generatedAudioClips[0], backupSuccess, !assetWasBlank));
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
