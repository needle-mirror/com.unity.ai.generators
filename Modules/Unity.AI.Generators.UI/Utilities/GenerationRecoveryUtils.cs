using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    /// <summary>
    /// Base class providing common functionality for generation recovery utilities across different generator modules
    /// </summary>
    static class GenerationRecoveryUtils
    {
        public static string sessionId = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Loads interrupted downloads from a JSON file.
        /// </summary>
        /// <typeparam name="T">Type of dictionary to load</typeparam>
        /// <param name="filePath">Path to the JSON file</param>
        /// <returns>The loaded dictionary or a new instance if loading fails</returns>
        public static T LoadInterruptedDownloads<T>(string filePath) where T : new()
        {
            if (!File.Exists(filePath))
            {
                return new T();
            }

            try
            {
                var json = FileIO.ReadAllText(filePath);
                var result = JsonUtility.FromJson<T>(json);
                if (result == null)
                {
                    Debug.LogWarning($"Failed to parse interrupted downloads file at {filePath}. Creating a new dictionary.");
                    return new T();
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error loading interrupted downloads from {filePath}: {ex.Message}");
                return new T();
            }
        }

        /// <summary>
        /// Saves interrupted downloads to a JSON file.
        /// </summary>
        /// <typeparam name="T">Type of dictionary to save</typeparam>
        /// <param name="data">Data to save</param>
        /// <param name="filePath">Path to the JSON file</param>
        public static void SaveInterruptedDownloads<T>(T data, string filePath)
        {
            var json = JsonUtility.ToJson(data, true);
            var directory = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            FileIO.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Gets the list for a specific environment from the dictionary, creating it if it doesn't exist.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <param name="environment">The environment key</param>
        /// <returns>The list for the specified environment</returns>
        public static List<TData> GetOrCreateListForEnvironment<TData>(
            this IDictionary<string, List<TData>> dictionary,
            string environment)
        {
            if (string.IsNullOrEmpty(environment))
                return new List<TData>();

            if (!dictionary.TryGetValue(environment, out var list) || list == null)
            {
                list = new List<TData>();
                dictionary[environment] = list;
            }

            return list;
        }

        /// <summary>
        /// Adds an interrupted download to the specified environment if it doesn't already exist.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <param name="environment">The environment key</param>
        /// <param name="data">The data to add</param>
        /// <param name="areEqual">Function to compare two data objects for equality</param>
        /// <returns>True if the item was added, false if it already existed</returns>
        public static bool AddInterruptedDownload<TData>(
            this IDictionary<string, List<TData>> dictionary,
            string environment,
            TData data,
            Func<TData, TData, bool> areEqual)
        {
            if (string.IsNullOrEmpty(environment))
                return false;

            var list = GetOrCreateListForEnvironment(dictionary, environment);

            if (list.Any(existing => existing != null && areEqual(existing, data)))
                return false;

            list.Add(data);
            return true;
        }

        /// <summary>
        /// Removes interrupted downloads that match the specified criteria.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <param name="environment">The environment key</param>
        /// <param name="predicate">Function to determine which items to remove</param>
        /// <returns>The number of items removed</returns>
        public static int RemoveInterruptedDownload<TData>(
            this IDictionary<string, List<TData>> dictionary,
            string environment,
            Predicate<TData> predicate)
        {
            if (string.IsNullOrEmpty(environment))
                return 0;

            if (!dictionary.TryGetValue(environment, out var list))
                return 0;

            return list.RemoveAll(predicate);
        }

        /// <summary>
        /// Gets interrupted downloads for a specific asset.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <param name="environment">The environment key</param>
        /// <param name="assetFilter">Function to filter items by asset</param>
        /// <returns>The filtered list of interrupted downloads</returns>
        public static List<TData> GetInterruptedDownloads<TData>(
            this IDictionary<string, List<TData>> dictionary,
            string environment,
            Func<TData, bool> assetFilter)
        {
            if (string.IsNullOrEmpty(environment))
                return new List<TData>();

            if (dictionary.TryGetValue(environment, out var list))
                return list.Where(data => data != null && assetFilter(data)).ToList();

            return new List<TData>();
        }

        /// <summary>
        /// Cleans up null entries in the interrupted downloads dictionary.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <returns>True if changes were made</returns>
        public static bool CleanupNullEntries<TData>(this IDictionary<string, List<TData>> dictionary)
        {
            var hasChanges = false;
            var keysToCheck = dictionary.Keys.ToList();
            
            foreach (var key in keysToCheck)
            {
                var list = dictionary[key];
                if (list == null)
                {
                    Debug.LogWarning($"Found null list for environment '{key}'. Replacing with empty list.");
                    dictionary[key] = new List<TData>();
                    hasChanges = true;
                    continue;
                }

                var nullCount = list.RemoveAll(item => item == null);
                if (nullCount <= 0)
                    continue;
                
                Debug.LogWarning($"Removed {nullCount} null entries from environment '{key}'.");
                hasChanges = true;
            }

            return hasChanges;
        }

        /// <summary>
        /// Gets the count of interrupted downloads for an asset.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <param name="environment">The environment key</param>
        /// <param name="assetFilter">Function to filter items by asset</param>
        /// <returns>The number of interrupted downloads for the asset</returns>
        public static int GetInterruptedDownloadCount<TData>(
            this IDictionary<string, List<TData>> dictionary,
            string environment,
            Func<TData, bool> assetFilter)
        {
            if (string.IsNullOrEmpty(environment))
                return 0;

            if (dictionary.TryGetValue(environment, out var list))
                return list.Count(data => data != null && assetFilter(data));

            return 0;
        }

        /// <summary>
        /// Clears all interrupted downloads from the dictionary.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        public static void ClearAllInterruptedDownloads<TData>(this IDictionary<string, List<TData>> dictionary)
        {
            dictionary.Clear();
        }

        /// <summary>
        /// Clears interrupted downloads for a specific environment.
        /// </summary>
        /// <typeparam name="TData">Type of the interrupted download data</typeparam>
        /// <param name="dictionary">Dictionary storing environment-specific lists</param>
        /// <param name="environment">The environment key</param>
        /// <returns>True if the environment existed and was cleared</returns>
        public static bool ClearInterruptedDownloadsForEnvironment<TData>(
            this IDictionary<string, List<TData>> dictionary,
            string environment)
        {
            if (string.IsNullOrEmpty(environment))
                return false;

            if (!dictionary.ContainsKey(environment))
                return false;

            dictionary[environment] = new List<TData>();
            return true;
        }
    }
}