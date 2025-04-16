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

        [MenuItem("internal:AI Toolkit/Internals/Chat/Generate Material", false, 3)]
        public static void OnGenerateMaterial()
        {
            ShowWindow(
                "Generate Material Prompt (Chat Plugin Test)",
                "Enter the prompt for the material to be generated:",
                PluginEntryPoints.GenerateMaterial
            );
        }

        [MenuItem("internal:AI Toolkit/Internals/Chat/Generate Sprite", false, 4)]
        public static void OnGenerateSprite()
        {
            ShowWindow(
                "Generate Sprite Prompt (Chat Plugin Test)",
                "Enter the prompt for the sprite to be generated:",
                PluginEntryPoints.GenerateSprite
            );
        }

        [MenuItem("internal:AI Toolkit/Internals/Chat/Generate Animation", false, 5)]
        public static void OnGenerateAnimation()
        {
            ShowWindow(
                "Generate Animation Prompt (Chat Plugin Test)",
                "Enter the prompt for the animation to be generated:",
                PluginEntryPoints.GenerateAnimation
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
