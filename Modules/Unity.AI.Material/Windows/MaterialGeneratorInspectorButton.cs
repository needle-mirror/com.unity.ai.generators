using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Toolkit.GenerationContextMenu;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Material.Windows
{
    static class MaterialGeneratorInspectorButton
    {
        [GenerateContextMenu(nameof(GenerateMaterialValidation))]
        public static void GenerateMaterial() => OnAssetGenerationRequest(new[] { Selection.activeObject });

        public static bool GenerateMaterialValidation() => OnAssetGenerationValidation(new[] { Selection.activeObject }) && Selection.objects.Length == 1;

        [MenuItem("Assets/Create/Rendering/Generate Material", false, -1000)]
        public static void EmptyMaterialMenu() => EmptyMaterial();

        public static UnityEngine.Material EmptyMaterial()
        {
            var material = AssetUtils.CreateAndSelectBlankMaterial();
            Selection.activeObject = material;
            OnAssetGenerationRequest(new[] { Selection.activeObject });
            return material;
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
                    $"Use generative ai to transform this material.")))
                OnAssetGenerationRequest(editor.targets);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        static void OnAssetGenerationRequest(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (AssetUtils.IsShaderGraph(obj))
                {
                    var shaderPath = AssetDatabase.GetAssetPath(obj);
                    var newMaterial = AssetUtils.CreateMaterialFromShaderGraph(shaderPath);
                    if (newMaterial != null)
                    {
                        OpenGenerationWindow(AssetDatabase.GetAssetPath(newMaterial));
                    }
                }
                else if (TryGetValidMaterialPath(obj, out var validPath))
                {
                    OpenGenerationWindow(validPath);
                }
            }
        }

        static bool OnAssetGenerationValidation(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (AssetDatabase.IsOpenForEdit(obj) && (TryGetValidMaterialPath(obj, out _) || AssetUtils.IsShaderGraph(obj)))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetValidMaterialPath(Object obj, out string path)
        {
            path = obj switch
            {
                UnityEngine.Material material => AssetDatabase.GetAssetPath(material),
                _ => null
            };

            if (string.IsNullOrEmpty(path))
                path = AssetDatabase.GetAssetPath(obj);

            return obj is UnityEngine.Material && !string.IsNullOrEmpty(path);
        }

        static bool OnAssetGenerationMultipleValidation(IReadOnlyCollection<Object> objects) =>
            objects.Any(o => TryGetValidMaterialPath(o, out _) || AssetUtils.IsShaderGraph(o));

        public static void OpenGenerationWindow(string assetPath) => MaterialGeneratorWindow.Display(assetPath);
    }
}
