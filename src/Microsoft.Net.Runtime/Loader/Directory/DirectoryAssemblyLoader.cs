using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader.Directory
{
    public class DirectoryAssemblyLoader : IAssemblyLoader, IPackageLoader
    {
        private readonly string _path;

        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>();

        public DirectoryAssemblyLoader(string path)
        {
            _path = path;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string name = loadContext.AssemblyName;

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return new AssemblyLoadResult(assembly);
            }

            string path = Path.Combine(_path, name + ".dll");
            if (File.Exists(path))
            {
                assembly = Assembly.LoadFile(path);
            }

            if (assembly != null)
            {
                _cache[name] = assembly;

                return new AssemblyLoadResult(assembly);
            }

            return null;
        }

        public PackageDetails GetDetails(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            string path = Path.Combine(_path, name + ".dll");

            if (File.Exists(path))
            {
                return new PackageDetails
                {
                    Identity = new PackageReference { Name = name, Version = version },
                    Dependencies = Enumerable.Empty<PackageReference>()
                };
            }

            return null;
        }

        public void Initialize(IEnumerable<PackageReference> packages, FrameworkName frameworkName)
        {

        }
    }
}
