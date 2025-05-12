using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.UI.Utilities;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ModelSettings
    {
        public string id;
        public string name;
        public List<string> tags = new();
        public string description;
        public List<string> thumbnails = new();
        public string icon;
        public ProviderEnum provider = ProviderEnum.None;
        public ModalityEnum modality = ModalityEnum.None;
        public List<OperationSubTypeEnum> operations = new();
        public ImmutableArray<OperationSubTypeEnum[]> operationCombinations = Array.Empty<OperationSubTypeEnum[]>();
        public int[] nativeResolution = {1024, 1024};
        public ImmutableArray<int[]> imageSizes = new[]{ new[]{1024, 1024} };
        public string baseModelId;
        public bool isFavorite;
    }
}
