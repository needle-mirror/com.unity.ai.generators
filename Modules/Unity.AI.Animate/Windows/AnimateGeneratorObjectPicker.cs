using System;
using Unity.AI.Animate.Services.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Animate.Windows
{
    static class AnimateGeneratorObjectPicker
    {
        [InitializeOnLoadMethod]
        static void ObjectPickerBlankGenerationHook()
        {
            Toolkit.GenerationObjectPicker.RegisterTemplate<AnimationClip>(
                $"Assets/AI Toolkit/Templates/!New Animation Asset from Generation...{AssetUtils.defaultAssetExtension}",
                AssetUtils.CreateBlankAnimation,
                $"Assets/New Animation{AssetUtils.defaultAssetExtension}",
                AnimateGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
