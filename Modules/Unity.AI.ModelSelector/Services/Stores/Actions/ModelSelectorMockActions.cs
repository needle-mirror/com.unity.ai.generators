using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Redux.Thunks;

namespace Unity.AI.ModelSelector.Services.Stores.Actions
{
    static class ModelSelectorMockActions
    {
        public const string unityLogo = "Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/UnityLogo.png";
        public static readonly AsyncThunkCreatorWithPayload<List<ModelSettings>> fetchModels = new($"{ModelSelectorActions.slice}/fetchModelsMock", async _ =>
        {
            await Task.Yield();
            var parsed = new List<ModelSettings>();

            // Constant legacy models
            parsed.Add(new()
            {
                name = "Unity Animate Model v1.0",
                id = "unity-animate-model-v1.0",
                description = "The official Unity Animate Model.",
                partner = "Unity",
                tags = new List<string> { "Animation", "Humanoid" },
                thumbnails = new List<string> { Path.GetFullPath(unityLogo) },
                icon = Path.GetFullPath(unityLogo),
                provider = ProviderEnum.Unity,
                modality = ModalityEnum.Animate
            });

            return parsed;
        });
    }
}
