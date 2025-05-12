﻿using System;
using System.IO;
using Unity.AI.Generators.Asset;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Material.Services.Utilities
{
    static class AssetUtils
    {
        public const string defaultNewAssetName = "New Material";
        public const string materialExtension = ".mat";
        public const string defaultTerrainLayerName = "New Terrain Layer";
        public const string terrainLayerExtension = ".terrainlayer";
        public static readonly string[] supportedExtensions = { ".mat", ".terrainlayer" };

        public static string CreateBlankMaterial(string path, bool force = true) => CreateBlankMaterial(path, null, force);

        static string CreateBlankMaterial(string path, Shader defaultShader = null, bool force = true)
        {
            if (!defaultShader)
                defaultShader = MaterialUtilities.GetDefaultMaterial().shader;

            var newMaterial = new UnityEngine.Material(defaultShader);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(newMaterial, assetPath);
            return assetPath;
        }

        public static UnityEngine.Material CreateAndSelectBlankMaterial(bool force = true)
        {
            var newAssetName = defaultNewAssetName;
            var defaultShader = Selection.activeObject as Shader;
            if (defaultShader)
                newAssetName = Path.GetFileNameWithoutExtension(defaultShader.name);

            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{newAssetName}{materialExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankMaterial(path, defaultShader);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create material for '{path}'.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            Selection.activeObject = material;
            return material;
        }

        public static UnityEngine.Material CreateMaterialFromShaderGraph(string shaderGraphPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderGraphPath);
            if (shader == null)
            {
                Debug.LogError($"Could not load shader from {shaderGraphPath}");
                return null;
            }

            var shaderGraphName = Path.GetFileNameWithoutExtension(shaderGraphPath);
            var materialName = $"{shaderGraphName} Material";

            var directory = Path.GetDirectoryName(shaderGraphPath);
            var materialPath = Path.Combine(directory, materialName + ".mat");
            materialPath = AssetDatabase.GenerateUniqueAssetPath(materialPath);

            var material = new UnityEngine.Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(material);
            Selection.activeObject = material;

            return material;
        }

        public static bool IsShaderGraph(Object obj)
        {
            if (obj == null)
                return false;

            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase);
        }

        public static string CreateBlankTerrainLayer(string path, bool force = true)
        {
            var newTerrainLayer = new UnityEngine.TerrainLayer();
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(newTerrainLayer, assetPath);
            return assetPath;
        }

        public static UnityEngine.TerrainLayer CreateBlankTerrainLayer(bool force = true)
        {
            var basePath = AssetUtilities.GetSelectionPath();
            var path = $"{basePath}/{defaultTerrainLayerName}{terrainLayerExtension}";
            if (force || !File.Exists(path))
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = CreateBlankTerrainLayer(path);
                if (string.IsNullOrEmpty(path))
                    Debug.Log($"Failed to create terrain layer for '{path}'.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var terrainLayer = AssetDatabase.LoadAssetAtPath<UnityEngine.TerrainLayer>(path);
            return terrainLayer;
        }

        public static UnityEngine.TerrainLayer CreateAndSelectBlankTerrainLayer(bool force = true)
        {
            var terrainLayer = CreateBlankTerrainLayer(force);
            Selection.activeObject = terrainLayer;
            return terrainLayer;
        }

        public static TerrainData CreateTerrainDataForTerrain(Terrain terrain)
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 513,
                baseMapResolution = 1024,
                size = new Vector3(1000, 600, 1000)
            };

            var terrainDataPath = AssetDatabase.GenerateUniqueAssetPath("Assets/New Terrain.asset");

            AssetDatabase.CreateAsset(terrainData, terrainDataPath);
            AssetDatabase.SaveAssets();

            terrain.terrainData = terrainData;

            return terrainData;
        }
    }
}
