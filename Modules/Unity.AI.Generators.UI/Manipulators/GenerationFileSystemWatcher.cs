using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI
{
    class GenerationFileSystemWatcher : Manipulator
    {
        static SynchronizationContext s_UnitySynchronizationContext;

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
            s_UnitySynchronizationContext = SynchronizationContext.Current;
        }

        void OnChanged(object sender, FileSystemEventArgs e) => s_UnitySynchronizationContext.Post(_ => Rebuild(), null);

        void OnCreated(object sender, FileSystemEventArgs e) => s_UnitySynchronizationContext.Post(_ => Rebuild(), null);

        void OnDeleted(object sender, FileSystemEventArgs e) => s_UnitySynchronizationContext.Post(_ => Rebuild(), null);

        void OnRenamed(object sender, RenamedEventArgs e) => s_UnitySynchronizationContext.Post(_ => Rebuild(), null);

        async void Rebuild(bool immediately = false)
        {
            m_RebuildCancellationTokenSource?.Cancel();
            m_RebuildCancellationTokenSource?.Dispose();
            m_RebuildCancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (immediately)
                    await Task.Yield(); // otherwise redux blows up
                else
                    await Task.Delay(k_DelayValue, m_RebuildCancellationTokenSource.Token);
                RebuildNow();
            }
            catch (TaskCanceledException)
            {
                // The task was canceled, do nothing
            }
            finally
            {
                m_RebuildCancellationTokenSource?.Dispose();
                m_RebuildCancellationTokenSource = null;
            }
        }

        void RebuildNow()
        {
            if (m_Watcher == null)
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

            m_Watcher?.Dispose();
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

            Rebuild(true);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            try
            {
                m_Watcher?.Dispose();
                m_RebuildCancellationTokenSource?.Cancel();
                m_RebuildCancellationTokenSource?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                m_Watcher = null;
                m_RebuildCancellationTokenSource = null;
            }
        }
    }
}
