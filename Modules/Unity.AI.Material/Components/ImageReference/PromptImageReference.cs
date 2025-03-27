using System;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class PromptImageReference : VisualElement, IImageReference
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/ImageReference/PromptImageReference.uxml";

        public Image image { get; set; }

        public PromptImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("prompt-image-reference");

            image = this.Q<Image>();

            this.Bind(
                GenerationSettingsActions.setPromptImageReferenceAsset,
                Selectors.SelectPromptImageReferenceAsset);

            var deleteImageReference = this.Q<Button>("delete-image-reference");
            deleteImageReference.clicked += () => {
                this.Dispatch(GenerationSettingsActions.setPromptImageReference, new Services.Stores.States.PromptImageReference());
            };

            this.Use(state => state.SelectPromptImageReferenceBackground(this)?.GetInstanceID() ?? -1, UpdateImage);
        }

        void UpdateImage(int _) => image.style.backgroundImage = this.GetState().SelectPromptImageReferenceBackground(this);
    }
}
