using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.Animate.Services.Stores.States
{
    [Serializable]
    record GenerationSetting
    {
        public SerializableDictionary<RefinementMode, ModelSelection> selectedModels = new();
        public float lastModelDiscoveryTime = 0;
        public string prompt = "";
        public string negativePrompt = "";
        public int variationCount = 1;
        public float duration = 4;
        public bool useCustomSeed;
        public int customSeed;
        public RefinementMode refinementMode;

        public VideoInputReference videoReference = new();

        public float historyDrawerHeight = 200;
    }

    [Serializable]
    record VideoInputReference
    {
        public AssetReference asset = new();
    }

    enum RefinementMode : int
    {
        TextToMotion = 0,
        VideoToMotion = 1,
        First = 0,
        Last = VideoToMotion
    }

    [Serializable]
    record ModelSelection
    {
        public string modelID = "";
    }
}
