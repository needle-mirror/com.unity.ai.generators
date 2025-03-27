﻿using System;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Generators.Contexts;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Services.Utilities
{
    static class AssetContextExtensions
    {
        /// <summary>
        /// Get the Asset from an element
        /// </summary>
        public static AssetReference GetAsset(this VisualElement element) => element.GetContext<AssetReference>(Unity.AI.Generators.Asset.AssetContextExtensions.assetKey);

        public static Session GetSession(this VisualElement element) => element.GetState().SelectSession();

        public static GenerationSetting GetGenerationSetting(this VisualElement element) => element.GetState().SelectGenerationSetting(element);

        public static GenerationResult GenerationResult(this VisualElement element) => element.GetState().SelectGenerationResult(element);

        public static void UseAsset(this VisualElement element, Action<AssetReference> callback) => element.UseContext(Unity.AI.Generators.Asset.AssetContextExtensions.assetKey, callback);
    }
}
