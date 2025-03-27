using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.ModelTrainer.Services.Stores.Slices;
using Unity.AI.Generators.Redux;

namespace Unity.AI.ModelTrainer.Services.Stores
{
    class ModelTrainerStore : Store
    {
        internal const string name = "store";

        static IStore s_Store;

        internal static IStore instance
        {
            get
            {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (s_Store == null)
                    s_Store = new ModelTrainerStore();
                return s_Store;
            }
        }

        ModelTrainerStore()
        {
            SessionSlice.Create(this);
            ApplyMiddleware(PersistenceMiddleware);
            this.Dispatch(SessionActions.fetchBaseModels.Invoke());
        }

        static Middleware PersistenceMiddleware => api => next => async action =>
        {
            await next(action);
            ModelTrainerLocalSettings.instance.session = api.State.SelectSession();
        };
    }
}
