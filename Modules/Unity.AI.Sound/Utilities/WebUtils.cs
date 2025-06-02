using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment =>
            Unsupported.IsDeveloperMode()
                ? EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.soundEnvironmentKey,
                    ModelSelector.Services.Utilities.WebUtils.prodEnvironment)
                : ModelSelector.Services.Utilities.WebUtils.prodEnvironment;
    }
}
