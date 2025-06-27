using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using Unity.AI.Toolkit;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI
{
    class GenerationFileSystemWatcher : Manipulator
    {
        readonly IEnumerable<string> m_Suffixes;
        readonly string m_WatchPath;
        FileSystemWatcher m_Watcher;
        CancellationTokenSource m_RebuildCancellationTokenSource;
        readonly Action<IEnumerable<string>> m_OnRebuild;

        const int k_DelayValue = 1000; // delay in milliseconds

        public GenerationFileSystemWatcher(AssetReference asset, IEnumerable<string> suffixes, Action<IEnumerable<string>> onRebuild)
        {
            m_Suffixes = suffixes;
            m_WatchPath = asset.GetGeneratedAssetsPath();
            m_OnRebuild = onRebuild;
        }

        async Task ScheduleRebuildOnMainThread()
        {
            await EditorThread.EnsureMainThreadAsync();
            _ = Rebuild();
        }

        void OnChanged(object sender, FileSystemEventArgs e) => _ = ScheduleRebuildOnMainThread();
        void OnCreated(object sender, FileSystemEventArgs e) => _ = ScheduleRebuildOnMainThread();
        void OnDeleted(object sender, FileSystemEventArgs e) => _ = ScheduleRebuildOnMainThread();
        void OnRenamed(object sender, RenamedEventArgs e) => _ = ScheduleRebuildOnMainThread();

        async Task Rebuild(bool immediately = false)
        {
            m_RebuildCancellationTokenSource?.Cancel();
            m_RebuildCancellationTokenSource?.Dispose();
            m_RebuildCancellationTokenSource = new CancellationTokenSource();

            var token = m_RebuildCancellationTokenSource.Token;

            try
            {
                if (immediately)
                    await EditorTask.Yield(); // otherwise redux blows up
                else
                    await EditorTask.Delay(k_DelayValue, token);

                if (token.IsCancellationRequested)
                    return;

                RebuildNow();
            }
            catch (TaskCanceledException)
            {
                // The task was canceled (either by new event or during UnregisterCallbacksFromTarget), do nothing
            }
        }

        void RebuildNow()
        {
            if (m_Watcher is not { EnableRaisingEvents: true })
                return;
            try
            {
                var files = Directory.GetFiles(m_Watcher.Path)
                    .Where(file => m_Suffixes.Any(suffix => Path.GetFileName(file).EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToArray();
                m_OnRebuild?.Invoke(files);
            }
            catch (DirectoryNotFoundException)
            {
                m_OnRebuild?.Invoke(Array.Empty<string>());
            }
        }

        protected override void RegisterCallbacksOnTarget()
        {
            Directory.CreateDirectory(m_WatchPath);

            if (m_Watcher != null)
            {
                m_Watcher.EnableRaisingEvents = false;
                m_Watcher.Changed -= OnChanged;
                m_Watcher.Created -= OnCreated;
                m_Watcher.Deleted -= OnDeleted;
                m_Watcher.Renamed -= OnRenamed;
                m_Watcher.Dispose();
            }

            m_Watcher = new FileSystemWatcher
            {
                Path = m_WatchPath,
                NotifyFilter = NotifyFilters.LastWrite
                    | NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                Filter = "*.*"
            };
            m_Watcher.Changed += OnChanged;
            m_Watcher.Created += OnCreated;
            m_Watcher.Deleted += OnDeleted;
            m_Watcher.Renamed += OnRenamed;
            m_Watcher.EnableRaisingEvents = true;

            _ = ScheduleRebuildOnMainThreadForInitial();
        }

        async Task ScheduleRebuildOnMainThreadForInitial()
        {
            await EditorThread.EnsureMainThreadAsync();
            _ = Rebuild(immediately: true);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            if (m_Watcher != null)
            {
                m_Watcher.EnableRaisingEvents = false;
                m_Watcher.Changed -= OnChanged;
                m_Watcher.Created -= OnCreated;
                m_Watcher.Deleted -= OnDeleted;
                m_Watcher.Renamed -= OnRenamed;
                m_Watcher.Dispose();
                m_Watcher = null;
            }

            m_RebuildCancellationTokenSource?.Cancel();
            m_RebuildCancellationTokenSource?.Dispose();
            m_RebuildCancellationTokenSource = null;
        }
    }
}
