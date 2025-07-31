using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Redux;

namespace Unity.AI.ModelSelector.Services.Stores.Selectors
{
    static partial class ModelSelectorSelectors
    {
        public static States.ModelSelector SelectModels(this IState state) => state.Get<States.ModelSelector>(ModelSelectorActions.slice);

        public static IEnumerable<ModelSettings> SelectModelSettings(this IState state) =>
            state.SelectModels().settings.models
                .Presort()
                .ToArray();

        public static ModelSettings SelectModelSettingsWithModelId(this IState state, string modelId) => state.SelectModelSettings().FirstOrDefault(s => s.id == modelId);

        public const double timeToLiveGlobally = 30;
        public const double timeToLivePerModality = 60;

        public static bool SelectModelSelectorSettingsReady(this IState state)
        {
            var elapsed = new TimeSpan(DateTime.UtcNow.Ticks - state.SelectLastModelDiscoveryTimestamp());
            return elapsed.TotalSeconds < timeToLivePerModality && SelectModelSettings(state).ToList() is { Count: > 0 };
        }

        public static IEnumerable<ModelSettings> SelectUnityModels(this IState state) => state.SelectModelSettings().Where(s => s.provider == ProviderEnum.Unity);

        public static IEnumerable<ModelSettings> SelectPartnersModels(this IState state) => state.SelectModelSettings().Where(s => s.provider != ProviderEnum.Unity);

        public static long SelectLastModelDiscoveryTimestamp(this IState state) => state.SelectModels().settings.lastModelDiscoveryTimestamp;

        public static string SelectRecolorModel(this IState state)
        {
            var modelId = SelectModel(state.SelectUnityModels(), OperationSubTypeEnum.RecolorReference);
            if (!string.IsNullOrEmpty(modelId))
                return modelId;

            modelId = SelectModel(state.SelectPartnersModels(), OperationSubTypeEnum.RecolorReference);
            return !string.IsNullOrEmpty(modelId) ? modelId : string.Empty;
        }

        public static bool SelectShouldAutoAssignModel(
            this IState state,
            IEnumerable<ModalityEnum> modalities = null,
            IEnumerable<OperationSubTypeEnum> operations = null)
            => state.SelectFilteredModelSettings(modalities, operations) is not { Count: > 1 };

        public static ModelSettings SelectAutoAssignModel(
            this IState state,
            IEnumerable<ModalityEnum> modalities = null,
            IEnumerable<OperationSubTypeEnum> operations = null)
        {
            var models = state.SelectFilteredModelSettings(modalities, operations);
            return models is { Count: 1 } ? models[0] : k_InvalidModelSettings;
        }

        public static string SelectModel(IEnumerable<ModelSettings> models, OperationSubTypeEnum operatorSubType)
        {
            var operatorModels = models.Where(s => s.operations.Contains(operatorSubType)).ToList();
            return operatorModels.Any() ? operatorModels.First().id : string.Empty;
        }

        public static ModelSettings SelectModelById(this IState state, string modelID)
        {
            var models = SelectModelSettings(state);
            var model = models.FirstOrDefault(m => m.id == modelID);
            return model ?? k_InvalidModelSettings;
        }

        public static ModelSettings SelectBaseModel(this IState state, ModelSettings model)
        {
            return string.IsNullOrEmpty(model?.baseModelId) ? null : state.SelectModelSettings().FirstOrDefault(m => m.id == model.baseModelId);
        }

