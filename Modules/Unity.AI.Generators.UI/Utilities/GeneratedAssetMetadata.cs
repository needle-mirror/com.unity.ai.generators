using System;

namespace Unity.AI.Generators.UI.Utilities
{
    [Serializable]
    record GeneratedAssetMetadata
    {
        public string asset;
        public string fileName;
        public string prompt;
        public string negativePrompt;
        public string model;
        public int customSeed = -1;
    }
}
