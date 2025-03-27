using System;
using Unity.AI.Generators.UI.Utilities;

namespace Unity.AI.ModelTrainer.Services.Stores.States
{
    [Serializable]
    record Session
    {
        public ImmutableArray<UserModel> userModels = ImmutableArray<UserModel>.Empty;

        public string selectedUserModelId;

        public ImmutableArray<BaseModel> baseModels = ImmutableArray<BaseModel>.Empty;

        public string searchFilter;

        public ImmutableArray<string> tags = ImmutableArray<string>.Empty;
    }
}
