// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Loader;
using Microsoft.Dnx.Runtime.Servicing;
using NuGet;

namespace Microsoft.Dnx.ApplicationHost
{
    public class DefaultHost
    {
        private static readonly string RuntimeAbstractionsPackageName =
            typeof(IRuntimeEnvironment).GetTypeInfo().Assembly.GetName().Name;

        private ApplicationHostContext _applicationHostContext;

        private readonly string _projectDirectory;
        private readonly FrameworkName _targetFramework;
        private readonly IList<IAssemblyLoader> _loaders = new List<IAssemblyLoader>();
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly IRuntimeEnvironment _runtimeEnvironment;

        private Project _project;
        private readonly ServiceProvider _serviceProvider;

        public DefaultHost(DefaultHostOptions options,
                           IAssemblyLoadContextAccessor loadContextAccessor)
        {
            _projectDirectory = Path.GetFullPath(options.ApplicationBaseDirectory);
            _targetFramework = options.TargetFramework;
            _loadContextAccessor = loadContextAccessor;
            _runtimeEnvironment = PlatformServices.Default.Runtime;
            _serviceProvider = new ServiceProvider();
            Initialize(options, loadContextAccessor);
        }

        public IServiceProvider ServiceProvider
        {
            get { return _serviceProvider; }
        }
        
        public Project Project
        {
            get { return _project; }
        }

        public Assembly GetEntryPoint(string applicationName)
        {
            var sw = Stopwatch.StartNew();

            if (Project == null)
            {
                return null;
            }

            if (!_applicationHostContext.MainProject.Resolved)
            {
                var targetFrameworkShortName = VersionUtility.GetShortFrameworkName(_targetFramework);
                var runtimeFrameworkInfo = $@"Current runtime target framework: '{_targetFramework} ({targetFrameworkShortName})'
{_runtimeEnvironment.GetFullVersion()}";
                string exceptionMsg;

                // If the main project cannot be resolved, it means the app doesn't support current target framework
                // (i.e. project.json doesn't contain a framework that is compatible with target framework of current runtime)

                exceptionMsg = $@"The current runtime target framework is not compatible with '{Project.Name}'.
{runtimeFrameworkInfo}
Please make sure the runtime matches a framework specified in {Project.ProjectFileName}";
                throw new InvalidOperationException(exceptionMsg);
            }

            return _loadContextAccessor.Default.Load(applicationName);
        }

        public IDisposable AddLoaders(IAssemblyLoaderContainer container)
        {
            var disposables = new List<IDisposable>();
            foreach (var loader in _loaders)
            {
                disposables.Add(container.AddLoader(loader));
            }

            return new DisposableAction(() =>
            {
                foreach (var d in Enumerable.Reverse(disposables))
                {
                    d.Dispose();
                }
            });
        }

        private void Initialize(DefaultHostOptions options, IAssemblyLoadContextAccessor loadContextAccessor)
        {
            var applicationHostContext = new ApplicationHostContext
            {
                ProjectDirectory = _projectDirectory,
                RuntimeIdentifiers = _runtimeEnvironment.GetAllRuntimeIdentifiers(),
                TargetFramework = _targetFramework
            };

            var libraries = ApplicationHostContext.GetRuntimeLibraries(applicationHostContext, throwOnInvalidLockFile: true);

            Logger.TraceInformation("[{0}]: Project path: {1}", GetType().Name, applicationHostContext.ProjectDirectory);
            Logger.TraceInformation("[{0}]: Project root: {1}", GetType().Name, applicationHostContext.RootDirectory);
            Logger.TraceInformation("[{0}]: Project configuration: {1}", GetType().Name, options.Configuration);
            Logger.TraceInformation("[{0}]: Packages path: {1}", GetType().Name, applicationHostContext.PackagesDirectory);

            _applicationHostContext = applicationHostContext;

            _project = applicationHostContext.Project;

#if FEATURE_DNX_MIN_VERSION_CHECK
            ValidateMinRuntimeVersion(libraries);
#endif

            // Create a new Application Environment for running the app. It needs a reference to the Host's application environment
            // (if any), which we can get from the service provider we were given.
            // If this is null (i.e. there is no Host Application Environment), that's OK, the Application Environment we are creating
            // will just have it's own independent set of global data.
            var hostEnvironment = PlatformServices.Default.Application;
            var applicationEnvironment = new ApplicationEnvironment(Project, _targetFramework, hostEnvironment);

            var compilationContext = new CompilationEngineContext(
                applicationEnvironment,
                _runtimeEnvironment,
                loadContextAccessor.Default,
                new CompilationCache());

            var compilationEngine = new CompilationEngine(compilationContext);
            var runtimeLibraryExporter = new RuntimeLibraryExporter(() => compilationEngine.CreateProjectExporter(Project, _targetFramework, options.Configuration));

            var runtimeLibraryManager = new RuntimeLibraryManager(applicationHostContext);

            // Default services
            _serviceProvider.Add(typeof(ILibraryExporter), runtimeLibraryExporter);
            _serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);
            _serviceProvider.Add(typeof(IRuntimeEnvironment), PlatformServices.Default.Runtime);
            _serviceProvider.Add(typeof(ILibraryManager), runtimeLibraryManager);
            _serviceProvider.Add(typeof(IAssemblyLoadContextAccessor), PlatformServices.Default.AssemblyLoadContextAccessor);
            _serviceProvider.Add(typeof(IAssemblyLoaderContainer), PlatformServices.Default.AssemblyLoaderContainer);
            
