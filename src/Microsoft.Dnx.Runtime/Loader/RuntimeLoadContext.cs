using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime.Compilation;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class RuntimeLoadContext : LoadContext
    {
        private readonly PackageAssemblyLoader _packageAssemblyLoader;
        private readonly ProjectAssemblyLoader _projectAssemblyLoader;
        private readonly IAssemblyLoadContext _defaultContext;

        public RuntimeLoadContext(IEnumerable<LibraryDescription> libraries,
                                  ICompilationEngine compilationEngine,
                                  IAssemblyLoadContext defaultContext)
        {
            // TODO: Make this all lazy
            // TODO: Unify this logic with default host
            var projects = libraries.Where(p => p.Type == LibraryTypes.Project)
                                    .ToDictionary(p => p.Identity.Name, p => (ProjectDescription)p);

            var assemblies = PackageDependencyProvider.ResolvePackageAssemblyPaths(libraries);

            _projectAssemblyLoader = new ProjectAssemblyLoader(loadContextAccessor: null, compilationEngine: compilationEngine, projects: projects);
            _packageAssemblyLoader = new PackageAssemblyLoader(loadContextAccessor: null, assemblies: assemblies);
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
