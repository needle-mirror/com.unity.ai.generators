using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment =>
            Unsupported.IsDeveloperMode()
                ? EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.imageEnvironmentKey,
                    ModelSelector.Services.Utilities.WebUtils.prodEnvironment)
                : ModelSelector.Services.Utilities.WebUtils.prodEnvironment;
    }
}
