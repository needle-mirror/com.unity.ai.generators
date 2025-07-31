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
using AiEditorToolsSdk.Components.Modalities.Audio.Requests.Generate;
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
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Toolkit;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Generators.Sdk.Logger;
using Random = UnityEngine.Random;

namespace Unity.AI.Sound.Services.Stores.Actions.Backend
{
    static class Generation
    {
        public static readonly AsyncThunkCreatorWithArg<GenerateAudioData> generateAudioClips =
            new($"{GenerationResultsActions.slice}/generateAudioClipsSuperProxy", GenerateAudioClipsAsync);

        static async Task GenerateAudioClipsAsync(GenerateAudioData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Generating audio clips.");

            var asset = new AssetReference { guid = arg.asset.guid };

            var generationSetting = arg.generationSetting;
            var generationMetadata = generationSetting.MakeMetadata(arg.asset);
            var variations = generationSetting.SelectVariationCount();
            var cost = 0;

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

            var duration = generationSetting.SelectGenerableDuration();
            var prompt = generationSetting.SelectPrompt();
            var negativePrompt = generationSetting.SelectNegativePrompt();
            var modelID = api.State.SelectSelectedModelID(asset);
            var soundReference = generationSetting.SelectSoundReference();
            var referenceAudioStrength = soundReference.strength;
            var (useCustomSeed, customSeed) = generationSetting.SelectGenerationOptions();

            // clamping is important as the backend will increment the value
            var seed = useCustomSeed ? Math.Clamp(customSeed, 0, int.MaxValue - variations) : Random.Range(0, int.MaxValue - variations);

            Guid.TryParse(modelID, out var generativeModelID);

            var ids = new List<Guid>();
            int[] customSeeds = { };

            try
            {
                UploadReferencesData uploadReferences;

                using var progressTokenSource0 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.02f, 0.15f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Uploading references."), 1, progressTokenSource0.Token);

                    uploadReferences = await UploadReferencesAsync(asset, soundReference, api);
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
                    progressTokenSource0.Cancel();
                }

                using var progressTokenSource1 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.15f, 0.25f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Sending request."), 1, progressTokenSource1.Token);

                    using var httpClientLease = HttpClientManager.instance.AcquireLease();

                    var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                        projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                    var audioComponent = builder.AudioComponent();

                    List<AudioGenerateRequest> requests;
                    if (uploadReferences.referenceGuid != Guid.Empty)
                    {
                        var request = AudioGenerateRequestBuilder.Initialize(generativeModelID, prompt, duration)
                            .GenerateWithReference(uploadReferences.referenceGuid, referenceAudioStrength, negativePrompt, seed);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                    }
                    else
                    {
                        var request = AudioGenerateRequestBuilder.Initialize(generativeModelID, prompt, duration).Generate(negativePrompt, seed);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                    }

                    var generateResults = await EditorTask.Run(() => audioComponent.Generate(requests));
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

            var downloadAudioData = new DownloadAudioData(asset, ids, arg.progressTaskId, Guid.NewGuid(), generationMetadata, customSeeds, false, false);
            GenerationRecovery.AddInterruptedDownload(downloadAudioData); // 'potentially' interrupted

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
                    downloadAudioData = downloadAudioData with { retryable = retryCount < maxRetries };
                    await DownloadAudioClipsAsync(downloadAudioData, api);
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

        static async Task<UploadReferencesData> UploadReferencesAsync(AssetReference asset, SoundReferenceState soundReference, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Uploading sound references.");

            var referenceGuid = Guid.Empty;

            if (soundReference.asset.IsValid())
            {
                using var httpClientLease = HttpClientManager.instance.AcquireLease();

                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true);
                var assetComponent = builder.AssetComponent();

                await using var uploadStream = await ReferenceAssetStream(api.State, asset);
                var assetStreamWithResult = await assetComponent.StoreAssetWithResult(uploadStream, httpClientLease.client);
                if (!api.DispatchStoreAssetMessage(asset, assetStreamWithResult, out referenceGuid))
                {
                    throw new HandledFailureException();
                }
            }

            return new(referenceGuid);
        }

