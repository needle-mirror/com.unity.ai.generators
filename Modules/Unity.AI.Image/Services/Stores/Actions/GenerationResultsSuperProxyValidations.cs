using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Modalities.Image;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate;
using AiEditorToolsSdk.Components.Modalities.Image.Requests.Generate.OperationSubTypes;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using Logger = Unity.AI.Generators.Sdk.Logger;

namespace Unity.AI.Image.Services.Stores.Actions
{
    static class GenerationResultsSuperProxyValidations
    {
        record CanAddReferencesKey(ImageReferenceType referenceType, bool prompt, bool negativePrompt, string model, int referencesBitmask);

        static readonly Dictionary<CanAddReferencesKey, bool> k_CanAddReferencesCache = new();

        public static readonly Func<(AddImageReferenceTypeData payload, IStoreApi api), (bool success, bool[] results)> canAddReferencesToPromptCached = arg =>
        {
            var asset = new AssetReference { guid = arg.payload.asset.guid };
            var modelID = arg.api.State.SelectSelectedModelID(asset);
            var activeReferencesBitmask = arg.api.State.SelectActiveReferencesBitMask(asset);
            var results = new bool[arg.payload.types.Length];
            var typesToFetch = new List<(int index, ImageReferenceType type)>();
            for (var i = 0; i < arg.payload.types.Length; i++)
            {
                var type = arg.payload.types[i];
                var cacheKey = new CanAddReferencesKey(type, true, false, modelID, activeReferencesBitmask);

                if (k_CanAddReferencesCache.TryGetValue(cacheKey, out var canAdd))
                    results[i] = canAdd;
                else
                    typesToFetch.Add((i, type));
            }

            return (typesToFetch.Count == 0, results);
        };

        public static readonly Func<(AddImageReferenceTypeData payload, IStoreApi api), Task<bool[]>> canAddReferencesToPrompt = async arg =>
        {
            var asset = new AssetReference { guid = arg.payload.asset.guid };

            var generationSetting = arg.api.State.SelectGenerationSetting(asset);
            var mode = generationSetting.SelectRefinementMode();
            var modelID = arg.api.State.SelectSelectedModelID(asset);
            var dimensions = generationSetting.SelectImageDimensionsVector2();
            var refs = generationSetting.SelectImageReferencesByRefinement();
            var activeReferencesBitmask = arg.api.State.SelectActiveReferencesBitMask(asset);
            var results = new bool[arg.payload.types.Length];

            // Track which types still need to be fetched (not in cache)
            var typesToFetch = new List<(int index, ImageReferenceType type)>();
            for (var i = 0; i < arg.payload.types.Length; i++)
            {
                var type = arg.payload.types[i];
                var cacheKey = new CanAddReferencesKey(type, true, false, modelID, activeReferencesBitmask);

                // Check if we have a cached result
                if (k_CanAddReferencesCache.TryGetValue(cacheKey, out var canAdd))
                    results[i] = canAdd;
                else
                    typesToFetch.Add((i, type));
            }

            // If all results were cached, return early
            if (typesToFetch.Count == 0)
                return results;

            if (WebUtilities.AreCloudProjectSettingsInvalid())
                return arg.payload.types.Select(_ => false).ToArray();

            // We need to fetch some results
            switch (mode)
            {
                case RefinementMode.Generation:
                {
                    using var httpClientLease = HttpClientManager.instance.AcquireLease();

                    var builder = Builder.Build(
                        orgId: CloudProjectSettings.organizationKey,
                        userId: CloudProjectSettings.userId,
                        projectId: CloudProjectSettings.projectId,
                        httpClient: httpClientLease.client,
                        baseUrl: WebUtils.selectedEnvironment,
                        logger: new Logger(),
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(),
                        traceIdProvider: new TraceIdProvider(asset),
                        enableDebugLogging: true,
                        defaultOperationTimeout: Constants.mandatoryTimeout);

                    var imageComponent = builder.ImageComponent();
                    Guid.TryParse(modelID, out var generativeModelID);

                    var requestBuilder = ImageGenerateRequestBuilder.Initialize(generativeModelID, dimensions.x, dimensions.y, null);
                    var textPrompt = new TextPrompt("reference test", "");
                    var requests = new List<(int index, ImageReferenceType type, ImageGenerateRequest request)>();
                    foreach (var (index, type) in typesToFetch)
                    {
                        // Create a modified mask that includes the current type we're testing
                        var currentMask = activeReferencesBitmask | (1 << (int)type);
                        try
                        {
                            var request = requestBuilder.GenerateWithReferences(textPrompt,
                                IsActive(ImageReferenceType.PromptImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.PromptImage].strength) : null,
                                IsActive(ImageReferenceType.StyleImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.StyleImage].strength) : null,
                                IsActive(ImageReferenceType.CompositionImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.CompositionImage].strength) : null,
                                IsActive(ImageReferenceType.PoseImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.PoseImage].strength) : null,
                                IsActive(ImageReferenceType.DepthImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.DepthImage].strength) : null,
                                IsActive(ImageReferenceType.LineArtImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.LineArtImage].strength) : null,
                                IsActive(ImageReferenceType.FeatureImage) ? new (Guid.NewGuid(), refs[mode][ImageReferenceType.FeatureImage].strength) : null);

                            requests.Add((index, type, request));
                        }
                        catch (UnhandledReferenceCombinationException)
                        {
                            k_CanAddReferencesCache[new CanAddReferencesKey(type, true, false, modelID, activeReferencesBitmask)] = false;
                            results[index] = false;
                        }

                        bool IsActive(ImageReferenceType refType) => (currentMask & (1 << (int)refType)) != 0;
                    }

                    var fetchTasks = requests.Select(r => FetchAndCacheResult(r.index, r.type, r.request, imageComponent));
                    await Task.WhenAll(fetchTasks);

                    return results;
                }

                default:
                {
                    foreach (var (index, _) in typesToFetch)
                    {
                        results[index] = false;
                    }
                    return results;
                }
            }

            async Task FetchAndCacheResult(int index, ImageReferenceType type, ImageGenerateRequest request, IImageComponent imageComponent)
            {
                try
                {
                    var quoteResult = await EditorTask.Run(() => imageComponent.GenerateQuote(request.AsSingleInAList(), Constants.mandatoryTimeout));
                    bool? isSuccess = null;
                    if (!quoteResult.Result.IsSuccessful)
                    {
                        if (quoteResult.Result.Error.AiResponseError == AiResultErrorEnum.UnsupportedModelOperation)
                            isSuccess = false;
                    }
                    else
                    {
                        isSuccess = true;
                    }

                    if (isSuccess != null)
                    {
                        k_CanAddReferencesCache[new CanAddReferencesKey(type, true, false, modelID, activeReferencesBitmask)] = isSuccess.Value;
                        results[index] = isSuccess.Value;
                    }
                    else
                        results[index] = false;
                }
                catch
                {
                    results[index] = false;
                }
            }
        };
    }
}
