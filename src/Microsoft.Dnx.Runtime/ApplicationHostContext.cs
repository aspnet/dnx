// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class ApplicationHostContext
    {
        private List<LibraryDescription> _libraries;
        private bool _validLockFile;
        private bool _lockFileExists;
        private string _lockFileValidationMessage;

        public Project Project { get; set; }

        // TODO: Remove this, it's kinda hacky
        public ProjectDescription MainProject { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public string RootDirectory { get; set; }

        public string ProjectDirectory { get; set; }

        public string PackagesDirectory { get; set; }

        public bool SkipLockfileValidation { get; set; }

        public FrameworkReferenceResolver FrameworkResolver { get; set; }

        public LibraryManager LibraryManager { get; private set; }

        public static void InitializeForRuntime(ApplicationHostContext context)
        {
            InitializeCore(context);

            context.LibraryManager = new LibraryManager(context.Project.ProjectFilePath, context.TargetFramework, context._libraries);

            if (!context._validLockFile)
            {
                throw new InvalidOperationException($"{context._lockFileValidationMessage}. Please run \"dnu restore\" to generate a new lock file.");
            }

            if (!context._lockFileExists)
            {
                throw new InvalidOperationException("The expected lock file doesn't exist. Please run \"dnu restore\" to generate a new lock file.");
            }
        }

        public static void Initialize(ApplicationHostContext context)
        {
            InitializeCore(context);

            // Map dependencies
            var lookup = context._libraries.ToDictionary(l => l.Identity.Name);
            context._libraries = null;

            var frameworkReferenceResolver = context.FrameworkResolver ?? new FrameworkReferenceResolver();
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(frameworkReferenceResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var unresolvedDependencyProvider = new UnresolvedDependencyProvider();

            // Fix up dependencies
            foreach (var library in lookup.Values.ToList())
            {
                library.Framework = library.Framework ?? context.TargetFramework;
                foreach (var dependency in library.Dependencies)
                {
                    LibraryDescription dep;
                    if (!lookup.TryGetValue(dependency.Name, out dep))
                    {
                        if (dependency.LibraryRange.IsGacOrFrameworkReference)
                        {
                            dep = referenceAssemblyDependencyResolver.GetDescription(dependency.LibraryRange, context.TargetFramework) ??
                                  gacDependencyResolver.GetDescription(dependency.LibraryRange, context.TargetFramework) ??
                                  unresolvedDependencyProvider.GetDescription(dependency.LibraryRange, context.TargetFramework);

                            dep.Framework = context.TargetFramework;
                            lookup[dependency.Name] = dep;
                        }
                        else
                        {
                            dep = unresolvedDependencyProvider.GetDescription(dependency.LibraryRange, context.TargetFramework);
                            lookup[dependency.Name] = dep;
                        }
                    }

                    // REVIEW: This isn't quite correct but there's a many to one relationship here.
                    // Different ranges can resolve to this dependency but only one wins
                    dep.RequestedRange = dependency.LibraryRange;
                    dependency.Library = dep;
                }
            }

            var libraries = lookup.Values.ToList();

            context.LibraryManager = new LibraryManager(context.Project.ProjectFilePath, context.TargetFramework, libraries);

            AddLockFileDiagnostics(context);

            // Clear all the temporary memory aggressively here if we don't care about reuse
            // e.g. runtime scenarios
            lookup.Clear();
        }

        private static void InitializeCore(ApplicationHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.ProjectDirectory = context.Project?.ProjectDirectory ?? context.ProjectDirectory;
            context.RootDirectory = context.RootDirectory ?? ProjectResolver.ResolveRootDirectory(context.ProjectDirectory);
            context.PackagesDirectory = context.PackagesDirectory ?? PackageDependencyProvider.ResolveRepositoryPath(context.RootDirectory);

            LockFileLookup lockFileLookup = null;
            LockFile lockFile = null;

            if (context.Project == null)
            {
                Project project;
                if (Project.TryGetProject(context.ProjectDirectory, out project))
                {
                    context.Project = project;
                }
                else
                {
                    throw new InvalidOperationException($"Unable to resolve project from {context.ProjectDirectory}");
                }
            }

            var projectLockJsonPath = Path.Combine(context.ProjectDirectory, LockFileReader.LockFileName);
            var lockFileExists = File.Exists(projectLockJsonPath);
            var validLockFile = false;
            var skipLockFileValidation = context.SkipLockfileValidation;
            string lockFileValidationMessage = null;

            if (lockFileExists)
            {
                var lockFileReader = new LockFileReader();
                lockFile = lockFileReader.Read(projectLockJsonPath);
                validLockFile = lockFile.IsValidForProject(context.Project, out lockFileValidationMessage);

                // When the only invalid part of a lock file is version number,
                // we shouldn't skip lock file validation because we want to leave all dependencies unresolved, so that
                // VS can be aware of this version mismatch error and automatically do restore
                skipLockFileValidation = context.SkipLockfileValidation && (lockFile.Version == Constants.LockFileVersion);

                if (validLockFile || skipLockFileValidation)
                {
                    lockFileLookup = new LockFileLookup(lockFile);
                }
            }

            var libraries = new List<LibraryDescription>();
            var packageResolver = new PackageDependencyProvider(context.PackagesDirectory);
            var projectResolver = new ProjectDependencyProvider();

            context.MainProject = projectResolver.GetDescription(context.TargetFramework, context.Project); ;

            // Add the main project
            libraries.Add(context.MainProject);

            if (lockFileLookup != null)
            {
                foreach (var target in lockFile.Targets)
                {
                    if (target.TargetFramework != context.TargetFramework)
                    {
                        continue;
                    }

                    foreach (var library in target.Libraries)
                    {
                        if (string.Equals(library.Type, "project"))
                        {
                            var projectLibrary = lockFileLookup.GetProject(library.Name);

                            var path = Path.GetFullPath(Path.Combine(context.ProjectDirectory, projectLibrary.Path));

                            var projectDescription = projectResolver.GetDescription(context.TargetFramework, path, library);

                            libraries.Add(projectDescription);
                        }
                        else
                        {
                            var packageEntry = lockFileLookup.GetPackage(library.Name, library.Version);

                            var packageDescription = packageResolver.GetDescription(packageEntry, library);

                            libraries.Add(packageDescription);
                        }
                    }
                }
            }

            context._libraries = libraries;
            context._validLockFile = validLockFile;
            context._lockFileExists = lockFileExists;
            context._lockFileValidationMessage = lockFileValidationMessage;

            lockFileLookup?.Clear();
        }

        private static void AddLockFileDiagnostics(ApplicationHostContext context)
        {
            if (!context._validLockFile)
            {
                context.LibraryManager.AddGlobalDiagnostics(new DiagnosticMessage(
                    $"{context._lockFileValidationMessage}. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(context.Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }

            if (!context._lockFileExists)
            {
                context.LibraryManager.AddGlobalDiagnostics(new DiagnosticMessage(
                    $"The expected lock file doesn't exist. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(context.Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }
        }
    }
}