using System;
using System.Threading.Tasks;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Services.Utilities
{
    interface IModelTitleCard {}

    static class ModelTitleCardExtensions
    {
        public static async Task SetModelAsync<T>(this T card, ModelSettings model) where T: VisualElement, IModelTitleCard
        {
            var modelImage = card.Q<UnityEngine.UIElements.Image>(className: "model-title-card-image");
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
                modelImage.image = null;
        }

        public static void SetModel<T>(this T card, ModelSettings model) where T : VisualElement, IModelTitleCard
        {
#pragma warning disable CS4014
            card.SetModelAsync(model);
#pragma warning restore CS4014
        }
    }
}
