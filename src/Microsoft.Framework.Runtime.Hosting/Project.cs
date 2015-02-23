using System;
using Microsoft.Framework.Runtime.FileGlobbing;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class Project
    {
        public Project(PackageSpec project)
        {
            Metadata = project;
            Files = new ProjectFilesCollection(project.Properties, project.ProjectDirectory);
        }

        public ProjectFilesCollection Files { get; }
        public PackageSpec Metadata { get; }
    }
}