// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private IAssemblyLoader _loader;
        private ApplicationHostContext _applicationHostContext;

        private IFileWatcher _watcher;
        private readonly string _projectDirectory;
        private readonly FrameworkName _targetFramework;
        private readonly ApplicationShutdown _shutdown = new ApplicationShutdown();

        private Project _project;

        public DefaultHost(DefaultHostOptions options, 
                          IServiceProvider hostServices)
        {
            _projectDirectory = Normalize(options.ApplicationBaseDirectory);
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
            Trace.TraceInformation("Project root is {0}", _projectDirectory);

            var sw = Stopwatch.StartNew();

            if (Project == null)
            {
                return null;
            }

            _applicationHostContext.DependencyWalker.Walk(Project.Name, Project.Version, _targetFramework);

            // If there's any unresolved dependencies then complain
            if (_applicationHostContext.UnresolvedDependencyProvider.UnresolvedDependencies.Any())
            {
                var sb = new StringBuilder();

                // TODO: Localize messages

                sb.AppendLine("Failed to resolve the following dependencies:");

                foreach (var d in _applicationHostContext.UnresolvedDependencyProvider.UnresolvedDependencies.OrderBy(d => d.Identity.Name))
                {
                    sb.AppendLine("   " + d.Identity.ToString());
                }

                sb.AppendLine();
                sb.AppendLine("Searched Locations:");

                foreach (var path in _applicationHostContext.UnresolvedDependencyProvider.GetAttemptedPaths(_targetFramework))
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

        private void Initialize(DefaultHostOptions options, IServiceProvider hostServices)
        {
            _applicationHostContext = new ApplicationHostContext(hostServices, _projectDirectory, options.PackageDirectory);

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

            var applicationEnvironment = new ApplicationEnvironment(Project, _targetFramework, options.Configuration);


            _applicationHostContext.AddService(typeof(IApplicationEnvironment), applicationEnvironment);
            _applicationHostContext.AddService(typeof(IApplicationShutdown), _shutdown);

            // TODO: Get rid of this and just use the IFileWatcher
            _applicationHostContext.AddService(typeof(IFileMonitor), _watcher);
            _applicationHostContext.AddService(typeof(IFileWatcher), _watcher);
            _applicationHostContext.AddService(typeof(ILibraryManager),
                new LibraryManager(_targetFramework,
                                   applicationEnvironment.Configuration,
                                   _applicationHostContext.DependencyWalker,
                                   (ILibraryExportProvider)ServiceProvider.GetService(typeof(ILibraryExportProvider))));

            var loaders = new[]
            {
                typeof(ProjectAssemblyLoader),
                typeof(NuGetAssemblyLoader),
            }
            .Select(loaderType => (IAssemblyLoader)ActivatorUtilities.CreateInstance(ServiceProvider, loaderType))
            .ToList();

            var nugetLoader = ActivatorUtilities.CreateInstance<NuGetAssemblyLoader>(ServiceProvider);

            _loader = new CompositeAssemblyLoader(applicationEnvironment, loaders);

            CallContextServiceLocator.Locator.ServiceProvider = ServiceProvider;
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
