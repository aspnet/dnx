using System;
using System.IO;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime.Loader
{
    public class AssemblyLoadContextFactory : IAssemblyLoadContextFactory
    {
        private readonly IAssemblyLoadContext _defaultContext;
        private readonly IServiceProvider _serviceProvider;

        public AssemblyLoadContextFactory(IServiceProvider serviceProvider)
        {
            var accessor = serviceProvider.GetService(typeof(IAssemblyLoadContextAccessor)) as IAssemblyLoadContextAccessor;
            _serviceProvider = serviceProvider;
            _defaultContext = accessor.Default;
        }

        public IAssemblyLoadContext Create()
        {
            var projectAssemblyLoader = (ProjectAssemblyLoader)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(ProjectAssemblyLoader));
            var nugetAsseblyLoader = (NuGetAssemblyLoader)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(NuGetAssemblyLoader));

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

            public override Assembly LoadAssembly(AssemblyName name)
            {
                try
                {
                    return _defaultContext.Load(name);
                }
                catch (FileNotFoundException)
                {
                    return _projectAssemblyLoader.Load(name, this) ??
                           _nugetAssemblyLoader.Load(name, this);
                }
            }
        }
    }
}
