
using System;

namespace Microsoft.Framework.Runtime.FileSystem
{
    public interface IFileWatcher : IDisposable
    {
        void WatchDirectory(string path, string extension);

        bool WatchFile(string path);
    }
}