            PlatformServices.SetDefault(new ApplicationHostPlatformServices(PlatformServices.Default, applicationEnvironment, runtimeLibraryManager));

            if (options.CompilationServerPort.HasValue)
            {
                // Change the project reference provider
                Project.DefaultCompiler = Project.DefaultDesignTimeCompiler;
                Project.DesignTimeCompilerPort = options.CompilationServerPort.Value;
            }

            // TODO: Dedupe this logic in the RuntimeLoadContext
            var projects = libraries.Where(p => p.Type == Runtime.LibraryTypes.Project)
                                    .ToDictionary(p => p.Identity.Name, p => (ProjectDescription)p);

            var assemblies = PackageDependencyProvider.ResolvePackageAssemblyPaths(libraries);

            // Configure Assembly loaders
            _loaders.Add(new ProjectAssemblyLoader(loadContextAccessor, compilationEngine, projects.Values, options.Configuration));
            _loaders.Add(new PackageAssemblyLoader(loadContextAccessor, assemblies, libraries));

            var compilerOptionsProvider = new CompilerOptionsProvider(projects);

            _serviceProvider.Add(typeof(ICompilerOptionsProvider), compilerOptionsProvider);

            CompilationServices.SetDefault(
                    CompilationServices.Create(
                            libraryExporter: runtimeLibraryExporter,
                            compilerOptionsProvider: compilerOptionsProvider
                        )
                );

#if DNX451
            PackageDependencyProvider.EnableLoadingNativeLibraries(libraries);
#endif
            AddBreadcrumbs(libraries);
        }

#if FEATURE_DNX_MIN_VERSION_CHECK
        private void ValidateMinRuntimeVersion(IEnumerable<LibraryDescription> libraries)
        {
            if (string.Equals(Environment.GetEnvironmentVariable(EnvironmentNames.DnxDisableMinVersionCheck), "1", StringComparison.OrdinalIgnoreCase))
            {
                // Min version is disabled
                return;
            }

            foreach (var lib in libraries)
            {
                // We only need to validate if runtime abstractions is precompiled
                // because when building from sources the versions are not ordered
                // correctly
                if (lib.Type == LibraryTypes.Package &&
                    string.Equals(lib.Identity.Name, RuntimeAbstractionsPackageName, StringComparison.Ordinal) &&
                    lib.Identity.Version > new SemanticVersion(_runtimeEnvironment.RuntimeVersion))
                {
                    throw new InvalidOperationException($"This application requires DNX version {lib.Identity.Version} or newer to run.");
                }
            }
    }
#endif

        private void AddBreadcrumbs(IEnumerable<LibraryDescription> libraries)
        {
            AddRuntimeServiceBreadcrumb();

            AddPackagesBreadcrumb(libraries);

            Breadcrumbs.Instance.WriteAllBreadcrumbs(background: true);
        }

        private void AddPackagesBreadcrumb(IEnumerable<LibraryDescription> libraries)
        {
            foreach (var library in libraries)
            {
                if (library.Type == Runtime.LibraryTypes.Package)
                {
                    var package = (PackageDescription)library;

                    if (Breadcrumbs.Instance.IsPackageServiceable(package))
                    {
                        Breadcrumbs.Instance.AddBreadcrumb(package.Identity.Name, package.Identity.Version);
                    }
                }
            }
        }

        private void AddRuntimeServiceBreadcrumb()
        {
            var frameworkBreadcrumbName = $"{Runtime.Constants.RuntimeShortName}-{_runtimeEnvironment.RuntimeType}-{_runtimeEnvironment.RuntimeArchitecture}-{_runtimeEnvironment.RuntimeVersion}";
            Breadcrumbs.Instance.AddBreadcrumb(frameworkBreadcrumbName);
        }
    }
}
