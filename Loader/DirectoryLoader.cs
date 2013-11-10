using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Loader
{
    public class DirectoryLoader : IAssemblyLoader
    {
        private readonly string _path;

        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>();

        public DirectoryLoader(string path)
        {
            _path = path;
        }

        public Assembly Load(LoadOptions options)
        {
            string name = options.AssemblyName;

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            string path = Path.Combine(_path, name + ".dll");
            if (File.Exists(path))
            {
                assembly = Assembly.LoadFile(path);
            }

            if (assembly != null)
            {
                _cache[name] = assembly;
            }

            return assembly;
        }
    }
}
