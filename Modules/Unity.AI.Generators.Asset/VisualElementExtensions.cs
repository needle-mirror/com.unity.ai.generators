using System;
using System.ComponentModel;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.Asset
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class VisualElementExtensions
    {
        public static void SetAssetContext(this VisualElement ve, AssetReference value)
        {
            ve?.ProvideContext(AssetContextExtensions.assetKey, value);
            ve?.SetStoreApi(EditorWindowExtensions.AssetContextMiddleware(value));
        }

        internal static AssetReference GetAssetContext(this VisualElement ve) => ve?.GetContext<AssetReference>(AssetContextExtensions.assetKey);
    }
}
