using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Actions
{
    static class GenerationActions
    {
        public static readonly string slice = "generationResults";
        public static Creator<GenerationAllowedData> setGenerationAllowed => new($"{slice}/setGenerationAllowed");
        public static Creator<GenerationsProgressData> setGenerationProgress => new($"{slice}/setGenerationProgress");
        public static Creator<GenerationsFeedbackData> addGenerationFeedback => new($"{slice}/addGenerationFeedback");
        public static Creator<AssetReference> removeGenerationFeedback => new($"{slice}/removeGenerationFeedback");
        public static Creator<GenerationsValidationResult> setGenerationValidationResult => new($"{slice}/setGenerationValidationResult");

        public static Func<IStoreApi, string> selectedEnvironment = null;

        public static void DispatchProgress(this IStoreApi api, AssetReference asset, GenerationProgressData payload, string description, bool backgroundReport = false)
        {
            if (backgroundReport)
                EditorAsyncKeepAliveScope.ShowProgressOrCancelIfUnfocused("Editor background worker", description, payload.progress);

            if (payload.taskID > 0)
                Progress.Report(payload.taskID, payload.progress, description);
            api.Dispatch(setGenerationProgress, new GenerationsProgressData(asset, payload));
        }

        public static bool DispatchStoreAssetMessage(this IStoreApi api, AssetReference asset, OperationResult<BlobAssetResult> assetResults,
            out Guid assetGuid)
        {
            assetGuid = Guid.Empty;
            if (!assetResults.Result.IsSuccessful)
            {
                DispatchFailedMessage(api, asset, assetResults.Result.Error);
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

        public static void DispatchValidatingUserMessage(this IStoreApi api, AssetReference asset)
        {
            var messages = new[] { "Validating user, project and organization..." };
            api.Dispatch(setGenerationValidationResult,
                new(asset,
                    new(false, AiResultErrorEnum.Unknown, 0,
                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
        }

        public static void DispatchValidatingMessage(this IStoreApi api, AssetReference asset)
        {
            var messages = new[] { "Validating generation inputs..." };
            api.Dispatch(setGenerationValidationResult,
                new(asset,
                    new(false, AiResultErrorEnum.Unknown, 0,
                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
        }

        public static void DispatchInvalidCloudProjectMessage(this IStoreApi api, AssetReference asset)
        {
            api.Dispatch(setGenerationAllowed, new(asset, true));
            var messages = new[] { $"Could not obtain organizations for user \"{UnityConnectProvider.userName}\"." };
            foreach (var message in messages)
            {
                Debug.Log(message);
                api.Dispatch(addGenerationFeedback, new GenerationsFeedbackData(asset, new GenerationFeedbackData(message)));
            }
        }

        public static void DispatchReferenceUploadFailedMessage(this IStoreApi api, AssetReference asset)
        {
            api.Dispatch(setGenerationAllowed, new(asset, true));
            var messages = new[] { $"Could not upload references. Please try again." };
            foreach (var message in messages)
            {
                Debug.Log(message);
                api.Dispatch(addGenerationFeedback, new GenerationsFeedbackData(asset, new GenerationFeedbackData(message)));
            }
        }

        public static void DispatchGenerationRequestFailedMessage(this IStoreApi api, AssetReference asset)
        {
            api.Dispatch(setGenerationAllowed, new(asset, true));
            var messages = new[] { $"Could not make generation request. Please try again." };
            foreach (var message in messages)
            {
                Debug.Log(message);
                api.Dispatch(addGenerationFeedback, new GenerationsFeedbackData(asset, new GenerationFeedbackData(message)));
            }
        }

        public static void DispatchFailedBatchMessage<T>(this IStoreApi api, AssetReference asset, BatchOperationResult<T> results) where T : class
        {
            if (!results.Batch.IsSuccessful)
                DispatchFailedMessage(api, asset, results.Batch.Error);
            Debug.Log($"Trace Id '{results.SdkTraceId}' => W3CTraceId '{results.W3CTraceId}'");
        }

        public static void DispatchFailedMessage(this IStoreApi api, AssetReference asset, AiOperationFailedResult result)
        {
            var selectedEnv = string.Empty;
            if (selectedEnvironment != null)
                selectedEnv = selectedEnvironment(api);

            api.Dispatch(setGenerationAllowed, new(asset, true));
            var messages = result.Errors.Count == 0
                ? new[] { $"Received '{result.AiResponseError.ToString()}' from url '{selectedEnv}'." }
                : result.Errors.Distinct().Select(m => $"{result.AiResponseError.ToString()}: {m}").ToArray();
            foreach (var message in messages)
            {
                Debug.Log(message);
                api.Dispatch(addGenerationFeedback, new GenerationsFeedbackData(asset, new GenerationFeedbackData(message)));
            }
        }

        public static void DispatchFailedDownloadMessage<T>(this IStoreApi api, AssetReference asset, OperationResult<T> result, string lastW3CTraceId = null, bool willRetry = false) where T : class
        {
            if (!result.Result.IsSuccessful && !willRetry)
                DispatchFailedDownloadMessage(api, asset, result.Result.Error);
            Debug.Log($"Trace Id '{result.SdkTraceId}' => W3CTraceId '{(!string.IsNullOrWhiteSpace(result.W3CTraceId) ? result.W3CTraceId : lastW3CTraceId)}'");
        }

        public static void DispatchFailedDownloadMessage(this IStoreApi api, AssetReference asset, AiOperationFailedResult result)
        {
            var selectedEnv = string.Empty;
            if (selectedEnvironment != null)
                selectedEnv = selectedEnvironment(api);

            var messages = result.Errors.Count == 0
                ? new[] { $"Received '{result.AiResponseError.ToString()}' from url '{selectedEnv}'." }
                : result.Errors.Distinct().Select(m => $"{result.AiResponseError.ToString()}: {m}").ToArray();
            foreach (var message in messages)
            {
                Debug.Log(message);
                api.Dispatch(addGenerationFeedback, new GenerationsFeedbackData(asset, new GenerationFeedbackData(message)));
            }
        }

        static JobStatusSdkEnum s_LastJobStatus = JobStatusSdkEnum.None;

        public static void DispatchJobUpdates(this IStoreApi _, JobStatusSdkEnum jobStatus)
        {
            if (s_LastJobStatus == jobStatus)
                return;
            s_LastJobStatus = jobStatus;
            if (LoggerUtilities.sdkLogLevel == 0)
                return;
            Debug.Log($"Job status: {jobStatus}");
        }
    }
}
