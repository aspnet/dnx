using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class RuntimeLoadContext : LoadContext
    {
        private readonly PackageAssemblyLoader _packageAssemblyLoader;
        private readonly ProjectAssemblyLoader _projectAssemblyLoader;
        private readonly IAssemblyLoadContext _defaultContext;

        public RuntimeLoadContext(string friendlyName,
                                  IEnumerable<LibraryDescription> libraries,
                                  ICompilationEngine compilationEngine,
                                  IAssemblyLoadContext defaultContext,
                                  string configuration) : base(friendlyName)
        {
            // TODO: Make this all lazy
            // TODO: Unify this logic with default host
            var projects = libraries.Where(p => p.Type == LibraryTypes.Project)
                                    .OfType<ProjectDescription>();

            var assemblies = PackageDependencyProvider.ResolvePackageAssemblyPaths(libraries);

            _projectAssemblyLoader = new ProjectAssemblyLoader(loadContextAccessor: null, compilationEngine: compilationEngine, projects: projects, configuration: configuration);
            _packageAssemblyLoader = new PackageAssemblyLoader(loadContextAccessor: null, assemblies: assemblies, libraryDescriptions: libraries);
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
                return LoadWithoutDefault(assemblyName);
            }
        }

        public Assembly LoadWithoutDefault(AssemblyName assemblyName)
        {
            return _projectAssemblyLoader.Load(assemblyName, this) ??
                   _packageAssemblyLoader.Load(assemblyName, this);
        }
    }
}
