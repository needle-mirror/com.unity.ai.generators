using System;
using System.Linq;
using Unity.AI.ModelTrainer.Services.Stores.Actions;
using Unity.AI.ModelTrainer.Services.Stores.Selectors;
using Unity.AI.ModelTrainer.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;

namespace Unity.AI.ModelTrainer.Services.Stores.Slices
{
    static class SessionSlice
    {
        public static void Create(ModelTrainerStore store)
        {
            var settings = ModelTrainerLocalSettings.instance.session;
            var initialState = settings != null ? settings with { } : new Session();

            store.CreateSlice(SessionActions.slice, initialState,
                reducers => reducers
                    .AddCase(SessionActions.fetchBaseModels.Fulfilled)
                    .With(static (state, action) => state with {baseModels = new ImmutableArray<BaseModel>(action.payload)})
                    .AddCase(SessionActions.setSearchFilter)
                    .With(static (state, action) => state with {searchFilter = action.payload})
                    .AddCase(SessionActions.selectModel)
                    .With(static (state, action) => state with {selectedUserModelId = action.payload})
                    .AddCase(SessionActions.addModel)
                    .With(static (state, _) =>
                    {
                        var id = Guid.NewGuid().ToString();
                        var baseModel = state.baseModels?.FirstOrDefault();
                        return state with
                        {
                            userModels = state.userModels.Add(new UserModel
                            {
                                id = id,
                                name = "New Model",
                                baseModelId = baseModel?.id,
                                settings = baseModel?.settings?.Select(s => new UserSetting
                                {
                                    name = s.name,
                                    type = s.type,
                                    value = s.defaultValue,
                                }).ToArray() ?? ImmutableArray<UserSetting>.Empty,
                            }),
                            selectedUserModelId = id,
                        };
                    })
                    .AddCase(SessionActions.deleteModel)
                    .With(static (state, _) =>
                    {
                        var model = state.SelectSelectedModel();
                        var newModels = state.userModels.Remove(model);
                        var filteredModels = state.SelectFilteredModels().ToList();
                        var index = filteredModels.IndexOf(model);
                        var nextIndex = (index - 1 < 0 && filteredModels.Count > 1) ? 1 : index - 1;
                        var nextModel = filteredModels.ElementAtOrDefault(nextIndex);
                        return state with
                        {
                            userModels = newModels,
                            selectedUserModelId = nextModel?.id,
                        };
                    })
                    .AddCase(SessionActions.setName)
                    .With(static (state, action) => ReduceSelectedModel(state, model => model with {name = action.payload}))
                    .AddCase(SessionActions.addImage)
                    .With(static (state, action) => ReduceSelectedModel(state, model => model with
                    {
                        trainingImages = model.trainingImages.Add(new TrainingImageReference
                        {
                            id = Guid.NewGuid().ToString(),
                            url = null,
                            prompt = "Enter a prompt",
                        }),
                    }))
                    .AddCase(SessionActions.deleteImage)
                    .With(static (state, action) => ReduceSelectedModel(state, model =>
                    {
                        var imageToRemove = model.trainingImages.Find(x => x.id == action.payload);
                        var newImages = model.trainingImages.Remove(imageToRemove);
                        return model with {trainingImages = newImages};
                    }))
                    .AddCase(SessionActions.addTag)
                    .With(static (state, action) =>
                    {
                        var newState = ReduceSelectedModel(state, model =>
                        {
                            var newTags = model.tags.AddDistinct(action.payload);
                            return model with {tags = newTags};
                        });
                        var tags = newState.tags.AddDistinct(action.payload);
                        return newState with {tags = tags};
                    })
                    .AddCase(SessionActions.deleteTag)
                    .With(static (state, action) => ReduceSelectedModel(state, model =>
                    {
                        var tagToRemove = model.tags.Find(x => x == action.payload);
                        var newTags = model.tags.Remove(tagToRemove);
                        return model with {tags = newTags};
                    }))
                    .AddCase(SessionActions.trainModel.Fulfilled)
                    .With(static (state, action) => ReduceSelectedModel(state, model => model with {trainingStatus = action.payload}))
                    .AddCase(SessionActions.setUserSettingValue)
                    .With(static (state, action) => ReduceSelectedModel(state, model => model with
                    {
                        settings = model.settings.AddOrReplace(s => s.name == action.payload.setting.name, new UserSetting
                        {
                            name = action.payload.setting.name,
                            type = action.payload.setting.type,
                            value = action.payload.value,
                        }),
                    }))
                    .AddCase(SessionActions.setBaseModel)
                    .With(static (state, action) => ReduceSelectedModel(state, model => model with
                    {
                        baseModelId = action.payload,
                        settings = state.SelectBaseModel(action.payload)?.settings?.Select(s => new UserSetting
                        {
                            name = s.name,
                            type = s.type,
                            value = s.defaultValue,
                        }).ToArray() ?? ImmutableArray<UserSetting>.Empty,
                    })));
        }

        static Session ReduceSelectedModel(Session state, Func<UserModel, UserModel> reducer)
        {
            var idx = state.SelectSelectedModelIndex();
            var newModel = reducer(state.userModels[idx]);
            var newModels = state.userModels.ReplaceAt(newModel, idx);
            return state with {userModels = newModels};
        }
    }
}
