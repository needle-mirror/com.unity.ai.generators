using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.Image.Services.Stores.States
{
    [Serializable]
    record GenerationResults
    {
        public SerializableDictionary<AssetReference, GenerationResult> generationResults = new();
    }
}
