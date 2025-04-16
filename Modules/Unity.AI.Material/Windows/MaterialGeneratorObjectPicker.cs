using System;
using Unity.AI.Material.Services.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Windows
{
    static class MaterialGeneratorObjectPicker
    {
        [InitializeOnLoadMethod]
        static void ObjectPickerBlankGenerationHook()
        {
            Toolkit.GenerationObjectPicker.RegisterTemplate<UnityEngine.Material>(
                "Material",
                AssetUtils.CreateBlankMaterial,
                "Assets/New Material.mat",
                MaterialGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
