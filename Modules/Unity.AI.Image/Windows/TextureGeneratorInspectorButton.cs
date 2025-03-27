using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Toolkit.GenerationContextMenu;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Image.Windows
{
    static class TextureGeneratorInspectorButton
    {
        [GenerateContextMenu(nameof(GenerateImageValidation))]
        public static void GenerateImage() => OnAssetGenerationRequest(new[] { Selection.activeObject });

        public static bool GenerateImageValidation() => OnAssetGenerationValidation(new[] { Selection.activeObject }) && Selection.objects.Length == 1;

        [MenuItem("Assets/Create/Rendering/Generate Texture 2D", false, -1000)]
        public static void EmptyTextureMenu() => EmptyTexture();

        public static Texture2D EmptyTexture()
        {
            var texture = AssetUtils.CreateAndSelectBlankTexture();
            Selection.activeObject = texture;
            GenerateImage();
            return texture;
        }

        [InitializeOnLoadMethod]
        static void EditorHeaderButtons() => Editor.finishedDefaultHeaderGUI += OnHeaderControlsGUI;

        static void OnHeaderControlsGUI(Editor editor)
        {
            if (!EditorUtility.IsPersistent(editor.target))
                return;

            if (!OnAssetGenerationValidation(editor.targets))
                return;

            var assetPath = AssetDatabase.GetAssetPath(editor.target);
            var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!OnAssetGenerationMultipleValidation(editor.targets));
            if (GUILayout.Button(new GUIContent("Generate",
                    $"Use generative ai to transform this {(textureImporter ? textureImporter.textureType : TextureImporterType.Default).ToString()} texture.")))
                OnAssetGenerationRequest(editor.targets);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        static void OnAssetGenerationRequest(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (TryGetValidTexturePath(obj, out var validPath))
                {
                    OpenGenerationWindow(validPath);
                }
            }
        }

        static bool OnAssetGenerationValidation(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (AssetDatabase.IsOpenForEdit(obj) && TryGetValidTexturePath(obj, out _))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetValidTexturePath(Object obj, out string path)
        {
            switch (obj)
            {
                case Texture2D texture:
                    path = AssetDatabase.GetAssetPath(texture);
                    break;
                case TextureImporter importer:
                    path = importer.assetPath;
                    // todo we don't support cubemaps yet
                    if (importer.textureShape == TextureImporterShape.TextureCube)
                        return false;
                    break;
                default:
                    path = null;
                    break;
            }

            if (string.IsNullOrEmpty(path))
                path = AssetDatabase.GetAssetPath(obj);

            if (!string.IsNullOrEmpty(path))
            {
                var extension = Path.GetExtension(path).ToLower();
                if (ImageFileUtilities.knownExtensions.Any(suffix => suffix.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        static bool OnAssetGenerationMultipleValidation(IReadOnlyCollection<Object> objects) => objects.FirstOrDefault(o => TryGetValidTexturePath(o, out _));

        internal static void OpenGenerationWindow(string assetPath) => TextureGeneratorWindow.Display(assetPath);
    }
}
