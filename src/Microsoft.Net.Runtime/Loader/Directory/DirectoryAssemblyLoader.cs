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

        public Assembly Load(LoadOptions options)
        {
            string name = options.AssemblyName;

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            string path = Path.Combine(_path, name + ".dll");
            if (System.IO.File.Exists(path))
            {
                assembly = Assembly.LoadFile(path);
            }

            if (assembly != null)
            {
                _cache[name] = assembly;
            }

            return assembly;
        }

        public IEnumerable<PackageReference> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            string path = Path.Combine(_path, name + ".dll");

            if (System.IO.File.Exists(path))
            {
                return Enumerable.Empty<PackageReference>();
            }

            return null;
        }

        public void Initialize(IEnumerable<PackageReference> packages, FrameworkName frameworkName)
        {

        }
    }
}
