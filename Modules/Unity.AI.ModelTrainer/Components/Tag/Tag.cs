using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class Tag : VisualElement
    {
        const string k_Uxml =
            "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/Tag/Tag.uxml";

        readonly Label m_Label;

        readonly VisualElement m_RemoveButton;

        [UxmlAttribute]
        public string text
        {
            get => m_Label.text;
            set => m_Label.text = value;
        }

        public Tag()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);

            m_Label = this.Q<Label>("label");
            m_RemoveButton = this.Q<VisualElement>("removeButton");
            m_RemoveButton.AddManipulator(new Clickable(() =>
            {
                this.GetStoreApi().Dispatch(SessionActions.deleteTag.Invoke(text));
            }));
        }
    }
}
