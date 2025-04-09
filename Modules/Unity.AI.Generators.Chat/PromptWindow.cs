using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.Chat
{
    class PromptWindow : EditorWindow
    {
        [MenuItem("internal:AI Toolkit/Internals/Chat/Generate Sound", false, 1)]
        public static void OnGenerateSound()
        {
            ShowWindow(
                "Generate Sound Prompt (Chat Plugin Test)",
                "Enter the prompt for the sound to be generated:",
                PluginEntryPoints.GenerateSound
            );
        }

        [MenuItem("internal:AI Toolkit/Internals/Chat/Generate Texture", false, 2)]
        public static void OnGenerateTexture()
        {
            ShowWindow(
                "Generate Texture Prompt (Chat Plugin Test)",
                "Enter the prompt for the texture to be generated:",
                PluginEntryPoints.GenerateTexture
            );
        }

        [MenuItem("internal:AI Toolkit/Internals/Chat/Generate Sprite", false, 3)]
        public static void OnGenerateSprite()
        {
            ShowWindow(
                "Generate Sprite Prompt (Chat Plugin Test)",
                "Enter the prompt for the sprite to be generated:",
                PluginEntryPoints.GenerateSprite
            );
        }
        
        string m_InputText = "";
        string m_Label;
        Action<string> m_OnConfirm;

        public static void ShowWindow(string title, string label, Action<string> onConfirm)
        {
            var window = GetWindow<PromptWindow>(true, title, true);
            window.minSize = new Vector2(399, 99);
            window.maxSize = new Vector2(400, 100);
            window.m_Label = label;
            window.m_OnConfirm = onConfirm;
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(m_Label, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            m_InputText = EditorGUILayout.TextField("Prompt:", m_InputText);
            EditorGUILayout.Space();

            if (GUILayout.Button("OK"))
            {
                m_OnConfirm?.Invoke(m_InputText);
                Close();
            }
        }
    }
}
