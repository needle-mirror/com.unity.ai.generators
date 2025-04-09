using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.GenerativeModels.Responses;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using Logger = Unity.AI.Generators.Sdk.Logger;

namespace Unity.AI.ModelSelector.Services.Stores.Actions
{
    static class ModelSelectorSuperProxyActions
    {
        public const string unityLogo = "Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/UnityLogo.png";

        public static ModelSettings FromSuperProxy(PlatformGenerativeModelsResult info)
        {
            var model = new ModelSettings
            {
                id = info.GenerativeModelId.ToString(),
                name = info.Name,
                tags = info.Tags,
                description = info.Description,
                provider = info.Provider,
                thumbnails = info.ThumbnailsUrls.ToList(),
                icon = info.IconImageUrl,
                modality = info.Modality,
                operations = info.OperationSubTypes.SelectMany(r => r).Distinct().ToList(),
                nativeResolution = new []{info.NativeResolutionWidth, info.NativeResolutionHeight},
                imageSizes = info.ImageSizes.Select(r => new []{r.Width, r.Height}).Append(new []{info.NativeResolutionWidth, info.NativeResolutionHeight}).OrderBy(r => r[0]).ToArray(),
            };

            // ugh
            if (model.provider == ProviderEnum.Unity)
            {
                model.icon = new Uri(Path.GetFullPath(unityLogo), UriKind.Absolute).GetLocalPath();
                model.thumbnails = new List<string> { model.icon };
            }

            if (model.thumbnails.Count == 0)
                model.thumbnails = new List<string> { model.icon };

            // cache immediately
            if (model.thumbnails.Count > 0)
                _ = TextureCache.GetPreview(new Uri(model.thumbnails[0]), (int)TextureSizeHint.Carousel);

            return model;
        }

        public static readonly AsyncThunkCreatorWithPayload<List<ModelSettings>> fetchModels = new($"{ModelSelectorActions.slice}/fetchModelsSuperProxy", async api =>
        {
            var taskID = Progress.Start($"Requesting models.");
            using var progressTokenSource = new CancellationTokenSource();
            List<ModelSettings> models = new();
            try
            {
                SetProgress(0.0f, "Authenticating with UnityConnect.");
                if (!WebUtilities.AreCloudProjectSettingsValid())
                {
                    LogInvalidCloudProjectSettings();
                    api.Cancel();
                    return models;
                }

                SetProgress(0.1f, "Preparing request.");

                {
                    _ = ProgressUtils.RunFuzzyProgress(0.1f, 1f,
                        value => SetProgress(value, "Working."),
                        1, progressTokenSource.Token);

                    using var httpClientLease = HttpClientManager.instance.AcquireLease();
                    var builder = Builder.Build(orgId: CloudProjectSettings.organizationKey, userId: CloudProjectSettings.userId,
                        projectId: CloudProjectSettings.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), enableDebugLogging: true);
                    var generativeModelsComponent = builder.GenerativeModelsComponent();
                    var modelResults = await generativeModelsComponent.GetGenerativeModelsList();

                    if (!modelResults.Batch.IsSuccessful)
                    {
                        if (modelResults.Batch.Error.Errors.Count == 0)
                            Debug.Log($"Error reason is '{modelResults.Batch.Error.AiResponseError.ToString()}' and no additional error information was provided ({WebUtils.selectedEnvironment}).");
                        else
                            modelResults.Batch.Error.Errors.ForEach(e => Debug.Log($"{modelResults.Batch.Error.AiResponseError.ToString()}: {e}"));

                        // we can simply return without throwing or additional logging because the error is already logged
                        return models;
                    }

                    foreach (var modelResult in modelResults.Batch.Value)
                    {
                        if (modelResult.Value.Modality != ModalityEnum.None || modelResult.Value.Provider == ProviderEnum.None)
                            models.Add(FromSuperProxy(modelResult.Value));
                    }
                }

                return models;
            }
            finally
            {
                progressTokenSource.Cancel();
                SetProgress(1, models.Count > 0 ? $"Retrieved {models.Count} models." : "Failed to retrieve models.");
                Progress.Finish(taskID);
            }

            void SetProgress(float progress, string description)
            {
                if (taskID > 0)
                    Progress.Report(taskID, progress, description);
            }

            void LogInvalidCloudProjectSettings() =>
                Debug.Log($"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{CloudProjectSettings.userName}\".");
        });
    }
}
