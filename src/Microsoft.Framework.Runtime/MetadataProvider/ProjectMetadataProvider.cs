using System;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.Runtime
{
    public class ProjectMetadataProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly ILibraryManager _libraryManager;

        public ProjectMetadataProvider(IProjectResolver projectResolver, ILibraryManager libraryManager)
        {
            _projectResolver = projectResolver;
            _libraryManager = libraryManager;
        }

        public ProjectMetadata GetProjectMetadata(string name)
        {
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var export = _libraryManager.GetAllExports(name);

            if (export == null)
            {
                return null;
            }

            return new ProjectMetadata(project, export);
        }
    }
}