using System;
using System.Linq;
using AiEditorToolsSdk.Components.Asset.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
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

        public static Func<AsyncThunkApi<bool>, string> selectedEnvironment = null;

        public static void DispatchProgress(this AsyncThunkApi<bool> api, AssetReference asset, GenerationProgressData payload, string description, bool backgroundReport = true)
        {
            if (backgroundReport)
                EditorFocusScope.ShowProgressOrCancelIfUnfocused("Editor background worker", description, payload.progress);

            if (payload.taskID > 0)
                Progress.Report(payload.taskID, payload.progress, description);
            api.Dispatch(setGenerationProgress, new GenerationsProgressData(asset, payload));
        }

        public static bool DispatchStoreAssetMessage(this AsyncThunkApi<bool> api, AssetReference asset, OperationResult<BlobAssetResult> assetResults,
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

        public static void DispatchValidatingMessage(this AsyncThunkApi<bool> api, AssetReference asset)
        {
            var messages = new[] { "Validating generation inputs..." };
            api.Dispatch(setGenerationValidationResult,
                new(asset,
                    new(false, AiResultErrorEnum.Unknown, 0,
                        messages.Select(m => new GenerationFeedbackData(m)).ToList())));
        }

        public static void DispatchInvalidCloudProjectMessage(this AsyncThunkApi<bool> api, AssetReference asset)
        {
            api.Dispatch(setGenerationAllowed, new(asset, true));
            var messages = new[] { $"Could not obtain organizations for user \"{CloudProjectSettings.userName}\"." };
            foreach (var message in messages)
            {
                Debug.Log(message);
                api.Dispatch(addGenerationFeedback, new GenerationsFeedbackData(asset, new GenerationFeedbackData(message)));
            }
        }

        public static void DispatchFailedBatchMessage<T>(this AsyncThunkApi<bool> api, AssetReference asset, BatchOperationResult<T> results) where T : class
        {
            if (!results.Batch.IsSuccessful)
                DispatchFailedMessage(api, asset, results.Batch.Error);
            Debug.Log($"Trace Id {results.SdkTraceId} => {results.W3CTraceId}");
        }

        public static void DispatchFailedMessage(this AsyncThunkApi<bool> api, AssetReference asset, AiOperationFailedResult result)
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

        public static void DispatchFailedDownloadMessage<T>(this AsyncThunkApi<bool> api, AssetReference asset, OperationResult<T> result) where T : class
        {
            if (!result.Result.IsSuccessful)
                DispatchFailedDownloadMessage(api, asset, result.Result.Error);
            Debug.Log($"Trace Id {result.SdkTraceId} => {result.W3CTraceId}");
        }

        public static void DispatchFailedDownloadMessage(this AsyncThunkApi<bool> api, AssetReference asset, AiOperationFailedResult result)
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

        public static void DispatchJobUpdates(this AsyncThunkApi<bool> _, JobStatusSdkEnum jobStatus)
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
