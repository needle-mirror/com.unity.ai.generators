using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.Redux;
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
        VisualElement m_ModelsElement;
        VisualElement m_ModelsContainer;
        VisualElement m_BaseModelsElement;
        VisualElement m_BaseModelsContainer;
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
        ModalityEnum[] m_SelectedModalities = Array.Empty<ModalityEnum>();
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

            this.UseArray(ModelSelectorSelectors.SelectModelSettings, OnModelsChanged);
            this.UseArray(state => state.SelectLastSelectedModalities(), OnModalitiesSelected);
            this.UseArray(state => state.SelectLastSelectedOperations(), OnOperationsSelected);
            this.Use(state => state.SelectLastSelectedModelID(), OnModelSelected);
            this.UseStore(OnStoreReady);
        }

        void OnStoreReady(Store store)
        {
            if (store == null)
                return;

            OnSearch();
        }

        void OnModalitiesSelected(IEnumerable<ModalityEnum> modalities)
        {
            m_SelectedModalities = modalities.ToArray();
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
            ShowMisc();
            ShowBaseModels();
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
            m_ModelsElement = this.Q<VisualElement>(className: "misc");
            m_ModelsContainer = this.Q<VisualElement>(className: "misc-container");
            m_BaseModelsElement = this.Q<VisualElement>(className: "base-models");
            m_BaseModelsContainer = this.Q<VisualElement>(className: "base-models-container");
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
                if (m_SelectedModalities.Contains(modality))
                {
                    modalityElement.SetValueWithoutNotify(true);
                    modalityElement.SetEnabled(false);
                }
                else
                {
                    modalityElement.SetShown(false);
                }
                modalityElement.RegisterValueChangedCallback(OnModalityFilterSelectionChanged);
                m_ModalitiesElement.Add(modalityElement);
            }

            m_ModalitiesContainer.SetShown(Unsupported.IsDeveloperMode());
        }

        void ShowSources()
        {
            // Display only sources from selected modalities
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

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

            m_SourcesContainer.SetShown(Unsupported.IsDeveloperMode());
        }

        void ShowTags()
        {
            // Display only tags from selected modalities
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

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

            m_TagsContainer.SetShown(tags.Any());
        }

        void ShowMisc()
        {
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

            m_ModelsElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_ModelsElement.Clear();

            var availableMisc = new HashSet<MiscModelEnum>();
            var baseModels = new HashSet<string>();
            foreach (var model in m_Models)
            {
                if (!selectedModalities.Contains(model.modality.ToString(), StringComparer.InvariantCultureIgnoreCase))
                    continue;

                if (model.operations.Contains(OperationSubTypeEnum.StyleTraining))
                    baseModels.Add(model.id);
            }
            foreach (var model in m_Models)
            {
                if (!selectedModalities.Contains(model.modality.ToString(), StringComparer.InvariantCultureIgnoreCase))
                    continue;

                availableMisc.Add(!string.IsNullOrEmpty(model.baseModelId) && baseModels.Contains(model.baseModelId) ? MiscModelEnum.Custom : MiscModelEnum.Default);
                if (model.isFavorite)
                    availableMisc.Add(MiscModelEnum.Favorites);
            }
            foreach (var misc in Enum.GetValues(typeof(MiscModelEnum)))
            {
                var miscElement = new Toggle { name = misc.ToString(), label = misc.ToString().AddSpaceBeforeCapitalLetters() };
                miscElement.SetEnabled(availableMisc.Contains((MiscModelEnum)misc));
                if (!miscElement.enabledSelf)
                    miscElement.tooltip = "There is no model available for this filter.";
                miscElement.RegisterValueChangedCallback(OnFilterSelectionChanged);
                m_ModelsElement.Add(miscElement);
            }
        }

        void ShowBaseModels()
        {
            var selectedModalities = m_ModalitiesElement.Query<Toggle>().ToList()
                .Where(t => t.value).Select(t => t.name).ToList();

            m_BaseModelsElement.Query<Toggle>().ForEach(t => t.UnregisterValueChangedCallback(OnFilterSelectionChanged));
            m_BaseModelsElement.Clear();

            var availableBaseModels = new HashSet<string>();

            // Base Models work only for Image Modality (StyleTraining Operation)
            if (selectedModalities.Contains(nameof(ModalityEnum.Image), StringComparer.InvariantCultureIgnoreCase))
            {

                foreach (var model in m_Models)
                {
                    if (!selectedModalities.Contains(model.modality.ToString(), StringComparer.InvariantCultureIgnoreCase))
                        continue;

                    if (model.operations.Contains(OperationSubTypeEnum.StyleTraining) && availableBaseModels.Add(model.id))
                    {
                        var baseModelElement = new Toggle
                        {
                            name = model.id,
                            label = model.name.AddSpaceBeforeCapitalLetters(),
                            tooltip = model.description
                        };
                        baseModelElement.SetEnabled(m_Models.Any(m =>
                            selectedModalities.Contains(m.modality.ToString(), StringComparer.InvariantCultureIgnoreCase) &&
                            m.baseModelId == model.id));
                        baseModelElement.RegisterValueChangedCallback(OnFilterSelectionChanged);
                        m_BaseModelsElement.Add(baseModelElement);
                    }
                }
            }

            m_BaseModelsContainer.SetShown(availableBaseModels.Any());
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
                    operationElement.SetShown(false);
                }
                operationElement.AddToClassList("operation");
                operationElement.RegisterValueChangedCallback(OnFilterSelectionChanged);
                m_OperationsElement.Add(operationElement);
            }

            m_OperationsContainer.SetShown(Unsupported.IsDeveloperMode());
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
            ShowMisc();
            ShowBaseModels();
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

            m_GridView.SetShown(m_FilteredModels.Count > 0);
            m_SearchResultsText.SetShown(m_FilteredModels.Count == 0);
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

            var selectedMiscModels = m_ModelsElement.Query<Toggle>().ToList()
                .Where(t => t.value)
                .Select(t => (MiscModelEnum)Enum.Parse(typeof(MiscModelEnum), t.name))
                .ToList();

            var selectedBaseModels = m_BaseModelsElement.Query<Toggle>().ToList()
                .Where(t => t.value)
                .ToDictionary(t => t.name, t => t.label);

            var sortMode = m_SortOrder.value switch
            {
                0 => SortMode.RecentlyUsed,
                1 => SortMode.Popularity,
                2 => SortMode.Name,
                _ => SortMode.NameDescending
            };

            var lastUsedRanking = this.GetState().SelectLastUsedRanking();
            var popularityRanking = this.GetState().SelectPopularityRanking();

            return ModelSelectorSelectors.SelectModels(
                filteredModels,
                lastUsedRanking,
                popularityRanking,
                selectedModalities, selectedOperations, selectedSources,
                selectedMiscModels,
                selectedBaseModels,
                selectedTags, searchText, sortMode);
        }
    }
}
