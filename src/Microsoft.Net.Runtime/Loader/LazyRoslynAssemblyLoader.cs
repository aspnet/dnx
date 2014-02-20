using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.Roslyn;
using Microsoft.Net.Runtime.Services;
using NuGet;

namespace Microsoft.Net.Runtime
{
    internal class LazyRoslynAssemblyLoader : IAssemblyLoader, IPackageLoader, IDependencyExportResolver, IProjectMetadataProvider
    {
        private readonly ProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly AssemblyLoader _loader;
        private object _roslynLoaderInstance;
        private bool _roslynInitializing;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public LazyRoslynAssemblyLoader(IAssemblyLoaderEngine loaderEngine,
                                        ProjectResolver projectResolver,
                                        IFileWatcher watcher,
                                        AssemblyLoader loader)
        {
            _loaderEngine = loaderEngine;
            _projectResolver = projectResolver;
            _watcher = watcher;
            _loader = loader;
        }

        public DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            Project project;

            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }
            else if (version != null && !project.Version.EqualsSnapshot(version))
            {
                return null;
            }

            var config = project.GetTargetFrameworkConfiguration(targetFramework);

            return new DependencyDescription
            {
                Identity = new Dependency { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(config.Dependencies),
            };
        }

        public void Initialize(IEnumerable<DependencyDescription> packages, FrameworkName targetFramework)
        {
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            if (_roslynInitializing)
            {
                return null;
            }

            return ExecuteWith<IAssemblyLoader, AssemblyLoadResult>(loader =>
            {
                return loader.Load(loadContext);
            });
        }

        public DependencyExport GetDependencyExport(string name, FrameworkName targetFramework)
        {
            return ExecuteWith<IDependencyExportResolver, DependencyExport>(resolver =>
            {
                return resolver.GetDependencyExport(name, targetFramework);
            });
        }

        public IProjectMetadata GetProjectMetadata(string name, FrameworkName targetFramework)
        {
            return ExecuteWith<IProjectMetadataProvider, IProjectMetadata>(provider =>
            {
                return provider.GetProjectMetadata(name, targetFramework);
            });
        }

        private TResult ExecuteWith<TInterface, TResult>(Func<TInterface, TResult> execute)
        {
            if (_roslynLoaderInstance == null)
            {
                try
                {
                    _roslynInitializing = true;

                    var assembly = Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Roslyn"));

                    var roslynAssemblyLoaderType = assembly.GetType("Microsoft.Net.Runtime.Roslyn.RoslynAssemblyLoader");

                    var ctors = roslynAssemblyLoaderType.GetTypeInfo().DeclaredConstructors;

                    var args = new object[] { _loaderEngine, _projectResolver, _watcher, _loader };

                    var ctor = ctors.First(c => c.GetParameters().Length == args.Length);

                    _roslynLoaderInstance = ctor.Invoke(args);

                    return execute((TInterface)_roslynLoaderInstance);
                }
                finally
                {
                    _roslynInitializing = false;
                }
            }

            return execute((TInterface)_roslynLoaderInstance);
        }
    }
}
