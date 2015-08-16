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
        private ICompilationEngine _compilationEngine;

        private Project _project;

        public DefaultHost(RuntimeOptions options,
                           IServiceProvider hostServices,
                           IAssemblyLoadContextAccessor loadContextAccessor,
                           IFileWatcher fileWatcher,
                           ICompilationEngineFactory compilationEngineFactory)
        {
            _projectDirectory = Path.GetFullPath(options.ApplicationBaseDirectory);
            _targetFramework = options.TargetFramework;
            _compilationEngineFactory = compilationEngineFactory;

            Initialize(options, hostServices, loadContextAccessor, fileWatcher);
        }

        public IServiceProvider ServiceProvider
        {
            get { return _applicationHostContext.ServiceProvider; }
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
                var runtimeEnv = ServiceProvider.GetService(typeof(IRuntimeEnvironment)) as IRuntimeEnvironment;
                var runtimeFrameworkInfo = $@"Current runtime target framework: '{_targetFramework} ({targetFrameworkShortName})'
{runtimeEnv.GetFullVersion()}";
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
                    exceptionMsg = $@"{_applicationHostContext.GetMissingDependenciesWarning(_targetFramework)}{lockFileErrorMessage}
{runtimeFrameworkInfo}";
                }

                throw new InvalidOperationException(exceptionMsg);
            }

            var accessor = (IAssemblyLoadContextAccessor)ServiceProvider.GetService(typeof(IAssemblyLoadContextAccessor));

            return accessor.Default.Load(applicationName);
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
                hostServices,
                _projectDirectory,
                options.PackageDirectory,
                options.Configuration,
                _targetFramework);

            _compilationEngine = _compilationEngineFactory.CreateEngine(
                new CompilationEngineContext(
                    _applicationHostContext.LibraryManager,
                    _applicationHostContext.ProjectGraphProvider,
                    fileWatcher,
                    _applicationHostContext.ServiceProvider,
                    _targetFramework,
                    options.Configuration));

            Logger.TraceInformation("[{0}]: Project path: {1}", GetType().Name, _projectDirectory);
            Logger.TraceInformation("[{0}]: Project root: {1}", GetType().Name, _applicationHostContext.RootDirectory);
            Logger.TraceInformation("[{0}]: Project configuration: {1}", GetType().Name, _applicationHostContext.Configuration);
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

            _applicationHostContext.AddService(typeof(IApplicationShutdown), _shutdown);
            _applicationHostContext.AddService(typeof(RuntimeOptions), options);

            if (options.CompilationServerPort.HasValue)
            {
                // Change the project reference provider
                Project.DefaultCompiler = Project.DefaultDesignTimeCompiler;
            }

            CallContextServiceLocator.Locator.ServiceProvider = ServiceProvider;

            // Configure Assembly loaders
            _loaders.Add(new ProjectAssemblyLoader(
                loadContextAccessor,
                _compilationEngine,
                _applicationHostContext.LibraryManager));

            _loaders.Add(new NuGetAssemblyLoader(loadContextAccessor, _applicationHostContext.LibraryManager));
        }

        private void AddRuntimeServiceBreadcrumb()
        {
            var env = (IRuntimeEnvironment)ServiceProvider.GetService(typeof(IRuntimeEnvironment));
            var frameworkBreadcrumbName = $"{Constants.RuntimeShortName}-{env.RuntimeType}-{env.RuntimeArchitecture}-{env.RuntimeVersion}";
            Servicing.Breadcrumbs.Instance.AddBreadcrumb(frameworkBreadcrumbName);
        }
    }
}
