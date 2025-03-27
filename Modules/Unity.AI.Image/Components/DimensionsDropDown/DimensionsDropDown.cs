using System;
using System.Collections.Generic;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class DimensionsDropDown : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/DimensionsDropDown/DimensionsDropDown.uxml";

        DropdownField m_DimensionsDropdown;

        public DimensionsDropDown()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_DimensionsDropdown = this.Q<DropdownField>("dimensions-dropdown");
            m_DimensionsDropdown.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setImageDimensions, evt.newValue));

            this.UseArray(state => state.SelectModelSettingsResolutions(this), OnPartnerResolutionsChanged);
            this.Use(state => state.SelectImageDimensions(this), OnImageDimensionsChanged);
        }

        void OnImageDimensionsChanged(string dimensions) => m_DimensionsDropdown.value = dimensions;

        void OnPartnerResolutionsChanged(List<string> resolutions) => m_DimensionsDropdown.choices = resolutions ?? new List<string>();
    }
}
