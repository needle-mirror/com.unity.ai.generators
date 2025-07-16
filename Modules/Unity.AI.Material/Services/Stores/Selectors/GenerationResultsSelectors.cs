using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Material.Services.Undo;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Payloads;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Services.Stores.Selectors
{
    static partial class Selectors
    {
        public static GenerationResults SelectGenerationResults(this IState state) => state.Get<GenerationResults>(GenerationResultsActions.slice);
        public static GenerationResult SelectGenerationResult(this IState state, VisualElement element) => state.SelectGenerationResult(element.GetAsset());
        public static GenerationResult SelectGenerationResult(this IState state, AssetReference asset)
        {
            if (state == null)
                return new GenerationResult();
            var results = state.SelectGenerationResults().generationResults;
            return results.Ensure(asset);
        }
        public static bool SelectGenerationAllowed(this IState state, VisualElement element)
        {
            var results = state.SelectGenerationResult(element);
            return results.generationAllowed && results.generationValidation.success;
        }
        public static List<GenerationProgressData> SelectGenerationProgress(this IState state, VisualElement element) => state.SelectGenerationResult(element).generationProgress;
        public static GenerationProgressData SelectGenerationProgress(this IState state, VisualElement element, MaterialResult result)
        {
            if (result is MaterialSkeleton textureSkeleton)
            {
                var progressReports = state.SelectGenerationResult(element).generationProgress;
                var progressReport = progressReports.FirstOrDefault(d => d.taskID == textureSkeleton.taskID);
                if (progressReport != null)
                    return progressReport;
            }

            return new GenerationProgressData(-1, 1, 1);
        }
        public static List<GenerationFeedbackData> SelectGenerationFeedback(this IState state, VisualElement element) => state.SelectGenerationResult(element).generationFeedback;
        public static GenerationValidationResult SelectGenerationValidationResult(this IState state, VisualElement element) => state.SelectGenerationResult(element).generationValidation;

        public static int SelectGeneratedResultVisibleCount(this IState state, VisualElement element) => state.SelectGenerationResult(element)
            .generatedResultSelectorSettings.Values.Select(hints => hints.itemCountHint).DefaultIfEmpty(0).Max();
        public static int SelectGeneratedResultVisibleCount(this IState state, AssetReference asset) => state.SelectGenerationResult(asset)
            .generatedResultSelectorSettings.Values.Select(hints => hints.itemCountHint).DefaultIfEmpty(0).Max();

        public static List<MaterialResult> SelectGeneratedMaterials(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedMaterials;
        public static List<MaterialResult> SelectGeneratedMaterials(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedMaterials;
        public static List<MaterialSkeleton> SelectGeneratedSkeletons(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedSkeletons;
        public static List<MaterialSkeleton> SelectGeneratedSkeletons(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedSkeletons;
        public static List<MaterialResult> SelectGeneratedMaterialsAndSkeletons(this IState state, VisualElement element)
        {
            var generationResults = state.SelectGenerationResult(element);
            return generationResults.generatedSkeletons.Concat(generationResults.generatedMaterials).ToList();
        }

        public static bool HasHistory(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedMaterials.Count > 0;
        public static MaterialResult SelectSelectedGeneration(this IState state, VisualElement element) => state.SelectGenerationResult(element).selectedGeneration;
        public static MaterialResult SelectSelectedGeneration(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).selectedGeneration;
        public static Dictionary<MapType, string> SelectGeneratedMaterialMapping(this IState state, VisualElement element) => state.SelectGenerationResult(element).generatedMaterialMapping;
        public static Dictionary<MapType, string> SelectGeneratedMaterialMapping(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).generatedMaterialMapping;
        public static bool SelectGeneratedMaterialMappingIsNone(this IState state, AssetReference asset) =>
            state.SelectGenerationResult(asset).generatedMaterialMapping.Values.All(v => v == GenerationResult.noneMapping);

        public static AssetUndoManager SelectAssetUndoManager(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).assetUndoManager;
        public static bool SelectReplaceWithoutConfirmationEnabled(this IState state, AssetReference asset) => state.SelectGenerationResult(asset).replaceWithoutConfirmation;

        public static (bool success, string texturePropertyName) GetDefaultTexturePropertyName(IMaterialAdapter material, MapType mapType)
        {
            // Fall back to built-in defaults if no cached mapping was found or applicable
            switch (mapType)
            {
                case MapType.Preview:
                    // No texture property to return for Preview
                    break;

                case MapType.Height:
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_ParallaxMap"))
                        return (true, "_ParallaxMap");
                    // HDRP/Lit
                    if (material.HasTexture("_HeightMap"))
                        return (true, "_HeightMap");
                    break;

                case MapType.Normal:
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_BumpMap"))
                        return (true, "_BumpMap");
                    // HDRP/Lit, Terrain
                    if (material.HasTexture("_NormalMap"))
                        return (true, "_NormalMap");
                    break;

                case MapType.Emission:
#if AI_TK_MATERIAL_EMISSIVE_DEFAULT
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_EmissionMap"))
                        return (true, "_EmissionMap");

                    // HDRP/Lit
                    if (material.HasTexture("_EmissiveColorMap"))
                        return (true, "_EmissiveColorMap");
#endif
                    break;

                case MapType.Metallic:
                    // Muse
                    if (material.HasTexture("_MetallicMap"))
                        return (true, "_MetallicMap");
                    break;

                case MapType.Roughness:
                    // Muse
                    if (material.HasTexture("_RoughnessMap"))
                        return (true, "_RoughnessMap");
                    break;

                case MapType.Delighted: // Albedo
                    // Muse
                    if (material.HasTexture("_AlbedoMap"))
                        return (true, "_AlbedoMap");

                    // Universal Render Pipeline/Lit, Universal Render Pipeline/Unlit
                    if (material.HasTexture("_BaseMap"))
                        return (true, "_BaseMap");

                    // HDRP/Lit
                    if (material.HasTexture("_BaseColorMap"))
                        return (true, "_BaseColorMap");

                    // HDRP/Unlit
                    if (material.HasTexture("_UnlitColorMap"))
                        return (true, "_UnlitColorMap");

                    // Unlit/Texture, Standard
                    if (material.HasTexture("_MainTex"))
                        return (true, "_MainTex");

                    // Terrain
                    if (material.HasTexture("_Diffuse"))
                        return (true, "_Diffuse");

                    break;

                case MapType.Occlusion:
                    // Muse
                    if (material.HasTexture("_AmbientOcclusionMap"))
                        return (true, "_AmbientOcclusionMap");

                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_OcclusionMap"))
                        return (true, "_OcclusionMap");

                    break;

                case MapType.MaskMap:
                    // HDRP/Lit, Terrain
                    if (material.HasTexture("_MaskMap"))
                        return (true, "_MaskMap");

                    break;

                case MapType.Smoothness:
                    // Muse
                    if (material.HasTexture("_SmoothnessMap"))
                        return (true, "_SmoothnessMap");

                    break;

                case MapType.MetallicSmoothness:
                    break;

                case MapType.NonMetallicSmoothness:
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_MetallicGlossMap"))
                        return (true, "_MetallicGlossMap");

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mapType), mapType, null);
            }

            return (false, null);
        }

        public static (bool success, string texturePropertyName) GetTexturePropertyName(this IState state, IMaterialAdapter material, MapType mapType)
        {
            // First check if we have a cached mapping for this shader and map type
            if (state != null && material.IsValid && !string.IsNullOrEmpty(material.Shader))
            {
                var session = state.SelectSession();
                if (session?.settings?.lastMaterialMappings != null &&
                    session.settings.lastMaterialMappings.TryGetValue(material.Shader, out var mappings) &&
                    mappings.TryGetValue(mapType, out var cachedMapping) && !string.IsNullOrEmpty(cachedMapping))
                {
                    return material.HasTexture(cachedMapping) && cachedMapping != GenerationResult.noneMapping ? (true, cachedMapping) : (false, null);
                }
            }

            return GetDefaultTexturePropertyName(material, mapType);
        }

        public static (bool success, string texturePropertyName) GetTexturePropertyName(this IState state, AssetReference asset, MapType mapType, bool forceDefault = false)
        {
            var material = asset.GetMaterialAdapter();
            if (forceDefault)
                return GetDefaultTexturePropertyName(asset, mapType);
            return !material.IsValid ? (false, null) : state.GetTexturePropertyName(material, mapType);
        }

        static (bool success, string texturePropertyName) GetDefaultTexturePropertyName(AssetReference asset, MapType mapType)
        {
            var material = asset.GetMaterialAdapter();
            return !material.IsValid ? (false, null) : GetDefaultTexturePropertyName(material, mapType);
        }
    }
}
