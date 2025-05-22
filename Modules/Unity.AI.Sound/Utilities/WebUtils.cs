using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment
        {
            get => EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.soundEnvironmentKey, ModelSelector.Services.Utilities.WebUtils.prodEnvironment);
            set => EditorPrefs.SetString(ModelSelector.Services.Utilities.WebUtils.soundEnvironmentKey, value);
        }
    }
}
