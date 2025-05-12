using System;
using Unity.AI.Animate.Windows;
using Unity.AI.Image.Windows;
using Unity.AI.Material.Windows;
using Unity.AI.Sound.Windows;
using Unity.AI.Generators.Asset;
using Unity.AI.Toolkit.Chat;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.TerrainLayer.Windows;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.Chat
{
    static class PluginEntryPoints
    {
        static VisualElement s_ImageVisualElement;
        static VisualElement s_SpriteVisualElement;
        static VisualElement s_AnimateVisualElement;
        static VisualElement s_SoundVisualElement;
        static VisualElement s_MaterialVisualElement;
        static VisualElement s_TerrainLayerVisualElement;

        [Plugin("Plugin for creating a sound given a prompt.", toolName: "Sound", actionText: "Generate Sound")]
        public static void GenerateSound(
            [Parameter("The prompt to guide what sound will be generated")]
            string prompt)
        {
            var audioClip = SoundGeneratorInspectorButton.EmptyAudioClip();

            s_SoundVisualElement ??= new VisualElement();
            var assetPath = AssetDatabase.GetAssetPath(audioClip);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            Contexts.ContextExtensions.ProvideContext(s_SoundVisualElement, StoreExtensions.storeKey, Sound.Services.SessionPersistence.SharedStore.Store);
            s_SoundVisualElement.SetAssetContext(asset);

            s_SoundVisualElement.Dispatch(Sound.Services.Stores.Actions.GenerationSettingsActions.setPrompt, prompt);

            // todo: maybe generate 1 right away (model selection is ambiguous here)
        }

        [Plugin("Plugin for creating a material given a prompt.", toolName: "Material", actionText: "Generate Material")]
        public static void GenerateMaterial(
            [Parameter("The prompt to guide what material will be generated")]
            string prompt)
        {
            var material = MaterialGeneratorInspectorButton.EmptyMaterial();

            s_MaterialVisualElement ??= new VisualElement();
            var assetPath = AssetDatabase.GetAssetPath(material);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            Contexts.ContextExtensions.ProvideContext(s_MaterialVisualElement, StoreExtensions.storeKey, Material.Services.SessionPersistence.SharedStore.Store);
            s_MaterialVisualElement.SetAssetContext(asset);

            s_MaterialVisualElement.Dispatch(Material.Services.Stores.Actions.GenerationSettingsActions.setPrompt, prompt);

            // todo: maybe generate 1 right away (model selection is ambiguous here)
        }

        [Plugin("Plugin for creating a sprite given a prompt.", toolName: "Sprite", actionText: "Generate Sprite")]
        public static void GenerateSprite(
            [Parameter("The prompt to guide what sprite will be generated")]
            string prompt)
        {
            var sprite = TextureGeneratorInspectorButton.EmptySprite();

            s_SpriteVisualElement ??= new VisualElement();
            var assetPath = AssetDatabase.GetAssetPath(sprite);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            Contexts.ContextExtensions.ProvideContext(s_SpriteVisualElement, StoreExtensions.storeKey, Image.Services.SessionPersistence.SharedStore.Store);
            s_SpriteVisualElement.SetAssetContext(asset);

            s_SpriteVisualElement.Dispatch(Image.Services.Stores.Actions.GenerationSettingsActions.setPrompt, prompt);

            // todo: maybe generate 1 right away (model selection is ambiguous here)
        }

        [Plugin("Plugin for creating a texture given a prompt.", toolName: "Texture", actionText: "Generate Texture")]
        public static void GenerateTexture(
            [Parameter("The prompt to guide what texture will be generated")]
            string prompt)
        {
            var texture = TextureGeneratorInspectorButton.EmptyTexture();

            s_ImageVisualElement ??= new VisualElement();
            var assetPath = AssetDatabase.GetAssetPath(texture);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            Contexts.ContextExtensions.ProvideContext(s_ImageVisualElement, StoreExtensions.storeKey, Image.Services.SessionPersistence.SharedStore.Store);
            s_ImageVisualElement.SetAssetContext(asset);

            s_ImageVisualElement.Dispatch(Image.Services.Stores.Actions.GenerationSettingsActions.setPrompt, prompt);

            // todo: maybe generate 1 right away (model selection is ambiguous here)
        }

        [Plugin("Plugin for creating an animation given a prompt.", toolName: "Animation", actionText: "Generate Animation")]
        public static void GenerateAnimation(
            [Parameter("The prompt to guide what animation will be generated")]
            string prompt)
        {
            var animation = AnimateGeneratorInspectorButton.EmptyAnimate();

            s_AnimateVisualElement ??= new VisualElement();
            var assetPath = AssetDatabase.GetAssetPath(animation);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            Contexts.ContextExtensions.ProvideContext(s_AnimateVisualElement, StoreExtensions.storeKey, Animate.Services.SessionPersistence.SharedStore.Store);
            s_AnimateVisualElement.SetAssetContext(asset);

            s_AnimateVisualElement.Dispatch(Animate.Services.Stores.Actions.GenerationSettingsActions.setPrompt, prompt);

            // todo: maybe generate 1 right away (model selection is ambiguous here)
        }

        [Plugin("Plugin for creating a terrain layer given a prompt.", toolName: "TerrainLayer", actionText: "Generate Terrain Layer")]
        public static void GenerateTerrainLayer(
            [Parameter("The prompt to guide what material will be generated as terrain layer")]
            string prompt)
        {
            var terrainLayer = TerrainLayerGeneratorInspectorButton.EmptyTerrainLayer();

            s_TerrainLayerVisualElement ??= new VisualElement();
            var assetPath = AssetDatabase.GetAssetPath(terrainLayer);
            var asset = new AssetReference { guid = AssetDatabase.AssetPathToGUID(assetPath) };
            Contexts.ContextExtensions.ProvideContext(s_TerrainLayerVisualElement, StoreExtensions.storeKey, Material.Services.SessionPersistence.SharedStore.Store);
            s_TerrainLayerVisualElement.SetAssetContext(asset);

            s_TerrainLayerVisualElement.Dispatch(Material.Services.Stores.Actions.GenerationSettingsActions.setPrompt, prompt);

            // todo: maybe generate 1 right away (model selection is ambiguous here)
        }
    }
}
