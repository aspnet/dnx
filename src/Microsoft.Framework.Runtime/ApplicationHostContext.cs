using System;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.FileSystem;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for ApplicationHostContext
    /// </summary>
    public class ApplicationHostContext
    {
        private readonly ServiceProvider _serviceProvider;

        public ApplicationHostContext(IServiceProvider serviceProvider,
                                      string projectDirectory) :
            this(serviceProvider,
                 projectDirectory,
                 packagesDirectory: null)
        {

        }

        public ApplicationHostContext(IServiceProvider serviceProvider,
                                      string projectDirectory,
                                      string packagesDirectory)
        {
            ProjectDirectory = projectDirectory;
            RootDirectory = Runtime.ProjectResolver.ResolveRootDirectory(ProjectDirectory);
            ProjectResolver = new ProjectResolver(ProjectDirectory, RootDirectory);
            FrameworkReferenceResolver = new FrameworkReferenceResolver();
            _serviceProvider = new ServiceProvider(serviceProvider);

            packagesDirectory = packagesDirectory ?? NuGetDependencyResolver.ResolveRepositoryPath(RootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(FrameworkReferenceResolver);
            NuGetDependencyProvider = new NuGetDependencyResolver(packagesDirectory, FrameworkReferenceResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            ProjectDepencyProvider = new ProjectReferenceDependencyProvider(ProjectResolver);
            UnresolvedDependencyProvider = new UnresolvedDependencyProvider();

            DependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                ProjectDepencyProvider,
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                NuGetDependencyProvider,
                UnresolvedDependencyProvider
            });

            UnresolvedDependencyProvider.AttemptedProviders = DependencyWalker.DependencyProviders;

            var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] {
                new ProjectLibraryExportProvider(ProjectResolver, ServiceProvider),
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                NuGetDependencyProvider
            });

            // Default services
            _serviceProvider.Add(typeof(ILibraryExportProvider), compositeDependencyExporter);
            _serviceProvider.Add(typeof(IProjectResolver), ProjectResolver);
            _serviceProvider.Add(typeof(IFileWatcher), NoopWatcher.Instance);

            _serviceProvider.Add(typeof(NuGetDependencyResolver), NuGetDependencyProvider);
            _serviceProvider.Add(typeof(ProjectReferenceDependencyProvider), ProjectDepencyProvider);
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

        public UnresolvedDependencyProvider UnresolvedDependencyProvider { get; private set; }

        public NuGetDependencyResolver NuGetDependencyProvider { get; private set; }

        public ProjectReferenceDependencyProvider ProjectDepencyProvider { get; private set; }

        public IProjectResolver ProjectResolver { get; private set; }
        public DependencyWalker DependencyWalker { get; private set; }
        public FrameworkReferenceResolver FrameworkReferenceResolver { get; private set; }
        public string RootDirectory { get; private set; }
        public string ProjectDirectory { get; private set; }
        public string PackagesDirectory { get; private set; }
    }
}