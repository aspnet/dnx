// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Dnx.Runtime.Loader;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class ApplicationHostContext
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly LockFile _lockFile;
        private readonly Lazy<List<DiagnosticMessage>> _lockFileDiagnostics =
            new Lazy<List<DiagnosticMessage>>();

        public ApplicationHostContext(IServiceProvider hostServices,
                                      string projectDirectory,
                                      string packagesDirectory,
                                      string configuration,
                                      FrameworkName targetFramework,
                                      IAssemblyLoadContextFactory loadContextFactory = null,
                                      bool skipLockFileValidation = false)
        {
            ProjectDirectory = projectDirectory;
            Configuration = configuration;
            RootDirectory = Runtime.ProjectResolver.ResolveRootDirectory(ProjectDirectory);
            ProjectResolver = new ProjectResolver(ProjectDirectory, RootDirectory);
            FrameworkReferenceResolver = new FrameworkReferenceResolver();
            ProjectGraphProvider = new ProjectGraphProvider(hostServices);
            _serviceProvider = new ServiceProvider(hostServices);

            PackagesDirectory = packagesDirectory ?? NuGetDependencyResolver.ResolveRepositoryPath(RootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(FrameworkReferenceResolver);
            NuGetDependencyProvider = new NuGetDependencyResolver(new PackageRepository(PackagesDirectory));
            var gacDependencyResolver = new GacDependencyResolver();
            ProjectDependencyProvider = new ProjectReferenceDependencyProvider(ProjectResolver);
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

            var projectLockJsonPath = Path.Combine(ProjectDirectory, LockFileReader.LockFileName);
            var lockFileExists = File.Exists(projectLockJsonPath);
            var validLockFile = false;

            if (lockFileExists)
            {
                var lockFileReader = new LockFileReader();
                _lockFile = lockFileReader.Read(projectLockJsonPath);
                validLockFile = _lockFile.IsValidForProject(project);

                // When the only invalid part of a lock file is version number,
                // we shouldn't skip lock file validation because we want to leave all dependencies unresolved, so that
                // VS can be aware of this version mismatch error and automatically do restore
                skipLockFileValidation = skipLockFileValidation && (_lockFile.Version == Constants.LockFileVersion);

                if (validLockFile || skipLockFileValidation)
                {
                    NuGetDependencyProvider.ApplyLockFile(_lockFile);

                    DependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                        ProjectDependencyProvider,
                        NuGetDependencyProvider,
                        referenceAssemblyDependencyResolver,
                        gacDependencyResolver,
                        unresolvedDependencyProvider
                    });
                }
            }

            if ((!validLockFile && !skipLockFileValidation) || !lockFileExists)
            {
                // We don't add NuGetDependencyProvider to DependencyWalker
                // It will leave all NuGet packages unresolved and give error message asking users to run "dnu restore"
                DependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                    ProjectDependencyProvider,
                    referenceAssemblyDependencyResolver,
                    gacDependencyResolver,
                    unresolvedDependencyProvider
                });
            }

            LibraryManager = new LibraryManager(() => DependencyWalker.Libraries);

            AssemblyLoadContextFactory = loadContextFactory ?? new RuntimeLoadContextFactory(ServiceProvider);

            // Create a new Application Environment for running the app. It needs a reference to the Host's application environment
            // (if any), which we can get from the service provider we were given.
            // If this is null (i.e. there is no Host Application Environment), that's OK, the Application Environment we are creating
            // will just have it's own independent set of global data.
            IApplicationEnvironment hostEnvironment = null;
            if (hostServices != null)
            {
                hostEnvironment = (IApplicationEnvironment)hostServices.GetService(typeof(IApplicationEnvironment));
            }
            ApplicationEnvironment = new ApplicationEnvironment(Project, targetFramework, configuration, hostEnvironment);

            // Default services
            _serviceProvider.Add(typeof(IApplicationEnvironment), ApplicationEnvironment);
            _serviceProvider.Add(typeof(ILibraryManager), LibraryManager);

            // Not exposed to the application layer
            _serviceProvider.Add(typeof(IProjectResolver), ProjectResolver, includeInManifest: false);
            _serviceProvider.Add(typeof(NuGetDependencyResolver), NuGetDependencyProvider, includeInManifest: false);
            _serviceProvider.Add(typeof(ProjectReferenceDependencyProvider), ProjectDependencyProvider, includeInManifest: false);
            _serviceProvider.Add(typeof(IAssemblyLoadContextFactory), AssemblyLoadContextFactory, includeInManifest: false);
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

        public ProjectReferenceDependencyProvider ProjectDependencyProvider { get; private set; }

        public IProjectResolver ProjectResolver { get; private set; }

        public LibraryManager LibraryManager { get; private set; }

        public DependencyWalker DependencyWalker { get; private set; }

        public FrameworkReferenceResolver FrameworkReferenceResolver { get; private set; }

        public IProjectGraphProvider ProjectGraphProvider { get; }

        public string Configuration { get; private set; }

        public string RootDirectory { get; private set; }

        public string ProjectDirectory { get; private set; }

        public string PackagesDirectory { get; private set; }
        public IApplicationEnvironment ApplicationEnvironment { get; private set; }

        public IEnumerable<DiagnosticMessage> GetLockFileDiagnostics()
        {
            if (_lockFileDiagnostics.IsValueCreated)
            {
                return _lockFileDiagnostics.Value;
            }

            if (_lockFile == null)
            {
                _lockFileDiagnostics.Value.Add(new DiagnosticMessage(
                    $"The expected lock file doesn't exist. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }
            else
            {
                _lockFileDiagnostics.Value.AddRange(_lockFile.GetDiagnostics(Project));
            }
            return _lockFileDiagnostics.Value;
        }

        public IEnumerable<DiagnosticMessage> GetAllDiagnostics()
        {
            return GetLockFileDiagnostics()
                .Concat(DependencyWalker.GetDependencyDiagnostics(Project.ProjectFilePath));
        }
    }
}