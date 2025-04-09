using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class SmoothnessUtils
    {
        static Shader s_FragmentShader;
        static UnityEngine.Material s_BlitMaterial;

        public static Texture2D GenerateSmoothnessMap(Texture2D roughness)
        {
            if (!s_FragmentShader)
                s_FragmentShader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.ai.generators/Modules/Unity.AI.Material/Shaders/Smoothness.shader");
            if (!s_BlitMaterial)
                s_BlitMaterial = new UnityEngine.Material(s_FragmentShader);

            var destRT = RenderTexture.GetTemporary(roughness.width, roughness.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(roughness, destRT, s_BlitMaterial);

            var activeRT = RenderTexture.active;
            RenderTexture.active = destRT;

            var smoothnessMap = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
            smoothnessMap.ReadPixels(new Rect(0, 0, smoothnessMap.width, smoothnessMap.height), 0, 0);
            smoothnessMap.Apply();

            RenderTexture.active = activeRT;
            RenderTexture.ReleaseTemporary(destRT);

            return smoothnessMap;
        }
    }
}
