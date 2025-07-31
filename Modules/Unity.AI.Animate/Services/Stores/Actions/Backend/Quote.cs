using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Modalities.Animation.Requests.Generate;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit;
using UnityEditor;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Random = UnityEngine.Random;

namespace Unity.AI.Animate.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteAnimationsData> quoteAnimations =
            new($"{GenerationResultsActions.slice}/quoteAnimationsSuperProxy", QuoteAnimationsAsync);

        static async Task QuoteAnimationsAsync(QuoteAnimationsData arg, AsyncThunkApi<bool> api)
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
                    var messages = new[]
                    {
                        $"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\"."
                    };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, AiResultErrorEnum.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
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
                        new(arg.asset, new(false, AiResultErrorEnum.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
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

                if (generativeModelID == Guid.Empty)
                {
                    var messages = new[] { $"Error reason is 'Invalid Model'." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, AiResultErrorEnum.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var referenceVideoGuid = generationSetting.SelectVideoReference().asset.IsValid() ? Guid.NewGuid() : Guid.Empty;

                var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                    projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);
                var animationComponent = builder.AnimationComponent();

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);

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
                        var request = AnimationGenerateRequestBuilder.Initialize(generativeModelID)
                            .Generate(AnimationClipUtilities.bipedVersion.ToString(), prompt, roundedFrameDuration, seed, defaultTemperature);
                        requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        break;
                    }
                }

                var quoteResults = await EditorTask.Run(() =>
                    animationComponent.GenerateAnimationQuote(requests, Constants.realtimeTimeout, linkedTokenSource.Token));
                if (!quoteResults.Result.IsSuccessful)
                {
                    var messages = quoteResults.Result.Error.Errors.Count == 0
                        ? new[]
                        {
                            $"Error reason is '{quoteResults.Result.Error.AiResponseError.ToString()}' and no additional error information was provided ({WebUtils.selectedEnvironment})."
                        }
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
                // Only dispose if this is still the current token source for this asset
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                {
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                }

                cancellationTokenSource.Dispose();
            }

            void SendValidatingMessage()
            {
                var messages = new[] { "Validating generation inputs..." };
                api.Dispatch(GenerationActions.setGenerationValidationResult,
                    new(arg.asset, new(false, AiResultErrorEnum.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
            }
        }
    }
}
