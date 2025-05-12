using System;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEngine;

namespace Unity.AI.Material.Services.Stores.States
{
    [Serializable]
    record Settings
    {
        public SerializableDictionary<RefinementMode, ModelSelection> lastSelectedModels = new();
        public SerializableDictionary<string, SerializableDictionary<MapType, string>> lastMaterialMappings = new();
        public PreviewSettings previewSettings = new();
    }
}
