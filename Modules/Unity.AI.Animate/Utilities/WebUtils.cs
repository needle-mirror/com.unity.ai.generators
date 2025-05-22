using System;
using UnityEditor;

namespace Unity.AI.Animate.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment
        {
            get => EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.animateEnvironmentKey, ModelSelector.Services.Utilities.WebUtils.prodEnvironment);
            set => EditorPrefs.SetString(ModelSelector.Services.Utilities.WebUtils.animateEnvironmentKey, value);
        }
    }
}
