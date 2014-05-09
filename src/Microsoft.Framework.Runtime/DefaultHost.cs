// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Infrastructure;
using Microsoft.Framework.Runtime.Loader;
using Microsoft.Framework.Runtime.Loader.NuGet;

namespace Microsoft.Framework.Runtime
{
    public class DefaultHost : IHost
    {
        private AssemblyLoader _loader;
        private DependencyWalker _dependencyWalker;

        private IFileWatcher _watcher;
        private readonly string _projectDir;
        private readonly FrameworkName _targetFramework;
        private readonly string _name;
        private readonly ServiceProvider _serviceProvider;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly UnresolvedDependencyProvider _unresolvedProvider = new UnresolvedDependencyProvider();

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
                throw new InvalidOperationException(
                    String.Format("Unable to resolve depedendencies {0}",
                        String.Join(",", _unresolvedProvider.UnresolvedDependencies.Select(d => d.Identity.ToString()))));
            }

            _serviceProvider.Add(typeof(IApplicationEnvironment), new ApplicationEnvironment(Project, _targetFramework));

            Trace.TraceInformation("Loading entry point from {0}", applicationName);

            var assembly = Assembly.Load(new AssemblyName(applicationName));

            sw.Stop();

            Trace.TraceInformation("Load took {0}ms", sw.ElapsedMilliseconds);

            return assembly;
        }

        public Assembly Load(string name)
        {
            return _loader.LoadAssembly(new LoadContext(name, _targetFramework));
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }

        private void Initialize(DefaultHostOptions options)
        {
            var dependencyProviders = new List<IDependencyProvider>();
            var loaders = new List<IAssemblyLoader>();

            string rootDirectory = ResolveRootDirectory(_projectDir);

            if (options.WatchFiles)
            {
                _watcher = new FileWatcher(rootDirectory);
            }
            else
            {
                _watcher = NoopWatcher.Instance;
            }

            if (!Project.TryGetProject(_projectDir, out _project))
            {
                throw new Exception("Unable to locate " + Project.ProjectFileName);
            }

            var projectResolver = new ProjectResolver(_projectDir, rootDirectory);

            var nugetDependencyResolver = new NuGetDependencyResolver(_projectDir, options.PackageDirectory);
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
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

            var dependencyExporter = new CompositeLibraryExportProvider(libraryExporters);
            var roslynLoader = new LazyRoslynAssemblyLoader(_loaderEngine, projectResolver, _watcher, dependencyExporter);

            // Project.json projects
            loaders.Add(roslynLoader);
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
            _loader = new AssemblyLoader(loaders);
            _serviceProvider.Add(typeof(IFileMonitor), _watcher);
            _serviceProvider.Add(typeof(ILibraryManager),
                new LibraryManager(_targetFramework, 
                                   _dependencyWalker, 
                                   libraryExporters.Concat(new[] { roslynLoader })));
        }

        public static string ResolveRootDirectory(string projectDir)
        {
            var di = new DirectoryInfo(Path.GetDirectoryName(projectDir));

            while (di.Parent != null)
            {
                if (di.EnumerateFiles("*." + GlobalSettings.GlobalFileName).Any() ||
                    di.EnumerateFiles("*.sln").Any() ||
                    di.EnumerateDirectories("packages").Any() ||
                    di.EnumerateDirectories(".git").Any())
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            return Path.GetDirectoryName(projectDir);
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
