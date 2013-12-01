using System.Diagnostics;
using System.IO;
using System.Reflection;
using NuGet;

namespace Microsoft.Net.Runtime.Loader.Roslyn
{
    public class CachedCompilationLoader : IAssemblyLoader
    {
        private readonly string _rootPath;

        public CachedCompilationLoader(string rootPath)
        {
            _rootPath = rootPath;
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
            string path = Path.Combine(_rootPath, name);

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(loadContext.TargetFramework);
            string cachedFile = Path.Combine(path, "bin", targetFrameworkFolder, name + ".dll");

            if (File.Exists(cachedFile))
            {
                Trace.TraceInformation("[{0}]: Loading '{1}' from {2}.", GetType().Name, name, cachedFile);

                return new AssemblyLoadResult(Assembly.LoadFile(cachedFile));
            }

            return null;
        }
    }
}
