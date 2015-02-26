// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Loader;
using Microsoft.Framework.Runtime.DependencyManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet;

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
            NuGetDependencyProvider = new NuGetDependencyResolver(new PackageRepository(PackagesDirectory));
            var gacDependencyResolver = new GacDependencyResolver();
            ProjectDepencyProvider = new ProjectReferenceDependencyProvider(ProjectResolver);
            var unresolvedDependencyProvider = new UnresolvedDependencyProvider();

            var projectName = PathUtility.GetDirectoryName(ProjectDirectory);

            Project project;
            if (ProjectResolver.TryResolveProject(projectName, out project))
            {
                Project = project;
            }
            else
            {
                throw new InvalidOperationException(
                    string.Format("Unable to resolve project '{0}' from {1}", projectName, ProjectDirectory));
            }

            var projectLockJsonPath = Path.Combine(ProjectDirectory, LockFileFormat.LockFileName);
            var lockFileExists = File.Exists(projectLockJsonPath);
            var validLockFile = false;

            if (lockFileExists)
            {
                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Read(projectLockJsonPath);
                validLockFile = IsValidLockFile(lockFile);

                if (validLockFile)
                {
                    NuGetDependencyProvider.ApplyLockFile(lockFile);

                    DependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                        ProjectDepencyProvider,
                        NuGetDependencyProvider,
                        referenceAssemblyDependencyResolver,
                        gacDependencyResolver,
                        unresolvedDependencyProvider
                    });
                }
            }

            if (!lockFileExists || !validLockFile)
            {
                // If we are unable to apply the lockfile, we don't add NuGetDependencyProvider to DependencyWalker
                // It will leave all NuGet packages unresolved and give error message asking users to run "kpm restore"
                DependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                    ProjectDepencyProvider,
                    referenceAssemblyDependencyResolver,
                    gacDependencyResolver,
                    unresolvedDependencyProvider
                });
            }

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

            var compilerOptionsProvider = new CompilerOptionsProvider(ProjectResolver);
            _serviceProvider.Add(typeof(ICompilerOptionsProvider), compilerOptionsProvider);
        }

        private bool IsValidLockFile(LockFile lockFile)
        {
            var actualTargetFrameworks = Project.GetTargetFrameworks();

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (lockFile.ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in lockFile.ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(group.FrameworkName))
                {
                    actualDependencies = Project.Dependencies.Select(x => x.LibraryRange.ToString()).OrderBy(x => x);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f =>
                            string.Equals(f.FrameworkName.ToString(), group.FrameworkName, StringComparison.Ordinal));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(d => d.LibraryRange.ToString()).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            return true;
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

        public Project Project { get; private set; }

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