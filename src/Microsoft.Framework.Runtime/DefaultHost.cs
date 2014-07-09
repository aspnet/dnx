// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Infrastructure;
using Microsoft.Framework.Runtime.Loader;

namespace Microsoft.Framework.Runtime
{
    public class DefaultHost : IHost
    {
        private CompositeAssemblyLoader _loader;
        private DependencyWalker _dependencyWalker;

        private IFileWatcher _watcher;
        private readonly string _projectDir;
        private readonly FrameworkName _targetFramework;
        private readonly string _name;
        private readonly ServiceProvider _serviceProvider;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly UnresolvedDependencyProvider _unresolvedProvider = new UnresolvedDependencyProvider();
        private readonly ApplicationShutdown _shutdown = new ApplicationShutdown();

        private Project _project;

        public DefaultHost(DefaultHostOptions options, IServiceProvider hostProvider)
        {
            _projectDir = Normalize(options.ApplicationBaseDirectory);

            _name = options.ApplicationName;

            _targetFramework = options.TargetFramework;

            _loaderEngine = (IAssemblyLoaderEngine)hostProvider.GetService(typeof(IAssemblyLoaderEngine));

            _serviceProvider = new ServiceProvider(hostProvider);
            CallContextServiceLocator.Locator.ServiceProvider = _serviceProvider;

            Initialize(options);
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
            Trace.TraceInformation("Project root is {0}", _projectDir);

            var sw = Stopwatch.StartNew();

            if (Project == null)
            {
                return null;
            }

            _dependencyWalker.Walk(Project.Name, Project.Version, _targetFramework);

            // If there's any unresolved dependencies then complain
            if (_unresolvedProvider.UnresolvedDependencies.Any())
            {
                var sb = new StringBuilder();

                // TODO: Localize messages

                sb.AppendLine("Failed to resolve the following dependencies:");

                foreach (var d in _unresolvedProvider.UnresolvedDependencies.OrderBy(d => d.Identity.Name))
                {
                    sb.AppendLine("   " + d.Identity.ToString());
                }

                sb.AppendLine();
                sb.AppendLine("Searched Locations:");

                foreach (var path in _unresolvedProvider.GetAttemptedPaths(_targetFramework))
                {
                    sb.AppendLine("  " + path);
                }

                sb.AppendLine();
                sb.AppendLine("Try running 'kpm restore'.");

                throw new InvalidOperationException(sb.ToString());
            }

            Trace.TraceInformation("Loading entry point from {0}", applicationName);

            var assembly = Assembly.Load(new AssemblyName(applicationName));

            sw.Stop();

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public Assembly Load(string name)
        {
            return _loader.Load(name);
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }

        private void Initialize(DefaultHostOptions options)
        {
            var dependencyProviders = new List<IDependencyProvider>();
            var loaders = new List<IAssemblyLoader>();

            string rootDirectory = ProjectResolver.ResolveRootDirectory(_projectDir);

            if (options.WatchFiles)
            {
                var watcher = new FileWatcher(rootDirectory);
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

            if (!Project.TryGetProject(_projectDir, out _project))
            {
                throw new Exception("Unable to locate " + Project.ProjectFileName);
            }

            var applicationEnvironment = new ApplicationEnvironment(Project, _targetFramework, options.Configuration);
            var projectResolver = new ProjectResolver(_projectDir, rootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
            var nugetDependencyResolver = new NuGetDependencyResolver(_projectDir, options.PackageDirectory, referenceAssemblyDependencyResolver.FrameworkResolver);
            var gacDependencyResolver = new GacDependencyResolver();

            var nugetLoader = new NuGetAssemblyLoader(_loaderEngine, nugetDependencyResolver);

            // Roslyn needs to be able to resolve exported references and sources
            var libraryExporters = new List<ILibraryExportProvider>();

            // Reference assemblies
            libraryExporters.Add(referenceAssemblyDependencyResolver);

            // GAC assemblies
            libraryExporters.Add(gacDependencyResolver);

            // NuGet exporter
            libraryExporters.Add(nugetDependencyResolver);

            var projectLoader = new ProjectAssemblyLoader(projectResolver, _serviceProvider);
            libraryExporters.Add(projectLoader);

            // Project.json projects
            loaders.Add(projectLoader);
            dependencyProviders.Add(new ProjectReferenceDependencyProvider(projectResolver));

            // GAC and reference assembly resolver
            dependencyProviders.Add(referenceAssemblyDependencyResolver);
            dependencyProviders.Add(gacDependencyResolver);

            // NuGet packages
            loaders.Add(nugetLoader);
            dependencyProviders.Add(nugetDependencyResolver);

            // Catch all for unresolved depedencies
            dependencyProviders.Add(_unresolvedProvider);

            _dependencyWalker = new DependencyWalker(dependencyProviders);

            // Setup the attempted providers in case there are unresolved
            // dependencies
            _unresolvedProvider.AttemptedProviders = dependencyProviders;

            _loader = new CompositeAssemblyLoader(applicationEnvironment, loaders);

            _serviceProvider.Add(typeof(IApplicationEnvironment), applicationEnvironment);
            _serviceProvider.Add(typeof(IApplicationShutdown), _shutdown);

            // TODO: Get rid of this and just use the IFileWatcher
            _serviceProvider.Add(typeof(IFileMonitor), _watcher);

            _serviceProvider.Add(typeof(IFileWatcher), _watcher);

            var exportProvider = new CompositeLibraryExportProvider(libraryExporters);
            _serviceProvider.Add(typeof(ILibraryExportProvider), exportProvider);
            _serviceProvider.Add(typeof(ILibraryManager),
                new LibraryManager(_targetFramework,
                                   applicationEnvironment.Configuration,
                                   _dependencyWalker,
                                   exportProvider));

            _serviceProvider.Add(typeof(IProjectResolver), projectResolver);
        }


        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }
    }
}
