﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Toolkit.Accounts.Services;
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
        public static void EmptyAudioClipMenu() => CreateAndNameAudioClip();

        [MenuItem("Assets/Create/Audio/Generate Audio Clip", true)]
        static bool ValidateEmptyAudioClipMenu() => Account.settings.AiGeneratorsEnabled;

        public static AudioClip EmptyAudioClip()
        {
            var audioClip = AssetUtils.CreateAndSelectBlankAudioClip();
            Selection.activeObject = audioClip;
            GenerateAudioClip();
            return audioClip;
        }

        public static void CreateAndNameAudioClip()
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(AudioClip))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateBlankAsset>();
            doCreate.action = (_, pathName, _) =>
            {
                pathName = AssetUtils.CreateBlankAudioClip(pathName);
                if (string.IsNullOrEmpty(pathName))
                    Debug.Log($"Failed to create audio clip file for '{pathName}'.");
                AssetDatabase.ImportAsset(pathName, ImportAssetOptions.ForceUpdate);
                var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(pathName);
                Selection.activeObject = audioClip;
                GenerateAudioClip();
            };
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                doCreate,
                $"{AssetUtils.defaultNewAssetName}{AssetUtils.defaultAssetExtension}",
                icon,
                null,
                true);
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
            var generatorsEnabled = Account.settings.AiGeneratorsEnabled;
            EditorGUI.BeginDisabledGroup(!OnAssetGenerationMultipleValidation(editor.targets));
            var generateButtonTooltip = "Use generative ai to transform this audio clip.";
            if (!generatorsEnabled)
                generateButtonTooltip = Generators.UI.AIDropdownIntegrations.GenerativeMenuRoot.generatorsIsDisabledTooltip;
            EditorGUI.BeginDisabledGroup(!generatorsEnabled);
            if (GUILayout.Button(new GUIContent("Generate", generateButtonTooltip)))
                OnAssetGenerationRequest(editor.targets);
            EditorGUI.EndDisabledGroup();
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
