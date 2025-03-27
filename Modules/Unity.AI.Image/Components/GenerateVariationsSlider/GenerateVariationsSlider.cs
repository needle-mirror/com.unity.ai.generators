﻿using System;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class GenerateVariationsSlider : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Image/Components/GenerateVariationsSlider/GenerateVariationsSlider.uxml";

        readonly SliderInt m_Slider;
        readonly Label m_Label;

        [UxmlAttribute]
        public string label
        {
            get => m_Label == null ? "Images" : m_Label.text;
            set => m_Label.text = value;
        }

        public GenerateVariationsSlider()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Label = this.Q<Label>();

            m_Slider = this.Q<SliderInt>();
            m_Slider.RegisterValueChangedCallback(evt => this.Dispatch(GenerationSettingsActions.setVariationCount, evt.newValue));

            this.Use(state => state.SelectVariationCount(this), OnVariationCountChanged);
        }

        void OnVariationCountChanged(int variationCount)
        {
            m_Slider.SetValueWithoutNotify(variationCount);
        }
    }
}
