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
                "Assets/AI Toolkit/Templates/!New Material Asset from Generation....mat",
                AssetUtils.CreateBlankMaterial,
                "Assets/New Material.mat",
                MaterialGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
