using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Hosting.DependencyProviders;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHostBuilder
    {
        public IList<IDependencyProvider> DependencyProviders { get; }
        public NuGetFramework TargetFramework { get; set; }
        public Project Project { get; set; }
        public LockFile LockFile { get; set;  }

        public RuntimeHostBuilder()
        {
            DependencyProviders = new List<IDependencyProvider>();
        }

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
        public static RuntimeHostBuilder ForProjectDirectory(string projectDirectory, IApplicationEnvironment applicationEnvironment)
        {
            var hostBuilder = new RuntimeHostBuilder();

            // Load the Project
            hostBuilder.Project = ProjectReader.ReadProjectFile(projectDirectory);

            // Load the Lock File if present
            if (ProjectReader.HasLockFile(projectDirectory))
            {
                hostBuilder.LockFile = ProjectReader.ReadLockFile(projectDirectory);
            }

            // Set the framework
            hostBuilder.TargetFramework = NuGetFramework.Parse(applicationEnvironment.RuntimeFramework.FullName);

            var projectResolver = new PackageSpecResolver(projectDirectory);
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
    }
}