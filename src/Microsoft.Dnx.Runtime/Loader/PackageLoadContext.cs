using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class PackageLoadContext : LoadContext
    {
        private readonly PackageAssemblyLoader _packageAssemblyLoader;
        private readonly IAssemblyLoadContext _defaultContext;

        public PackageLoadContext(LibraryManager libraryManager,
                                  IAssemblyLoadContext defaultContext)
        {
            _packageAssemblyLoader = new PackageAssemblyLoader(loadContextAccessor: null, libraryManager: libraryManager);
            _defaultContext = defaultContext;
        }

        public override Assembly LoadAssembly(AssemblyName assemblyName)
        {
            try
            {
                return _defaultContext.Load(assemblyName);
            }
            catch (FileNotFoundException)
            {
                return _packageAssemblyLoader.Load(assemblyName, this);
            }
        }
    }
}
