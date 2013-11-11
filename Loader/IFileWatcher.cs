
using System;

namespace Loader
{
    public interface IFileWatcher
    {
        void WatchDirectory(string path, string extension);

        bool WatchFile(string path);

        event Action OnChanged;
    }
}
