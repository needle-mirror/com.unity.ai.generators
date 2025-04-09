using System;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.Actions.Payloads;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using WebUtils = Unity.AI.Animate.Services.Utilities.WebUtils;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class ModelSelectorButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Animate/Components/ModelSelectorButton/ModelSelectorButton.uxml";

        [UxmlAttribute]
        public bool enabled
        {
            get => m_Enabled;
            set
            {
                m_Enabled = value;
                m_Button.SetEnabled(m_Enabled);
            }
        }

        bool m_Enabled = true;

        readonly Button m_Button;

        public ModelSelectorButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Button = this.Q<Button>();
            // ReSharper disable once AsyncVoidLambda
            m_Button.clickable = new Clickable(async () => {
                if (!m_Button.enabledSelf)
                    return;
                try
                {
                    m_Button.SetEnabled(false);
                    await this.GetStoreApi().Dispatch(GenerationSettingsActions.openSelectModelPanel, this);
                }
                finally
                {
                    m_Button.SetEnabled(m_Enabled);
                }
            });
            // ReSharper disable once AsyncVoidLambda
            this.UseStoreApi(async store =>
            {
                var success = await WebUtilities.WaitForCloudProjectSettings();
                if (!success)
                    return;
                await store.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.discoverModels, new DiscoverModelsData(WebUtils.selectedEnvironment));
                this.Dispatch(GenerationSettingsActions.setLastModelDiscoveryTime, Time.time);
            });
            this.Use(state => state.SelectShouldAutoAssignModel(this), payload =>
            {
                m_Button.SetEnabled(!payload.should);
                if (!payload.should)
                    return;
                var autoAssignModel = this.GetState().SelectAutoAssignModel(this);
                if (!string.IsNullOrEmpty(autoAssignModel?.id))
                    this.Dispatch(GenerationSettingsActions.setSelectedModelID, (payload.mode, autoAssignModel.id));
            });
        }
    }
}
