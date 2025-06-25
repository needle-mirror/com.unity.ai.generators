using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Image.Services.Utilities
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

            // Only compare asset, ids, and modality.
            return asset.Equals(other.asset) && ids.Equals(other.ids);
        }

        public int progressTaskId => taskId;
    }

    static class GenerationRecoveryUtils
    {
        const string k_LoadInterruptedDownloads = "internal:AI Toolkit/Internals/Tests/(Re)Load Interrupted Downloads";

        [MenuItem(k_LoadInterruptedDownloads, false, 1000)]
        static void ReLoadInterruptedDownloads() => EditorUtility.RequestScriptReload();

        internal static SerializableDictionary<string, List<InterruptedDownloadData>> s_InterruptedDownloadsByEnv;

        static GenerationRecoveryUtils() => LoadInterruptedDownloads();

        public static void AddInterruptedDownload(DownloadImagesData data) => AddInterruptedDownload(new InterruptedDownloadData
        {
            asset = data.asset,
            ids = new ImmutableStringList(data.ids.Select(id => id.ToString())),
            taskId = data.taskID,
            generationMetadata =  data.generationMetadata,
            customSeeds = new ImmutableArray<int>(data.customSeeds)
        });

        public static void RemoveInterruptedDownload(DownloadImagesData data) => RemoveInterruptedDownload(new InterruptedDownloadData
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

            if (!list.Any(existing => existing.AreKeyFieldsEqual(data)))
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

        public static void LoadInterruptedDownloads()
        {
            if (System.IO.File.Exists(InterruptedDownloadsFilePath))
            {
                var json = FileIO.ReadAllText(InterruptedDownloadsFilePath);
                s_InterruptedDownloadsByEnv = JsonUtility.FromJson<SerializableDictionary<string, List<InterruptedDownloadData>>>(json);
                if (s_InterruptedDownloadsByEnv == null)
                    s_InterruptedDownloadsByEnv = new SerializableDictionary<string, List<InterruptedDownloadData>>();
            }
            else
            {
                s_InterruptedDownloadsByEnv = new SerializableDictionary<string, List<InterruptedDownloadData>>();
            }
        }

        internal static void SaveInterruptedDownloads()
        {
            var json = JsonUtility.ToJson(s_InterruptedDownloadsByEnv, true);
            var directory = System.IO.Path.GetDirectoryName(InterruptedDownloadsFilePath);

            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            FileIO.WriteAllText(InterruptedDownloadsFilePath, json);
        }

        /// <summary>
        /// Path to the file where interrupted downloads are stored.
        /// Can be overridden for testing purposes.
        /// </summary>
        public static string InterruptedDownloadsFilePath { get; set; } = "Library/AI.Image/InterruptedDownloads.json";

        /// <summary>
        /// Clears all interrupted downloads from memory and optionally from disk.
        /// </summary>
        /// <param name="persistToDisk">Whether to save the empty state to disk.</param>
        public static void ClearAllInterruptedDownloads(bool persistToDisk = false)
        {
            s_InterruptedDownloadsByEnv = new SerializableDictionary<string, List<InterruptedDownloadData>>();
            if (persistToDisk)
                SaveInterruptedDownloads();
        }

        /// <summary>
        /// Clears interrupted downloads for a specific environment.
        /// </summary>
        /// <param name="environment">The environment to clear. If null, uses the current environment.</param>
        /// <param name="persistToDisk">Whether to save the changes to disk.</param>
        public static void ClearInterruptedDownloadsForEnvironment(string environment = null, bool persistToDisk = false)
        {
            environment ??= WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return;

            if (!s_InterruptedDownloadsByEnv.ContainsKey(environment))
                return;

            s_InterruptedDownloadsByEnv[environment] = new List<InterruptedDownloadData>();
            if (!persistToDisk)
                return;

            SaveInterruptedDownloads();
        }

        /// <summary>
        /// Gets the count of interrupted downloads for an asset.
        /// </summary>
        /// <param name="asset">The asset to check.</param>
        /// <param name="environment">Optional environment to check. If null, uses the current environment.</param>
        /// <returns>The number of interrupted downloads for the asset.</returns>
        public static int GetInterruptedDownloadCount(AssetReference asset, string environment = null)
        {
            environment ??= WebUtils.selectedEnvironment;
            if (string.IsNullOrEmpty(environment))
                return 0;

            if (s_InterruptedDownloadsByEnv.TryGetValue(environment, out var list))
                return list.Count(data => data != null && data.asset == asset);

            return 0;
        }
    }
}
