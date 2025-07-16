using System;
using Unity.AI.Generators.Redux;
using Unity.AI.ModelSelector.Services.Stores.Actions.Payloads;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    [UxmlElement]
    partial class ModelSelectorButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Sound/Components/ModelSelectorButton/ModelSelectorButton.uxml";

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
            this.UseStoreApi(DiscoverModels);
            this.Use(state => state.SelectShouldAutoAssignModel(this), payload =>
            {
                var (should, _) = payload;
                m_Button.SetEnabled(!should);
                m_Button.tooltip = payload.should ? "No additional model currently available" : "Choose another AI model";
                if (!should)
                    return;
                var autoAssignModel = this.GetState().SelectAutoAssignModel(this);
                if (!string.IsNullOrEmpty(autoAssignModel?.id))
                    this.Dispatch(GenerationSettingsActions.setSelectedModelID, autoAssignModel.id);
            });
        }

        static bool s_Mutex = false;

        async static void DiscoverModels(IStoreApi store)
        {
            try
            {
                while (s_Mutex)
                    await EditorTask.Yield();
                s_Mutex = true;
                await store.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.discoverModels, new DiscoverModelsData(WebUtils.selectedEnvironment));
            }
            finally
            {
                s_Mutex = false;
            }
        }
    }
}
