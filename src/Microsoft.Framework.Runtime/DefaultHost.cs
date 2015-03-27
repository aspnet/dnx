// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Caching;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.Compilation;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Infrastructure;
using Microsoft.Framework.Runtime.Loader;

namespace Microsoft.Framework.Runtime
{
    public class DefaultHost : IDisposable
    {
        private ApplicationHostContext _applicationHostContext;

        private IFileWatcher _watcher;
        private readonly string _projectDirectory;
        private readonly FrameworkName _targetFramework;
        private readonly ApplicationShutdown _shutdown = new ApplicationShutdown();

        private Project _project;

        public DefaultHost(DefaultHostOptions options,
                           IServiceProvider hostServices)
        {
            _projectDirectory = Path.GetFullPath(options.ApplicationBaseDirectory);
            _targetFramework = options.TargetFramework;

            Initialize(options, hostServices);
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

            // If there's any unresolved dependencies then complain
            if (_applicationHostContext.DependencyWalker.Libraries.Any(l => !l.Resolved))
            {
                var exceptionMsg = _applicationHostContext.DependencyWalker.GetMissingDependenciesWarning(
                    _targetFramework);
                throw new InvalidOperationException(exceptionMsg);
            }

            return Assembly.Load(new AssemblyName(applicationName));
        }

        public void Initialize()
        {
            AddRuntimeServiceBreadcrumb();

            _applicationHostContext.DependencyWalker.Walk(Project.Name, Project.Version, _targetFramework);

            Servicing.Breadcrumbs.Instance.WriteAllBreadcrumbs(background: true);
        }

        public IDisposable AddLoaders(IAssemblyLoaderContainer container)
        {
            var loaders = new[]
            {
                typeof(ProjectAssemblyLoader),
                typeof(NuGetAssemblyLoader),
            };

            var disposables = new List<IDisposable>();
            foreach (var loaderType in loaders)
            {
                var loader = (IAssemblyLoader)ActivatorUtilities.CreateInstance(ServiceProvider, loaderType);
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

        public void Dispose()
        {
            _watcher.Dispose();
        }

        private void Initialize(DefaultHostOptions options, IServiceProvider hostServices)
        {
            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);
            var namedCacheDependencyProvider = new NamedCacheDependencyProvider();

            _applicationHostContext = new ApplicationHostContext(
                hostServices,
                _projectDirectory,
                options.PackageDirectory,
                options.Configuration,
                _targetFramework,
                cache,
                cacheContextAccessor,
                namedCacheDependencyProvider);

            Logger.TraceInformation("[{0}]: Project path: {1}", GetType().Name, _projectDirectory);
            Logger.TraceInformation("[{0}]: Project root: {1}", GetType().Name, _applicationHostContext.RootDirectory);
            Logger.TraceInformation("[{0}]: Packages path: {1}", GetType().Name, _applicationHostContext.PackagesDirectory);

            _project = _applicationHostContext.Project;

            if (Project == null)
            {
                throw new Exception("Unable to locate " + Project.ProjectFileName);
            }

            if (options.WatchFiles)
            {
                var watcher = new FileWatcher(_applicationHostContext.RootDirectory);
                _watcher = watcher;
                watcher.OnChanged += _ =>
                {
                    _shutdown.RequestShutdownWaitForDebugger();
                };
            }
            else
            {
                _watcher = NoopWatcher.Instance;
            }

            _applicationHostContext.AddService(typeof(IApplicationShutdown), _shutdown);
            _applicationHostContext.AddService(typeof(IRuntimeOptions), options);

            // TODO: Get rid of this and just use the IFileWatcher
            _applicationHostContext.AddService(typeof(IFileMonitor), _watcher);
            _applicationHostContext.AddService(typeof(IFileWatcher), _watcher);

            if (options.CompilationServerPort.HasValue)
            {
                // Change the project reference provider
                Project.DefaultCompiler = Project.DefaultDesignTimeCompiler;
            }

            CallContextServiceLocator.Locator.ServiceProvider = ServiceProvider;
        }

        private void AddRuntimeServiceBreadcrumb()
        {
#if DNX451
            var runtimeAssembly = typeof(Servicing.Breadcrumbs).Assembly;
#else
            var runtimeAssembly = typeof(Servicing.Breadcrumbs).GetTypeInfo().Assembly;
#endif

            var version = runtimeAssembly
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(version))
            {
                var semanticVersion = new NuGet.SemanticVersion(version);
                Servicing.Breadcrumbs.Instance.AddBreadcrumb(runtimeAssembly.GetName().Name, semanticVersion);
            }
        }

    }
}
