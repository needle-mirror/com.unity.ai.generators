using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEngine;

namespace Unity.AI.Animate.Services.Utilities
{
    [Serializable]
    record InterruptedDownloadData : IInterruptedDownloadData
    {
        public AssetReference asset = new();
        public ImmutableStringList ids = new(new List<string>());
        public int taskId;
        public GenerationMetadata generationMetadata;
        public ImmutableArray<int> customSeeds = ImmutableArray<int>.Empty;

        public bool AreKeyFieldsEqual(InterruptedDownloadData other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            // Only compare asset, ids.
            return asset.Equals(other.asset) && ids.Equals(other.ids);
        }

        public int progressTaskId => taskId;
    }

    static class GenerationRecoveryUtils
    {
        const string k_InterruptedDownloadsFilePath = "Library/AI.Animate/InterruptedDownloads.json";

        static SerializableDictionary<string, List<InterruptedDownloadData>> s_InterruptedDownloadsByEnv;

        static GenerationRecoveryUtils() => LoadInterruptedDownloads();

        public static void AddInterruptedDownload(DownloadAnimationsData data) => AddInterruptedDownload(new InterruptedDownloadData
        {
            asset = data.asset,
            ids = new ImmutableStringList(data.ids.Select(id => id.ToString())),
            taskId = data.taskID,
            generationMetadata =  data.generationMetadata,
            customSeeds = new ImmutableArray<int>(data.customSeeds)
        });

        public static void RemoveInterruptedDownload(DownloadAnimationsData data) => RemoveInterruptedDownload(new InterruptedDownloadData
            {
                asset = data.asset,
                ids = new ImmutableStringList(data.ids.Select(id => id.ToString())),
                taskId = data.taskID,
                generationMetadata =  data.generationMetadata
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

            if (!list.Contains(data))
            {
                list.Add(data);
                SaveInterruptedDownloads();
            }
        }

        public static void RemoveInterruptedDownload(InterruptedDownloadData data)
        {
            var environment = WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return;

            if (s_InterruptedDownloadsByEnv.TryGetValue(environment, out var list))
            {
                if (list.RemoveAll(d => d.AreKeyFieldsEqual(data)) > 0)
                    SaveInterruptedDownloads();
            }
        }

        public static List<InterruptedDownloadData> GetInterruptedDownloads(AssetReference asset)
        {
            var environment = WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return new List<InterruptedDownloadData>();

            if (s_InterruptedDownloadsByEnv.TryGetValue(environment, out var list))
                return list.Where(data => data != null && data.asset == asset).ToList();

            return new List<InterruptedDownloadData>();
        }

        static void LoadInterruptedDownloads()
        {
            if (System.IO.File.Exists(k_InterruptedDownloadsFilePath))
            {
                var json = FileIO.ReadAllText(k_InterruptedDownloadsFilePath);
                s_InterruptedDownloadsByEnv = JsonUtility.FromJson<SerializableDictionary<string, List<InterruptedDownloadData>>>(json);
                if (s_InterruptedDownloadsByEnv == null)
                    s_InterruptedDownloadsByEnv = new SerializableDictionary<string, List<InterruptedDownloadData>>();
            }
            else
            {
                s_InterruptedDownloadsByEnv = new SerializableDictionary<string, List<InterruptedDownloadData>>();
            }
        }

        static void SaveInterruptedDownloads()
        {
            var json = JsonUtility.ToJson(s_InterruptedDownloadsByEnv, true);
            var directory = System.IO.Path.GetDirectoryName(k_InterruptedDownloadsFilePath);

            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            FileIO.WriteAllText(k_InterruptedDownloadsFilePath, json);
        }
    }
}
