using System;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class AssemblyLoadContextFactory : IAssemblyLoadContextFactory
    {
        private readonly IAssemblyLoadContext _defaultContext;

        public AssemblyLoadContextFactory(IServiceProvider serviceProvider)
        {
            var accessor = serviceProvider.GetService(typeof(IAssemblyLoadContextAccessor)) as IAssemblyLoadContextAccessor;
            _defaultContext = accessor.Default;
        }

        public IAssemblyLoadContext Create(IServiceProvider serviceProvider)
        {
            var projectAssemblyLoader = (ProjectAssemblyLoader)ActivatorUtilities.CreateInstance(serviceProvider, typeof(ProjectAssemblyLoader));
            var nugetAsseblyLoader = (NuGetAssemblyLoader)ActivatorUtilities.CreateInstance(serviceProvider, typeof(NuGetAssemblyLoader));

            return new LibraryAssemblyLoadContext(projectAssemblyLoader, nugetAsseblyLoader, _defaultContext);
        }

        private class LibraryAssemblyLoadContext : LoadContext
        {
            private readonly ProjectAssemblyLoader _projectAssemblyLoader;
            private readonly NuGetAssemblyLoader _nugetAssemblyLoader;
            private readonly IAssemblyLoadContext _defaultContext;

            public LibraryAssemblyLoadContext(ProjectAssemblyLoader projectAssemblyLoader,
                                              NuGetAssemblyLoader nugetAssemblyLoader,
                                              IAssemblyLoadContext defaultContext)
            {
                _projectAssemblyLoader = projectAssemblyLoader;
                _nugetAssemblyLoader = nugetAssemblyLoader;
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
                           _nugetAssemblyLoader.Load(assemblyName, this);
                }
            }
        }
    }
}
