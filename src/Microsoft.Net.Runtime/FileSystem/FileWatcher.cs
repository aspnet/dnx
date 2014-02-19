using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Net.Runtime.Services;

namespace Microsoft.Net.Runtime.FileSystem
{
    public class FileWatcher : IFileWatcher, IFileMonitor
    {
        private readonly HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<string>> _directories = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly FileSystemWatcher _watcher;

        internal FileWatcher()
        {

        }

        public FileWatcher(string path)
        {
            _watcher = new FileSystemWatcher(path);
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            _watcher.Changed += OnWatcherChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Deleted += OnWatcherChanged;
            _watcher.Created += OnWatcherChanged;
        }

        public event Action<string> OnChanged;

        public void WatchDirectory(string path, string extension)
        {
            var extensions = _directories.GetOrAdd(path, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            extensions.Add(extension);
        }

        public bool WatchFile(string path)
        {
            return _files.Add(path);
        }
        public void Dispose()
        {
            _watcher.Dispose();
        }

        public bool ReportChange(string newPath, WatcherChangeTypes changeType)
        {
            return ReportChange(oldPath: null, newPath: newPath, changeType: changeType);
        }

        public bool ReportChange(string oldPath, string newPath, WatcherChangeTypes changeType)
        {
            if (HasChanged(oldPath, newPath, changeType))
            {
                if (oldPath != null)
                {
                    Trace.TraceInformation("{0} -> {1}", oldPath, newPath);
                }
                else
                {
                    Trace.TraceInformation("{0} -> {1}", changeType, newPath);
                }

                if (OnChanged != null)
                {
                    OnChanged(oldPath ?? newPath);
                }

                return true;
            }

            return false;
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            ReportChange(e.OldFullPath, e.FullPath, e.ChangeType);
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            ReportChange(e.FullPath, e.ChangeType);
        }

        private bool HasChanged(string oldPath, string newPath, WatcherChangeTypes changeType)
        {
            // File changes
            if (_files.Contains(newPath) ||
                (oldPath != null && _files.Contains(oldPath)))
            {
                return true;
            }

            HashSet<string> extensions;
            if (_directories.TryGetValue(newPath, out extensions) ||
                _directories.TryGetValue(Path.GetDirectoryName(newPath), out extensions))
            {
                string extension = Path.GetExtension(newPath);

                if (String.IsNullOrEmpty(extension))
                {
                    // Assume it's a directory
                    if (changeType == WatcherChangeTypes.Created ||
                        changeType == WatcherChangeTypes.Renamed)
                    {
                        foreach (var e in extensions)
                        {
                            WatchDirectory(newPath, e);
                        }
                    }
                    else if (changeType == WatcherChangeTypes.Deleted)
                    {
                        return true;
                    }

                    // Ignore anything else
                    return false;
                }

                return extensions.Contains(extension);
            }

            return false;
        }
    }

    public sealed class NoopWatcher : IFileWatcher, IFileMonitor
    {
        public static readonly NoopWatcher Instance = new NoopWatcher();

        private NoopWatcher()
        {

        }

        public bool WatchFile(string path)
        {
            return true;
        }

        public event Action<string> OnChanged;

        public void WatchDirectory(string path, string extension)
        {
        }

        public void Dispose()
        {
        }
    }
}
