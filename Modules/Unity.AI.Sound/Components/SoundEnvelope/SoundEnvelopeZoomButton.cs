using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    [UxmlElement]
    partial class SoundEnvelopeZoomButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.Sound/Components/SoundEnvelope/SoundEnvelopeZoomButton.uxml";

        GenericDropdownMenu m_Menu;
        bool m_MenuHasDisabledItemsOnly;
        readonly Button m_ZoomButton;

        public const float zoomFactor = 1.2f;
        float m_ZoomValue = 1;
        public Action<float> onZoomChanged;

        string ZoomButtonLabelFormat => $"{1 / zoomValue:0%}";

        public float zoomValue => m_ZoomValue;

        public SoundEnvelopeZoomButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_ZoomButton = this.Q<Button>(classes: "zoom-button");

            ;

            m_Menu = new GenericDropdownMenu();
            m_Menu.AddItem("Zoom in", false, () => ZoomOption(0));
            m_Menu.AddItem("Zoom out", false, () => ZoomOption(1));
            m_Menu.AddItem("Zoom to 50%", false, () => ZoomOption(2));
            m_Menu.AddItem("Zoom to 100%", false, () => ZoomOption(3));
            m_Menu.AddItem("Zoom to 200%", false, () => ZoomOption(4));
            m_Menu.AddItem("Zoom to 400%", false, () => ZoomOption(5));

            m_ZoomButton.clicked += () => m_Menu.DropDown(m_ZoomButton.worldBound, m_ZoomButton, false);
            return;

            void ZoomOption(int index)
            {
                switch (index)
                {
                    case 0:
                        ZoomIncrement(-zoomFactor);
                        break;
                    case 1:
                        ZoomIncrement(zoomFactor);
                        break;
                    case 2:
                        SetZoom(2);
                        break;
                    case 3:
                        SetZoom(1);
                        break;
                    case 4:
                        SetZoom(0.5f);
                        break;
                    case 5:
                        SetZoom(0.25f);
                        break;
                }

                m_ZoomButton.text = ZoomButtonLabelFormat;
            }
        }

        public void ZoomIncrement(float factor) => SetZoom(factor > 0 ? zoomValue * factor : zoomValue / -factor);

        public void SetZoom(float zoomValue)
        {
            m_ZoomValue = Mathf.Clamp(zoomValue, 1 / 128f, 8f);
            m_ZoomButton.text = ZoomButtonLabelFormat;
            onZoomChanged?.Invoke(this.zoomValue);
        }
    }
}
