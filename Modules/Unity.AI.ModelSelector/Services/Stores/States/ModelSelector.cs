using System;
using AiEditorToolsSdk.Components.Common.Enums;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ModelSelector
    {
        public string lastSelectedModelID = "";
        public ModalityEnum lastSelectedModality = ModalityEnum.None;
        public OperationSubTypeEnum[] lastSelectedOperations = Array.Empty<OperationSubTypeEnum>();
        public Settings settings = new();
    }
}
