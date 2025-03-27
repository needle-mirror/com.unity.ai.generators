using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Panel
{
    [UxmlElement]
    partial class InpaintingPanel : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/Modules/Unity.AI.Image/Panels/InpaintingPanel/InpaintingPanel.uxml";
        public InpaintingPanel()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
        }
    }
}