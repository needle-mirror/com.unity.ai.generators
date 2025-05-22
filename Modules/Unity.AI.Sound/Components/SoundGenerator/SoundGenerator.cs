using System;
using System.Collections.Generic;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    class SoundGenerator : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Sound/Components/SoundGenerator/SoundGenerator.uxml";
        public SoundGenerator()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            this.Q<Splitter>("vertical-splitter").Bind(
                this,
                GenerationSettingsActions.setHistoryDrawerHeight,
                Selectors.SelectHistoryDrawerHeight);

            this.Q<Splitter>("horizontal-splitter").BindHorizontal(
                this,
                GenerationSettingsActions.setGenerationPaneWidth,
                Selectors.SelectGenerationPaneWidth);

            this.UseArray(state => state.SelectGenerationFeedback(this), OnGenerationFeedbackChanged);
        }

        void OnGenerationFeedbackChanged(IEnumerable<GenerationFeedbackData> messages)
        {
            foreach (var feedback in messages)
            {
                this.ShowToast(feedback.message);
                this.Dispatch(GenerationResultsActions.removeGenerationFeedback, this.GetAsset());
            }
        }
    }
}
