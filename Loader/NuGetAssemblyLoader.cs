using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NuGet;

namespace Loader
{
    public class NuGetAssemblyLoader : IAssemblyLoader
    {
        private readonly LocalPackageRepository _repository;

        public NuGetAssemblyLoader(string packagesDirectory)
        {
            _repository = new LocalPackageRepository(packagesDirectory);
        }

        public Assembly Load(string name)
        {
            var package = _repository.FindPackage(name);

            if (package == null)
            {
                return null;
            }

            var path = _repository.PathResolver.GetInstallPath(package);

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
                        return Assembly.LoadFile(fileName);
                    }
                }
            }

            return null;
        }
    }
}
