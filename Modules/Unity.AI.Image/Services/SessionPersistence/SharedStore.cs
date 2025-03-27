using System;
using System.ComponentModel;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.Generators.Redux;
using UnityEngine;

namespace Unity.AI.Image.Services.SessionPersistence
{
    static class SharedStore
    {
        static Store s_Store;

        public static Store Store
        {
            get
            {
                if (s_Store == null)
                {
                    s_Store = new AIImageStore();
                    MemoryPersistence.Persist(s_Store, AppActions.init, Selectors.SelectAppData);
                    MemoryPersistence.Persist(s_Store, ModelSelectorActions.init, ModelSelectorSelectors.SelectAppData);
                    MemoryPersistence.Persist(s_Store, DoodleWindowActions.init, DoodleWindowSelectors.SelectDoodleAppData);
                    Store.ApplyMiddleware(PersistenceMiddleware);
                }

                return s_Store;
            }
        }

        static Middleware PersistenceMiddleware => api => next => async action =>
        {
            await next(action);
            TextureGeneratorSettings.instance.session = api.State.SelectSession();
        };
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SharedStoreExtensions
    {
        public static IStore Store => SharedStore.Store;
    }
}
