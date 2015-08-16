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
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Dnx.Runtime.Loader;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class DefaultHost
    {
        private ApplicationHostContext _applicationHostContext;

        private readonly string _projectDirectory;
        private readonly FrameworkName _targetFramework;
        private readonly ApplicationShutdown _shutdown = new ApplicationShutdown();
        private readonly IList<IAssemblyLoader> _loaders = new List<IAssemblyLoader>();
        private readonly ICompilationEngineFactory _compilationEngineFactory;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly IRuntimeEnvironment _runtimeEnvironment;

        private Project _project;
        private readonly ServiceProvider _serviceProvider;

        public DefaultHost(RuntimeOptions options,
                           IServiceProvider hostServices,
                           IAssemblyLoadContextAccessor loadContextAccessor,
                           IFileWatcher fileWatcher,
                           ICompilationEngineFactory compilationEngineFactory)
        {
            _projectDirectory = Path.GetFullPath(options.ApplicationBaseDirectory);
            _targetFramework = options.TargetFramework;
            _compilationEngineFactory = compilationEngineFactory;
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

            Initialize();

            var unresolvedLibs = _applicationHostContext.LibraryManager.GetLibraryDescriptions().Where(l => !l.Resolved);

            // If there's any unresolved dependencies then complain
            if (unresolvedLibs.Any())
            {
                var targetFrameworkShortName = VersionUtility.GetShortFrameworkName(_targetFramework);
                var runtimeFrameworkInfo = $@"Current runtime target framework: '{_targetFramework} ({targetFrameworkShortName})'
{_runtimeEnvironment.GetFullVersion()}";
                string exceptionMsg;

                // If the main project cannot be resolved, it means the app doesn't support current target framework
                // (i.e. project.json doesn't contain a framework that is compatible with target framework of current runtime)
                if (unresolvedLibs.Any(l => string.Equals(l.Identity.Name, Project.Name)))
                {
                    exceptionMsg = $@"The current runtime target framework is not compatible with '{Project.Name}'.

{runtimeFrameworkInfo}
Please make sure the runtime matches a framework specified in {Project.ProjectFileName}";
                }
                else
                {
                    var lockFileErrorMessage = string.Join(string.Empty,
                        _applicationHostContext.GetLockFileDiagnostics().Select(x => $"{Environment.NewLine}{x.FormattedMessage}"));
                    exceptionMsg = $@"{_applicationHostContext.LibraryManager.GetMissingDependenciesWarning(_targetFramework)}{lockFileErrorMessage}
{runtimeFrameworkInfo}";
                }

                throw new InvalidOperationException(exceptionMsg);
            }

            // Don't need these anymore
            _applicationHostContext = null;
            _project = null;

            return _loadContextAccessor.Default.Load(applicationName);
        }

        public void Initialize()
        {
            AddRuntimeServiceBreadcrumb();

            Servicing.Breadcrumbs.Instance.WriteAllBreadcrumbs(background: true);
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
            _applicationHostContext = new ApplicationHostContext(
                _projectDirectory,
                options.PackageDirectory,
                _targetFramework);

            var compilationContext = new CompilationEngineContext(
                    _applicationHostContext.LibraryManager,
                    new ProjectGraphProvider(),
                    fileWatcher,
                    _targetFramework,
                    options.Configuration,
                    loadContextAccessor.Default);

            var compilationEngine = _compilationEngineFactory.CreateEngine(compilationContext);

            Logger.TraceInformation("[{0}]: Project path: {1}", GetType().Name, _projectDirectory);
            Logger.TraceInformation("[{0}]: Project root: {1}", GetType().Name, _applicationHostContext.RootDirectory);
            Logger.TraceInformation("[{0}]: Project configuration: {1}", GetType().Name, options.Configuration);
            Logger.TraceInformation("[{0}]: Packages path: {1}", GetType().Name, _applicationHostContext.PackagesDirectory);

            _project = _applicationHostContext.Project;

            if (Project == null)
            {
                throw new Exception("Unable to locate " + Project.ProjectFileName);
            }

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

            // Default services
            _serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);
            _serviceProvider.Add(typeof(ILibraryManager), _applicationHostContext.LibraryManager);
            _serviceProvider.Add(typeof(ILibraryExporter), compilationEngine.RootLibraryExporter);
            _serviceProvider.Add(typeof(IApplicationShutdown), _shutdown);
            _serviceProvider.Add(typeof(ICompilerOptionsProvider), new CompilerOptionsProvider(_applicationHostContext.LibraryManager));

            // Compilation services available only for runtime compilation
            compilationContext.AddCompilationService(typeof(RuntimeOptions), options);
            compilationContext.AddCompilationService(typeof(IApplicationShutdown), _shutdown);

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

        private void AddRuntimeServiceBreadcrumb()
        {
            var frameworkBreadcrumbName = $"{Constants.RuntimeShortName}-{_runtimeEnvironment.RuntimeType}-{_runtimeEnvironment.RuntimeArchitecture}-{_runtimeEnvironment.RuntimeVersion}";
            Servicing.Breadcrumbs.Instance.AddBreadcrumb(frameworkBreadcrumbName);
        }
    }
}
