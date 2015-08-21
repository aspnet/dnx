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
        public Project Project { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public string RootDirectory { get; set; }

        public string ProjectDirectory { get; set; }

        public string PackagesDirectory { get; set; }

        public bool SkipLockfileValidation { get; set; }

        public FrameworkReferenceResolver FrameworkResolver { get; set; }

        public LibraryManager LibraryManager { get; private set; }

        public static void Initialize(ApplicationHostContext context)
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
            var lookup = new Dictionary<string, LibraryDescription>();

            var packageResolver = new PackageDependencyProvider(context.PackagesDirectory);
            var projectResolver = new ProjectDependencyProvider();

            var mainProjectDescription = projectResolver.GetDescription(context.TargetFramework, context.Project);

            // Add the main project
            libraries.Add(mainProjectDescription);
            lookup[context.Project.Name] = mainProjectDescription;

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
                        // REVIEW: Do we fallback to looking for projects on disk?
                        if (string.Equals(library.Type, "project"))
                        {
                            var projectLibrary = lockFileLookup.GetProject(library.Name);

                            var projectDescription = projectResolver.GetDescription(context.TargetFramework, projectLibrary, library);

                            lookup[library.Name] = projectDescription;
                        }
                        else
                        {
                            var packageEntry = lockFileLookup.GetPackage(library.Name, library.Version);

                            var packageDescription = packageResolver.GetDescription(packageEntry, library);

                            lookup[library.Name] = packageDescription;
                        }
                    }
                }
            }

            var frameworkReferenceResolver = context.FrameworkResolver ?? new FrameworkReferenceResolver();
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(frameworkReferenceResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var unresolvedDependencyProvider = new UnresolvedDependencyProvider();

            // REVIEW: This can be done lazily
            // Fix up dependencies
            foreach (var library in lookup.Values.ToList())
            {
                library.Framework = library.Framework ?? context.TargetFramework;
                foreach (var dependency in library.Dependencies)
                {
                    LibraryDescription dep;
                    if (lookup.TryGetValue(dependency.Name, out dep))
                    {
                        // REVIEW: This isn't quite correct but there's a many to one relationship here.
                        // Different ranges can resolve to this dependency but only one wins
                        dep.RequestedRange = dependency.LibraryRange;
                        dependency.Library = dep.Identity;
                    }
                    else if (dependency.LibraryRange.IsGacOrFrameworkReference)
                    {
                        var fxReference = referenceAssemblyDependencyResolver.GetDescription(dependency.LibraryRange, context.TargetFramework) ??
                                          gacDependencyResolver.GetDescription(dependency.LibraryRange, context.TargetFramework) ??
                                          unresolvedDependencyProvider.GetDescription(dependency.LibraryRange, context.TargetFramework);

                        fxReference.RequestedRange = dependency.LibraryRange;
                        dependency.Library = fxReference.Identity;

                        lookup[dependency.Name] = fxReference;
                    }
                    else
                    {
                        lookup[dependency.Name] = unresolvedDependencyProvider.GetDescription(dependency.LibraryRange, context.TargetFramework);
                    }
                }
            }

            libraries = lookup.Values.ToList();

            context.LibraryManager = new LibraryManager(context.Project.ProjectFilePath, context.TargetFramework, libraries);

            if (!validLockFile)
            {
                context.LibraryManager.AddGlobalDiagnostics(new DiagnosticMessage(
                    $"{lockFileValidationMessage}. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(context.Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }

            if (!lockFileExists)
            {
                context.LibraryManager.AddGlobalDiagnostics(new DiagnosticMessage(
                    $"The expected lock file doesn't exist. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(context.Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }

            // Clear all the temporary memory aggressively here if we don't care about reuse
            // e.g. runtime scenarios
            lockFileLookup?.Clear();
            lookup.Clear();
        }
    }
}