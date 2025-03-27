using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses;
using AiEditorToolsSdk.Components.Modalities.Animation.Requests.Generate;
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
        public static readonly AsyncThunkCreatorWithArg<QuoteAnimationsData> quoteAnimations = new($"{GenerationResultsActions.slice}/quoteAnimationsSuperProxy", async (arg, api) =>
        {
            var success = await WebUtilities.WaitForCloudProjectSettings(arg.asset);
            if (!success)
            {
                var messages = new[] { $"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\"." };
                api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                    new (arg.asset,
                        new (false, AiResultErrorEnum.UnknownError, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                return;
            }

            var asset = new AssetReference { guid = arg.asset.guid };
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
            var quoteResults = await animationComponent.GenerateAnimationQuote(requests, Constants.realtimeTimeout);
            if (!quoteResults.Result.IsSuccessful)
            {
                var messages = quoteResults.Result.Error.Errors.Count == 0
                    ? new[] { $"Error reason is '{quoteResults.Result.Error.AiResponseError.ToString()}' and no additional error information was provided ({WebUtils.selectedEnvironment})." }
                    : quoteResults.Result.Error.Errors.Distinct().Select(m => $"{quoteResults.Result.Error.AiResponseError.ToString()}: {m}").ToArray();

                api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                    new (arg.asset,
                        new (quoteResults.Result.IsSuccessful, quoteResults.Result.Error.AiResponseError, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                return;
            }

            api.Dispatch(GenerationResultsActions.setGenerationValidationResult,
                new(arg.asset,
                    new(quoteResults.Result.IsSuccessful,
                        !quoteResults.Result.IsSuccessful ? quoteResults.Result.Error.AiResponseError : AiResultErrorEnum.UnknownError,
                        quoteResults.Result.Value.PointsCost, new List<GenerationFeedbackData>())));
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
                                : videoReference.asset.GetFileStream();

                        var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                            projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                            unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                        var assetComponent = builder.AssetComponent();

                        var result = await assetComponent.StoreAssetWithResult(uploadStream, httpClientLease.client);
                        if (!result.Result.IsSuccessful)
                        {
                            LogFailedResult(result.Result.Error);
                            // we can simply return without throwing or additional logging because the error is already logged
                            return;
                        }

                        referenceVideoGuid = result.Result.Value.AssetId;
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
                    LogFailedResult(generateResults.Batch.Error);
                    // we can simply return without throwing or additional logging because the error is already logged
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

                    var failedResult = result.Result.IsSuccessful
                        ? new AiOperationFailedResult(AiResultErrorEnum.UnknownError, new List<string> { "Simulated server timeout" })
                        : result.Result.Error;
                    LogFailedDownload(failedResult);
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
                AssetDatabase.Refresh();
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
