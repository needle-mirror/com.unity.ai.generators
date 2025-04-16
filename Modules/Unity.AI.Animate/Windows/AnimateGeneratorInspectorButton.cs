using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Toolkit.GenerationContextMenu;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Animate.Windows
{
    static class AnimateGeneratorInspectorButton
    {
        [GenerateContextMenu(nameof(GenerateAnimateValidation))]
        public static void GenerateAnimate() => OnAssetGenerationRequest(new[] { Selection.activeObject });

        public static bool GenerateAnimateValidation() => OnAssetGenerationValidation(new[] { Selection.activeObject }) && Selection.objects.Length == 1;

        [MenuItem("Assets/Create/Animation/Generate Animation Clip", false, -1000)]
        public static void EmptyAnimateMenu() => EmptyAnimate();

        public static AnimationClip EmptyAnimate()
        {
            var animate = AssetUtils.CreateAndSelectBlankAnimation();
            Selection.activeObject = animate;
            GenerateAnimate();
            return animate;
        }

        [InitializeOnLoadMethod]
        static void EditorHeaderButtons() => Editor.finishedDefaultHeaderGUI += OnHeaderControlsGUI;

        static void OnHeaderControlsGUI(Editor editor)
        {
            if (!OnAssetGenerationValidation(editor.targets))
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!OnAssetGenerationMultipleValidation(editor.targets));
            if (GUILayout.Button(new GUIContent("Generate",
                    $"Use generative ai to transform this animate.")))
                OnAssetGenerationRequest(editor.targets);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        static void OnAssetGenerationRequest(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (TryGetValidAnimatePath(obj, out var validPath))
                {
                    OpenGenerationWindow(validPath);
                }
            }
        }

        static bool OnAssetGenerationValidation(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (AssetDatabase.IsOpenForEdit(obj) && TryGetValidAnimatePath(obj, out _))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetValidAnimatePath(Object obj, out string path)
        {
            path = obj switch
            {
                AnimationClip animate => AssetDatabase.GetAssetPath(animate),
                _ => null
            };

            if (string.IsNullOrEmpty(path))
                path = AssetDatabase.GetAssetPath(obj);

            return obj is AnimationClip && !string.IsNullOrEmpty(path);
        }

        static bool OnAssetGenerationMultipleValidation(IReadOnlyCollection<Object> objects) => objects.FirstOrDefault(o => TryGetValidAnimatePath(o, out _));

        public static void OpenGenerationWindow(string assetPath) => AnimateGeneratorWindow.Display(assetPath);
    }
}
