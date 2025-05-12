using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Redux;

namespace Unity.AI.ModelSelector.Services.Stores.Selectors
{
    enum SortMode
    {
        RecentlyUsed,
        Popularity,
        Name,
        NameDescending
    }

    enum MiscModelEnum
    {
        Favorites,
        Default,
        Custom
    }

    static partial class ModelSelectorSelectors
    {
        public static States.ModelSelector SelectModels(this IState state) => state.Get<States.ModelSelector>(ModelSelectorActions.slice);

        public static IEnumerable<ModelSettings> SelectModelSettings(this IState state) => state.SelectModels().settings.models;

        public static bool SelectModelSelectorSettingsReady(this IState state) => SelectModelSettings(state).ToList() is { Count: > 0 };

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

        public static bool SelectShouldAutoAssignModel(this IState state, IEnumerable<ModalityEnum> modalities, IEnumerable<OperationSubTypeEnum> operations) =>
            state.SelectModels(modalities, operations) is not { Count: > 1 };

        public static ModelSettings SelectAutoAssignModel(this IState state, IEnumerable<ModalityEnum> modalities, IEnumerable<OperationSubTypeEnum> operations)
        {
            var models = state.SelectModels(modalities, operations);
            return models is { Count: 1 } ? models[0] : k_InvalidModelSettings;
        }

        public static string SelectModel(IEnumerable<ModelSettings> models, OperationSubTypeEnum operatorSubType)
        {
            var operatorModels = models.Where(s => s.operations.Contains(operatorSubType)).ToList();
            return operatorModels.Any() ? operatorModels.First().id : string.Empty;
        }

        public static ModelSettings SelectBaseModel(this IState state, ModelSettings model)
        {
            return string.IsNullOrEmpty(model?.baseModelId) ? null : state.SelectModelSettings().FirstOrDefault(m => m.id == model.baseModelId);
        }

        public static List<ModelSettings> SelectModels(
            this IState state,
            IEnumerable<ModalityEnum> modalities,
            IEnumerable<OperationSubTypeEnum> operations = null,
            IEnumerable<ProviderEnum> providers = null,
            IEnumerable<MiscModelEnum> miscModels = null,
            IReadOnlyDictionary<string, string> baseModels = null,
            IEnumerable<string> tags = null,
            string searchText = null,
            SortMode sortMode = SortMode.Name)
        {
            return state
                .SelectModelSettings()
                .SelectModels(
                    state.SelectLastUsedRanking(),
                    state.SelectPopularityRanking(),
                    modalities,
                    operations,
                    providers,
                    miscModels,
                    baseModels,
                    tags,
                    searchText,
                    sortMode);
        }

        public static IReadOnlyDictionary<string, string> SelectLastUsedRanking(this IState state) => state.SelectModels().lastUsedModels;

        public static IReadOnlyDictionary<string, int> SelectPopularityRanking(this IState state) => state.SelectModels().modelPopularityScore;

        public static List<ModelSettings> SelectModels(
            this IEnumerable<ModelSettings> models,
            IReadOnlyDictionary<string, string> lastUsedRanking = null,
            IReadOnlyDictionary<string, int> popularityRanking = null,
            IEnumerable<ModalityEnum> modalities = null,
            IEnumerable<OperationSubTypeEnum> operations = null,
            IEnumerable<ProviderEnum> providers = null,
            IEnumerable<MiscModelEnum> miscModels = null,
            IReadOnlyDictionary<string, string> baseModels = null,
            IEnumerable<string> tags = null,
            string searchText = null,
            SortMode sortMode = SortMode.Name)
        {
            var filteredModels = models;

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
                            (miscModelsList.Contains(MiscModelEnum.Default) && (string.IsNullOrEmpty(m.baseModelId) || baseModels == null || !baseModels.ContainsKey(m.baseModelId))) ||
                            (miscModelsList.Contains(MiscModelEnum.Favorites) && m.isFavorite) ||
                            (miscModelsList.Contains(MiscModelEnum.Custom) && !string.IsNullOrEmpty(m.baseModelId) && baseModels != null && baseModels.ContainsKey(m.baseModelId)))
                        .ToList();
                }
            }

            if (baseModels is {Count: > 0})
                filteredModels = filteredModels.Where(m => baseModels.ContainsKey(m.baseModelId)).ToList();

            if (!string.IsNullOrEmpty(searchText))
            {
                var search = searchText.ToLower();
                filteredModels = filteredModels.Where(m =>
                    m.name.ToLower().Contains(search) ||
                    m.description.ToLower().Contains(search) ||
                    m.provider.ToString().ToLower().Contains(search) ||
                    (!string.IsNullOrEmpty(m.baseModelId) && baseModels != null && baseModels.ContainsKey(m.baseModelId) && baseModels[m.baseModelId].ToLower().Contains(search)) ||
                    m.tags.Any(t => t.ToLower().Contains(search))
                ).ToList();
            }

            return sortMode switch
            {
                SortMode.Name => filteredModels.OrderBy(m => m.name).ToList(),
                SortMode.NameDescending => filteredModels.OrderByDescending(m => m.name).ToList(),
                SortMode.RecentlyUsed => filteredModels
                    .OrderByDescending(m => lastUsedRanking != null && lastUsedRanking.TryGetValue(m.id, out var value) ? DateTime.Parse(value) : DateTime.UnixEpoch)
                    .ToList(),
                SortMode.Popularity => filteredModels
                    .OrderByDescending(m => popularityRanking != null && popularityRanking.TryGetValue(m.id, out var value) ? value : 0)
                    .ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(sortMode), sortMode, null)
            };
        }

        public static string SelectEnvironment(this IState state) => state.SelectModels().settings.environment;
        public static string SelectLastSelectedModelID(this IState state) => state.SelectModels().lastSelectedModelID;
        public static ModalityEnum[] SelectLastSelectedModalities(this IState state) => state.SelectModels().lastSelectedModalities;
        public static OperationSubTypeEnum[] SelectLastSelectedOperations(this IState state) => state.SelectModels().lastSelectedOperations;

        static readonly ModelSettings k_InvalidModelSettings = new();

        public static ModelSettings SelectSelectedModel(this IState state)
        {
            var modelSelector = state.SelectModels();
            var modelID = modelSelector.lastSelectedModelID;
            var models = SelectModelSettings(state);
            var model = models.FirstOrDefault(m => m.id == modelID);
            return model ?? k_InvalidModelSettings;
        }
    }
}
