using System;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    [Serializable]
    [FilePath("ProjectSettings/GeneratedAssetCache.asset", FilePathAttribute.Location.ProjectFolder)]
    class GeneratedAssetCache : ScriptableSingleton<GeneratedAssetCache>
    {
        [SerializeField]
        public SerializableDictionary<string, string> assetCacheEntries = new();

        public void EnsureSaved()
        {
            Save(true);
        }
    }
}
