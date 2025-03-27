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
                ShowModalities();
                ShowSources();
                ShowOperationTypes();
                ShowTags();
                OnSearch();
            }
        }

        public ModelView()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            InitUI();

            this.UseArray(ModelSelectorSelectors.SelectModelSettings, OnModelsChanged);
            this.Use(state => state.SelectLastSelectedModality(), OnModalitySelected);
            this.UseArray(state => state.SelectLastSelectedOperations(), OnOperationsSelected);
            this.Use(state => state.SelectLastSelectedModelID(), OnModelSelected);
        }

        void OnModalitySelected(ModalityEnum modality)
        {
            m_SelectedModality = modality;
            ShowModalities();
            ShowSources();
            ShowOperationTypes();
            ShowTags();
            OnSearch();
        }

        void OnOperationsSelected(IEnumerable<OperationSubTypeEnum> operations)
        {
            m_SelectedOperations = operations.ToArray();
            ShowModalities();
            ShowSources();
            ShowOperationTypes();
            ShowTags();
            OnSearch();
        }

        bool IsModelBroken(ModelSettings model)
        {
            // layer inpainting is broken
            var op = m_SelectedOperations.Length == 1 && m_SelectedOperations.Contains(OperationSubTypeEnum.MaskReference);
            return op && model.provider == ProviderEnum.Layer;
        }

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
            m_GridView.selectionType = SelectionType.Single;
            m_GridView.makeItem = () =>
            {
                var tile = new ModelTile();
                tile.showModelDetails += ShowModelDetails;
                return tile;
            };
            m_GridView.bindItem = (element, i) =>
            {
                if (element is ModelTile modelOption)
                {
                    modelOption.SetModel(m_FilteredModels[i]);
                    modelOption.SetEnabled(!IsModelBroken(m_FilteredModels[i]));
                    if (!modelOption.enabledSelf)
                        modelOption.tooltip = "Temporarily not available for inpainting.";
                }
            };
            m_GridView.unbindItem = (element, _) =>
            {
                if (element is ModelTile modelOption)
                {
                    modelOption.SetModel(new ModelSettings());
                    modelOption.SetEnabled(true);
                }
            };
            m_GridView.selectedIndicesChanged += OnSelectionChanged;

            var closeButton = this.Q<Button>(className: "close-button");
            closeButton.clickable = new Clickable(OnCancelButtonPressed);

            var backButton = this.Q<Button>(className: "back-button");
            backButton.clickable = new Clickable(OnBackButtonPressed);

            m_ModelDetailsCard = this.Q<DetailsModelTitleCard>();
            m_ModelDetailsGrid = this.Q<GridView>(className: "model-details-grid");

            m_ModelDetailsGrid.MakeTileGrid(() => (float)TextureSizeHint.Carousel);
            m_ModelDetailsGrid.selectionType = SelectionType.None;
            m_ModelDetailsGrid.makeItem = () => new Image();
            // ReSharper disable once AsyncVoidLambda
            m_ModelDetailsGrid.bindItem = async (element, i) =>
            {
                if (element is Image image)
                    image.image = await TextureCache.GetPreview(new Uri((string)m_ModelDetailsGrid.itemsSource[i]), (int)TextureSizeHint.Carousel);
            };
            m_ModelDetailsGrid.unbindItem = (element, i) =>
            {
                if (element is Image image)
                    image.image = null;
            };
        }

        void OnAttachToPanel(AttachToPanelEvent _) => RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        void OnDetachFromPanel(DetachFromPanelEvent _) => UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        void OnBackButtonPressed() => m_TabView.selectedTabIndex = 0;

        void ShowModelDetails(ModelSettings model)
        {
            m_ModelDetailsCard.SetModel(model);
            m_ModelDetailsGrid.itemsSource = model.thumbnails.Skip(1).ToList();

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
                .Select(model => model.partner).Distinct().OrderBy(s => s).ToList();

            m_SourcesElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_SourcesElement.Clear();
            foreach (var source in sources)
            {
                var sourceElement = new Toggle { label = source.Replace("TextTo", ""), name = source };
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
                var tagElement = new Toggle { label = tag };
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
                var operationElement = new Toggle { label = operation.ToString().AddSpaceBeforeCapitalLetters() };
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
            m_FilteredModels = models;

            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();
            if (selectedModalities.Count > 0)
                m_FilteredModels = m_FilteredModels.Where(m => selectedModalities.Any(t => m.modality.ToString().Equals(t, StringComparison.InvariantCultureIgnoreCase))).ToList();

            var search = m_SearchBar?.value?.ToLower();
            if (!string.IsNullOrEmpty(search))
                m_FilteredModels = m_FilteredModels.Where(m => m.name.ToLower().Contains(search) ||
                    m.description.ToLower().Contains(search) ||
                    m.partner.ToLower().Contains(search) ||
                    m.tags.Any(t => t.ToLower().Contains(search))
                ).ToList();

            var selectedTags = m_TagsElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.label).ToList();

            // If we want the models that match all of the selected tags we can change this to selectedTags.All.
            if (selectedTags.Count > 0)
                m_FilteredModels = m_FilteredModels.Where(m => selectedTags.Any(t => m.tags.Contains(t))).ToList();

            var selectedOperations = m_OperationsElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.label).ToList();

            if (selectedOperations.Count > 0)
                m_FilteredModels = m_FilteredModels.Where(m => selectedOperations.Any(t => m.operations.Any(o => o.ToString().AddSpaceBeforeCapitalLetters().Equals(t, StringComparison.InvariantCultureIgnoreCase)))).ToList();

            var selectedSources = m_SourcesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

            // If we want the models that match all of the selected sources we can change this to selectedTags.All.
            if (selectedSources.Count > 0)
                m_FilteredModels = m_FilteredModels.Where(m => selectedSources.Any(t => m.partner.ToLower().Contains(t.ToLower()))).ToList();

            m_FilteredModels = m_SortOrder.value == 0
                ? m_FilteredModels.OrderBy(m => m.name).ToList()
                : m_FilteredModels.OrderByDescending(m => m.name).ToList();

            m_GridView.ClearSelection();
            m_GridView.itemsSource = m_FilteredModels;
            if (m_FilteredModels.Count > 0 && m_SelectedModelId != null)
                m_GridView.SetSelectionWithoutNotify(new []{ m_FilteredModels.FindIndex(m => m.id == m_SelectedModelId) });
            m_GridView.EnableInClassList("hide", m_FilteredModels.Count == 0);
            m_SearchResultsText.EnableInClassList("hide", m_FilteredModels.Count > 0);
        }

        void OnSelectionChanged(IEnumerable<int> indices)
        {
            using var enumerator = indices.GetEnumerator();
            if (!enumerator.MoveNext())
                return;

            var index = enumerator.Current;
            var model = m_FilteredModels[index];

            SelectModel(model);
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
    }
}
