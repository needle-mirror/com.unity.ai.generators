using System;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class ThumbnailFieldItem : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/ThumbnailFieldItem/ThumbnailFieldItem.uxml";

        readonly Label m_Text;

        readonly Button m_RemoveButton;

        readonly VisualElement m_Thumbnail;

        [UxmlAttribute]
        public string text
        {
            get => m_Text.text;
            set => m_Text.text = value;
        }

        public bool isDeletable
        {
            get => ClassListContains("is-removable");
            set => EnableInClassList("is-removable", value);
        }

        public Texture2D thumbnail
        {
            get => m_Thumbnail.resolvedStyle.backgroundImage.texture;
            set => m_Thumbnail.style.backgroundImage = value;
        }

        public ThumbnailFieldItem()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);

            m_Text = this.Q<Label>("label");
            m_RemoveButton = this.Q<Button>("removeButton");
            m_Thumbnail = this.Q<VisualElement>("thumbnail");

            m_RemoveButton.clicked += OnRemoveButtonClicked;
        }

        void OnRemoveButtonClicked()
        {
            if (userData is string id)
                this.GetStoreApi().Dispatch(SessionActions.deleteImage.Invoke(id));
        }
    }
}
