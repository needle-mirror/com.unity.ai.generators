using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.Material.Services.Stores.States
{
    [Serializable]
    record GenerationSetting
    {
        public SerializableDictionary<RefinementMode, ModelSelection> selectedModels = new();
        public float lastModelDiscoveryTime = 0;
        public string prompt = "";
        public string negativePrompt = "";
        public int variationCount = 2;
        public bool useCustomSeed;
        public int customSeed;
        public RefinementMode refinementMode;
        public string imageDimensions = "512 x 512";

        public PromptImageReference promptImageReference = new();
        public PatternImageReference patternImageReference = new();

        public float historyDrawerHeight = 200;
    }

    [Serializable]
    record PromptImageReference
    {
        public AssetReference asset = new();
    }

    [Serializable]
    record PatternImageReference
    {
        public float strength = 0.5f;
        public AssetReference asset = new() { guid = "df1d8d51ae8ca65429a68345c1e58832" }; // blank pattern to help the object picker show the correct asset
    }

    enum RefinementMode : int
    {
        Generation = 0,
        Upscale = 1,
        Pbr = 2,
        // ...
        First = 0,
        Last = Pbr
    }

    [Serializable]
    record ModelSelection
    {
        public string modelID = "";
    }
}
