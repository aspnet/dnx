using System;

namespace Microsoft.Dnx.Compilation
{
    // We don't usually seal classes but this is literally designed to be single-instance, non-overridable
    public sealed class NoopWatcher : IFileWatcher
    {
        public static readonly NoopWatcher Instance = new NoopWatcher();

        private NoopWatcher()
        {
        }

        public bool WatchFile(string path)
        {
            return true;
        }

        // Suppressing warning CS0067: The event is never used
#pragma warning disable 0067

        public event Action<string> OnChanged;

#pragma warning restore 0067

        public void WatchDirectory(string path, string extension)
        {
        }

        public void Dispose()
        {
        }

        public void WatchProject(string path)
        {
        }
    }
}
