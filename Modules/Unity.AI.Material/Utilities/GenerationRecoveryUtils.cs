using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Material.Services.Stores.Actions.Payloads;
using Unity.AI.Material.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    [Serializable]
    record InterruptedDownloadData : IInterruptedDownloadData
    {
        public AssetReference asset = new();

        public ImmutableArray<SerializableDictionary<int, string>> ids =
            ImmutableArray<SerializableDictionary<int, string>>.From(new[] { new SerializableDictionary<int, string>() });

        public int taskId;
        public GenerationMetadata generationMetadata;
        public ImmutableArray<int> customSeeds = ImmutableArray<int>.Empty;

        public bool AreKeyFieldsEqual(InterruptedDownloadData other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (!asset.Equals(other.asset))
                return false;

            if (ids.Length != other.ids.Length)
                return false;

            for (var i = 0; i < ids.Length; i++)
            {
                var dictA = ids[i];
                var dictB = other.ids[i];

                if (dictA.Count != dictB.Count)
                    return false;

                foreach (var kvp in dictA)
                {
                    if (!dictB.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                        return false;
                }
            }

            return true;
        }

        public int progressTaskId => taskId;
    }

    static class GenerationRecoveryUtils
    {
        public static List<Dictionary<MapType, Guid>> ConvertIds(this ImmutableArray<SerializableDictionary<int, string>> immutableIds)
        {
            return immutableIds
                .Select(dict => dict.ToDictionary(kvp => (MapType)kvp.Key, kvp => Guid.Parse(kvp.Value)))
                .ToList();
        }

        const string k_InterruptedDownloadsFolderPath = "Library/AI.Material";
        const string k_InterruptedDownloadsFilePath = k_InterruptedDownloadsFolderPath + "/InterruptedDownloads.json";

        static SerializableDictionary<string, List<InterruptedDownloadData>> s_InterruptedDownloadsByEnv;

        static GenerationRecoveryUtils() => LoadInterruptedDownloads();

        public static async Task AddCachedDownload(byte[] data, string fileName)
        {
            if (!Directory.Exists(k_InterruptedDownloadsFolderPath))
                Directory.CreateDirectory(k_InterruptedDownloadsFolderPath);

            var fullFilePath = Path.Combine(k_InterruptedDownloadsFolderPath, fileName);
            await FileIO.WriteAllBytesAsync(fullFilePath, data);
        }

        public static async Task AddCachedDownload(Stream dataStream, string fileName)
        {
            if (!Directory.Exists(k_InterruptedDownloadsFolderPath))
                Directory.CreateDirectory(k_InterruptedDownloadsFolderPath);

            var fullFilePath = Path.Combine(k_InterruptedDownloadsFolderPath, fileName);
            await FileIO.WriteAllBytesAsync(fullFilePath, dataStream);
        }

        public static void RemoveCachedDownload(string fileName)
        {
            if (!Directory.Exists(k_InterruptedDownloadsFolderPath))
                return;

            var fullFilePath = Path.Combine(k_InterruptedDownloadsFolderPath, fileName);
            try
            {
                if (File.Exists(fullFilePath))
                    File.Delete(fullFilePath);
            }
            catch
            {
                // ignored
            }
        }

        public static Uri GetCachedDownloadUrl(string fileName)
        {
            if (!Directory.Exists(k_InterruptedDownloadsFolderPath))
                return null;

            var fullFilePath = Path.Combine(k_InterruptedDownloadsFolderPath, fileName);
            return new Uri(Path.GetFullPath(fullFilePath), UriKind.Absolute);
        }

        public static bool HasCachedDownload(string fileName)
        {
            if (!Directory.Exists(k_InterruptedDownloadsFolderPath))
                return false;

            var fullFilePath = Path.Combine(k_InterruptedDownloadsFolderPath, fileName);
            return File.Exists(fullFilePath);
        }

        public static void AddInterruptedDownload(DownloadMaterialsData data) =>
            AddInterruptedDownload(new InterruptedDownloadData
            {
                asset = data.asset,
                ids = ImmutableArray<SerializableDictionary<int, string>>.From(
                         data.ids
                             .Select(dict =>
                                 new SerializableDictionary<int, string>(
                                     dict.ToDictionary(kvp => (int)kvp.Key, kvp => kvp.Value.ToString())))
                             .ToArray()),
                taskId = data.taskID,
                generationMetadata = data.generationMetadata,
                customSeeds = new ImmutableArray<int>(data.customSeeds)
            });

        public static void RemoveInterruptedDownload(DownloadMaterialsData data) =>
            RemoveInterruptedDownload(new InterruptedDownloadData
            {
                asset = data.asset,
                ids = ImmutableArray<SerializableDictionary<int, string>>.From(
                         data.ids
                             .Select(dict =>
                                 new SerializableDictionary<int, string>(
                                     dict.ToDictionary(kvp => (int)kvp.Key, kvp => kvp.Value.ToString())))
                             .ToArray()),
                taskId = data.taskID,
                generationMetadata = data.generationMetadata
            });

        public static void AddInterruptedDownload(InterruptedDownloadData data)
        {
            var environment = WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return;

            if (!s_InterruptedDownloadsByEnv.TryGetValue(environment, out var list) || list == null)
            {
                list = new List<InterruptedDownloadData>();
                s_InterruptedDownloadsByEnv[environment] = list;
            }

            if (!list.Any(existing => existing.AreKeyFieldsEqual(data)))
                return;

            list.Add(data);
            SaveInterruptedDownloads();
        }

        public static void RemoveInterruptedDownload(InterruptedDownloadData data)
        {
            var environment = WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return;

            if (s_InterruptedDownloadsByEnv.TryGetValue(environment, out var list))
            {
                if (list.RemoveAll(d => {
                        if (d.AreKeyFieldsEqual(data))
                        {
                            foreach (var generatedMaterial in d.ids)
                                RemoveCachedDownload(generatedMaterial[(int)MapType.Preview]);
                            return true;
                        }
                        return false;
                    }) > 0)
                    SaveInterruptedDownloads();
            }
        }

        public static List<InterruptedDownloadData> GetInterruptedDownloads(AssetReference asset)
        {
            var environment = WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return new List<InterruptedDownloadData>();

            return s_InterruptedDownloadsByEnv.TryGetValue(environment, out var list)
                ? list.Where(data => data != null && data.asset == asset).ToList()
                : new List<InterruptedDownloadData>();
        }

        static void LoadInterruptedDownloads()
        {
            s_InterruptedDownloadsByEnv = new SerializableDictionary<string, List<InterruptedDownloadData>>();
            if (!File.Exists(k_InterruptedDownloadsFilePath))
                return;
            var json = FileIO.ReadAllText(k_InterruptedDownloadsFilePath);
            s_InterruptedDownloadsByEnv = JsonUtility.FromJson<SerializableDictionary<string, List<InterruptedDownloadData>>>(json) ??
                new SerializableDictionary<string, List<InterruptedDownloadData>>();
        }

        static void SaveInterruptedDownloads()
        {
            var json = JsonUtility.ToJson(s_InterruptedDownloadsByEnv, true);
            var directory = Path.GetDirectoryName(k_InterruptedDownloadsFilePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            FileIO.WriteAllText(k_InterruptedDownloadsFilePath, json);
        }
    }
}
