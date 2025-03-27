using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Panel
{
    [UxmlElement]
    partial class RecolorPanel : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/Modules/Unity.AI.Image/Panels/RecolorPanel/RecolorPanel.uxml";

        public RecolorPanel()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
        }
    }
}
