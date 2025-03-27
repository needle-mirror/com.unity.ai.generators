﻿using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class MetallicSmoothnessUtils
    {
        static Shader s_FragmentShader;
        static UnityEngine.Material s_BlitMaterial;
        
        public static Texture2D GenerateMetallicSmoothnessMap(Texture2D smoothness, Texture2D metallic = null)
        {
            if (!s_FragmentShader)
                s_FragmentShader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.ai.generators/Modules/Unity.AI.Material/Shaders/MetallicSmoothness.shader");
            if (!s_BlitMaterial)
                s_BlitMaterial = new UnityEngine.Material(s_FragmentShader);

            var destRT = RenderTexture.GetTemporary(smoothness.width, smoothness.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            s_BlitMaterial.SetTexture("_SmoothnessTex", smoothness);
            Graphics.Blit(metallic, destRT, s_BlitMaterial);

            var activeRT = RenderTexture.active;
            RenderTexture.active = destRT;

            var metallicSmoothnessMap = new Texture2D(smoothness.width, smoothness.height, TextureFormat.RGBA32, true, true);
            metallicSmoothnessMap.ReadPixels(new Rect(0, 0, metallicSmoothnessMap.width, metallicSmoothnessMap.height), 0, 0);
            metallicSmoothnessMap.Apply();

            RenderTexture.active = activeRT;
            RenderTexture.ReleaseTemporary(destRT);

            return metallicSmoothnessMap;
        }
    }
}
