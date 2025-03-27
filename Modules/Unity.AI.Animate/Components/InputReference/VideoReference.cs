using System;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class VideoReference : VisualElement, IInputReference
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Animate/Components/InputReference/VideoReference.uxml";

        public VideoReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("prompt-input-reference");

            this.Bind(
                GenerationSettingsActions.setVideoInputReferenceAsset,
                Selectors.SelectVideoReferenceAsset);

            var deleteInputReference = this.Q<Button>("delete-input-reference");
            deleteInputReference.clicked += () => {
                this.Dispatch(GenerationSettingsActions.setVideoInputReference, new Services.Stores.States.VideoInputReference());
            };
        }
    }
}
