﻿using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.ModelSelector.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.SessionPersistence;
using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEditor;

namespace Unity.AI.Sound.Services.Utilities
{
    static class WebUtils
    {
        public const string soundEnvironmentKey = "AI_Toolkit_Sound_Environment";

        public static string selectedEnvironment => Environment.GetSelectedEnvironment(soundEnvironmentKey);

        [InitializeOnLoadMethod]
        static void RegisterEnvironmentKeys() => Environment.RegisterEnvironmentKey(soundEnvironmentKey, "Sound Environment",
            _ => SharedStore.Store.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.discoverModels,
                new DiscoverModelsData(selectedEnvironment)));
    }
}
