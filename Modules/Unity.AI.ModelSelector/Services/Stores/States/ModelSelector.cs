using System;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ModelSelector
    {
        public string lastSelectedModelID = "";
        public SerializableDictionary<string, string> lastUsedModels = new ();
        public SerializableDictionary<string, int> modelPopularityScore = new ();
        public Settings settings = new();
    }
}
