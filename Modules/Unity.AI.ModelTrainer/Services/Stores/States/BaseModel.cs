using System;
using Unity.AI.Generators.UI.Utilities;

namespace Unity.AI.ModelTrainer.Services.Stores.States
{
    [Serializable]
    record BaseModel
    {
        public string id;
        public string name;
        public ImmutableArray<Setting> settings = ImmutableArray<Setting>.Empty;
    }
}
