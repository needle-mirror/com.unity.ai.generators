using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class WebUtils
    {
        public static string selectedEnvironment
        {
            get => EditorPrefs.GetString(ModelSelector.Services.Utilities.WebUtils.materialEnvironmentKey, ModelSelector.Services.Utilities.WebUtils.prodEnvironment);
            set => EditorPrefs.SetString(ModelSelector.Services.Utilities.WebUtils.materialEnvironmentKey, value);
        }
    }
}
