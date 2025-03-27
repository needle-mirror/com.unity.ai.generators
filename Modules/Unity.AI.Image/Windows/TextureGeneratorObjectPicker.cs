using Unity.AI.Image.Services.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Image.Windows
{
    static class TextureGeneratorObjectPicker
    {
        [InitializeOnLoadMethod]
        static void ObjectPickerBlankGenerationHook()
        {
            Toolkit.GenerationObjectPicker.RegisterTemplate<Texture2D>(
                "Assets/AI Toolkit/Templates/!New Texture Asset from Generation....png",
                AssetUtils.CreateBlankTexture,
                "Assets/New Texture.png",
                TextureGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
