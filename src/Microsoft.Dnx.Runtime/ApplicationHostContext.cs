// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.Versioning;
using NuGet;

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
            var projectResolver = new ProjectResolver(context.ProjectDirectory, context.RootDirectory);
            var frameworkReferenceResolver = context.FrameworkResolver ?? new FrameworkReferenceResolver();
            context.PackagesDirectory = context.PackagesDirectory ?? PackageDependencyProvider.ResolveRepositoryPath(context.RootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(frameworkReferenceResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var projectDependencyProvider = new ProjectReferenceDependencyProvider(projectResolver);
            var unresolvedDependencyProvider = new UnresolvedDependencyProvider();

            DependencyWalker dependencyWalker = null;
            LockFileLookup lockFileLookup = null;

            if (context.Project == null)
            {
                var projectName = PathUtility.GetDirectoryName(context.ProjectDirectory);

                Project project;
                if (projectResolver.TryResolveProject(projectName, out project) && 
                    string.Equals(project.ProjectDirectory, context.ProjectDirectory))
                {
                    context.Project = project;
                }
                else
                {
                    throw new InvalidOperationException(
                        string.Format("Unable to resolve project '{0}' from {1}", projectName, context.ProjectDirectory));
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
                var lockFile = lockFileReader.Read(projectLockJsonPath);
                validLockFile = lockFile.IsValidForProject(context.Project, out lockFileValidationMessage);

                // When the only invalid part of a lock file is version number,
                // we shouldn't skip lock file validation because we want to leave all dependencies unresolved, so that
                // VS can be aware of this version mismatch error and automatically do restore
                skipLockFileValidation = context.SkipLockfileValidation && (lockFile.Version == Constants.LockFileVersion);

                if (validLockFile || skipLockFileValidation)
                {
                    lockFileLookup = new LockFileLookup(lockFile);
                    var packageDependencyProvider = new PackageDependencyProvider(context.PackagesDirectory, lockFileLookup);

                    dependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                        projectDependencyProvider,
                        packageDependencyProvider,
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
                dependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                    projectDependencyProvider,
                    referenceAssemblyDependencyResolver,
                    gacDependencyResolver,
                    unresolvedDependencyProvider
                });
            }

            dependencyWalker.Walk(context.Project.Name, context.Project.Version, context.TargetFramework);

            context.LibraryManager = new LibraryManager(context.Project.ProjectFilePath, dependencyWalker.Libraries);

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
            projectResolver.Clear();
        }
    }
}