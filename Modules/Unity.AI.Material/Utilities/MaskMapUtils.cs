using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    static class MaskMapUtils
    {
        static Shader s_FragmentShader;
        static UnityEngine.Material s_BlitMaterial;

        public static Texture2D GenerateMaskMap(Texture2D roughness, Texture2D metallic, Texture2D occlusion)
        {
            if (!s_FragmentShader)
                s_FragmentShader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.ai.generators/Modules/Unity.AI.Material/Shaders/MaskMap.shader");
            if (!s_BlitMaterial)
                s_BlitMaterial = new UnityEngine.Material(s_FragmentShader);

            var destRT = RenderTexture.GetTemporary(roughness.width, roughness.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            s_BlitMaterial.SetTexture("_RoughnessTex", roughness);
            s_BlitMaterial.SetTexture("_OcclusionTex", occlusion);
            Graphics.Blit(metallic, destRT, s_BlitMaterial);

            var activeRT = RenderTexture.active;
            RenderTexture.active = destRT;

            var maskMap = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
            maskMap.ReadPixels(new Rect(0, 0, maskMap.width, maskMap.height), 0, 0);
            maskMap.Apply();

            RenderTexture.active = activeRT;
            RenderTexture.ReleaseTemporary(destRT);

            return maskMap;
        }
    }
}
