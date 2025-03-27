using System;
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
    partial class ModelListView : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelTrainer/Components/ModelListView/ModelListView.uxml";

        readonly TextField m_SearchField;

        readonly ListView m_ListView;

        public ModelListView()
        {
            var uxmlTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            uxmlTemplate.CloneTree(this);

            var addModelButton = this.Q<VisualElement>("addItem");
            addModelButton.AddManipulator(new Clickable(() => this.GetStoreApi().Dispatch(SessionActions.addModel.Invoke())));

            m_ListView = this.Q<ListView>("modelListView");
            m_ListView.selectedIndicesChanged += OnSelectedIndicesChanged;
            m_ListView.bindItem = (element, i) =>
            {
                var model = (UserModel) m_ListView.itemsSource[i];
                var baseModel = this.GetState().SelectBaseModel(model.baseModelId);
                var trained = model.trainingStatus switch
                {
                    TrainingStatus.NotStarted => "Untrained",
                    TrainingStatus.InProgress => "Training",
                    TrainingStatus.Failed => "Failed",
                    TrainingStatus.Succeeded => "Trained",
                    _ => throw new ArgumentOutOfRangeException()
                };
                element.Q<Label>("label").text = model.name;
                element.Q<Label>("tags").text = $"{baseModel.name}, {trained}";
                element.Q<VisualElement>("thumbnail").style.backgroundImage = model.thumbnailTexture;
            };

            m_SearchField = this.Q<TextField>("searchField");
            m_SearchField.RegisterValueChangedCallback(evt => this.GetStoreApi().Dispatch(
                SessionActions.setSearchFilter.Invoke(evt.newValue)));

            this.Use(SessionSelectors.SelectSearchFilter, OnSearchFilterChanged);
            this.Use(SessionSelectors.SelectFilteredModels, OnModelsChanged);
            this.Use(SessionSelectors.SelectSelectedModel, OnSelectedModelChanged);
        }

        void OnSelectedModelChanged(UserModel model)
        {
            var index = m_ListView.itemsSource.IndexOf(model);
            m_ListView.SetSelectionWithoutNotify(new []{ index });
        }

        void OnModelsChanged(IEnumerable<UserModel> models)
        {
            var list = new List<UserModel>(models);
            m_ListView.SetSelectionWithoutNotify(new int[] {});

            // if we provide a new source everytime, it will by costly to update the listview
            if (m_ListView.itemsSource == null)
                m_ListView.itemsSource = list;
            // check if we can just update the listview
            else
            {
                var source = (List<UserModel>) m_ListView.itemsSource;
                source.Clear();
                source.AddRange(list);
                m_ListView.RefreshItems();
            }

            var selectedModel = this.GetStoreApi().State.SelectSelectedModel();
            var index = list.IndexOf(selectedModel);
            m_ListView.SetSelectionWithoutNotify(new []{ index });
        }

        void OnSelectedIndicesChanged(IEnumerable<int> _)
        {
            var id = m_ListView.selectedItem is UserModel model ? model.id : null;
            var action = SessionActions.selectModel.Invoke(id);
            this.GetStoreApi().Dispatch(action);
        }

        void OnSearchFilterChanged(string searchFilter)
        {
            m_SearchField.SetValueWithoutNotify(searchFilter);
        }
    }
}
