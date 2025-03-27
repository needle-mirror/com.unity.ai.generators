using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Panel
{
    [UxmlElement]
    partial class RemoveBackgroundPanel : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/Modules/Unity.AI.Image/Panels/RemoveBackgroundPanel/RemoveBackgroundPanel.uxml";
        public RemoveBackgroundPanel()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
        }
    }
}