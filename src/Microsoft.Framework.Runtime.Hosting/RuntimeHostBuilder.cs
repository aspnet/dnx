using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Hosting.DependencyProviders;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHostBuilder
    {
        public IList<IDependencyProvider> DependencyProviders { get; } = new List<IDependencyProvider>();
        public NuGetFramework TargetFramework { get; set; }
        public Project Project { get; set; }
        public LockFile LockFile { get; set; }

        /// <summary>
        /// Create a <see cref="RuntimeHostBuilder"/> for the project in the specified
        /// <paramref name="projectDirectory"/>.
        /// </summary>
        /// <remarks>
        /// This method will throw if the project.json file cannot be found in the
        /// specified folder. If a project.lock.json file is present in the directory
        /// it will be loaded. 
        /// </remarks>
        /// <param name="projectDirectory">The directory of the project to host</param>
        public static RuntimeHostBuilder ForProjectDirectory(string projectDirectory, NuGetFramework runtimeFramework)
        {
            if (string.IsNullOrEmpty(projectDirectory))
            {
                throw new ArgumentNullException(nameof(projectDirectory));
            }
            if (runtimeFramework == null)
            {
                throw new ArgumentNullException(nameof(runtimeFramework));
            }

            var hostBuilder = new RuntimeHostBuilder();

            // Load the Project
            var projectResolver = new PackageSpecResolver(projectDirectory);
            PackageSpec packageSpec;
            if (projectResolver.TryResolvePackageSpec(GetProjectName(projectDirectory), out packageSpec))
            {
                hostBuilder.Project = new Project(packageSpec);
            }

            // Load the Lock File if present
            LockFile lockFile;
            if (TryReadLockFile(projectDirectory, out lockFile))
            {
                hostBuilder.LockFile = lockFile;
            }

            // Set the framework
            hostBuilder.TargetFramework = runtimeFramework;

            hostBuilder.DependencyProviders.Add(new PackageSpecReferenceDependencyProvider(projectResolver));

            if (hostBuilder.LockFile != null)
            {
                hostBuilder.DependencyProviders.Add(new LockFileDependencyProvider(hostBuilder.LockFile));
            }

            var referenceResolver = new FrameworkReferenceResolver();
            hostBuilder.DependencyProviders.Add(new ReferenceAssemblyDependencyProvider(referenceResolver));

            // GAC resolver goes here! :)

            return hostBuilder;
        }

        /// <summary>
        /// Builds a <see cref="RuntimeHost"/> from the parameters specified in this
        /// object.
        /// </summary>
        public RuntimeHost Build()
        {
            return new RuntimeHost(this);
        }

        private static string GetProjectName(string projectDirectory)
        {
            projectDirectory = projectDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return projectDirectory.Substring(Path.GetDirectoryName(projectDirectory).Length).Trim(Path.DirectorySeparatorChar);
        }

        private static bool TryReadLockFile(string directory, out LockFile lockFile)
        {
            lockFile = null;
            string file = Path.Combine(directory, LockFileFormat.LockFileName);
            if (File.Exists(file))
            {
                using (var stream = File.OpenRead(file))
                {
                    lockFile = LockFileFormat.Read(stream);
                }
                return true;
            }
            return false;
        }

    }
}