using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectDescription : LibraryDescription
    {
        public ProjectDescription(
            LibraryRange libraryRange,
            Project project,
            IEnumerable<LibraryDependency> dependencies,
            IEnumerable<string> assemblies,
            TargetFrameworkInformation targetFrameworkInfo,
            bool resolved) :
                base(
                    libraryRange,
                    new LibraryIdentity(project.Name, project.Version, isGacOrFrameworkReference: false),
                    project.ProjectFilePath,
                    LibraryTypes.Project,
                    dependencies,
                    assemblies,
                    targetFrameworkInfo.FrameworkName)
        {
            Project = project;
            Resolved = resolved;
            Compatible = resolved;
            TargetFrameworkInfo = targetFrameworkInfo;
        }

        public Project Project { get; }
        public TargetFrameworkInformation TargetFrameworkInfo { get; }
    }
}
