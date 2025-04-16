#define AI_TK_MATERIAL_EMISSIVE_DEFAULT
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.AI.Material.Services.Utilities
{
    static class MaterialUtilities
    {
        static UnityEngine.Material s_BuiltinUnlitMaterial;
        static UnityEngine.Material s_BuiltinLitMaterial;

        static readonly Dictionary<UnityEngine.Material, UnityEngine.Material> k_DefaultLitMaterials = new();
        static readonly Dictionary<UnityEngine.Material, UnityEngine.Material> k_DefaultUnlitMaterials = new();

        record CacheKey(string url);

        static readonly Dictionary<CacheKey, UnityEngine.Material> k_Cache = new();

        public const string mapsFolderSuffix = "_Generated Maps";

        public static bool IsBlank(this UnityEngine.Material material)
        {
            if (!material)
                return true;

            var texturePropertyNames = material.GetTexturePropertyNames();
            foreach (var propertyName in texturePropertyNames)
            {
                var texture = material.GetTexture(propertyName);
                if (texture != null)
                    return false;
            }

            return true;
        }

        public static string GetMapsPath(this UnityEngine.Material material) => AssetReferenceExtensions.GetMapsPath(AssetDatabase.GetAssetPath(material));

        public static UnityEngine.Material GetTemporary(this MaterialResult result)
        {
            if (result.IsMat())
            {
                var cacheKey = new CacheKey(result.uri.GetLocalPath());
                if (k_Cache.TryGetValue(cacheKey, out var material) && material)
                    return material;

                material = result.ImportMaterialTemporarily();

                k_Cache[cacheKey] = material;
                return material;
            }

            result.Sanitize();
            return GetTemporaryMaterialFromTextures(result.textures);
        }

        public static UnityEngine.Material GetTemporaryMaterialFromTextures(this IDictionary<MapType, TextureResult> textures)
        {
            var material = GetDefaultMaterial(textures.Count == 1);
            var generatedMaterialMapping = material.GetDefaultGeneratedMaterialMapping();

            // since we don't go through the asset importer (unlike CopyTo(...)) we need to manually set/unset some properties

            // all RPs
            foreach (var mapping in generatedMaterialMapping)
                material.SetTexture(mapping.Value, null);
            if (material.HasColor("_BaseColor"))
                material.SetColor("_BaseColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            // builtin and urp
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_EMISSION");
            if (material.HasColor("_EmissionColor"))
                material.SetColor("_EmissionColor", Color.black);

            // hdrp
            material.DisableKeyword("_EMISSIVE_COLOR_MAP");
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_MASKMAP");
            material.DisableKeyword("_HEIGHTMAP");
            if (material.HasFloat("_EmissiveExposureWeight"))
                material.SetFloat("_EmissiveExposureWeight", 1.0f);

            // set
            foreach (var textureResult in textures)
            {
                var key = textureResult.Key;
                if (textures.Count == 1 && textureResult.Key == MapType.Preview)
                    key = MapType.Delighted;
                if (!generatedMaterialMapping.TryGetValue(key, out var materialProperty))
                    continue;
                if (!material.HasTexture(materialProperty))
                    continue;
                var sourceFilePath = textureResult.Value.uri.GetLocalPath();

                var valid = File.Exists(sourceFilePath) && materialProperty != GenerationResult.noneMapping;
                switch (key)
                {
                    case MapType.Delighted:
                    {
                        material.EnableKeyword("_ALPHATEST_ON");
                        if (material.HasColor("_BaseColor"))
                            material.SetColor("_BaseColor", Color.white);
                        material.SetTexture(materialProperty, valid ? TextureCache.GetTextureUnsafe(textureResult.Value.uri) : null);
                        break;
                    }
                    case MapType.Normal:
                    {
                        material.EnableKeyword("_NORMALMAP");
                        material.SetTexture(materialProperty, valid ? TextureCache.GetNormalMapUnsafe(textureResult.Value.uri) : null);
                        break;
                    }
                    case MapType.Emission:
                        material.EnableKeyword("_EMISSIVE_COLOR_MAP");
                        material.EnableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        if (material.HasColor("_EmissionColor"))
                            material.SetColor("_EmissionColor", Color.white);
                        if (material.HasFloat("_EmissiveExposureWeight"))
                            material.SetFloat("_EmissiveExposureWeight", 0.5f);
                        material.SetTexture(materialProperty, valid ? TextureCache.GetTextureUnsafe(textureResult.Value.uri) : null);
                        break;
                    case MapType.MaskMap:
                        material.EnableKeyword("_MASKMAP");
                        material.SetTexture(materialProperty, valid ? TextureCache.GetTextureUnsafe(textureResult.Value.uri) : null);
                        break;
                    case MapType.Height:
                        material.EnableKeyword("_HEIGHTMAP");
                        material.SetTexture(materialProperty, valid ? TextureCache.GetTextureUnsafe(textureResult.Value.uri) : null);
                        break;
                    default:
                        material.SetTexture(materialProperty, valid ? TextureCache.GetTextureUnsafe(textureResult.Value.uri) : null);
                        break;
                }
            }

            return material;
        }

        public static UnityEngine.Material GetDefaultMaterial(bool unlit = false)
        {
            var renderPipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            if (!renderPipeline || !renderPipeline.defaultMaterial)
            {
                // builtin
                if (unlit)
                {
                    if (!s_BuiltinUnlitMaterial)
                        s_BuiltinUnlitMaterial = new UnityEngine.Material(Shader.Find("Unlit/Texture"));
                    return s_BuiltinUnlitMaterial;
                }

                if (!s_BuiltinLitMaterial)
                    s_BuiltinLitMaterial = new UnityEngine.Material(Shader.Find("Standard"));
                return s_BuiltinLitMaterial;
            }

            UnityEngine.Material material;
            if (!unlit)
            {
                // urp and hdrp (lit) and maybe builtin or custom rps
                if (k_DefaultLitMaterials.TryGetValue(renderPipeline.defaultMaterial, out material) && material)
                    return material;
                material = new UnityEngine.Material(renderPipeline.defaultMaterial);
                k_DefaultLitMaterials[renderPipeline.defaultMaterial] = material;
                return material;
            }

            // unlit
            if (k_DefaultUnlitMaterials.TryGetValue(renderPipeline.defaultMaterial, out material) && material)
                return material;

            // urp and hdrp (unlit) and maybe builtin or custom rps
            if (renderPipeline.defaultMaterial.shader.name.EndsWith("/Lit"))
            {
                var unlitShaderName = renderPipeline.defaultMaterial.shader.name.Replace("/Lit", "/Unlit");
                var unlitShader = Shader.Find(unlitShaderName);
                if (unlitShader)
                {
                    material = new UnityEngine.Material(unlitShader);
                    k_DefaultUnlitMaterials[renderPipeline.defaultMaterial] = material;
                    return material;
                }
            }

            // fallback to urp
            material = new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Unlit"));
            k_DefaultUnlitMaterials[renderPipeline.defaultMaterial] = material;
            return material;
        }

        public static void CopyTo(this UnityEngine.Material from, UnityEngine.Material to)
        {
            if (!from || !to)
                return;

            var texturePropertyNames = from.GetTexturePropertyNames();
            foreach (var propertyName in texturePropertyNames)
            {
                var texture = from.GetTexture(propertyName);
                if (to.HasTexture(propertyName))
                    to.SetTexture(propertyName, texture);
            }

            EditorUtility.SetDirty(to);
        }

        public static bool CopyTo(this MaterialResult from, UnityEngine.Material to, Dictionary<MapType, string> generatedMaterialMapping)
        {
            from.Sanitize();

            var mapsPath = to.GetMapsPath();
            if (!AssetDatabase.IsValidFolder(mapsPath))
            {
                mapsPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(Path.GetDirectoryName(mapsPath), Path.GetFileName(mapsPath)));
                if (string.IsNullOrEmpty(mapsPath))
                {
                    Debug.LogError("Failed to create new folder for material maps.");
                    return false;
                }
            }

            // the material's AI tag
            to.EnableGenerationLabel();

            // clear
            foreach (var mapping in generatedMaterialMapping)
                to.SetTexture(mapping.Value, null);

            // set
            foreach (var textureResult in from.textures)
            {
                var key = textureResult.Key;
                if (from.textures.Count == 1 && key == MapType.Preview)
                    key = MapType.Delighted;
                if (!generatedMaterialMapping.TryGetValue(key, out var materialProperty))
                    continue;
                if (!to.HasTexture(materialProperty))
                    continue;
                var sourceFilePath = textureResult.Value.uri.GetLocalPath();
                if (!File.Exists(sourceFilePath))
                {
                    Debug.LogWarning("Source file does not exist: " + sourceFilePath);
                    continue;
                }

                Texture2D importedTexture = null;
                if (materialProperty != GenerationResult.noneMapping)
                {
                    var extension = Path.GetExtension(sourceFilePath);
                    var destFilePath = Path.Combine(mapsPath, $"{materialProperty.TrimStart('_')}{extension}");
                    File.Copy(sourceFilePath, destFilePath, overwrite: true);
                    AssetDatabase.ImportAsset(destFilePath, ImportAssetOptions.ForceUpdate);
                    importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(destFilePath);

                    // each texture's AI tag
                    importedTexture.EnableGenerationLabel();
                }

                switch (key)
                {
                    case MapType.Normal:
                    {
                        if (importedTexture)
                        {
                            var textureImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(importedTexture)) as TextureImporter;
                            if (textureImporter != null)
                            {
                                textureImporter.textureType = TextureImporterType.NormalMap;
                                textureImporter.sRGBTexture = false;
                                textureImporter.SaveAndReimport();
                            }
                        }

                        break;
                    }
                }
                to.SetTexture(materialProperty, importedTexture);
            }

            // when we set our map and the related color is black, set it to white
            if (to.TryGetDefaultTexturePropertyName(MapType.Delighted, out var delightedPropertyName) &&
                generatedMaterialMapping.ContainsValue(delightedPropertyName) && to.HasTexture(delightedPropertyName) && to.GetTexture(delightedPropertyName))
            {
                if (to.HasColor("_BaseColor") && to.GetColor("_BaseColor") == Color.black)
                    to.SetColor("_BaseColor", Color.white);
            }
            // if we are not setting our map and the material doesn't have a map in that property, set the color to black if it was fully white
            else if (generatedMaterialMapping.ContainsValue(delightedPropertyName) && to.HasTexture(delightedPropertyName) && !to.GetTexture(delightedPropertyName))
            {
                if (to.HasColor("_BaseColor") && to.GetColor("_BaseColor") == Color.white)
                    to.SetColor("_BaseColor", Color.black);
            }

            // when we set our map and the related color is black, set it to white
            if (to.TryGetDefaultTexturePropertyName(MapType.Emission, out var emissionPropertyName) &&
                generatedMaterialMapping.ContainsValue(emissionPropertyName) && to.HasTexture(emissionPropertyName) && to.GetTexture(emissionPropertyName))
            {
                to.EnableKeyword("_EMISSION");
                to.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                if (to.HasColor("_EmissionColor") && to.GetColor("_EmissionColor") == Color.black)
                    to.SetColor("_EmissionColor", Color.white);
                if (to.HasFloat("_EmissiveExposureWeight"))
                    to.SetFloat("_EmissiveExposureWeight", 0.5f);
            }
            // if we are not setting our map and the material doesn't have a map in that property, set the color to black if it was fully white
            else if (generatedMaterialMapping.ContainsValue(emissionPropertyName) && to.HasTexture(emissionPropertyName) && !to.GetTexture(emissionPropertyName))
            {
                if (to.HasColor("_EmissionColor") && to.GetColor("_EmissionColor") == Color.white)
                    to.SetColor("_EmissionColor", Color.black);
                if (to.HasFloat("_EmissiveExposureWeight"))
                    to.SetFloat("_EmissiveExposureWeight", 1.0f);
                to.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                to.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(to);

            return true;
        }

        public static Dictionary<MapType, string> GetDefaultGeneratedMaterialMapping(this UnityEngine.Material material)
        {
            var mapping = new Dictionary<MapType, string>();
            foreach (MapType mapType in Enum.GetValues(typeof(MapType)))
            {
                if (mapType == MapType.Preview)
                    continue;

                if (material.TryGetDefaultTexturePropertyName(mapType, out var texturePropertyName))
                    mapping[mapType] = texturePropertyName;
            }
            return mapping;
        }

        public static bool TryGetDefaultTexturePropertyName(this UnityEngine.Material material, MapType mapType, out string texturePropertyName)
        {
            switch (mapType)
            {
                case MapType.Preview:
                    // No texture property to return for Preview
                    break;

                case MapType.Height:
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_ParallaxMap"))
                    {
                        texturePropertyName = "_ParallaxMap";
                        return true;
                    }
                    // HDRP/Lit
                    if (material.HasTexture("_HeightMap"))
                    {
                        texturePropertyName = "_HeightMap";
                        return true;
                    }
                    break;

                case MapType.Normal:
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_BumpMap"))
                    {
                        texturePropertyName = "_BumpMap";
                        return true;
                    }
                    // HDRP/Lit
                    if (material.HasTexture("_NormalMap"))
                    {
                        texturePropertyName = "_NormalMap";
                        return true;
                    }
                    break;

                case MapType.Emission:
#if AI_TK_MATERIAL_EMISSIVE_DEFAULT
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_EmissionMap"))
                    {
                        texturePropertyName = "_EmissionMap";
                        return true;
                    }
                    // HDRP/Lit
                    if (material.HasTexture("_EmissiveColorMap"))
                    {
                        texturePropertyName = "_EmissiveColorMap";
                        return true;
                    }
#endif
                    break;

                case MapType.Metallic:
                    // Muse
                    if (material.HasTexture("_MetallicMap"))
                    {
                        texturePropertyName = "_MetallicMap";
                        return true;
                    }

                    break;

                case MapType.Roughness:
                    // Muse
                    if (material.HasTexture("_RoughnessMap"))
                    {
                        texturePropertyName = "_RoughnessMap";
                        return true;
                    }

                    break;

                case MapType.Delighted: // Albedo
                    // Muse
                    if (material.HasTexture("_AlbedoMap"))
                    {
                        texturePropertyName = "_AlbedoMap";
                        return true;
                    }

                    // Universal Render Pipeline/Lit, Universal Render Pipeline/Unlit
                    if (material.HasTexture("_BaseMap"))
                    {
                        texturePropertyName = "_BaseMap";
                        return true;
                    }
                    // HDRP/Lit
                    if (material.HasTexture("_BaseColorMap"))
                    {
                        texturePropertyName = "_BaseColorMap";
                        return true;
                    }
                    // HDRP/Unlit
                    if (material.HasTexture("_UnlitColorMap"))
                    {
                        texturePropertyName = "_UnlitColorMap";
                        return true;
                    }
                    // Unlit/Texture, Standard
                    if (material.HasTexture("_MainTex"))
                    {
                        texturePropertyName = "_MainTex";
                        return true;
                    }
                    break;

                case MapType.Occlusion:
                    // Muse
                    if (material.HasTexture("_AmbientOcclusionMap"))
                    {
                        texturePropertyName = "_AmbientOcclusionMap";
                        return true;
                    }

                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_OcclusionMap"))
                    {
                        texturePropertyName = "_OcclusionMap";
                        return true;
                    }
                    break;

                case MapType.MaskMap:
                    // HDRP/Lit
                    if (material.HasTexture("_MaskMap"))
                    {
                        texturePropertyName = "_MaskMap";
                        return true;
                    }
                    break;

                case MapType.Smoothness:
                    // Muse
                    if (material.HasTexture("_SmoothnessMap"))
                    {
                        texturePropertyName = "_SmoothnessMap";
                        return true;
                    }

                    break;

                case MapType.MetallicSmoothness:
                    break;

                case MapType.NonMetallicSmoothness:
                    // Universal Render Pipeline/Lit, Standard
                    if (material.HasTexture("_MetallicGlossMap"))
                    {
                        texturePropertyName = "_MetallicGlossMap";
                        return true;
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mapType), mapType, null);
            }

            texturePropertyName = null;
            return false;
        }
    }
}
