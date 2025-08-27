using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Redux;
using Unity.AI.ModelSelector.Services.Utilities;

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

        public static IEnumerable<ModelSettings> SelectUnityModels(this IState state) => state.SelectModelSettings().Where(s => s.provider == ModelConstants.Providers.Unity);

        public static IEnumerable<ModelSettings> SelectPartnersModels(this IState state) => state.SelectModelSettings().Where(s => s.provider != ModelConstants.Providers.Unity);

        public static long SelectLastModelDiscoveryTimestamp(this IState state) => state.SelectModels().settings.lastModelDiscoveryTimestamp;

        public static string SelectRecolorModel(this IState state)
        {
            var modelId = SelectModel(state.SelectUnityModels(), ModelConstants.Operations.RecolorReference);
            if (!string.IsNullOrEmpty(modelId))
                return modelId;

            modelId = SelectModel(state.SelectPartnersModels(), ModelConstants.Operations.RecolorReference);
            return !string.IsNullOrEmpty(modelId) ? modelId : string.Empty;
        }

        public static bool SelectShouldAutoAssignModel(this IState state,
            string currentModel,
            IEnumerable<string> modalities = null,
            IEnumerable<string> operations = null)
        {
            var models = state.SelectUnfilteredModelSettings(modalities, operations);
            if (string.IsNullOrEmpty(currentModel))
            {
                if (models.Any(m => m.isFavorite))
                    return true;
            }

            return models is not { Count: > 1 };
        }

        public static ModelSettings SelectAutoAssignModel(this IState state,
            string currentModel,
            IEnumerable<string> modalities = null,
            IEnumerable<string> operations = null)
        {
            var models = state.SelectUnfilteredModelSettings(modalities, operations);
            if (string.IsNullOrEmpty(currentModel))
            {
                var favorite = models.FirstOrDefault(m => m.isFavorite);
                if (favorite != null && favorite.IsValid())
                    return favorite;
            }

            return models is { Count: 1 } ? models[0] : k_InvalidModelSettings;
        }

        public static string SelectModel(IEnumerable<ModelSettings> models, string operatorSubType)
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

        static readonly ModelSelectorFilters k_EmptyFilters = new();

        public static List<ModelSettings> SelectUnfilteredModelSettings(
            this IState state,
            IEnumerable<string> modalitiesList = null,
            IEnumerable<string> operationsList = null)
        {
            return state.SelectFilteredModelSettings(modalitiesList, operationsList, k_EmptyFilters);
        }

        public static List<ModelSettings> SelectFilteredModelSettings(
            this IState state,
            IEnumerable<string> modalitiesList = null,
            IEnumerable<string> operationsList = null,
            ModelSelectorFilters filters = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var allModels = state.SelectModelSettings();
            filters = filters ?? state.SelectModelSelectorFilters();
            var modalities = modalitiesList ?? filters.modalities;
            var operations = operationsList ?? filters.operations;
            var tags = filters.tags;
            var providers = filters.providers;
            var baseModelIds = filters.baseModelIds;
            var miscModels = filters.misc;
            var searchText = filters.searchQuery;
            var sortMode = state.SelectSortMode();
            var lastUsedRanking = state.SelectModels().lastUsedModels;
            var popularityRanking = state.SelectModels().modelPopularityScore;

            var modalitiesSet = CreateHashSetIfNotEmpty(modalities);
            var operationsSet = CreateHashSetIfNotEmpty(operations);
            var tagsSet = CreateHashSetIfNotEmpty(tags);
            var providersSet = CreateHashSetIfNotEmpty(providers);
            var miscModelsSet = CreateHashSetIfNotEmpty(miscModels);
            var baseModelsDict = CreateBaseModelsDictionary(baseModelIds, allModels);

            var filteredQuery = allModels.Where(m => ApplyFilters(m, modalitiesSet, operationsSet, tagsSet, providersSet, miscModelsSet, baseModelsDict));

            if (!string.IsNullOrEmpty(searchText))
            {
                filteredQuery = ApplySearchFilter(filteredQuery, searchText.ToLower(), allModels);
            }

            return ApplySorting(filteredQuery, sortMode, lastUsedRanking, popularityRanking);
        }

        static HashSet<T> CreateHashSetIfNotEmpty<T>(IEnumerable<T> collection)
        {
            if (collection == null) return null;
            var hashSet = new HashSet<T>();
            foreach (var item in collection)
            {
                hashSet.Add(item);
            }
            return hashSet.Count > 0 ? hashSet : null;
        }

        static Dictionary<string, string> CreateBaseModelsDictionary(IEnumerable<string> baseModelIds, IEnumerable<ModelSettings> allModels)
        {
            if (baseModelIds == null) return null;

            var dict = new Dictionary<string, string>();
            foreach (var id in baseModelIds)
            {
                var modelName = allModels.FirstOrDefault(m => m.id == id)?.name ?? string.Empty;
                dict[id] = modelName;
            }
            return dict.Count > 0 ? dict : null;
        }

        static bool ApplyFilters(
            ModelSettings model,
            HashSet<string> modalitiesSet,
            HashSet<string> operationsSet,
            HashSet<string> tagsSet,
            HashSet<string> providersSet,
            HashSet<MiscModelType> miscModelsSet,
            Dictionary<string, string> baseModelsDict)
        {
            return (modalitiesSet == null || modalitiesSet.Contains(model.modality)) &&
                   (tagsSet == null || (model.tags?.Any(t => tagsSet.Contains(t)) == true)) &&
                   (operationsSet == null || (model.operations?.Any(op => operationsSet.Contains(op)) == true)) &&
                   (providersSet == null || providersSet.Contains(model.provider)) &&
                   (baseModelsDict == null || (!string.IsNullOrEmpty(model.baseModelId) && baseModelsDict.ContainsKey(model.baseModelId))) &&
                   (miscModelsSet == null ||
                    (miscModelsSet.Contains(MiscModelType.Default) && !model.isCustom) ||
                    (miscModelsSet.Contains(MiscModelType.Favorites) && model.isFavorite) ||
                    (miscModelsSet.Contains(MiscModelType.Custom) && model.isCustom));
        }

        static IEnumerable<ModelSettings> ApplySearchFilter(IEnumerable<ModelSettings> query, string searchLower, IEnumerable<ModelSettings> allModels)
        {
            var baseModelCache = new Dictionary<string, ModelSettings>();

            return query.Where(m =>
            {
                if (m.MatchSearchText(searchLower))
                    return true;

                if (string.IsNullOrEmpty(m.baseModelId))
                    return false;

                if (!baseModelCache.TryGetValue(m.baseModelId, out var baseModel))
                {
                    baseModel = allModels.FirstOrDefault(bm => bm.id == m.baseModelId);
                    baseModelCache[m.baseModelId] = baseModel;
                }

                return baseModel?.name?.ToLower().Contains(searchLower) == true;
            });
        }

        static List<ModelSettings> ApplySorting(IEnumerable<ModelSettings> query, SortMode sortMode,
            Dictionary<string, string> lastUsedRanking, Dictionary<string, int> popularityRanking)
        {
            var orderedQuery = query.OrderBy(m => m.isFavorite ? 0 : 1);

            return sortMode switch
            {
                SortMode.Name => orderedQuery.ThenBy(m => m.name).ToList(),
                SortMode.NameDescending => orderedQuery.ThenByDescending(m => m.name).ToList(),
                SortMode.RecentlyUsed => orderedQuery.ThenByDescending(m => GetLastUsedDateTime(m.id, lastUsedRanking)).ToList(),
                SortMode.Popularity => orderedQuery.ThenByDescending(m => popularityRanking?.GetValueOrDefault(m.id, 0) ?? 0).ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(sortMode), sortMode, null)
            };
        }

        static DateTime GetLastUsedDateTime(string modelId, Dictionary<string, string> lastUsedRanking)
        {
            if (lastUsedRanking?.TryGetValue(modelId, out var dateString) == true &&
                DateTime.TryParse(dateString, out var parsedDate))
            {
                return parsedDate;
            }
            return DateTime.UnixEpoch;
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

        public static IEnumerable<string> SelectSelectedModalities(this IState state) => state.SelectModelSelectorFilters().modalities;

        public static IEnumerable<string> SelectSelectedOperations(this IState state) => state.SelectModelSelectorFilters().operations;

        public static IEnumerable<string> SelectSelectedTags(this IState state) => state.SelectModelSelectorFilters().tags;

        public static IEnumerable<string> SelectSelectedProviders(this IState state) => state.SelectModelSelectorFilters().providers;

        public static IEnumerable<string> SelectSelectedBaseModelIds(this IState state) => state.SelectModelSelectorFilters().baseModelIds;

        public static IEnumerable<MiscModelType> SelectSelectedMiscModels(this IState state) => state.SelectModelSelectorFilters().misc;

        public static string SelectSearchQuery(this IState state) => state.SelectModelSelectorFilters().searchQuery;

        public static SortMode SelectSortMode(this IState state) => state.SelectModels().settings.sortMode;

        public static IOrderedEnumerable<ModelSettings> Presort(this List<ModelSettings> models) => models.OrderBy(m => m.isFavorite ? 0 : 1);
    }
}
