using System;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class Prompt : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/Prompt/Prompt.uxml";

        public Prompt()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var promptText = this.Q<TextField>("prompt");
            var negativePromptText = this.Q<TextField>("negative-prompt");
            var promptLimitIndicator = this.Q<Label>("prompt-limit-indicator");
            var negativePromptLimitIndicator = this.Q<Label>("negative-prompt-limit-indicator");

            promptText.RegisterValueChangedCallback(evt =>
            {
                var truncatedPrompt = PromptUtilities.TruncatePrompt(evt.newValue);
                promptText.SetValueWithoutNotify(truncatedPrompt);
                this.Dispatch(GenerationSettingsActions.setPrompt, truncatedPrompt);
            });
            negativePromptText.RegisterValueChangedCallback(evt =>
            {
                var truncatedPrompt = PromptUtilities.TruncatePrompt(evt.newValue);
                negativePromptText.SetValueWithoutNotify(truncatedPrompt);
                this.Dispatch(GenerationSettingsActions.setNegativePrompt, truncatedPrompt);
            });

            promptText.RegisterTabEvent();
            negativePromptText.RegisterTabEvent();

            this.Use(state => state.SelectPrompt(this), prompt =>
            {
                promptText.value = prompt;
                promptLimitIndicator.text = $"{prompt.Length}/{PromptUtilities.maxPromptLength}";
            });
            this.Use(state => state.SelectNegativePrompt(this), negativePrompt =>
            {
                negativePromptText.value = negativePrompt;
                negativePromptLimitIndicator.text = $"{negativePrompt.Length}/{PromptUtilities.maxPromptLength}";
            });
        }
    }
}
