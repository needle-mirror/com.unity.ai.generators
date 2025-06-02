using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment =>
            Unsupported.IsDeveloperMode()
                ? EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.materialEnvironmentKey,
                    ModelSelector.Services.Utilities.WebUtils.prodEnvironment)
                : ModelSelector.Services.Utilities.WebUtils.prodEnvironment;
    }
}
