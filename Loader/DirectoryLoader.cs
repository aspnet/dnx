using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;

namespace Loader
{
    public class DirectoryLoader : IAssemblyLoader, IDependencyResolver
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

        public IEnumerable<Dependency> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            string path = Path.Combine(_path, name + ".dll");

            if (File.Exists(path))
            {
                return Enumerable.Empty<Dependency>();
            }

            return null;
        }

        public void Initialize(IEnumerable<Dependency> dependencies, FrameworkName frameworkName)
        {

        }
    }
}
