using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate.OperationSubTypes;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Pbr;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Transform;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Random = UnityEngine.Random;

namespace Unity.AI.Material.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteMaterialsData> quoteMaterials = new($"{GenerationResultsActions.slice}/quoteMaterialsSuperProxy", QuoteMaterialsAsync);

        static async Task QuoteMaterialsAsync(QuoteMaterialsData arg, AsyncThunkApi<bool> api)
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

                var variations = arg.generationSetting.SelectVariationCount();
                var refinementMode = generationSetting.SelectRefinementMode();
                if (refinementMode is RefinementMode.Upscale or RefinementMode.Pbr)
                {
                    variations = 1;
                }

                var prompt = generationSetting.SelectPrompt();
                var negativePrompt = generationSetting.SelectNegativePrompt();
                var modelID = api.State.SelectSelectedModelID(asset);
                var dimensions = generationSetting.SelectImageDimensionsVector2();
                var patternImageReference = generationSetting.SelectPatternImageReference();
                var seed = Random.Range(0, int.MaxValue - variations);
                Guid.TryParse(modelID, out var generativeModelID);
                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), traceIdProvider: new TraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout);
                var imageComponent = builder.ImageComponent();

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);

                var assetGuid = Guid.NewGuid();

                OperationResult<QuoteResponse> quoteResults = null;

                switch (refinementMode)
                {
                    case RefinementMode.Upscale:
                    {
                        var materialGenerations = new List<Dictionary<MapType, Guid>> { new() { [MapType.Preview] = assetGuid } };

                        var request = ImageTransformRequestBuilder.Initialize();
                        var requests = materialGenerations.Select(m => request.Upscale(new(m[MapType.Preview], 2, null, null))).ToList();
                        quoteResults = await EditorTask.Run(() =>
                            imageComponent.TransformQuote(requests, Constants.realtimeTimeout, linkedTokenSource.Token), linkedTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Pbr:
                    {
                        if (generativeModelID == Guid.Empty)
                        {
                            var messages = new[] { "No model selected. Please select a valid model." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, AiResultErrorEnum.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var materialGenerations = new List<Dictionary<MapType, Guid>> { new() { [MapType.Preview] = assetGuid } };

                        var requests = materialGenerations.Select(m => new Texture2DPbrRequest(generativeModelID, m[MapType.Preview])).ToList();
                        quoteResults = await EditorTask.Run(() =>
                            imageComponent.GeneratePbrQuote(requests, Constants.realtimeTimeout, linkedTokenSource.Token), linkedTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        if (generativeModelID == Guid.Empty)
                        {
                            var messages = new[] { "No model selected. Please select a valid model." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, AiResultErrorEnum.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var patternGuid = Guid.Empty;
                        if (patternImageReference.asset.IsValid())
                        {
                            patternGuid = Guid.NewGuid();
                        }

                        var request = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, seed)
                            .GenerateWithReference(new TextPrompt(prompt, negativePrompt),
                                new CompositionReference(patternGuid, patternImageReference.strength));
                        var requests = variations > 1 ? request.CloneBatch(variations) : request.AsSingleInAList();
                        quoteResults = await EditorTask.Run(() =>
                            imageComponent.GenerateQuote(requests, Constants.realtimeTimeout, linkedTokenSource.Token), linkedTokenSource.Token);
                        break;
                    }
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (quoteResults == null)
                {
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
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                {
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                }

                cancellationTokenSource.Dispose();
            }
        }
    }
}
