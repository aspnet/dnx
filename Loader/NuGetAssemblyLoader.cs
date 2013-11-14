using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NuGet;

namespace Loader
{
    public class NuGetAssemblyLoader : IAssemblyLoader
    {
        private readonly LocalPackageRepository _repository;
        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        public NuGetAssemblyLoader(string packagesDirectory)
        {
            _repository = new LocalPackageRepository(packagesDirectory);
        }

        public Assembly Load(LoadOptions options)
        {
            string name = options.AssemblyName;

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            var package = FindPackage(name);

            if (package == null)
            {
                return null;
            }

            var path = _repository.PathResolver.GetInstallPath(package);

            var results = new List<Assembly>();

            // REVIEW: How do we get the project framework?
            var framework = VersionUtility.ParseFrameworkName("net45");
            IEnumerable<IPackageAssemblyReference> references;
            if (VersionUtility.TryGetCompatibleItems(framework, package.AssemblyReferences, out references))
            {
                foreach (var reference in references)
                {
                    string fileName = Path.Combine(path, reference.Path);
                    if (File.Exists(fileName))
                    {
                        results.Add(Assembly.LoadFile(fileName));
                    }
                }
            }

            foreach (var a in results)
            {
                _cache[a.GetName().Name] = a;
                _cache[a.FullName] = a;
            }

            return results.FirstOrDefault();
        }

        private IPackage FindPackage(string name)
        {
            name = name.ToLowerInvariant();

            return (from p in _repository.GetPackages()
                    where p.Id.ToLower() == name
                    orderby p.Id
                    select p).FirstOrDefault();
        }
    }
}
