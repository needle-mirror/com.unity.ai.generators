using System;
using Unity.AI.Material.Services.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Windows
{
    static class MaterialGeneratorObjectPicker
    {
        [InitializeOnLoadMethod]
        public static void ObjectPickerBlankGenerationHook()
        {
            Toolkit.GenerationObjectPicker.RegisterTemplate<UnityEngine.Material>(
                "Material",
                AssetUtils.CreateBlankMaterial,
                $"Assets/New Material{AssetUtils.materialExtension}",
                MaterialGeneratorInspectorButton.OpenGenerationWindow
            );

            Toolkit.GenerationObjectPicker.RegisterTemplate<UnityEngine.TerrainLayer>(
                "TerrainLayer",
                AssetUtils.CreateBlankTerrainLayer,
                $"Assets/New Terrain Layer{AssetUtils.terrainLayerExtension}",
                MaterialGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
