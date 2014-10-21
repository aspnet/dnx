using System;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime.Loader
{
    public class AssemblyLoadContextFactory : IAssemblyLoadContextFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAssemblyLoader _parent;

        public AssemblyLoadContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _parent = serviceProvider.GetService(typeof(IAssemblyLoaderContainer)) as IAssemblyLoader;
        }

        public IAssemblyLoadContext Create()
        {
            var projectAssemblyLoader = (ProjectAssemblyLoader)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(ProjectAssemblyLoader));
            var nugetAsseblyLoader = (NuGetAssemblyLoader)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(NuGetAssemblyLoader));

            return new LibraryAssemblyLoadContext(projectAssemblyLoader, nugetAsseblyLoader, _parent);
        }

        private class LibraryAssemblyLoadContext : LoadContext
        {
            private readonly ProjectAssemblyLoader _projectAssemblyLoader;
            private readonly NuGetAssemblyLoader _nugetAssemblyLoader;
            private readonly IAssemblyLoader _parent;

            public LibraryAssemblyLoadContext(ProjectAssemblyLoader projectAssemblyLoader,
                                              NuGetAssemblyLoader nugetAssemblyLoader,
                                              IAssemblyLoader parent)
            {
                _projectAssemblyLoader = projectAssemblyLoader;
                _nugetAssemblyLoader = nugetAssemblyLoader;
                _parent = parent;
            }

            public override Assembly LoadAssembly(string name)
            {
                return _projectAssemblyLoader.Load(name, this) ??
                       _nugetAssemblyLoader.Load(name, this) ??
                       _parent.Load(name);
            }
        }
    }
}