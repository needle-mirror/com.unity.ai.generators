using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
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
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Toolkit;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Generators.Sdk.Logger;

namespace Unity.AI.Animate.Services.Stores.Actions.Backend
{
    static class Generation
    {
        public static readonly AsyncThunkCreatorWithArg<GenerateAnimationsData> generateAnimations =
            new($"{GenerationResultsActions.slice}/generateAnimationsSuperProxy", GenerateAnimationsAsync);

        static async Task GenerateAnimationsAsync(GenerateAnimationsData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Generating sound.");

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

            var ids = new List<Guid>();
            int[] customSeeds = { };

            try
            {
                UploadReferencesData uploadReferences;

                try
                {
                    uploadReferences = await UploadReferencesAsync(asset, refinementMode, videoReference, api, progress);
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

                using var progressTokenSource2 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.15f, 0.25f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request for motion."), 1, progressTokenSource2.Token);

                    using var httpClientLease = HttpClientManager.instance.AcquireLease();

                    var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                        projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                    var animationComponent = builder.AnimationComponent();

                    var requests = new List<AnimationGenerateRequest>();
                    switch (refinementMode)
                    {
                        case RefinementMode.VideoToMotion:
                        {
                            var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID).GenerateWithReference(uploadReferences.referenceGuid);
                            requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            break;
                        }
                        case RefinementMode.TextToMotion:
                        {
                            const float defaultTemperature = 0;
                            var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID)
                                .Generate(AnimationClipUtilities.bipedVersion.ToString(), prompt, roundedFrameDuration, seed, defaultTemperature);
                            requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                            break;
                        }
                    }

                    var generateResults = await EditorTask.Run(() => animationComponent.GenerateAnimation(requests));
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

                    if (ids.Count == 0)
                    {
                        throw new HandledFailureException();
                    }
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
                    progressTokenSource2.Cancel();
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

            var downloadAnimationData = new DownloadAnimationsData(asset, ids, arg.progressTaskId, Guid.NewGuid(), generationMetadata, customSeeds, false, false);
            GenerationRecovery.AddInterruptedDownload(downloadAnimationData); // 'potentially' interrupted

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
                    downloadAnimationData = downloadAnimationData with { retryable = retryCount < maxRetries };
                    await DownloadAnimationClipsAsync(downloadAnimationData, api);
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

        record UploadReferencesData(Guid referenceGuid);

        static async Task<UploadReferencesData> UploadReferencesAsync(AssetReference asset, RefinementMode refinementMode, VideoInputReference videoReference, AsyncThunkApi<bool> api, GenerationProgressData progress)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Uploading video references");

            var referenceGuid = Guid.Empty;

            if (refinementMode == RefinementMode.VideoToMotion && videoReference.asset.IsValid())
            {
                api.DispatchProgress(asset, progress with { progress = 0.02f }, "Converting video.");

                var videoClip = videoReference.asset.GetObject<VideoClip>();
                await using var uploadStream =
                    !Path.GetExtension(videoReference.asset.GetPath()).Equals(".mp4", StringComparison.OrdinalIgnoreCase) || videoClip.length > 10
                        ? await videoClip.ConvertAsync(0, 10, deleteOutputOnClose: true,
                            progressCallback: value =>
                                api.DispatchProgress(asset, progress with { progress = Mathf.Max(progress.progress, value * 0.1f) }, "Converting video.", false) // video conversion does its own background reporting
                        )
                        : FileIO.OpenReadAsync(videoReference.asset.GetPath());

                using var progressTokenSource0 = new CancellationTokenSource();

                _ = ProgressUtils.RunFuzzyProgress(0.10f, 0.15f,
                    value => api.DispatchProgress(asset, progress with { progress = value },
                        "Uploading references.")
                    , 1, progressTokenSource0.Token);

                try
                {
                    using var httpClientLease = HttpClientManager.instance.AcquireLease();

                    var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                        projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment,
                        logger: new Logger(), unityAuthenticationTokenProvider: new AuthenticationTokenProvider(),
                        traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                    var assetComponent = builder.AssetComponent();

                    if (!api.DispatchStoreAssetMessage(asset, await assetComponent.StoreAssetWithResult(uploadStream, httpClientLease.client),
                            out referenceGuid))
                    {
                        throw new HandledFailureException();
                    }
                }
                finally
                {
                    progressTokenSource0.Cancel();
                }
            }

            return new(referenceGuid);
        }

