using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHostBuilder
    {
        public string ApplicationBaseDirectory { get; }
        public IAssemblyLoaderContainer LoaderContainer { get; }
        public IList<IDependencyProvider> DependencyProviders { get; }
        public string RootDirectory { get; set; }
        public NuGetFramework TargetFramework { get; set; }

        public RuntimeHostBuilder(string applicationBaseDirectory, IAssemblyLoaderContainer loaderContainer)
        {
            ApplicationBaseDirectory = applicationBaseDirectory;
            RootDirectory = ProjectResolver.ResolveRootDirectory(ApplicationBaseDirectory);
            LoaderContainer = loaderContainer;
            DependencyProviders = new List<IDependencyProvider>();
        }

        public RuntimeHost Build()
        {
            // Load the Project
            Project project = null;

            PackageSpec packageSpec;
            if (JsonPackageSpecReader.TryReadPackageSpec(ApplicationBaseDirectory, out packageSpec))
            {
                project = new Project(packageSpec);
            }

            return new RuntimeHost(this, project);
        }
    }
}