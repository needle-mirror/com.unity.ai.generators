using System;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.UI.Utilities;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ModelSelectorFilters
    {
        public ImmutableArray<ModalityEnum> modalities = ImmutableArray<ModalityEnum>.Empty;
        public ImmutableArray<OperationSubTypeEnum> operations = ImmutableArray<OperationSubTypeEnum>.Empty;

        public ImmutableArray<ProviderEnum> providers = ImmutableArray<ProviderEnum>.Empty;
        public ImmutableArray<string> tags = ImmutableArray<string>.Empty;
        public ImmutableArray<string> baseModelIds = ImmutableArray<string>.Empty;
        public ImmutableArray<MiscModelEnum> misc = ImmutableArray<MiscModelEnum>.Empty;

        public string searchQuery = string.Empty;
    }
}
