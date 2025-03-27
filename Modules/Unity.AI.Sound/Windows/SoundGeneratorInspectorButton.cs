using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Toolkit.GenerationContextMenu;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Sound.Windows
{
    static class SoundGeneratorInspectorButton
    {
        [GenerateContextMenu(nameof(GenerateAudioClipValidation))]
        public static void GenerateAudioClip() => OnAssetGenerationRequest(new[] { Selection.activeObject });

        public static bool GenerateAudioClipValidation() => OnAssetGenerationValidation(new[] { Selection.activeObject }) && Selection.objects.Length == 1;

        [MenuItem("Assets/Create/Audio/Generate Audio Clip", false, -1000)]
        public static void EmptyAudioClipMenu() => EmptyAudioClip();

        public static AudioClip EmptyAudioClip()
        {
            var audioClip = AssetUtils.CreateAndSelectBlankAudioClip();
            Selection.activeObject = audioClip;
            GenerateAudioClip();
            return audioClip;
        }

        [InitializeOnLoadMethod]
        static void EditorHeaderButtons() => Editor.finishedDefaultHeaderGUI += OnHeaderControlsGUI;

        static void OnHeaderControlsGUI(Editor editor)
        {
            if (!EditorUtility.IsPersistent(editor.target))
                return;

            if (!OnAssetGenerationValidation(editor.targets))
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!OnAssetGenerationMultipleValidation(editor.targets));
            if (GUILayout.Button(new GUIContent("Generate", "Use generative ai to transform this audio clip.")))
                OnAssetGenerationRequest(editor.targets);
            if (GUILayout.Button(new GUIContent("Edit", "Trim and edit the envelope of this audio clip.")))
                OnAssetEditRequest(editor.targets);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        static void OnAssetEditRequest(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (TryGetValidAudioPath(obj, out var validPath))
                {
                    OpenEnvelopeWindow(validPath);
                }
            }
        }

        static void OnAssetGenerationRequest(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (TryGetValidAudioPath(obj, out var validPath))
                {
                    OpenGenerationWindow(validPath);
                }
            }
        }

        static bool OnAssetGenerationValidation(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (AssetDatabase.IsOpenForEdit(obj) && TryGetValidAudioPath(obj, out _))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetValidAudioPath(Object obj, out string path)
        {
            path = obj switch
            {
                AudioImporter importer => importer.assetPath,
                _ => null
            };

            if (string.IsNullOrEmpty(path))
                path = AssetDatabase.GetAssetPath(obj);

            if (!string.IsNullOrEmpty(path))
            {
                var extension = Path.GetExtension(path).ToLower();
                if (extension is ".wav")
                {
                    return true;
                }
            }

            return false;
        }

        static bool OnAssetGenerationMultipleValidation(IReadOnlyCollection<Object> objects) => objects.FirstOrDefault(o => TryGetValidAudioPath(o, out _));

        internal static void OpenGenerationWindow(string assetPath) => SoundGeneratorWindow.Display(assetPath);

        static void OpenEnvelopeWindow(string assetPath) => SoundEnvelopeWindow.Display(assetPath);
    }
}
