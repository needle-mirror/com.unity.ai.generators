using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
    class EditorFocusScope : IDisposable
    {
        static bool s_IsFocused = true;

        [InitializeOnLoadMethod]
        static void RegisterFocusChange() => EditorApplication.focusChanged += OnFocusChanged;

        static void OnFocusChanged(bool focus)
        {
            s_IsFocused = focus;
            if (s_IsFocused)
                EditorUtility.ClearProgressBar();
        }

        readonly bool m_OriginalRunInBackground;
        readonly bool m_OnlyWhenPlayingPaused;

        /// <summary>
        /// Creates a new editor focus scope that manages focus state and background processing.
        /// </summary>
        public EditorFocusScope(bool onlyWhenPlayingPaused = false)
        {
            m_OnlyWhenPlayingPaused = onlyWhenPlayingPaused;
            m_OriginalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        /// <summary>
        /// Displays a progress bar when the editor is out of focus.
        /// Throws OperationCanceledException if the user cancels the operation.
        /// </summary>
        public bool ShowProgressOrCancelIfUnfocused(string title, string message, float progress)
        {
            if (s_IsFocused)
                return false;

            if (m_OnlyWhenPlayingPaused && !EditorTask.isPlayingPaused)
                return false;

            return EditorUtility.DisplayCancelableProgressBar(title, message, progress);
        }

        /// <summary>
        /// Forces a player update if not in play mode and yields until the next editor update tick.
        /// </summary>
        public async Task UpdatePlayerAsync()
        {
            if (!Application.isPlaying)
                EditorApplication.QueuePlayerLoopUpdate();

            await EditorTask.Yield();
        }

        public void Dispose()
        {
            EditorUtility.ClearProgressBar();
            Application.runInBackground = m_OriginalRunInBackground;
        }
    }

    static class EditorFocus
    {
        public static async Task UpdateEditorAsync(string message, TimeSpan duration)
        {
            await EditorTask.Yield();

            using var focusScope = new EditorFocusScope();

            var timer = Stopwatch.StartNew();
            var focusScopeProgress = 1f;
            while (timer.Elapsed < duration)
            {
                if (focusScope.ShowProgressOrCancelIfUnfocused("Editor background worker", message, 1 - (focusScopeProgress /= 2)))
                    throw new OperationCanceledException();

                await EditorTask.Yield();
            }
        }
    }
}
