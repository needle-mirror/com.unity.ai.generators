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

namespace Unity.AI.ImageEditor.Services.Utilities
{
    [Serializable]
    record InterruptedDownloadData : IInterruptedDownloadData
    {
        public AssetReference asset = new();
        public ImmutableStringList ids = new(new List<string>());
        public GenerationRecoveryUtils.Modality modality = GenerationRecoveryUtils.Modality.SpritesMpp;
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
            return asset.Equals(other.asset) && ids.Equals(other.ids) && modality.Equals(other.modality);
        }

        public int progressTaskId => taskId;
    }

    static class GenerationRecoveryUtils
    {
        const string k_LoadInterruptedDownloads = "AI Toolkit/Internals/Tests/(Re)Load Interrupted Downloads";
        const string k_InterruptedDownloadsFilePath = "Library/AI.Image/InterruptedDownloads.json";

        [MenuItem(k_LoadInterruptedDownloads, false, 1000)]
        static void ReLoadInterruptedDownloads() => EditorUtility.RequestScriptReload();

        static SerializableDictionary<string, List<InterruptedDownloadData>> s_InterruptedDownloadsByEnv;

        static GenerationRecoveryUtils() => LoadInterruptedDownloads();

        public enum Modality
        {
            SpritesMpp,
            SpritesLegacy
        }

        public static void AddInterruptedDownload(DownloadImagesData data) => AddInterruptedDownload(new InterruptedDownloadData
        {
            asset = data.asset,
            modality = data.modality,
            ids = new ImmutableStringList(data.ids.Select(id => id.ToString())),
            taskId = data.taskID,
            generationMetadata =  data.generationMetadata,
            customSeeds = new ImmutableArray<int>(data.customSeeds)
        });

        public static void RemoveInterruptedDownload(DownloadImagesData data) => RemoveInterruptedDownload(new InterruptedDownloadData
            {
                asset = data.asset,
                modality = data.modality,
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
