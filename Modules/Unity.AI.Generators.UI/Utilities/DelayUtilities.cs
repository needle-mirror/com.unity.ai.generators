using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AI.Generators.UI.Utilities
{
    static class DelayUtilities
    {
        const int k_DefaultDelayMs = 2000;
        internal const string defaultDelayCategory = "default";
        static readonly Dictionary<string, CategoryData> k_CategoryData = new();

        class ScheduledTask
        {
            public TaskCompletionSource<object> TaskCompletionSource { get; }
            public int Priority { get; }
            public CancellationToken CancellationToken { get; }
            public DateTime EnqueueTime { get; }

            public ScheduledTask(int priority, CancellationToken cancellationToken)
            {
                TaskCompletionSource = new TaskCompletionSource<object>();
                Priority = priority;
                CancellationToken = cancellationToken;
                EnqueueTime = DateTime.UtcNow;
            }
        }

        class CategoryData
        {
            public List<ScheduledTask> PendingTasks { get; } = new();
            public DateTime LastExecutionTime { get; set; } = DateTime.MinValue;
            public bool IsProcessing { get; set; }
        }

        static readonly Comparison<ScheduledTask> k_ScheduledTaskComparer = (a, b) =>
        {
            var priorityComparison = b.Priority.CompareTo(a.Priority); // Higher priority first
            return priorityComparison != 0 ? priorityComparison : a.EnqueueTime.CompareTo(b.EnqueueTime); // FIFO for same priority
        };

        public static async Task Delay(CancellationToken token)
        {
            await Delay(defaultDelayCategory, 0, token);
        }

        public static async Task Delay(string category, CancellationToken token)
        {
            await Delay(category, 0, token);
        }

        public static async Task Delay(int priority, CancellationToken token)
        {
            await Delay(defaultDelayCategory, priority, token);
        }

        public static async Task Delay(string category, int priority, CancellationToken token)
        {
            if (!k_CategoryData.TryGetValue(category, out var categoryData))
            {
                categoryData = new CategoryData();
                k_CategoryData[category] = categoryData;
            }

            var scheduledTask = new ScheduledTask(priority, token);
            categoryData.PendingTasks.Add(scheduledTask);
            categoryData.PendingTasks.Sort(k_ScheduledTaskComparer);

            if (!categoryData.IsProcessing)
            {
                categoryData.IsProcessing = true;
                _ = ProcessCategoryQueue(categoryData);
            }

            await scheduledTask.TaskCompletionSource.Task;
            token.ThrowIfCancellationRequested();
        }

        static async Task ProcessCategoryQueue(CategoryData categoryData)
        {
            while (categoryData.PendingTasks.Count > 0)
            {
                // Select the next task to execute (assumed highest priority at this moment)
                var nextTask = categoryData.PendingTasks[0];

                var now = DateTime.UtcNow;
                var timeSinceLastExecution = Math.Min(k_DefaultDelayMs, (now - categoryData.LastExecutionTime).TotalMilliseconds);
                var delay = Math.Max(0, k_DefaultDelayMs - timeSinceLastExecution);

                try
                {
                    // Enforce the minimum delay between task executions
                    await Task.Delay((int)delay, nextTask.CancellationToken);
                }
                catch (TaskCanceledException)
                {
                    nextTask.TaskCompletionSource.TrySetCanceled(nextTask.CancellationToken);
                    categoryData.PendingTasks.Remove(nextTask);
                    continue;
                }

                // After the delay, new tasks may have been added, including tasks with higher priority
                // Re-sort the pending tasks to ensure the highest-priority task is selected
                categoryData.PendingTasks.Sort(k_ScheduledTaskComparer);
                nextTask = categoryData.PendingTasks[0];

                categoryData.LastExecutionTime = DateTime.UtcNow;

                // Complete the task
                if (nextTask.CancellationToken.IsCancellationRequested)
                    nextTask.TaskCompletionSource.TrySetCanceled(nextTask.CancellationToken);
                else
                    nextTask.TaskCompletionSource.TrySetResult(null);

                // Remove the executed task from the pending list
                categoryData.PendingTasks.Remove(nextTask);
            }

            categoryData.IsProcessing = false;
        }
    }
}