        class HandledFailureException : Exception { }

        class DownloadTimeoutException : Exception { }

        public static readonly AsyncThunkCreatorWithArg<DownloadAudioData> downloadAudioClips =
            new($"{GenerationResultsActions.slice}/downloadAudioClipsSuperProxy", DownloadAudioClipsAsync);

        static async Task DownloadAudioClipsAsync(DownloadAudioData arg, AsyncThunkApi<bool> api)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Downloading audio clips.");

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

            var retryTimeout = arg.retryable ? Constants.soundRetryTimeout : Constants.noTimeout;

            var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(arg.asset), enableDebugLogging: true,
                defaultOperationTimeout: retryTimeout);
            var assetComponent = builder.AssetComponent();

            using var retryTokenSource = new CancellationTokenSource(retryTimeout);

            List<AudioClipResult> generatedAudioClips;

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

                generatedAudioClips = assetResults.Select(pair =>
                    {
                        var (_, result) = pair;
                        if (result.Result.IsSuccessful && !WebUtilities.simulateServerSideFailures)
                        {
                            return AudioClipResult.FromUrl(result.Result.Value.AssetUrl.Url);
                        }

                        if (result.Result.IsSuccessful)
                        {
                            api.DispatchFailedDownloadMessage(arg.asset,
                                new AiOperationFailedResult(AiResultErrorEnum.Unknown, new List<string> { "Simulated server timeout" }));
                        }
                        else
                        {
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
            catch (Exception)
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
                _ = ProgressUtils.RunFuzzyProgress(0.75f, 0.95f,
                    value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Downloading results."), 1, progressTokenSource4.Token);

                var generativePath = arg.asset.GetGeneratedAssetsPath();
                var metadata = arg.generationMetadata;
                var saveTasks = generatedAudioClips.Select((t, index) =>
                    {
                        var metadataCopy = metadata with { };
                        if (arg.customSeeds.Length > 0 && generatedAudioClips.Count == arg.customSeeds.Length)
                        {
                            metadataCopy.customSeed = arg.customSeeds[index];
                        }

                        return t.DownloadToProject(metadataCopy, generativePath, httpClientLease.client);
                    })
                    .ToList();
                await Task.WhenAll(saveTasks); // saves to project and is picked up by GenerationFileSystemWatcherManipulator

                // the ui handles results, skeletons, and fulfilled skeletons to determine which tiles are ready for display (see Selectors.SelectGeneratedTexturesAndSkeletons)
                api.Dispatch(GenerationResultsActions.setFulfilledSkeletons,
                    new(arg.asset, generatedAudioClips.Select(res => new FulfilledSkeleton(arg.progressTaskId, res.uri.GetAbsolutePath())).ToList()));
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

            if (arg.generationMetadata.autoTrim)
            {
                // auto-trim on sounds generated from prompts, crop on sounds generated from sound references
                using var progressTokenSource5 = new CancellationTokenSource();
                try
                {
                    _ = ProgressUtils.RunFuzzyProgress(0.95f, 0.99f,
                        value => api.DispatchProgress(arg.asset, progress with { progress = value }, "Processing results."), 1, progressTokenSource5.Token);

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
                {
                    api.Dispatch(GenerationResultsActions.setReplaceWithoutConfirmation, new ReplaceWithoutConfirmationData(arg.asset, true));
                }
            }

            api.DispatchProgress(arg.asset, progress with { progress = 1f }, "Done.");

            // if you got here, no need to keep the potentially interrupted download
            GenerationRecovery.RemoveInterruptedDownload(arg);
        }

        public static async Task<Stream> ReferenceAssetStream(IState state, AssetReference asset) => await state.SelectReferenceAssetStream(asset);
    }
}
