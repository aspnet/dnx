using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Loader;

namespace OwinHost2
{
    public class Watcher : IFileWatcher
    {
        private readonly string _path;
        private readonly HashSet<string> _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly FileSystemWatcher _watcher;

        public Watcher(string path)
        {
            _path = path;
            _watcher = new FileSystemWatcher(path);
        }

        public void Watch(string path)
        {
            _paths.Add(path);    
        }


        private void Suicide(string path)
        {
            _watcher.EnableRaisingEvents = true;
            _watcher.IncludeSubdirectories = true;

            _watcher.Changed += OnWatcherChanged;
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            if (_paths.Contains(e.FullPath))
            {
                Trace.TraceInformation("Change detected in {0}", e.Name);

                Environment.Exit(250);
            }
        }
    }
}
