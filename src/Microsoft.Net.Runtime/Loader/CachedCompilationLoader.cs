using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public class CachedCompilationLoader : IAssemblyLoader, IPackageLoader
    {
        private readonly IProjectResolver _resolver;

        public CachedCompilationLoader(IProjectResolver resolver)
        {
            _resolver = resolver;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            // If there's an output path then skip all loading of cached compilations
            // This is to avoid using the cached version when trying to produce a new one.
            // Also skip if we're not loading assemblies
            if (loadContext.OutputPath != null)
            {
                return null;
            }

            string name = loadContext.AssemblyName;

            Project project;
            if (!_resolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(loadContext.TargetFramework);
            string cachedFile = Path.Combine(path, "bin", targetFrameworkFolder, name + ".dll");

            if (File.Exists(cachedFile))
            {
                Trace.TraceInformation("[{0}]: Loading '{1}' from {2}.", GetType().Name, name, cachedFile);

                return new AssemblyLoadResult(Assembly.LoadFile(cachedFile));
            }

            return null;
        }

        public DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            Project project;
            if (!_resolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(frameworkName);
            string cachedFile = Path.Combine(path, "bin", targetFrameworkFolder, name + ".dll");

            if (File.Exists(cachedFile) && 
                version == project.Version || 
                version == null)
            {
                var config = project.GetTargetFrameworkConfiguration(frameworkName);

                return new DependencyDescription
                {
                    Identity = new Dependency { Name = project.Name, Version = project.Version },
                    Dependencies = project.Dependencies.Concat(config.Dependencies),
                };
            }

            return null;
        }

        public void Initialize(IEnumerable<DependencyDescription> packages, FrameworkName frameworkName)
        {

        }
    }
}
