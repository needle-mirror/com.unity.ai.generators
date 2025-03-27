using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    class ModelTrainer : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/ModelTrainer/ModelTrainer.uxml";

        public ModelTrainer()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);
        }
    }
}
