using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.AI.Material.Services.Utilities
{
    static class MaterialAdapterFactory
    {
        public static IMaterialAdapter Create(UnityEngine.Material material) => new MaterialAdapter(material);

        public static IMaterialAdapter Create(UnityEngine.TerrainLayer terrainLayer) => new TerrainLayerAdapter(terrainLayer);

        public static IMaterialAdapter Create(UnityEngine.Object obj)
        {
            return obj switch
            {
                UnityEngine.Material material => Create(material),
                UnityEngine.TerrainLayer terrainLayer => Create(terrainLayer),
                _ => new InvalidMaterialAdapter()
            };
        }

        public static bool IsSupportedAssetType(Type assetType)
        {
            return assetType != null &&
                  (typeof(UnityEngine.Material).IsAssignableFrom(assetType) ||
                   typeof(UnityEngine.TerrainLayer).IsAssignableFrom(assetType));
        }

        public static bool IsSupportedAssetAtPath(string path)
        {
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            return IsSupportedAssetType(assetType);
        }
    }

    interface IMaterialAdapter
    {
        bool HasTexture(string propertyName);
        Texture GetTexture(string propertyName);
        string[] GetTexturePropertyNames();
        void SetTexture(string propertyName, Texture texture);
        UnityEngine.Object AsObject { get; }
        bool IsValid { get; }
        string Shader { get; }
    }

    readonly struct InvalidMaterialAdapter : IMaterialAdapter
    {
        public string ProviderName => "Invalid Material";
        public UnityEngine.Object AsObject => null;
        public bool IsValid => false;
        public bool HasTexture(string propertyName) => false;
        public Texture GetTexture(string propertyName) => null;
        public string[] GetTexturePropertyNames() => Array.Empty<string>();
        public void SetTexture(string propertyName, Texture texture) { }
        public string Shader => "Invalid";
    }

    readonly struct MaterialAdapter : IMaterialAdapter
    {
        readonly UnityEngine.Material m_Material;

        public string ProviderName => m_Material != null ? m_Material.name : "Null Material";

        public MaterialAdapter(UnityEngine.Material material) => m_Material = material;

        public bool HasTexture(string propertyName) => m_Material != null && m_Material.HasProperty(propertyName);

        public Texture GetTexture(string propertyName) => HasTexture(propertyName) ? m_Material.GetTexture(propertyName) : null;

        public string[] GetTexturePropertyNames() => m_Material == null ? Array.Empty<string>() : m_Material.GetTexturePropertyNames();

        public void SetTexture(string propertyName, Texture texture)
        {
            if (m_Material)
                m_Material.SetTexture(propertyName, texture);
        }

        public UnityEngine.Object AsObject => m_Material;

        public bool IsValid => m_Material != null;

        public string Shader => m_Material?.shader != null ? m_Material.shader.name : "Invalid";

        public static implicit operator bool(MaterialAdapter provider) => provider.m_Material != null;
    }

    readonly struct TerrainLayerAdapter : IMaterialAdapter
    {
        readonly UnityEngine.TerrainLayer m_TerrainLayer;

        static readonly Dictionary<string, int> k_PropertyMap = new()
        {
            { "_Diffuse", 0 },
            { "_NormalMap", 1 },
            { "_MaskMap", 2 }
        };

        public string ProviderName => m_TerrainLayer != null ? m_TerrainLayer.name : "Null TerrainLayer";

        public UnityEngine.Object AsObject => m_TerrainLayer;

        public TerrainLayerAdapter(UnityEngine.TerrainLayer terrainLayer) => m_TerrainLayer = terrainLayer;

        public bool HasTexture(string propertyName) => m_TerrainLayer != null && k_PropertyMap.ContainsKey(propertyName);

        public Texture GetTexture(string propertyName)
        {
            if (m_TerrainLayer == null || !k_PropertyMap.TryGetValue(propertyName, out var value))
                return null;

            return value switch
            {
                0 => m_TerrainLayer.diffuseTexture,
                1 => m_TerrainLayer.normalMapTexture,
                2 => m_TerrainLayer.maskMapTexture,
                _ => (Texture)null
            };
        }

        public string[] GetTexturePropertyNames() => k_PropertyMap.Keys.ToArray();

        public void SetTexture(string propertyName, Texture texture)
        {
            if (m_TerrainLayer == null || !k_PropertyMap.TryGetValue(propertyName, out var value))
                return;

            switch (value)
            {
                case 0:
                    m_TerrainLayer.diffuseTexture = texture as Texture2D;
                    break;
                case 1:
                    m_TerrainLayer.normalMapTexture = texture as Texture2D;
                    break;
                case 2:
                    m_TerrainLayer.maskMapTexture = texture as Texture2D;
                    break;
            }
        }

        public bool IsValid => m_TerrainLayer != null;

        public string Shader => "Nature/Terrain/Standard";

        public static implicit operator bool(TerrainLayerAdapter provider) => provider.m_TerrainLayer != null;
    }
}
