using System;
using Unity.AI.ModelSelector.Services.Stores.States;

namespace Unity.AI.ModelSelector.Services.Utilities
{
    static class ModelSettingsExtensions
    {
        public static bool IsValid(this ModelSettings model) => model != null && !string.IsNullOrEmpty(model.id);
    }
}
