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
                "Texture",
                AssetUtils.CreateBlankTexture,
                "Assets/New Texture.png",
                TextureGeneratorInspectorButton.OpenGenerationWindow
            );

            Toolkit.GenerationObjectPicker.RegisterTemplate<Sprite>(
                "Sprite",
                AssetUtils.CreateBlankSprite,
                "Assets/New Sprite.png",
                TextureGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
