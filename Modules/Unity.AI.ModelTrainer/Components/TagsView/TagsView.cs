using System.Collections.Generic;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class TagsView : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/TagsView/TagsView.uxml";

        readonly TextField m_TagTextField;

        readonly Button m_DropDown;

        public override VisualElement contentContainer { get; }

        public TagsView()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);

            contentContainer = this.Q<VisualElement>("contentContainer");

            m_TagTextField = this.Q<TextField>("tagTextField");
            m_TagTextField.isDelayed = true;
            m_TagTextField.RegisterValueChangedCallback(evt =>
            {
                this.GetStoreApi().Dispatch(SessionActions.addTag.Invoke(evt.newValue));
            });

            m_DropDown = this.Q<Button>("dropDown");
            m_DropDown.clicked += () =>
            {
                var genericMenu = new GenericMenu();
                var store = this.GetStoreApi();
                var state = store.State;
                foreach (var tag in state.SelectAllTags())
                {
                    var isOn = state.HasTag(tag);
                    genericMenu.AddItem(new GUIContent(tag), isOn, () =>
                    {
                        var action = isOn ? SessionActions.deleteTag : SessionActions.addTag;
                        store.Dispatch(action.Invoke(tag));
                    });
                }
                genericMenu.DropDown(m_DropDown.worldBound);
            };

            this.Use(SessionSelectors.SelectTags, OnTagsChanged);
        }

        void OnTagsChanged(IEnumerable<string> tags)
        {
            var list = tags != null ? new List<string>(tags) : new List<string>();
            // update/rebind existing item
            for (var i = 0; i < childCount; i++)
            {
                var item = (Tag)ElementAt(i);
                UnbindItem(item, i);
                if (i < list.Count)
                    BindItem(item, list[i]);
            }

            // add new items if needed
            for (var i = childCount; i < list.Count; i++)
            {
                var item = new Tag();
                BindItem(item, list[i]);
                contentContainer.Add(item);
            }

            // remove extra items
            for (var i = childCount - 1; i >= list.Count; i--)
            {
                var item = (Tag)ElementAt(i);
                UnbindItem(item, i);
                contentContainer.Remove(item);
            }
        }

        void UnbindItem(Tag element, int index)
        {

        }

        void BindItem(Tag element, string tag)
        {
            element.text = tag;
        }
    }
}
