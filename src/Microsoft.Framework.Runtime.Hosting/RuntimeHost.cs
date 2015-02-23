using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHost
    {
        public Project Project { get; }
        public string ApplicationBaseDirectory { get; }

        internal RuntimeHost(RuntimeHostBuilder builder, Project project)
        {
            Project = project;

            // Load properties from the mutable RuntimeHostBuilder into
            // immutable copies on this object
            ApplicationBaseDirectory = builder.ApplicationBaseDirectory;
        }
    }
}