        public static List<ModelSettings> SelectFilteredModelSettings(
            this IState state,
            IEnumerable<ModalityEnum> modalities = null,
            IEnumerable<OperationSubTypeEnum> operations = null)
        {
            var filteredModels = state.SelectModelSettings().ToList();
            var filters = state.SelectModelSelectorFilters();
            modalities ??= filters.modalities;
            operations ??= filters.operations;
            var tags = filters.tags;
            var providers = filters.providers;
            var baseModelIds = filters.baseModelIds;
            var miscModels = filters.misc;
            var searchText = filters.searchQuery;
            var sortMode = state.SelectSortMode();
            var lastUsedRanking = state.SelectModels().lastUsedModels;
            var popularityRanking = state.SelectModels().modelPopularityScore;
            var baseModels = baseModelIds
                .ToDictionary(id => id, id => state.SelectModelSettings().FirstOrDefault(m => m.id == id)?.name ?? string.Empty);

            if (modalities != null && modalities.Any())
                filteredModels = filteredModels.Where(m => modalities.Contains(m.modality)).ToList();

            if (tags != null && tags.Any())
                filteredModels = filteredModels.Where(m => tags.Any(t => m.tags.Contains(t))).ToList();

            if (operations != null && operations.Any())
                filteredModels = filteredModels.Where(m => operations.Any(op => m.operations.Contains(op))).ToList();

            if (providers != null && providers.Any())
                filteredModels = filteredModels.Where(m => providers.Any(p => m.provider == p)).ToList();

            if (miscModels != null)
            {
                var miscModelsList = miscModels.ToList();
                if (miscModelsList.Count > 0)
                {
                    filteredModels = filteredModels
                        .Where(m =>
                            (miscModelsList.Contains(MiscModelEnum.Default) && (string.IsNullOrEmpty(m.baseModelId) || !baseModels.ContainsKey(m.baseModelId))) ||
                            (miscModelsList.Contains(MiscModelEnum.Favorites) && m.isFavorite) ||
                            (miscModelsList.Contains(MiscModelEnum.Custom) && !string.IsNullOrEmpty(m.baseModelId) && baseModels.ContainsKey(m.baseModelId)))
                        .ToList();
                }
            }

            if (baseModels is {Count: > 0})
                filteredModels = filteredModels.Where(m => !string.IsNullOrEmpty(m.baseModelId) && baseModels.ContainsKey(m.baseModelId)).ToList();

            if (!string.IsNullOrEmpty(searchText))
            {
                var search = searchText.ToLower();
                filteredModels = filteredModels.Where(m =>
                    m.name.ToLower().Contains(search) ||
                    m.description.ToLower().Contains(search) ||
                    m.provider.ToString().ToLower().Contains(search) ||
                    (!string.IsNullOrEmpty(m.baseModelId) && baseModels.ContainsKey(m.baseModelId) && baseModels[m.baseModelId].ToLower().Contains(search)) ||
                    m.tags.Any(t => t.ToLower().Contains(search))
                ).ToList();
            }

            return sortMode switch
            {
                SortMode.Name => Presort(filteredModels).ThenBy(m => m.name).ToList(),
                SortMode.NameDescending => Presort(filteredModels).ThenByDescending(m => m.name).ToList(),
                SortMode.RecentlyUsed => filteredModels
                    .Presort()
                    .ThenByDescending(m => lastUsedRanking != null && lastUsedRanking.TryGetValue(m.id, out var value) ? DateTime.Parse(value) : DateTime.UnixEpoch)
                    .ToList(),
                SortMode.Popularity => filteredModels
                    .Presort()
                    .ThenByDescending(m => popularityRanking != null && popularityRanking.TryGetValue(m.id, out var value) ? value : 0)
                    .ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(sortMode), sortMode, null)
            };
        }

        public static string SelectEnvironment(this IState state) => state.SelectModels().settings.environment;
        public static string SelectSelectedModelID(this IState state) => state.SelectModels().lastSelectedModelID;

        static readonly ModelSettings k_InvalidModelSettings = new();

        public static ModelSettings SelectSelectedModel(this IState state)
        {
            var modelSelector = state.SelectModels();
            var modelID = modelSelector.lastSelectedModelID;
            var models = SelectModelSettings(state);
            var model = models.FirstOrDefault(m => m.id == modelID);
            return model ?? k_InvalidModelSettings;
        }

        public static ModelSelectorFilters SelectModelSelectorFilters(this IState state) => state.SelectModels().settings.filters;

        public static IEnumerable<ModalityEnum> SelectSelectedModalities(this IState state) => state.SelectModelSelectorFilters().modalities;

        public static IEnumerable<OperationSubTypeEnum> SelectSelectedOperations(this IState state) => state.SelectModelSelectorFilters().operations;

        public static IEnumerable<string> SelectSelectedTags(this IState state) => state.SelectModelSelectorFilters().tags;

        public static IEnumerable<ProviderEnum> SelectSelectedProviders(this IState state) => state.SelectModelSelectorFilters().providers;

        public static IEnumerable<string> SelectSelectedBaseModelIds(this IState state) => state.SelectModelSelectorFilters().baseModelIds;

        public static IEnumerable<MiscModelEnum> SelectSelectedMiscModels(this IState state) => state.SelectModelSelectorFilters().misc;

        public static string SelectSearchQuery(this IState state) => state.SelectModelSelectorFilters().searchQuery;

        public static SortMode SelectSortMode(this IState state) => state.SelectModels().settings.sortMode;

        public static IOrderedEnumerable<ModelSettings> Presort(this List<ModelSettings> models) => models.OrderBy(m => m.isFavorite ? 0 : 1);
    }
}
