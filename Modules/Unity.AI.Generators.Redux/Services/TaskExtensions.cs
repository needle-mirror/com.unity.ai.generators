using System;
using System.Threading.Tasks;

namespace Unity.AI.Generators.Redux.Services
{
    static class TaskExtensions
    {
        /// <summary>
        /// Ensure continuation on the main thread.
        /// </summary>
        public static Task UnityContinueWith<TResult>(this Task<TResult> task, Action<Task<TResult>> continuationAction) =>
            task.ContinueWith(continuationAction, TaskScheduler.FromCurrentSynchronizationContext());
        public static Task<TResult> UnityContinueWith<TResult>(this Task task, Func<Task, TResult> continuationAction) =>
            task.ContinueWith(continuationAction, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
