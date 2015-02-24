using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHostBuilder
    {
        public string ApplicationBaseDirectory { get; }
        public IAssemblyLoaderContainer LoaderContainer { get; }

        public RuntimeHostBuilder(string applicationBaseDirectory, IAssemblyLoaderContainer loaderContainer)
        {
            ApplicationBaseDirectory = applicationBaseDirectory;
            LoaderContainer = loaderContainer;
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