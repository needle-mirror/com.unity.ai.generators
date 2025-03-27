using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI
{
    [UxmlElement]
    partial class Splitter : VisualElement, INotifyValueChanged<float>
    {
        float m_Value;

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Generators.UI/Components/Splitter/Splitter.uxml";

        const string k_DraggingUssClassName = "aitk-splitter--dragging";

        public VisualElement topPane { get; set; }

        public VisualElement bottomPane { get; set; }

        public VisualElement paneContainer { get; set; }

        public Splitter()
        {
            pickingMode = PickingMode.Ignore;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var zone = this.Q<VisualElement>("zone");
            zone.AddManipulator(new Draggable(OnDragStart, OnDrag, OnDragEnd));
        }

        public void Reset() => value = 0;

        public void Refresh() => SetValueWithoutNotify(value);

        public void SetValueWithoutNotify(float newValue)
        {
            if (topPane == null || bottomPane == null || paneContainer == null)
                return;

            var topMinSize = topPane.resolvedStyle.minHeight.value;
            var bottomMinSize = bottomPane.resolvedStyle.minHeight.value;
            var totalSize = paneContainer.layout.height;
            var isBottomVisible = bottomPane.resolvedStyle.display == DisplayStyle.Flex;

            if (isBottomVisible)
            {
                newValue = Mathf.Max(bottomMinSize, newValue);
                newValue = Mathf.Min(totalSize - topMinSize, newValue);
                bottomPane.style.height = newValue;
                m_Value = newValue;
                topPane.style.height = totalSize - newValue;
            }
            else
            {
                topPane.style.height = new Length(100, LengthUnit.Percent);
            }
        }

        public float value
        {
            get => m_Value; // store a value instead of returning directly a resolved style because it is not up-to-date
            set
            {
                var previousValue = this.value;
                SetValueWithoutNotify(value);
                using var evt = ChangeEvent<float>.GetPooled(previousValue, this.value);
                evt.target = this;
                SendEvent(evt);
            }
        }

        void OnDragStart() => AddToClassList(k_DraggingUssClassName);

        void OnDragEnd() => RemoveFromClassList(k_DraggingUssClassName);

        void OnDrag(Vector3 delta) => value -= delta.y;
    }
}
