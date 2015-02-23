using System;
using Microsoft.Framework.Runtime.FileGlobbing;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    internal class ProjectWithSources
    {
        public ProjectWithSources(Project project)
        {
           // Metadata = project;
            Files = new ProjectFilesCollection(null, project.ProjectDirectory);
        }

        // public Project Metadata { get; }

        public ProjectFilesCollection Files { get; }
    }
}