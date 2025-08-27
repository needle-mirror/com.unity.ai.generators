using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    [UxmlElement]
    partial class PatternImageReference : VisualElement, IImageReference
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/ImageReference/PatternImageReference.uxml";

        public PatternImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("pattern-image-reference");

            this.Bind(
                GenerationSettingsActions.setPatternImageReferenceAsset,
                Selectors.SelectPatternImageReferenceAsset);
            this.BindWithStrength(
                GenerationSettingsActions.setPatternImageReferenceStrength,
                Selectors.SelectPatternImageReferenceStrength);

            var deleteImageReference = this.Q<Button>("delete-image-reference");
            deleteImageReference.clicked += () => {
                this.Dispatch(GenerationSettingsActions.setPatternImageReference, new Services.Stores.States.PatternImageReference());
            };

            var objectField = this.Q<ObjectField>();
            objectField.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                    this.Dispatch(GenerationSettingsActions.setPatternImageReference, new Services.Stores.States.PatternImageReference());
            });

            var browsePatterns = this.Q<Button>("image-reference-search-button");
            browsePatterns.clicked += async () =>
            {
                var patternAsset = await PatternsSearchProvider.SelectPatternAsync(this.GetStoreApi().State.SelectPrompt(this));
                var assetReference = new AssetReference { guid = AssetDatabase.AssetPathToGUID(patternAsset) };
                if (!assetReference.IsValid())
                    return;
                this.Dispatch(GenerationSettingsActions.setPatternImageReference,
                    new Services.Stores.States.PatternImageReference { asset = assetReference });
            };
        }
    }
}
