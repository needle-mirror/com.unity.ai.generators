using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.ModelSelector.Components
{
    /// <summary>
    /// Model view for selecting the AI model
    /// </summary>
    class ModelView : VisualElement
    {
        public event Action onDismissRequested;

        string m_SelectedModelId;
        TabView m_TabView;
        VisualElement m_TagsElement;
        VisualElement m_TagsContainer;
        VisualElement m_SourcesElement;
        VisualElement m_SourcesContainer;
        VisualElement m_ModalitiesElement;
        VisualElement m_ModalitiesContainer;
        RadioButtonGroup m_SortOrder;
        VisualElement m_OperationsElement;
        VisualElement m_OperationsContainer;
        TextField m_SearchBar;
        GridView m_GridView;
        VisualElement m_SearchResultsText;
        List<ModelSettings> m_Models = new();
        List<ModelSettings> m_FilteredModels = new();
        IVisualElementScheduledItem m_DelayedSearch;
        DetailsModelTitleCard m_ModelDetailsCard;
        GridView m_ModelDetailsGrid;
        ModalityEnum m_SelectedModality = ModalityEnum.None;
        OperationSubTypeEnum[] m_SelectedOperations = Array.Empty<OperationSubTypeEnum>();

        const string k_Uxml = "Packages/com.unity.ai.generators/modules/Unity.AI.ModelSelector/Components/ModelSelector/ModelView.uxml";

        public List<ModelSettings> models
        {
            get => m_Models;
            set
            {
                m_Models = value?.OrderBy(model => model.name).ToList() ?? new List<ModelSettings>();
                m_FilteredModels = models;
                RefreshFilters();
                OnSearch();
            }
        }

        readonly List<ModelTile> m_TilePool = new();

        public ModelView()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            InitUI();
            OnSearch();

            this.UseArray(ModelSelectorSelectors.SelectModelSettings, OnModelsChanged);
            this.Use(state => state.SelectLastSelectedModality(), OnModalitySelected);
            this.UseArray(state => state.SelectLastSelectedOperations(), OnOperationsSelected);
            this.Use(state => state.SelectLastSelectedModelID(), OnModelSelected);
        }

        void OnModalitySelected(ModalityEnum modality)
        {
            m_SelectedModality = modality;
            RefreshFilters();
            OnSearch();
        }

        void OnOperationsSelected(IEnumerable<OperationSubTypeEnum> operations)
        {
            m_SelectedOperations = operations.ToArray();
            RefreshFilters();
            OnSearch();
        }

        void RefreshFilters()
        {
            ShowModalities();
            ShowSources();
            ShowOperationTypes();
            ShowTags();
        }

        bool IsModelBroken(ModelSettings model) => false;

        void OnModelSelected(string modelID) => m_SelectedModelId = modelID;

        void OnModelsChanged(List<ModelSettings> data) => models = data;

        void OnCancelButtonPressed() => onDismissRequested?.Invoke();

        void InitUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_TabView = this.Q<TabView>();
            m_TagsElement = this.Q<VisualElement>(className: "tags");
            m_TagsContainer = this.Q<VisualElement>(className: "tags-container");
            m_SourcesElement = this.Q<VisualElement>(className: "sources");
            m_SourcesContainer = this.Q<VisualElement>(className: "sources-container");
            m_ModalitiesElement = this.Q<VisualElement>(className: "modalities");
            m_ModalitiesContainer = this.Q<VisualElement>(className: "modalities-container");
            m_SortOrder = this.Q<RadioButtonGroup>(className: "sort-radio-group");
            m_SortOrder.RegisterValueChangedCallback(OnSortValueChanged);
            m_OperationsElement = this.Q<VisualElement>(className: "operations");
            m_OperationsContainer = this.Q<VisualElement>(className: "operations-container");
            m_SearchBar = this.Q<TextField>(className: "searchbar-text-field");
            m_SearchBar.RegisterValueChangedCallback(OnSearchFieldChanging);
            m_SearchResultsText = this.Q<Label>(className: "no-search-results-text");

            m_GridView = this.Q<GridView>(className: "models-section-grid");
            m_GridView.keepScrollPositionWhenHidden = true;
            m_GridView.selectedIndicesChanged += indices =>
            {
                using var enumerator = indices.GetEnumerator();
                if (!enumerator.MoveNext()) return;

                var index = enumerator.Current;
                var model = m_GridView.itemsSource[index] as ModelSettings;

                SelectModel(model);
            };

            var closeButton = this.Q<Button>(className: "close-button");
            closeButton.clickable = new Clickable(OnCancelButtonPressed);

            var backButton = this.Q<Button>(className: "back-button");
            backButton.clickable = new Clickable(OnBackButtonPressed);

            m_ModelDetailsCard = this.Q<DetailsModelTitleCard>();
            m_ModelDetailsGrid = this.Q<GridView>(className: "model-details-grid");
        }

        void OnAttachToPanel(AttachToPanelEvent _) => RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        void OnDetachFromPanel(DetachFromPanelEvent _) => UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        void OnBackButtonPressed() => m_TabView.selectedTabIndex = 0;

        void ShowModelDetails(ModelSettings model)
        {
            m_ModelDetailsCard.SetModel(model);

            var thumbnails = model.thumbnails.Skip(1).ToList();
            m_ModelDetailsGrid.BindToModelDetails(thumbnails);

            var selectButton = m_ModelDetailsCard.Q<Button>(className: "model-title-card-select");
            selectButton.clickable = new Clickable(() => SelectModel(model));

            m_TabView.selectedTabIndex = 1;
        }

        void ShowModalities()
        {
            m_ModalitiesElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_ModalitiesElement.Clear();
            foreach (ModalityEnum modality in Enum.GetValues(typeof(ModalityEnum)))
            {
                var modalityElement = new Toggle { label = modality.GetModalityName(), name = modality.ToString() };
                modalityElement.AddToClassList("tag");
                if (modality == m_SelectedModality)
                {
                    modalityElement.SetValueWithoutNotify(true);
                    modalityElement.SetEnabled(false);
                }
                else
                {
                    modalityElement.EnableInClassList("hide", true);
                }
                modalityElement.RegisterValueChangedCallback(OnModalityFilterSelectionChanged);
                m_ModalitiesElement.Add(modalityElement);
            }

            m_ModalitiesContainer.EnableInClassList("hide", true);
        }

        void ShowSources()
        {
            // Display only sources from selected modalities
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

            if (selectedModalities.Count <= 0)
            {
                m_SourcesElement.AddToClassList("hide");
                return;
            }

            var sources = m_Models.Where(m => selectedModalities.Contains(m.modality.ToString(), StringComparer.InvariantCultureIgnoreCase))
                .Select(model => model.provider.ToString()).Distinct().OrderBy(s => s).ToList();

            m_SourcesElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_SourcesElement.Clear();
            foreach (var source in sources)
            {
                var sourceElement = new Toggle { label = source, name = source };
                sourceElement.AddToClassList("tag");
                sourceElement.RegisterValueChangedCallback(OnFilterSelectionChanged);
                m_SourcesElement.Add(sourceElement);
            }

            m_SourcesContainer.EnableInClassList("hide", !Unsupported.IsDeveloperMode());
        }

        void ShowTags()
        {
            // Display only tags from selected modalities
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

            if (selectedModalities.Count <= 0)
            {
                m_TagsElement.AddToClassList("hide");
                return;
            }

            var tags = m_Models.Where(m => selectedModalities.Contains(m.modality.ToString(), StringComparer.InvariantCultureIgnoreCase))
                .SelectMany(model => model.tags).Distinct().OrderBy(s => s).ToList();

            m_TagsElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_TagsElement.Clear();
            foreach (var tag in tags)
            {
                var tagElement = new Toggle { name = tag, label = tag };
                tagElement.AddToClassList("tag");
                tagElement.RegisterValueChangedCallback(OnFilterSelectionChanged);
                m_TagsElement.Add(tagElement);
            }

            m_TagsContainer.EnableInClassList("hide", !tags.Any());
        }

        void ShowOperationTypes()
        {
            m_OperationsElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_OperationsElement.Clear();
            foreach (OperationSubTypeEnum operation in Enum.GetValues(typeof(OperationSubTypeEnum)))
            {
                var operationElement = new Toggle { name = operation.ToString(), label = operation.ToString().AddSpaceBeforeCapitalLetters() };
                if (m_SelectedOperations.Contains(operation))
                {
                    operationElement.SetValueWithoutNotify(true);
                    operationElement.SetEnabled(false);
                }
                else
                {
                    operationElement.EnableInClassList("hide", true);
                }
                operationElement.AddToClassList("operation");
                operationElement.RegisterValueChangedCallback(OnFilterSelectionChanged);
                m_OperationsElement.Add(operationElement);
            }

            m_OperationsContainer.EnableInClassList("hide", !Unsupported.IsDeveloperMode());
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
                return;
            evt.StopPropagation();
            OnCancelButtonPressed();
        }

        void OnModalityFilterSelectionChanged(ChangeEvent<bool> evt)
        {
            ShowSources();
            ShowOperationTypes();
            ShowTags();
            OnFilterSelectionChanged(evt);
        }

        void OnFilterSelectionChanged(ChangeEvent<bool> evt) => OnSearch();

        void OnSearchFieldChanging(ChangeEvent<string> evt)
        {
            m_DelayedSearch?.Pause();
            m_DelayedSearch = schedule.Execute(OnSearch);
            m_DelayedSearch.ExecuteLater(500L);
        }

        void OnSortValueChanged(ChangeEvent<int> evt) => OnSearch();

        void OnSearch()
        {
            m_FilteredModels = ApplyFilters(models);

            m_GridView.BindToModels(
                m_TilePool,
                m_FilteredModels,
                IsModelBroken,
                ShowModelDetails,
                m_SelectedModelId
            );

            m_GridView.EnableInClassList("hide", m_FilteredModels.Count == 0);
            m_SearchResultsText.EnableInClassList("hide", m_FilteredModels.Count > 0);
        }

        void SelectModel(ModelSettings model)
        {
            if (!model.IsValid())
                return;

            if (IsModelBroken(model))
                return;

            this.Dispatch(ModelSelectorActions.setLastSelectedModelID, model.id);

            // Hold the modal open for a brief duration to show the change in model selection
            schedule.Execute(OnCancelButtonPressed).ExecuteLater(250);
        }

        List<ModelSettings> ApplyFilters(List<ModelSettings> filteredModels)
        {
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value)
                .Select(t => (ModalityEnum)Enum.Parse(typeof(ModalityEnum), t.name))
                .ToList();

            var searchText = m_SearchBar?.value;

            var selectedTags = m_TagsElement.Query<Toggle>().ToList()
                .Where(t => t.value)
                .Select(t => t.name)
                .ToList();

            var selectedOperations = m_OperationsElement.Query<Toggle>().ToList()
                .Where(t => t.value)
                .Select(t => (OperationSubTypeEnum)Enum.Parse(typeof(OperationSubTypeEnum), t.name))
                .ToList();

            var selectedSources = m_SourcesElement.Query<Toggle>().ToList()
                .Where(t => t.value)
                .Select(t => (ProviderEnum)Enum.Parse(typeof(ProviderEnum), t.name))
                .ToList();

            var sortDescending = m_SortOrder.value == 1;

            return ModelSelectorSelectors.SelectModels(filteredModels, selectedModalities, selectedOperations, selectedSources, selectedTags, searchText, sortDescending);
        }
    }
}
