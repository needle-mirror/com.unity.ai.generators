using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    [Serializable]
    class AssetUndoManager<T> : ScriptableObject, ISerializationCallbackReceiver
    {
        // mostly used for undo state comparison
        public string tempFilePath;

        // actually used to restore the state
        public SerializableDictionary<string, string> tempFilePaths = new();
        public AssetReference asset = new();
        public T selectedResult = default;

        [NonSerialized]
        List<string> m_AllTempFilePaths = new();

        [NonSerialized]
        string m_PreviousTempFilePath;

        /// <summary>
        /// Invoked when restoration has occurred.
        /// Specializations can subscribe to execute additional logic.
        /// </summary>
        protected Action<AssetReference, T> m_OnRestoreAsset;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (DomainReloadUtilities.WasDomainReloaded)
            {
                tempFilePath = null;
                tempFilePaths = new();
                asset = new();
                selectedResult = default;
                m_AllTempFilePaths = new();
                m_PreviousTempFilePath = null;
                return;
            }

            // Check if the data has changed due to undo/redo
            if (!string.IsNullOrEmpty(m_PreviousTempFilePath) && string.Equals(tempFilePath, m_PreviousTempFilePath, StringComparison.Ordinal))
                return;

            // Data has changed due to undo/redo; perform file restoration
            RestoreAsset(new AssetReference { guid = asset.guid }, tempFilePaths, Clone(selectedResult));

            // Update the snapshot of the previous state
            m_PreviousTempFilePath = tempFilePath;
        }

       void RestoreAsset(AssetReference assetToRestore, Dictionary<string, string> tempFilesToRestore, T resultToRestore)
        {
            foreach (var (assetPathToRestore, tempFilePathToRestore) in tempFilesToRestore)
            {
                if (!File.Exists(tempFilePathToRestore) || !File.Exists(assetPathToRestore))
                    continue;

                File.Copy(tempFilePathToRestore, assetPathToRestore, overwrite: true);
                AssetReferenceExtensions.ImportAsset(assetPathToRestore);
            }

            try { m_OnRestoreAsset?.Invoke(assetToRestore, resultToRestore); }
            catch { /**/ }
        }

        public void BeginRecord(AssetReference assetReference)
        {
            if (!File.Exists(tempFilePath))
                return;
            // update the previous file in case there were external edits
            File.Copy(assetReference.GetPath(), tempFilePath, overwrite: true);
        }

        public void EndRecord(AssetReference assetReference, T result, bool force = false)
        {
            Undo.RecordObject(this,
                !string.IsNullOrEmpty(result?.ToString())
                    ? $"Replace {Path.GetFileNameWithoutExtension(assetReference.GetPath())} with {Path.GetFileNameWithoutExtension(result.ToString())}"
                    : $"Load {Path.GetFileNameWithoutExtension(assetReference.GetPath())}"
            );

            asset = new AssetReference { guid = assetReference.guid };

            tempFilePath = UndoUtilities.GetTempFileName();
            tempFilePaths.Clear();
            tempFilePaths.Add(asset.GetPath(), tempFilePath);
            m_AllTempFilePaths.Add(tempFilePath);
            File.Copy(asset.GetPath(), tempFilePath, overwrite: true);

            selectedResult = Clone(result);

            EditorUtility.SetDirty(this);

            if (force)
                Undo.IncrementCurrentGroup();
        }

        static T Clone(T original)
        {
            if (original == null)
                return default;
            var json = JsonConvert.SerializeObject(original, Formatting.None);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
