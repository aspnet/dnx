using System;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime.Compilation;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class RuntimeLoadContext : LoadContext
    {
        private readonly PackageAssemblyLoader _packageAssemblyLoader;
        private readonly ProjectAssemblyLoader _projectAssemblyLoader;
        private readonly IAssemblyLoadContext _defaultContext;

        public RuntimeLoadContext(LibraryManager libraryManager,
                                  ICompilationEngine compilationEngine,
                                  IAssemblyLoadContext defaultContext)
        {
            _projectAssemblyLoader = new ProjectAssemblyLoader(loadContextAccessor: null, compilationEngine: compilationEngine, libraryManager: libraryManager);
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
                return _projectAssemblyLoader.Load(assemblyName, this) ?? 
                       _packageAssemblyLoader.Load(assemblyName, this);
            }
        }
    }
}
