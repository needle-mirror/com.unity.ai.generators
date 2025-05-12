using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Services.Utilities
{
    static class WebUtils
    {
        const string k_InternalMenu = "internal:";
        const string k_SetEnvironmentMenu = "AI Toolkit/Internals/AI.Sound/Set Environment";
        const string k_SelectedEnvironmentKey = "AI_Toolkit_Sound_Environment";

        public static string selectedEnvironment
        {
            get => EditorPrefs.GetString(k_SelectedEnvironmentKey, ModelSelector.Services.Utilities.WebUtils.prodEnvironment);
            set => EditorPrefs.SetString(k_SelectedEnvironmentKey, value);
        }

        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Production", false, 100)]
        static void SetProductionEnvironment()
        {
            selectedEnvironment = ModelSelector.Services.Utilities.WebUtils.prodEnvironment;
        }
        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Production", true, 100)]
        static bool ValidateSetProductionEnvironment()
        {
            Menu.SetChecked(k_SetEnvironmentMenu + "/Production", selectedEnvironment == ModelSelector.Services.Utilities.WebUtils.prodEnvironment);
            return true;
        }

        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Staging", false, 100)]
        static void SetStagingEnvironment()
        {
            selectedEnvironment = ModelSelector.Services.Utilities.WebUtils.stagingEnvironment;
        }
        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Staging", true, 100)]
        static bool ValidateSetStagingEnvironment()
        {
            Menu.SetChecked(k_SetEnvironmentMenu + "/Staging", selectedEnvironment == ModelSelector.Services.Utilities.WebUtils.stagingEnvironment);
            return true;
        }

        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Test", false, 100)]
        static void SetTestEnvironment()
        {
            selectedEnvironment = ModelSelector.Services.Utilities.WebUtils.testEnvironment;
        }
        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Test", true, 100)]
        static bool ValidateSetTestEnvironment()
        {
            Menu.SetChecked(k_SetEnvironmentMenu + "/Test", selectedEnvironment == ModelSelector.Services.Utilities.WebUtils.testEnvironment);
            return true;
        }

        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Local :5050", false, 101)]
        static void SetLocalEnvironment()
        {
            selectedEnvironment = ModelSelector.Services.Utilities.WebUtils.localEnvironment;
        }
        [MenuItem(k_InternalMenu + k_SetEnvironmentMenu + "/Local :5050", true, 101)]
        static bool ValidateSetLocalEnvironment()
        {
            Menu.SetChecked(k_SetEnvironmentMenu + "/Local :5050", selectedEnvironment == ModelSelector.Services.Utilities.WebUtils.localEnvironment);
            return true;
        }
    }
}
