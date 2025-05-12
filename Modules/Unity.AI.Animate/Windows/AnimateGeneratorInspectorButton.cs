using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit.Accounts.Services;
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
        public static void EmptyAnimateMenu() => CreateAndNameAnimate();

        [MenuItem("Assets/Create/Animation/Generate Animation Clip", true)]
        static bool ValidateEmptyAnimateMenu() => Account.settings.AiGeneratorsEnabled;

        public static AnimationClip EmptyAnimate()
        {
            var animate = AssetUtils.CreateAndSelectBlankAnimation();
            Selection.activeObject = animate;
            GenerateAnimate();
            return animate;
        }

        public static void CreateAndNameAnimate()
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(AnimationClip))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateBlankAsset>();
            doCreate.action = (_, pathName, _) =>
            {
                pathName = AssetUtils.CreateBlankAnimation(pathName);
                if (string.IsNullOrEmpty(pathName))
                    Debug.Log($"Failed to create animate file for '{pathName}'.");
                AssetDatabase.ImportAsset(pathName, ImportAssetOptions.ForceUpdate);
                var animate = AssetDatabase.LoadAssetAtPath<AnimationClip>(pathName);
                Selection.activeObject = animate;
                GenerateAnimate();
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
            if (!OnAssetGenerationValidation(editor.targets))
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var generatorsEnabled = Account.settings.AiGeneratorsEnabled;
            EditorGUI.BeginDisabledGroup(!OnAssetGenerationMultipleValidation(editor.targets)  || !generatorsEnabled);
            var generateButtonTooltip = $"Use generative ai to transform this animate.";
            if (!generatorsEnabled)
                generateButtonTooltip = Generators.UI.AIDropdownIntegrations.GenerativeMenuRoot.generatorsIsDisabledTooltip;
            if (GUILayout.Button(new GUIContent("Generate",
                    generateButtonTooltip)))
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
