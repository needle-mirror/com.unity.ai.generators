using System.Collections.Generic;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.ModelTrainer.Components
{
    [UxmlElement]
    partial class ThumbnailField : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/ThumbnailField/ThumbnailField.uxml";

        readonly ThumbnailFieldItem m_AddButton;

        readonly VisualElement m_ContentContainer;

        public override VisualElement contentContainer { get; }

        public ThumbnailField()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);

            contentContainer = this.Q<VisualElement>("thumbnailItemsContainer");

            m_AddButton = this.Q<ThumbnailFieldItem>("addThumbnailButton");
            m_AddButton.AddManipulator(new Clickable(() =>
            {
                this.GetStoreApi().Dispatch(SessionActions.addImage.Invoke());
            }));

            this.Use(SessionSelectors.SelectImages, OnImagesChanged);
        }

        void OnImagesChanged(IEnumerable<TrainingImageReference> images)
        {
            var list = images != null ? new List<TrainingImageReference>(images) : new List<TrainingImageReference>();
            // update/rebind existing item
            for (var i = 0; i < childCount; i++)
            {
                var item = (ThumbnailFieldItem)ElementAt(i);
                UnbindItem(item, i);
                if (i < list.Count)
                    BindItem(item, list[i]);
            }

            // add new items if needed
            for (var i = childCount; i < list.Count; i++)
            {
                var item = new ThumbnailFieldItem
                {
                    isDeletable = true
                };
                BindItem(item, list[i]);
                Add(item);
            }

            // remove extra items
            while (childCount > list.Count)
            {
                RemoveAt(childCount - 1);
            }
        }

        void BindItem(ThumbnailFieldItem element, TrainingImageReference image)
        {
            element.text = image.prompt;
            element.thumbnail = image.texture;
            element.userData = image.id;
        }

        void UnbindItem(ThumbnailFieldItem element, int index)
        {

        }
    }
}
