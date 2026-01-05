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
                "Texture2D",
                AssetUtils.CreateBlankTexture,
                $"Assets/{AssetUtils.defaultNewAssetName}.png",
                TextureGeneratorInspectorButton.OpenGenerationWindow
            );

            Toolkit.GenerationObjectPicker.RegisterTemplate<Sprite>(
                "Sprite",
                AssetUtils.CreateBlankSprite,
                $"Assets/{AssetUtils.defaultNewAssetNameSprite}.png",
                TextureGeneratorInspectorButton.OpenGenerationWindow
            );

            Toolkit.GenerationObjectPicker.RegisterTemplate<Cubemap>(
                "Cubemap",
                AssetUtils.CreateBlankCubemap,
                $"Assets/{AssetUtils.defaultNewAssetNameCube}.png",
                TextureGeneratorInspectorButton.OpenGenerationWindow
            );
        }
    }
}
