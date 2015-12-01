// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Internals;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime
{
    public class ApplicationHostContext
    {
        public Project Project { get; set; }

        public LockFile LockFile { get; set; }

        // TODO: Remove this, it's kinda hacky
        public ProjectDescription MainProject { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public IEnumerable<string> RuntimeIdentifiers { get; set; } = Enumerable.Empty<string>();

        public string RootDirectory { get; set; }

        public string ProjectDirectory { get; set; }

        public string PackagesDirectory { get; set; }

        public FrameworkReferenceResolver FrameworkResolver { get; set; }

        public LibraryManager LibraryManager { get; private set; }

        public static IList<LibraryDescription> GetRuntimeLibraries(ApplicationHostContext context)
        {
            return GetRuntimeLibraries(context, throwOnInvalidLockFile: false);
        }

        public static IList<LibraryDescription> GetRuntimeLibraries(ApplicationHostContext context, bool throwOnInvalidLockFile)
        {
            var result = InitializeCore(context);

            if (throwOnInvalidLockFile)
            {
                if (!result.ValidLockFile)
                {
                    throw new InvalidOperationException($"{result.LockFileValidationMessage}. Please run \"dnu restore\" to generate a new lock file.");
                }
            }

            return result.Libraries;
        }

        public static void Initialize(ApplicationHostContext context)
        {
            var result = InitializeCore(context);

            // Map dependencies
            var lookup = result.Libraries.ToDictionary(l => l.Identity.Name);

            var frameworkReferenceResolver = context.FrameworkResolver ?? new FrameworkReferenceResolver();
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(frameworkReferenceResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var unresolvedDependencyProvider = new UnresolvedDependencyProvider();

            // Fix up dependencies
            foreach (var library in lookup.Values.ToList())
            {
                if (string.Equals(library.Type, LibraryTypes.Package) &&
                    !Directory.Exists(library.Path))
                {
                    // If the package path doesn't exist then mark this dependency as unresolved
                    library.Resolved = false;
                }

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

            AddLockFileDiagnostics(context, result);

            // Clear all the temporary memory aggressively here if we don't care about reuse
            // e.g. runtime scenarios
            lookup.Clear();
        }

        private static Result InitializeCore(ApplicationHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.ProjectDirectory = context.Project?.ProjectDirectory ?? context.ProjectDirectory;
            context.RootDirectory = context.RootDirectory ?? ProjectRootResolver.ResolveRootDirectory(context.ProjectDirectory);
            context.PackagesDirectory = context.PackagesDirectory ?? PackageDependencyProvider.ResolveRepositoryPath(context.RootDirectory);

            LockFileLookup lockFileLookup = null;

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

            if (context.LockFile == null && File.Exists(projectLockJsonPath))
            {
                var lockFileReader = new LockFileReader();
                context.LockFile = lockFileReader.Read(projectLockJsonPath);
            }

            var validLockFile = true;
            string lockFileValidationMessage = null;

            if (context.LockFile != null)
            {
                validLockFile = (context.LockFile.Version == Constants.LockFileVersion) && context.LockFile.IsValidForProject(context.Project, out lockFileValidationMessage);

                lockFileLookup = new LockFileLookup(context.LockFile);
            }

            var libraries = new List<LibraryDescription>();
            var packageResolver = new PackageDependencyProvider(context.PackagesDirectory);
            var projectResolver = new ProjectDependencyProvider();

            context.MainProject = projectResolver.GetDescription(context.TargetFramework, context.Project);

            // Add the main project
            libraries.Add(context.MainProject);

            if (lockFileLookup != null)
            {
                var target = SelectTarget(context, context.LockFile);
                if (target != null)
                {
                    if (Logger.IsEnabled && string.IsNullOrEmpty(target.RuntimeIdentifier))
                    {
                        // REVIEW(anurse): Is there ever a reason we want to use the RID-less target now?
                        Logger.TraceWarning($"[{nameof(ApplicationHostContext)}] Lock File Target is Runtime-agnostic! This is generally not good.");
                    }

                    Logger.TraceInformation($"[{nameof(ApplicationHostContext)}] Using Lock File Target: {target.TargetFramework}/{target.RuntimeIdentifier}");
                    foreach (var library in target.Libraries)
                    {
                        if (string.Equals(library.Type, "project"))
                        {
                            var projectLibrary = lockFileLookup.GetProject(library.Name);

                            var path = Path.GetFullPath(Path.Combine(context.ProjectDirectory, projectLibrary.Path));

                            var projectDescription = projectResolver.GetDescription(library.Name, path, library);

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

            lockFileLookup?.Clear();

            var lockFilePresent = context.LockFile != null;
            context.LockFile = null; // LockFile is no longer needed

            return new Result(libraries, lockFilePresent, validLockFile, lockFileValidationMessage);
        }

        private static LockFileTarget SelectTarget(ApplicationHostContext context, LockFile lockFile)
        {
            foreach (var runtimeIdentifier in context.RuntimeIdentifiers)
            {
                foreach (var scanTarget in lockFile.Targets)
                {
                    if (scanTarget.TargetFramework == context.TargetFramework && string.Equals(scanTarget.RuntimeIdentifier, runtimeIdentifier, StringComparison.Ordinal))
                    {
                        return scanTarget;
                    }
                }
            }

            foreach (var scanTarget in lockFile.Targets)
            {
                if (scanTarget.TargetFramework == context.TargetFramework && string.IsNullOrEmpty(scanTarget.RuntimeIdentifier))
                {
                    return scanTarget;
                }
            }

            return null;
        }

        private static void AddLockFileDiagnostics(ApplicationHostContext context, Result result)
        {
            if (!result.LockFileExists)
            {
                context.LibraryManager.AddGlobalDiagnostics(new DiagnosticMessage(
                    DiagnosticMonikers.NU1009,
                    $"The expected lock file doesn't exist. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(context.Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }

            if (!result.ValidLockFile)
            {
                context.LibraryManager.AddGlobalDiagnostics(new DiagnosticMessage(
                    DiagnosticMonikers.NU1006,
                    $"{result.LockFileValidationMessage}. Please run \"dnu restore\" to generate a new lock file.",
                    Path.Combine(context.Project.ProjectDirectory, LockFileReader.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }
        }

        private struct Result
        {
            public List<LibraryDescription> Libraries { get; }
            public string LockFileValidationMessage { get; }
            public bool LockFileExists { get; }
            public bool ValidLockFile { get; }

            public Result(List<LibraryDescription> libraries, bool lockFileExists, bool validLockFile, string lockFileValidationMessage)
            {
                Libraries = libraries;
                LockFileExists = lockFileExists;
                ValidLockFile = validLockFile;
                LockFileValidationMessage = lockFileValidationMessage;
            }
        }
    }
}