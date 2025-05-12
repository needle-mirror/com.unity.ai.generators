using System;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ModelSelector
    {
        public string lastSelectedModelID = "";
        public ModalityEnum[] lastSelectedModalities = Array.Empty<ModalityEnum>();
        public OperationSubTypeEnum[] lastSelectedOperations = Array.Empty<OperationSubTypeEnum>();
        public SerializableDictionary<string, string> lastUsedModels = new ();
        public SerializableDictionary<string, int> modelPopularityScore = new ();
        public Settings settings = new();
    }
}
