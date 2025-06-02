using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.UI.Utilities;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ImageDimensions
    {
        public int width;
        public int height;
    }

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
        public List<List<OperationSubTypeEnum>> operationCombinations = Array.Empty<List<OperationSubTypeEnum>>().ToList();
        public ImageDimensions nativeResolution = new() { width = 1024, height = 1024 };
        public List<ImageDimensions> imageSizes = new[]{ new ImageDimensions { width = 1024, height = 1024 } }.ToList();
        public string baseModelId;
        public bool isFavorite;
    }
}
