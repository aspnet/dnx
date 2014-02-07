using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.Roslyn;
using NuGet;

namespace Microsoft.Net.Runtime
{
    internal class LazyRoslynAssemblyLoader : IAssemblyLoader, IPackageLoader
    {
        private readonly ProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly AssemblyLoader _loader;
        private IAssemblyLoader _roslynLoader;
        private bool _roslynInitializing;

        public LazyRoslynAssemblyLoader(ProjectResolver projectResolver,
                                        IFileWatcher watcher,
                                        AssemblyLoader loader)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _loader = loader;
        }

        public DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName frameworkName)
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

            var config = project.GetTargetFrameworkConfiguration(frameworkName);

            return new DependencyDescription
            {
                Identity = new Dependency { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(config.Dependencies),
            };
        }

        public void Initialize(IEnumerable<DependencyDescription> packages, FrameworkName frameworkName)
        {
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            if (_roslynInitializing)
            {
                return null;
            }

            if (_roslynLoader == null)
            {
                try
                {
                    _roslynInitializing = true;

                    var assembly = Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Roslyn"));

                    var roslynAssemblyLoaderType = assembly.GetType("Microsoft.Net.Runtime.Roslyn.RoslynAssemblyLoader");

                    var ctors = roslynAssemblyLoaderType.GetTypeInfo().DeclaredConstructors;

                    var ctor = ctors.First(c => c.GetParameters().Length == 3);

                    _roslynLoader = (IAssemblyLoader)ctor.Invoke(new object[] { _projectResolver, _watcher, _loader });

                    return _roslynLoader.Load(loadContext);
                }
                finally
                {
                    _roslynInitializing = false;
                }
            }

            return _roslynLoader.Load(loadContext);
        }
    }
}
