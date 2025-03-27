using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Creators;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class AddToPromptButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/AddToPromptButton/AddToPromptButton.uxml";

        GenericDropdownMenu m_AllowedOperatorsMenu;
        readonly Button m_AddToPrompt;

        bool m_HasItems = false;
        bool m_Once = false;
        bool m_IsDirty = false;

        public AddToPromptButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_AddToPrompt = this.Q<Button>("add-to-prompt-button");
            m_AddToPrompt.clicked += async () =>
            {
                if (m_Once)
                    return;
                m_Once = true;
                try
                {
                    while (!m_HasItems)
                        await Task.Yield();
                    m_AllowedOperatorsMenu.DropDown(m_AddToPrompt.worldBound, m_AddToPrompt, false);
                }
                finally
                {
                    m_Once = false;
                }
            };

            this.Use(state => state.SelectSelectedModelID(this), _ => MarkDirty());
            this.UseArray(state => state.SelectActiveReferencesTypes(this), _ => MarkDirty());
        }

        void MarkDirty()
        {
            m_IsDirty = true;
            m_AddToPrompt.SetEnabled(!m_IsDirty);
            m_AddToPrompt.tooltip = "Parsing reference types rules...";
            Debouncer.DebounceAction(this.GetAsset().guid, Rebuild);
        }

        void AddItemToMenu(ImageReferenceType type, bool itemVisible, bool enabled, AssetActionCreator<ImageReferenceActiveData> setImageReferenceIsActive)
        {
            if (!itemVisible)
                return;

            var text = $"{type.GetInternalDisplayNameForType()} Reference";

            m_HasItems = true;
            if (enabled)
            {
                m_AllowedOperatorsMenu.AddItem(text, false, () =>
                {
                    this.Dispatch(setImageReferenceIsActive, new ImageReferenceActiveData(type, true));
                    this.Dispatch(GenerationSettingsActions.setPendingPing, type.GetImageReferenceName());
                });
            }
            else
            {
                m_AllowedOperatorsMenu.AddDisabledItem(text, false);
            }
        }

        async void Rebuild()
        {
            if (!m_IsDirty)
                return;

            m_IsDirty = false;
            m_HasItems = false;

            var typesToValidate = new List<ImageReferenceType>();

            m_AllowedOperatorsMenu = new GenericDropdownMenu();
            foreach (var type in Enum.GetValues(typeof(ImageReferenceType)).Cast<ImageReferenceType>().OrderBy(t => t.GetDisplayOrder()))
            {
                if (type.GetRefinementModeForType().Contains(RefinementMode.Generation))
                    typesToValidate.Add(type);
            }

            var results = await GenerationResultsSuperProxyActions.canAddReferencesToPrompt((new AddImageReferenceTypeData(this.GetAsset(), typesToValidate.ToArray()), this.GetStoreApi()));
            for (var i = 0; i < typesToValidate.Count; i++)
            {
                AddItemToMenu(typesToValidate[i], true, !this.GetState().SelectImageReferenceIsActive(this, typesToValidate[i]) && results[i], GenerationSettingsActions.setImageReferenceActive);
            }

            m_AddToPrompt.SetEnabled(!m_IsDirty);
            m_AddToPrompt.tooltip = "";
        }
    }
}
