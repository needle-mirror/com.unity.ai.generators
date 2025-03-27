using System.Collections.Generic;
using System.Linq;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.Redux;

namespace Unity.AI.ModelTrainer.Services.Stores.Selectors
{

    static class SessionSelectors
    {
        public static Session SelectSession(this IState state) => state.Get<Session>(SessionActions.slice);

        public static IEnumerable<BaseModel> SelectBaseModels(this IState state)
        {
            return SelectSession(state).SelectBaseModels();
        }

        public static IEnumerable<BaseModel> SelectBaseModels(this Session state)
        {
            return state.baseModels;
        }

        public static BaseModel SelectBaseModel(this IState state, string id)
        {
            return state.SelectSession().SelectBaseModel(id);
        }

        public static BaseModel SelectBaseModel(this Session state, string id)
        {
            return state.SelectBaseModels().FirstOrDefault(x => x.id == id);
        }

        public static IEnumerable<UserSetting> SelectSettings(IState state)
        {
            return SelectSession(state).SelectSelectedModel()?.settings;
        }

        public static string SelectBaseModelId(this IState state)
        {
            return SelectSession(state).SelectSelectedModel()?.baseModelId;
        }

        public static TrainingStatus SelectTrainingStatus(this IState state)
        {
            return SelectSession(state).SelectSelectedModel()?.trainingStatus ?? TrainingStatus.NotStarted;
        }

        public static string SelectName(this IState state)
        {
            return SelectSession(state).SelectSelectedModel()?.name;
        }

        public static IEnumerable<string> SelectTags(this IState state)
        {
            return SelectSession(state).SelectSelectedModel()?.tags;
        }

        public static IEnumerable<string> SelectAllTags(this IState state)
        {
            return SelectSession(state).tags;
        }

        public static bool HasTag(this IState state, string tag)
        {
            return SelectSession(state).SelectSelectedModel()?.tags.Contains(tag) ?? false;
        }

        public static IEnumerable<TrainingImageReference> SelectImages(this IState state)
        {
            return SelectSession(state).SelectSelectedModel()?.trainingImages;
        }

        public static UserModel SelectSelectedModel(this IState state)
        {
            return SelectSession(state).SelectSelectedModel();
        }

        public static UserModel SelectSelectedModel(this Session session)
        {
            return session.userModels.Find(x => x.id == session.selectedUserModelId);
        }

        public static int SelectSelectedModelIndex(this Session session)
        {
            return session.userModels.FindIndex(x => x.id == session.selectedUserModelId);
        }

        public static string SelectSearchFilter(this IState state)
        {
            return SelectSession(state).searchFilter;
        }

        public static IEnumerable<UserModel> SelectFilteredModels(this IState state)
        {
            return SelectSession(state).SelectFilteredModels();
        }

        public static IEnumerable<UserModel> SelectFilteredModels(this Session session)
        {
            var filter = session.searchFilter;
            return session.userModels
                .Where(x => x.name?.Contains(filter, System.StringComparison.OrdinalIgnoreCase) ?? false);
        }
    }
}
