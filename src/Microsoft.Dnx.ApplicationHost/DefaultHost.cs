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
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Dnx.Runtime.Loader;
using Microsoft.Dnx.Runtime.Servicing;
using NuGet;

namespace Microsoft.Dnx.ApplicationHost
{
    public class DefaultHost
    {
        private ApplicationHostContext _applicationHostContext;

        private readonly string _projectDirectory;
        private readonly FrameworkName _targetFramework;
        private readonly ApplicationShutdown _shutdown = new ApplicationShutdown();
        private readonly IList<IAssemblyLoader> _loaders = new List<IAssemblyLoader>();
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly IRuntimeEnvironment _runtimeEnvironment;

        private Project _project;
        private readonly ServiceProvider _serviceProvider;

        public DefaultHost(RuntimeOptions options,
                           IServiceProvider hostServices,
                           IAssemblyLoadContextAccessor loadContextAccessor,
                           IFileWatcher fileWatcher)
        {
            _projectDirectory = Path.GetFullPath(options.ApplicationBaseDirectory);
            _targetFramework = options.TargetFramework;
            _loadContextAccessor = loadContextAccessor;
            _runtimeEnvironment = (IRuntimeEnvironment)hostServices.GetService(typeof(IRuntimeEnvironment));

            _serviceProvider = new ServiceProvider(hostServices);

            Initialize(options, hostServices, loadContextAccessor, fileWatcher);
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

            AddBreadcrumbs();

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

        private void Initialize(RuntimeOptions options, IServiceProvider hostServices, IAssemblyLoadContextAccessor loadContextAccessor, IFileWatcher fileWatcher)
        {
            var applicationHostContext = new ApplicationHostContext
            {
                ProjectDirectory = _projectDirectory,
                TargetFramework = _targetFramework
            };

            ApplicationHostContext.InitializeForRuntime(applicationHostContext);

            Logger.TraceInformation("[{0}]: Project path: {1}", GetType().Name, applicationHostContext.ProjectDirectory);
            Logger.TraceInformation("[{0}]: Project root: {1}", GetType().Name, applicationHostContext.RootDirectory);
            Logger.TraceInformation("[{0}]: Project configuration: {1}", GetType().Name, options.Configuration);
            Logger.TraceInformation("[{0}]: Packages path: {1}", GetType().Name, applicationHostContext.PackagesDirectory);

            _applicationHostContext = applicationHostContext;

            _project = applicationHostContext.Project;

            if (options.WatchFiles)
            {
                fileWatcher.OnChanged += _ =>
                {
                    _shutdown.RequestShutdownWaitForDebugger();
                };
            }

            // Create a new Application Environment for running the app. It needs a reference to the Host's application environment
            // (if any), which we can get from the service provider we were given.
            // If this is null (i.e. there is no Host Application Environment), that's OK, the Application Environment we are creating
            // will just have it's own independent set of global data.
            var hostEnvironment = (IApplicationEnvironment)hostServices.GetService(typeof(IApplicationEnvironment));
            var applicationEnvironment = new ApplicationEnvironment(Project, _targetFramework, options.Configuration, hostEnvironment);

            var compilationContext = new CompilationEngineContext(applicationEnvironment, loadContextAccessor.Default, new CompilationCache(), fileWatcher, new ProjectGraphProvider());

            // Compilation services available only for runtime compilation
            compilationContext.AddCompilationService(typeof(RuntimeOptions), options);
            compilationContext.AddCompilationService(typeof(IApplicationShutdown), _shutdown);

            var compilationEngine = new CompilationEngine(compilationContext);

            // Default services
            _serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);
            _serviceProvider.Add(typeof(ILibraryManager), _applicationHostContext.LibraryManager);

            // TODO: Make this lazy
            _serviceProvider.Add(typeof(ILibraryExporter), new RuntimeLibraryExporter(() => compilationEngine.CreateProjectExporter(Project, _targetFramework, options.Configuration)));
            _serviceProvider.Add(typeof(IApplicationShutdown), _shutdown);
            _serviceProvider.Add(typeof(ICompilerOptionsProvider), new CompilerOptionsProvider(_applicationHostContext.LibraryManager));

            if (options.CompilationServerPort.HasValue)
            {
                // Change the project reference provider
                Project.DefaultCompiler = Project.DefaultDesignTimeCompiler;
            }

            CallContextServiceLocator.Locator.ServiceProvider = ServiceProvider;

            // Configure Assembly loaders
            _loaders.Add(new ProjectAssemblyLoader(
                loadContextAccessor,
                compilationEngine,
                _applicationHostContext.LibraryManager));

            _loaders.Add(new PackageAssemblyLoader(loadContextAccessor, _applicationHostContext.LibraryManager));
        }

        private void AddBreadcrumbs()
        {
            AddRuntimeServiceBreadcrumb();

            AddPackagesBreadcrumb();

            Breadcrumbs.Instance.WriteAllBreadcrumbs(background: true);
        }

        private void AddPackagesBreadcrumb()
        {
            foreach (var library in _applicationHostContext.LibraryManager.GetLibraryDescriptions())
            {
                if (library.Type == LibraryTypes.Package)
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
