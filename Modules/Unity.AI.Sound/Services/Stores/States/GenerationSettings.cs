﻿using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;

namespace Unity.AI.Sound.Services.Stores.States
{
    [Serializable]
    record GenerationSettings
    {
        public SerializableDictionary<AssetReference, GenerationSetting> generationSettings = new();
    }
}
