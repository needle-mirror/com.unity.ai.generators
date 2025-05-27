#define UNITY_AI_OPEN_BETA
using System.Collections.Generic;
using Unity.AI.Generators.UI.Actions;
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

        public const string accountEnvironmentKey = "AI_Toolkit_Account_Environment";
        public const string animateEnvironmentKey = "AI_Toolkit_Animate_Environment";
        public const string imageEnvironmentKey = "AI_Toolkit_Image_Environment";
        public const string materialEnvironmentKey = "AI_Toolkit_Material_Environment";
        public const string soundEnvironmentKey = "AI_Toolkit_Sound_Environment";

        internal static string selectedEnvironment { get; set; } = prodEnvironment;
    }

    class EnvironmentInputWindow : EditorWindow
    {
        const string k_InternalMenu = "internal:";

        [MenuItem(k_InternalMenu + "AI Toolkit/Internals/Log Cloud Project Info")]
        static void ShowProjectInfo()
        {
            var traceID = "None";
            try { traceID = Selection.activeObject ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Selection.activeObject)) : traceID; }
            catch { /* Ignored */ }

            Debug.Log($"User ID: {CloudProjectSettings.userId}\n" +
                $"User Name: {CloudProjectSettings.userName}\n" +
                $"Organization Key: {CloudProjectSettings.organizationKey}\n" +
                $"Organization ID: {CloudProjectSettings.organizationId}\n" +
                $"Organization Name: {CloudProjectSettings.organizationName}\n" +
                $"Cloud Project ID: {CloudProjectSettings.projectId}\n" +
                $"Cloud Project Name: {CloudProjectSettings.projectName}\n" +
                $"{WebUtils.accountEnvironmentKey}: {EditorPrefs.GetString(WebUtils.accountEnvironmentKey, WebUtils.prodEnvironment)}\n" +
                $"{WebUtils.animateEnvironmentKey}: {EditorPrefs.GetString(WebUtils.animateEnvironmentKey, WebUtils.prodEnvironment)}\n" +
                $"{WebUtils.imageEnvironmentKey}: {EditorPrefs.GetString(WebUtils.imageEnvironmentKey, WebUtils.prodEnvironment)}\n" +
                $"{WebUtils.materialEnvironmentKey}: {EditorPrefs.GetString(WebUtils.materialEnvironmentKey, WebUtils.prodEnvironment)}\n" +
                $"{WebUtils.soundEnvironmentKey}: {EditorPrefs.GetString(WebUtils.soundEnvironmentKey, WebUtils.prodEnvironment)}\n" +
                $"(selected) Asset ID (trace ID): {traceID}");
        }

        [MenuItem(k_InternalMenu + "AI Toolkit/Internals/Set Environments", false, 99)]
        static void ShowEnvironmentWindow() => ShowWindow("AI Toolkit Environment");

        static readonly List<(string key, string label)> k_EnvironmentPrefs = new()
        {
            (WebUtils.accountEnvironmentKey, "Account Environment"),
            (WebUtils.animateEnvironmentKey, "Animate Environment"),
            (WebUtils.imageEnvironmentKey, "Image Environment"),
            (WebUtils.materialEnvironmentKey, "Material Environment"),
            (WebUtils.soundEnvironmentKey, "Sound Environment"),
        };

        readonly Dictionary<string, bool> m_EnvironmentStates = new();
        string m_InputText = WebUtils.prodEnvironment;

        static void ShowWindow(string title)
        {
            var window = GetWindow<EnvironmentInputWindow>(true, title, true);
            window.minSize = new Vector2(499, 200);
            window.maxSize = new Vector2(500, 300);
            window.InitializeEnvironmentStates();
            window.Show();
        }

        void InitializeEnvironmentStates()
        {
            m_EnvironmentStates.Clear();
            foreach (var (key, _) in k_EnvironmentPrefs)
            {
                m_EnvironmentStates[key] = true;
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space();

            m_InputText = EditorGUILayout.TextField("Environment URL:", m_InputText);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Set Environment Per Tool:", EditorStyles.boldLabel);
            foreach (var (key, label) in k_EnvironmentPrefs)
            {
                var currentValue = EditorPrefs.GetString(key, WebUtils.prodEnvironment);
                EditorGUILayout.BeginHorizontal();
                m_EnvironmentStates[key] = EditorGUILayout.Toggle(label, m_EnvironmentStates[key]);
                EditorGUILayout.LabelField($"({currentValue})", GUILayout.Width(300));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                foreach (var (key, _) in k_EnvironmentPrefs)
                {
                    if (!m_EnvironmentStates[key])
                        continue;

                    if (!string.IsNullOrWhiteSpace(m_InputText))
                        EditorPrefs.SetString(key, m_InputText);
                    else
                        EditorPrefs.DeleteKey(key);
                }
                Close();
            }
            if (GUILayout.Button("Reset All"))
            {
                foreach (var (key, _) in k_EnvironmentPrefs)
                    EditorPrefs.DeleteKey(key);
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        [InitializeOnLoadMethod]
        static void RegisterHook() => GenerationActions.selectedEnvironment = () => WebUtils.selectedEnvironment;
    }
}
