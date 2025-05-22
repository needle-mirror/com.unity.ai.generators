using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Toolkit;

namespace Unity.AI.Generators.UI.Utilities
{
    /// <summary>
    /// Provides debouncing functionality for operations that shouldn't be executed too frequently.
    /// </summary>
    static class Debouncer
    {
        static readonly Dictionary<string, CancellationTokenSource> k_TokenSources = new();

        /// <summary>
        /// Executes an action with debouncing.
        /// </summary>
        /// <param name="key">A unique identifier for this debounce operation</param>
        /// <param name="action">The action to execute after the debounce delay</param>
        /// <param name="delayMilliseconds">The debounce delay in milliseconds</param>
        public static async void DebounceAction(string key, Action action, int delayMilliseconds = 250)
        {
            Cancel(key);

            var tokenSource = new CancellationTokenSource();
            k_TokenSources[key] = tokenSource;

            try
            {
                await EditorTask.Delay(delayMilliseconds, tokenSource.Token);
                action();
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                if (k_TokenSources.TryGetValue(key, out var currentTokenSource) &&
                    currentTokenSource == tokenSource)
                {
                    k_TokenSources.Remove(key);
                }
                tokenSource.Dispose();
            }
        }

        /// <summary>
        /// Cancels a specific debounce operation.
        /// </summary>
        /// <param name="key">The key of the operation to cancel</param>
        public static void Cancel(string key)
        {
            if (k_TokenSources.TryGetValue(key, out var tokenSource))
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
                k_TokenSources.Remove(key);
            }
        }
    }
}
