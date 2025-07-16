using System;
using System.Collections.Generic;
using Unity.AI.Material.Services.Stores.Actions;
using Unity.AI.Material.Services.Stores.Selectors;
using Unity.AI.Material.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Material.Components
{
    class MaterialGenerator : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Material/Components/MaterialGenerator/MaterialGenerator.uxml";
        public MaterialGenerator()
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

            this.UseAsset(SetAsset);
            this.UseArray(state => state.SelectGenerationFeedback(this), OnGenerationFeedbackChanged);
        }

        void OnGenerationFeedbackChanged(IEnumerable<GenerationFeedbackData> messages)
        {
            foreach (var feedback in messages)
            {
                this.ShowToast(feedback.message);
                this.Dispatch(GenerationActions.removeGenerationFeedback, this.GetAsset());
            }
        }

        void SetAsset(AssetReference asset)
        {
            if (this.GetState().SelectGeneratedMaterialMappingIsNone(asset))
                this.Dispatch(GenerationResultsActions.autodetectMaterialMapping, new(this.GetAsset()));
        }
    }
}
