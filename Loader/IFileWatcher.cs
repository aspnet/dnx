
using System;

namespace Loader
{
    public interface IFileWatcher
    {
        bool Watch(string path);

        event Action OnChanged;
    }
}
