﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Modalities.Audio.Requests.Generate;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Sound.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Random = UnityEngine.Random;

namespace Unity.AI.Sound.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteAudioData> quoteAudioClips = new($"{GenerationResultsActions.slice}/quoteAudioClipsSuperProxy", QuoteAudioClipsAsync);

        static async Task QuoteAudioClipsAsync(QuoteAudioData arg, AsyncThunkApi<bool> api)
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
                api.DispatchValidatingUserMessage(arg.asset);

                var success = await WebUtilities.WaitForCloudProjectSettings(arg.asset);

                api.DispatchValidatingMessage(arg.asset);

                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                if (!success)
                {
                    var messages = new[]
                    {
                        $"Invalid Unity Cloud configuration. Could not obtain organizations for user \"{UnityConnectProvider.userName}\"."
                    };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, AiResultErrorEnum.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var asset = new AssetReference { guid = arg.asset.guid };

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (!asset.Exists())
                {
                    var messages = new[] { "Selected asset is invalid. Please select a valid asset." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, AiResultErrorEnum.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
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
                    var messages = new[] { "No model selected. Please select a valid model." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, AiResultErrorEnum.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);
                var audioComponent = builder.AudioComponent();

                var referenceAudioGuid = Guid.Empty;
                if (soundReference.asset.IsValid())
                {
                    referenceAudioGuid = Guid.NewGuid();
                }

                List<AudioGenerateRequest> requests;
                if (referenceAudioGuid != Guid.Empty)
                {
                    var request = AudioGenerateRequestBuilder.Initialize(generativeModelID, prompt, duration)
                        .GenerateWithReference(referenceAudioGuid, referenceAudioStrength, negativePrompt, seed);
                    requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                }
                else
                {
                    var request = AudioGenerateRequestBuilder.Initialize(generativeModelID, prompt, duration).Generate(negativePrompt, seed);
                    requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                }

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                var quoteResults = await EditorTask.Run(() => audioComponent.GenerateQuote(requests, Constants.realtimeTimeout, linkedTokenSource.Token), linkedTokenSource.Token);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (!quoteResults.Result.IsSuccessful)
                {
                    var errorEnum = quoteResults.Result.Error.AiResponseError;
                    var messages = quoteResults.Result.Error.Errors.Count == 0
                        ? new[] { $"An error occurred during validation ({WebUtils.selectedEnvironment})." }
                        : quoteResults.Result.Error.Errors.Distinct().ToArray();

                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(quoteResults.Result.IsSuccessful, errorEnum, 0,
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
                // Only dispose if this is still the current token source for this asset
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                {
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                }

                cancellationTokenSource.Dispose();
            }
        }
    }
}