        class HandledFailureException : Exception { }

        class DownloadTimeoutException : Exception { }

        public static readonly AsyncThunkCreatorWithArg<DownloadAnimationsData> downloadAnimationClips =
            new($"{GenerationResultsActions.slice}/downloadAnimationsSuperProxy", DownloadAnimationClipsAsync);

        static async Task DownloadAnimationClipsAsync(DownloadAnimationsData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Downloading animations.");

            var variations = arg.jobIds.Count;

            var skeletons = Enumerable.Range(0, variations).Select(i => new TextureSkeleton(arg.progressTaskId, i)).ToList();
            api.Dispatch(GenerationResultsActions.setGeneratedSkeletons, new(arg.asset, skeletons));

            var progress = new GenerationProgressData(arg.progressTaskId, variations, 0.25f);

            api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Authenticating with UnityConnect.");

            if (!WebUtilities.AreCloudProjectSettingsValid())
            {
                api.DispatchInvalidCloudProjectMessage(arg.asset);
                return;
            }

            using var httpClientLease = HttpClientManager.instance.AcquireLease();

            api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server.");

            var retryTimeout = arg.retryable ? Constants.motionRetryTimeout : Constants.noTimeout;
            using var retryTokenSource = new CancellationTokenSource(retryTimeout);

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true,
                defaultOperationTimeout: retryTimeout);
            var assetComponent = builder.AssetComponent();

            List<AnimationClipResult> generatedAnimationClips;

            using var progressTokenSource2 = new CancellationTokenSource();
            try
            {
                _ = ProgressUtils.RunFuzzyProgress(0.25f, 0.75f,
                    _ => api.DispatchProgress(arg.asset, progress with { progress = 0.25f }, "Waiting for server."), variations, progressTokenSource2.Token);

                var assetResults = new List<(Guid jobId, OperationResult<BlobAssetResult>)>();
                foreach (var jobId in arg.jobIds)
                {
                    // need to be very careful, we're taking each in turn to guarantee paused play mode support
                    // there's not much drawback as the generations are started way before
                    var url = await EditorTask.Run(() =>
                        assetComponent.CreateAssetDownloadUrl(jobId, retryTimeout, cancellationToken: retryTokenSource.Token), retryTokenSource.Token);
                    if (retryTokenSource.IsCancellationRequested)
                        throw new OperationCanceledException();
                    assetResults.Add((jobId, url));
                }

                generatedAnimationClips = assetResults.Select(pair =>
                    {
                        var (_, result) = pair;
                        if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                        {
                            return AnimationClipResult.FromUrl(result.Result.Value.AssetUrl.Url);
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

                            api.DispatchFailedDownloadMessage(arg.asset, result, arg.generationMetadata.w3CTraceId, arg.retryable);
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
                    backupSuccess = await arg.asset.SaveToGeneratedAssets();
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
                var saveTasks = generatedAnimationClips.Select((t, index) =>
                    {
                        var metadataCopy = metadata with { };
                        if (arg.customSeeds.Length > 0 && generatedAnimationClips.Count == arg.customSeeds.Length)
                        {
                            metadataCopy.customSeed = arg.customSeeds[index];
                        }

                        return t.DownloadToProject(metadataCopy, generativePath, httpClientLease.client);
                    })
                    .ToList();
                await Task.WhenAll(saveTasks); // saves to project and is picked up by GenerationFileSystemWatcherManipulator

                // the ui handles results, skeletons, and fulfilled skeletons to determine which tiles are ready for display (see Selectors.SelectGeneratedTexturesAndSkeletons)
                api.Dispatch(GenerationResultsActions.setFulfilledSkeletons,
                    new(arg.asset, generatedAnimationClips.Select(res => new FulfilledSkeleton(arg.progressTaskId, res.uri.GetAbsolutePath())).ToList()));
            }
            catch
            {
                api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Failed.");
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
                if (assetWasBlank)
                {
                    api.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(arg.asset, true));
                }
            }

            api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Done.");

            // if you got here, no need to keep the potentially interrupted download
            GenerationRecovery.RemoveInterruptedDownload(arg);
        }
    }
}
