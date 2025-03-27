using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class PreviewSizeSlider : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Animate/Components/PreviewSizeSlider/PreviewSizeSlider.uxml";

        readonly Slider m_Slider;

        public PreviewSizeSlider()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("preview-size-slider");

            m_Slider = this.Q<Slider>();
            m_Slider.RegisterValueChangedCallback(evt => this.Dispatch(SessionActions.setPreviewSizeFactor, evt.newValue));

            this.Use(state => state.SelectPreviewSizeFactor(), sizeFactor => m_Slider.SetValueWithoutNotify(sizeFactor));
        }
    }
}
