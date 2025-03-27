using System;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEngine;

namespace Unity.AI.Image.Services.Stores.States
{
    [Serializable]
    record Settings
    {
        public SerializableDictionary<RefinementMode, ModelSelection> lastSelectedModels = new();
        public PreviewSettings previewSettings = new();
    }
}
