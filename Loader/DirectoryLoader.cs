using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public IEnumerable<Dependency> GetDependencies(string name, SemanticVersion version)
        {
            string path = Path.Combine(_path, name + ".dll");

            if (File.Exists(path))
            {
                var assembly = Assembly.LoadFile(path);

                return assembly.GetReferencedAssemblies()
                               .Where(an => !IsFrameworkAssembly(an))
                               .Select(an => new Dependency
                               {
                                   Name = an.Name,
                                   Version = new SemanticVersion(an.Version)
                               });
            }

            return null;
        }

        private bool IsFrameworkAssembly(AssemblyName an)
        {
            return an.FullName.EndsWith("b77a5c561934e089", StringComparison.Ordinal);
        }

        public void Initialize(IEnumerable<Dependency> dependencies)
        {

        }
    }
}
