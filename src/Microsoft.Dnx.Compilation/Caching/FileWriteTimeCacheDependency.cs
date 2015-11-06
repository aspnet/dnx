using System;
using System.IO;
using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.Caching
{
    public class FileWriteTimeCacheDependency : ICacheDependency
    {
        private readonly string _path;
        private readonly DateTime _lastWriteTime;

        public FileWriteTimeCacheDependency(string path)
        {
            _path = path;
            _lastWriteTime = File.GetLastWriteTime(path);
        }

        public bool HasChanged
        {
            get
            {
                return _lastWriteTime < File.GetLastWriteTime(_path);
            }
        }

        public override string ToString()
        {
            return _path;
        }

        public override bool Equals(object obj)
        {
            var token = obj as FileWriteTimeCacheDependency;
            return token != null && token._path.Equals(_path, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return _path.GetHashCode();
        }
    }
}