
using System;

namespace Microsoft.Net.Runtime.FileSystem
{
    public interface IFileWatcher : IDisposable
    {
        void WatchDirectory(string path, string extension);

        bool WatchFile(string path);

        event Action OnChanged;
    }
}
