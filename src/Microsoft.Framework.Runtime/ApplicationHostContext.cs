using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Loader;

namespace Microsoft.Framework.Runtime
{
    public class ApplicationHostContext
    {
        private readonly ServiceProvider _serviceProvider;

        public ApplicationHostContext(IServiceProvider serviceProvider,
                                      string projectDirectory,
                                      string packagesDirectory,
                                      string configuration,
                                      FrameworkName targetFramework,
                                      ICache cache,
                                      ICacheContextAccessor cacheContextAccessor,
                                      INamedCacheDependencyProvider namedCacheDependencyProvider,
                                      IAssemblyLoadContextFactory loadContextFactory = null)
        {
            ProjectDirectory = projectDirectory;
            Configuration = configuration;
            RootDirectory = Runtime.ProjectResolver.ResolveRootDirectory(ProjectDirectory);
            ProjectResolver = new ProjectResolver(ProjectDirectory, RootDirectory);
            FrameworkReferenceResolver = new FrameworkReferenceResolver();
            _serviceProvider = new ServiceProvider(serviceProvider);

            PackagesDirectory = packagesDirectory ?? NuGetDependencyResolver.ResolveRepositoryPath(RootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(FrameworkReferenceResolver);
            NuGetDependencyProvider = new NuGetDependencyResolver(PackagesDirectory, RootDirectory);
            var gacDependencyResolver = new GacDependencyResolver();
            ProjectDepencyProvider = new ProjectReferenceDependencyProvider(ProjectResolver);
            var unresolvedDependencyProvider = new UnresolvedDependencyProvider();

            DependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                ProjectDepencyProvider,
                NuGetDependencyProvider,
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                unresolvedDependencyProvider
            });

            LibraryExportProvider = new CompositeLibraryExportProvider(new ILibraryExportProvider[] {
                new ProjectLibraryExportProvider(ProjectResolver, ServiceProvider),
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                NuGetDependencyProvider
            });

            LibraryManager = new LibraryManager(targetFramework, configuration, DependencyWalker,
                LibraryExportProvider, cache);

            AssemblyLoadContextFactory = loadContextFactory ?? new RuntimeLoadContextFactory(ServiceProvider);
            namedCacheDependencyProvider = namedCacheDependencyProvider ?? NamedCacheDependencyProvider.Empty;

            // Default services
            _serviceProvider.Add(typeof(IApplicationEnvironment), new ApplicationEnvironment(Project, targetFramework, configuration));
            _serviceProvider.Add(typeof(IFileWatcher), NoopWatcher.Instance);
            _serviceProvider.Add(typeof(ILibraryManager), LibraryManager);

            // Not exposed to the application layer
            _serviceProvider.Add(typeof(ILibraryExportProvider), LibraryExportProvider, includeInManifest: false);
            _serviceProvider.Add(typeof(IProjectResolver), ProjectResolver, includeInManifest: false);
            _serviceProvider.Add(typeof(NuGetDependencyResolver), NuGetDependencyProvider, includeInManifest: false);
            _serviceProvider.Add(typeof(ProjectReferenceDependencyProvider), ProjectDepencyProvider, includeInManifest: false);
            _serviceProvider.Add(typeof(ICache), cache, includeInManifest: false);
            _serviceProvider.Add(typeof(ICacheContextAccessor), cacheContextAccessor, includeInManifest: false);
            _serviceProvider.Add(typeof(INamedCacheDependencyProvider), namedCacheDependencyProvider, includeInManifest: false);
            _serviceProvider.Add(typeof(IAssemblyLoadContextFactory), AssemblyLoadContextFactory, includeInManifest: false);
        }

        public void AddService(Type type, object instance, bool includeInManifest)
        {
            _serviceProvider.Add(type, instance, includeInManifest);
        }

        public void AddService(Type type, object instance)
        {
            _serviceProvider.Add(type, instance);
        }

        public T CreateInstance<T>()
        {
            return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                return _serviceProvider;
            }
        }

        public Project Project
        {
            get
            {
                Project project;
                if (Project.TryGetProject(ProjectDirectory, out project))
                {
                    return project;
                }
                return null;
            }
        }

        public IAssemblyLoadContextFactory AssemblyLoadContextFactory { get; private set; }

        public NuGetDependencyResolver NuGetDependencyProvider { get; private set; }

        public ProjectReferenceDependencyProvider ProjectDepencyProvider { get; private set; }

        public IProjectResolver ProjectResolver { get; private set; }
        public ILibraryExportProvider LibraryExportProvider { get; private set; }
        public ILibraryManager LibraryManager { get; private set; }
        public DependencyWalker DependencyWalker { get; private set; }
        public FrameworkReferenceResolver FrameworkReferenceResolver { get; private set; }

        public string Configuration { get; private set; }
        public string RootDirectory { get; private set; }
        public string ProjectDirectory { get; private set; }
        public string PackagesDirectory { get; private set; }
    }
}