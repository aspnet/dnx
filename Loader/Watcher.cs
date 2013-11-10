using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Loader
{
    public class Watcher : IFileWatcher
    {
        private readonly string _path;
        private readonly HashSet<string> _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly FileSystemWatcher _watcher;

        public static readonly IFileWatcher Noop = new NoopWatcher();

        public Watcher(string path)
        {
            _path = path;
            _watcher = new FileSystemWatcher(path);
            _watcher.EnableRaisingEvents = true;
            _watcher.IncludeSubdirectories = true;

            _watcher.Changed += OnWatcherChanged;
        }

        public event Action OnChanged;

        public bool Watch(string path)
        {
            return _paths.Add(path);
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            if (_paths.Contains(e.FullPath))
            {
                Trace.TraceInformation("{0} in {1}", e.ChangeType, e.FullPath);

                if (OnChanged != null)
                {
                    OnChanged();
                }
            }
        }

        private class NoopWatcher : IFileWatcher
        {
            public bool Watch(string path)
            {
                return true;
            }

            public event Action OnChanged;
        }
    }
}
