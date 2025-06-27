using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Toolkit;
using UnityEditor;

namespace Unity.AI.Generators.UI.Utilities
{
    class EditorFocusScope : IDisposable
    {
        static bool s_IsFocused = true;
        static int s_ActiveInstances = 0;
        static string s_ProgressTitle = "Unity AI Processing";
        static string s_ProgressMessage = "Processing in background while editor is unfocused...";
        static float s_ProgressValue = 0.01f;

        static CancellationTokenSource s_BackgroundTaskCancellation;
        static Task s_BackgroundTask;

        [InitializeOnLoadMethod]
        static void RegisterFocusChange() => EditorApplication.focusChanged += OnFocusChanged;

        static void OnFocusChanged(bool focus)
        {
            s_IsFocused = focus;

            if (s_IsFocused)
            {
                StopBackgroundTask();
                EditorUtility.ClearProgressBar();
            }
            else if (s_ActiveInstances > 0)
            {
                StartBackgroundTask();
            }
        }

        static void StartBackgroundTask()
        {
            if (s_BackgroundTask != null && !s_BackgroundTask.IsCompleted)
                return;

            s_BackgroundTaskCancellation?.Cancel();
            s_BackgroundTaskCancellation = new CancellationTokenSource();
            var token = s_BackgroundTaskCancellation.Token;

            EditorUtility.DisplayProgressBar(s_ProgressTitle, s_ProgressMessage, s_ProgressValue);

            s_BackgroundTask = EditorTask.Run(async () => {
                while (!token.IsCancellationRequested)
                {
                    await EditorTask.RunOnMainThread(async () => {
                        if (token.IsCancellationRequested)
                            return;
                        EditorUtility.DisplayProgressBar(s_ProgressTitle, s_ProgressMessage, s_ProgressValue);
                        await EditorTask.Delay(50, token);
                    }, token);
                }}, token);
        }

        static void StopBackgroundTask()
        {
            s_BackgroundTaskCancellation?.Cancel();
            s_BackgroundTaskCancellation = null;
            s_BackgroundTask = null;
        }

        /// <summary>
        /// Creates a new editor focus scope that manages focus state and background processing.
        /// </summary>
        public EditorFocusScope()
        {
            s_ActiveInstances++;

            if (!s_IsFocused)
                StartBackgroundTask();
        }

        /// <summary>
        /// Displays a progress bar when the editor is out of focus.
        /// Throws OperationCanceledException if the user cancels the operation.
        /// </summary>
        public static bool ShowProgressOrCancelIfUnfocused(string title, string message, float progress)
        {
            if (s_IsFocused)
                return false;

            s_ProgressTitle = title;
            s_ProgressMessage = message;
            s_ProgressValue = progress;

            EditorUtility.DisplayProgressBar(s_ProgressTitle, s_ProgressMessage, progress);
            return false;
        }

        public void Dispose()
        {
            s_ActiveInstances--;
            if (s_ActiveInstances > 0)
                return;

            s_ActiveInstances = 0;
            if (!s_IsFocused)
                return;

            StopBackgroundTask();
            EditorUtility.ClearProgressBar();
        }
    }
}
