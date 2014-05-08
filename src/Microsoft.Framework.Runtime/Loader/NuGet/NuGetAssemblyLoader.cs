
namespace Microsoft.Framework.Runtime.Loader.NuGet
{
    public class NuGetAssemblyLoader : IAssemblyLoader
    {
        private readonly NuGetDependencyResolver _dependencyResolver;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public NuGetAssemblyLoader(IAssemblyLoaderEngine loaderEngine, NuGetDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
            _loaderEngine = loaderEngine;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string path;
            if (_dependencyResolver.PackageAssemblyPaths.TryGetValue(loadContext.AssemblyName, out path))
            {
                return new AssemblyLoadResult(_loaderEngine.LoadFile(path));
            }

            return null;
        }
    }
}
