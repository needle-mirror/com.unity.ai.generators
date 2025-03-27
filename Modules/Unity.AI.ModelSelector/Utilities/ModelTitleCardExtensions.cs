using System;
using System.Threading.Tasks;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelSelector.Services.Utilities
{
    interface IModelTitleCard {}

    static class ModelTitleCardExtensions
    {
        public static async Task SetModelAsync<T>(this T card, ModelSettings model) where T: VisualElement, IModelTitleCard
        {
            var modelImage = card.Q<Image>(className: "model-title-card-image");
            var modelName = card.Q<Label>(className: "model-title-card-label");
            var modelTags = card.Q<Label>(className: "model-title-card-tags");
            var modelDescription = card.Q<Label>(className: "model-title-card-description");

            modelName.text = model.name;
            modelTags.text = model.tags != null ? string.Join(", ", model.tags) : string.Empty;

            if (modelDescription != null)
                modelDescription.text = model.description;

            if (model.thumbnails is { Count: > 0 })
                modelImage.image = await TextureCache.GetPreview(new Uri(model.thumbnails[0]), (int)TextureSizeHint.Carousel);
            else
                modelImage.image = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.ai.generators/Modules/Unity.AI.Generators.UI/Icons/Warning.png");
        }

        public static void SetModel<T>(this T card, ModelSettings model) where T : VisualElement, IModelTitleCard
        {
#pragma warning disable CS4014
            card.SetModelAsync(model);
#pragma warning restore CS4014
        }
    }
}
