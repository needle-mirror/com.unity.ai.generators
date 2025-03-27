using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Animate.Services.Utilities
{
    [FilePath("UserSettings/AI.Animate/AnimationClipDatabase.asset", FilePathAttribute.Location.ProjectFolder)]
    class AnimationClipDatabase : ScriptableSingleton<AnimationClipDatabase>
    {
        [SerializeField]
        internal List<AnimationClipDatabaseItem> cachedClips = new();

        readonly Dictionary<string, AnimationClipDatabaseItem> m_ClipMap = new();

        const int k_MaxLruCacheSize = 50;

        bool m_Dirty;

        void OnEnable()
        {
            m_ClipMap.Clear();
            foreach (var entry in cachedClips)
            {
                if (!string.IsNullOrEmpty(entry.uri))
                    m_ClipMap[entry.uri] = entry;
            }
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>
        /// Save the database when the editor quits or the singleton is unloaded.
        /// </summary>
        void OnDisable()
        {
            Save();
            EditorApplication.quitting -= OnEditorQuitting;
        }

        /// <summary>
        /// Force a final save.
        /// </summary>
        void OnEditorQuitting() => Save();

        void Save()
        {
            if (!m_Dirty)
                return;

            try
            {
                var canceled = EditorUtility.DisplayCancelableProgressBar("AI.Animate", "Saving AI.Animate database...", 0.5f);
                if (!canceled)
                    Save(true);
            }
            finally
            {
                m_Dirty = false;
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Adds the AnimationClip to the cache using the provided URI.
        /// </summary>
        public bool AddClip(string uri, AnimationClip clip)
        {
            if (!clip)
                return false;

            m_Dirty = true;

            var now = EditorApplication.timeSinceStartup;

            if (m_ClipMap.TryGetValue(uri, out var existing))
            {
                existing.lastUsedTimestamp = now;
                // Don't save immediately.
                return true;
            }

            if (cachedClips.Count >= k_MaxLruCacheSize)
                EvictOldClips();

            var serializedData = AnimationClipDatabaseUtils.SerializeAnimationClip(clip);
            if (serializedData.data == null)
            {
                Debug.LogError("Failed to serialize AnimationClip.");
                return false;
            }

            var cached = new AnimationClipDatabaseItem
            {
                uri = uri,
                fileName = serializedData.fileName,
                clipData = serializedData.data,
                lastUsedTimestamp = now
            };

            cachedClips.Add(cached);
            m_ClipMap.Add(uri, cached);

            // Instead of saving now, wait until editor close or OnDisable.
            return true;
        }

        public bool AddClip(Uri uri, AnimationClip clip) => AddClip(uri.ToString(), clip);

        /// <summary>
        /// Returns the AnimationClip for the given URI, or null if it doesn’t exist.
        /// </summary>
        public AnimationClip GetClip(string uri)
        {
            if (!m_ClipMap.TryGetValue(uri, out var cached))
                return null;

            m_Dirty = true;

            cached.lastUsedTimestamp = EditorApplication.timeSinceStartup;

            // Instead of immediate save, rely on the OnDisable (or quitting) save.
            return AnimationClipDatabaseUtils.DeserializeAnimationClip(cached.fileName, cached.clipData);
        }

        public AnimationClip GetClip(Uri uri) => GetClip(uri.ToString());

        /// <summary>
        /// Simply verifies if a clip exists for this URI.
        /// </summary>
        public bool Peek(string uri) => m_ClipMap.ContainsKey(uri);

        public bool Peek(Uri uri) => Peek(uri.ToString());

        /// <summary>
        /// Evicts the least recently used items until the cache is below the limit.
        /// </summary>
        void EvictOldClips()
        {
            cachedClips.Sort((a, b) => a.lastUsedTimestamp.CompareTo(b.lastUsedTimestamp));
            while (cachedClips.Count >= k_MaxLruCacheSize)
            {
                m_Dirty = true;

                var toRemove = cachedClips[0];
                cachedClips.RemoveAt(0);
                m_ClipMap.Remove(toRemove.uri);
            }
        }
    }
}
