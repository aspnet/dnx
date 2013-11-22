
using System;

namespace Loader
{
    public interface IFileWatcher : IDisposable
    {
        void WatchDirectory(string path, string extension);

        bool WatchFile(string path);

        event Action OnChanged;
    }
}
