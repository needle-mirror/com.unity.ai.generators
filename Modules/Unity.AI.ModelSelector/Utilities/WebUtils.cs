#define UNITY_AI_OPEN_BETA
using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Utilities
{
    static class WebUtils
    {
#if UNITY_AI_OPEN_BETA
        public const string prodEnvironment = "https://generators-beta.ai.unity.com";
        public const string stagingEnvironment = "https://generators-stg-beta.ai.unity.com";
        public const string testEnvironment = "https://generators-test-beta.ai.unity.com";
#else
        public const string prodEnvironment = "https://musetools.unity.com";
        public const string stagingEnvironment = "https://musetools-stg.unity.com";
        public const string testEnvironment = "https://musetools-test.unity.com";
#endif
        public const string localEnvironment = "https://localhost:5050";

        internal static string selectedEnvironment { get; set; } = prodEnvironment;
    }

    class StringInputWindow : EditorWindow
    {
        const string k_InternalMenu = "internal:";

        [MenuItem(k_InternalMenu + "AI Toolkit/Internals/Set all Environments", false, 99)]
        static void OverrideAllEnvironments() =>
            StringInputWindow.ShowWindow("AI Toolkit Environment", url => {
                EditorPrefs.SetString("AI_Toolkit_Account_Environment", url);
                EditorPrefs.SetString("AI_Toolkit_Animate_Environment", url);
                EditorPrefs.SetString("AI_Toolkit_Image_Environment", url);
                EditorPrefs.SetString("AI_Toolkit_Material_Environment", url);
                EditorPrefs.SetString("AI_Toolkit_Sound_Environment", url);
            });

        string m_InputText = WebUtils.prodEnvironment;
        Action<string> m_OnConfirm;

        public static void ShowWindow(string title, Action<string> onConfirm)
        {
            var window = GetWindow<StringInputWindow>(true, title, true);
            window.minSize = new Vector2(399, 69);
            window.maxSize = new Vector2(400, 80);
            window.m_OnConfirm = onConfirm;
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();

            m_InputText = EditorGUILayout.TextField("Url:", m_InputText);
            EditorGUILayout.Space();

            if (GUILayout.Button("OK"))
            {
                m_OnConfirm?.Invoke(m_InputText);
                Close();
            }
        }
    }
}
