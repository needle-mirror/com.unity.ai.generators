using System;
using UnityEditor;

namespace Unity.AI.Animate.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment =>
            Unsupported.IsDeveloperMode()
                ? EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.animateEnvironmentKey,
                    ModelSelector.Services.Utilities.WebUtils.prodEnvironment)
                : ModelSelector.Services.Utilities.WebUtils.prodEnvironment;
    }
}
